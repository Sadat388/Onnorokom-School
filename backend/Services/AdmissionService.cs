using Microsoft.EntityFrameworkCore;

namespace Backend;

public class AdmissionService(AppDbContext db, IConfiguration config, TimeProvider clock)
{
    private readonly TimeSpan _holdDuration =
        TimeSpan.FromSeconds(config.GetValue("HOLD_DURATION_SECONDS", 600));

    // ---------- Classes ----------

    public async Task<ClassDto> CreateClassAsync(CreateClassRequest req)
    {
        var name = req.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 200)
            throw Invalid("Class name must be 1–200 characters.");
        if (req.TotalSeats is < 1 or > 60)
            throw Invalid("totalSeats must be between 1 and 60.");

        var entity = new SchoolClass
        {
            Id = NewId("c"),
            Name = name,
            TotalSeats = req.TotalSeats,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Classes.Add(entity);
        await db.SaveChangesAsync();
        return new ClassDto(entity.Id, entity.Name, entity.TotalSeats, 0, 0, entity.TotalSeats);
    }

    public async Task<List<ClassDto>> ListClassesAsync()
    {
        var now = clock.GetUtcNow();
        var classes = await db.Classes.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        var result = new List<ClassDto>();
        foreach (var c in classes) result.Add(await ToClassDtoAsync(c, now));
        return result;
    }

    public async Task<ClassDto> GetClassAsync(string classId)
    {
        var c = await db.Classes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == classId)
                ?? throw new DomainException(404, "CLASS_NOT_FOUND", "Class not found.");
        return await ToClassDtoAsync(c, clock.GetUtcNow());
    }

    // ---------- Holds (applications) ----------

    public async Task<HoldDto> CreateHoldAsync(string classId, string studentId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await LockClassAsync(classId);
        var now = clock.GetUtcNow();

        var alreadyActive = await db.Holds.AnyAsync(h =>
            h.ClassId == classId && h.StudentId == studentId &&
            (h.Status == HoldStatus.CONFIRMED ||
             (h.Status == HoldStatus.HELD && h.ExpiresAt > now)));
        if (alreadyActive)
            throw new DomainException(409, "ALREADY_ACTIVE",
                "You already have an active or confirmed application for this class.");

        var taken = await db.Holds.CountAsync(h =>
            h.ClassId == classId &&
            ((h.Status == HoldStatus.HELD && h.ExpiresAt > now) || h.Status == HoldStatus.CONFIRMED));
        if (taken >= c.TotalSeats)
            throw new DomainException(409, "NO_SEATS", "No seats are available in this class.");

        var hold = new SeatHold
        {
            Id = NewId("h"),
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.HELD,
            CreatedAt = now,
            ExpiresAt = now + _holdDuration,
        };
        db.Holds.Add(hold);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return ToDto(hold, c.Name, now);
    }

    public async Task<HoldDto> GetHoldAsync(string holdId, string studentId)
    {
        var h = await db.Holds.Include(x => x.Class).AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == holdId && x.StudentId == studentId)
            ?? throw HoldNotFound();
        return ToDto(h, h.Class.Name, clock.GetUtcNow());
    }

    public async Task<HoldDto> ConfirmHoldAsync(string holdId, string studentId)
    {
        var classId = await OwnedHoldClassIdAsync(holdId, studentId);
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockClassAsync(classId);

        var hold = await db.Holds.Include(h => h.Class).FirstAsync(h => h.Id == holdId);
        var now = clock.GetUtcNow();

        if (hold.Status == HoldStatus.HELD)
        {
            if (hold.ExpiresAt <= now)
                throw new DomainException(409, "HOLD_EXPIRED",
                    "The time limit has passed and the seat was released. Please apply again.");
            hold.Status = HoldStatus.CONFIRMED;
            await db.SaveChangesAsync();
        }
        else if (hold.Status == HoldStatus.CANCELLED)
        {
            throw new DomainException(409, "HOLD_NOT_ACTIVE", "This application is cancelled.");
        }
        // CONFIRMED: idempotent, nothing to change

        await tx.CommitAsync();
        return ToDto(hold, hold.Class.Name, now);
    }

    public async Task CancelHoldAsync(string holdId, string studentId)
    {
        var classId = await OwnedHoldClassIdAsync(holdId, studentId);
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockClassAsync(classId);

        var hold = await db.Holds.FirstAsync(h => h.Id == holdId);
        var now = clock.GetUtcNow();

        if (hold.Status == HoldStatus.CONFIRMED)
            throw new DomainException(409, "CANNOT_CANCEL_CONFIRMED",
                "A confirmed admission cannot be cancelled.");

        if (hold.Status == HoldStatus.HELD && hold.ExpiresAt > now)
        {
            hold.Status = HoldStatus.CANCELLED;
            await db.SaveChangesAsync();
        }
        // already cancelled or expired: treat as success (idempotent)

        await tx.CommitAsync();
    }

    public async Task<List<HoldDto>> MyHoldsAsync(string studentId)
    {
        var now = clock.GetUtcNow();
        var holds = await db.Holds.Include(h => h.Class).AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
        return holds.Select(h => ToDto(h, h.Class.Name, now)).ToList();
    }

    // ---------- Helpers ----------

    // Row lock on the class. Every seat-changing operation for this class waits here.
    private async Task<SchoolClass> LockClassAsync(string classId)
    {
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE classes SET \"TotalSeats\" = \"TotalSeats\" WHERE \"Id\" = {classId}");
        if (rows == 0)
            throw new DomainException(404, "CLASS_NOT_FOUND", "Class not found.");
        return await db.Classes.AsNoTracking().FirstAsync(c => c.Id == classId);
    }

    private async Task<string> OwnedHoldClassIdAsync(string holdId, string studentId) =>
        await db.Holds.AsNoTracking()
            .Where(h => h.Id == holdId && h.StudentId == studentId)
            .Select(h => h.ClassId)
            .FirstOrDefaultAsync() ?? throw HoldNotFound();

    private async Task<ClassDto> ToClassDtoAsync(SchoolClass c, DateTimeOffset now)
    {
        var held = await db.Holds.CountAsync(h =>
            h.ClassId == c.Id && h.Status == HoldStatus.HELD && h.ExpiresAt > now);
        var confirmed = await db.Holds.CountAsync(h =>
            h.ClassId == c.Id && h.Status == HoldStatus.CONFIRMED);
        return new ClassDto(c.Id, c.Name, c.TotalSeats, held, confirmed, c.TotalSeats - held - confirmed);
    }

    private static HoldDto ToDto(SeatHold h, string className, DateTimeOffset now) =>
        new(h.Id, h.ClassId, className, h.StudentId, EffectiveStatus(h, now), h.ExpiresAt);

    private static string EffectiveStatus(SeatHold h, DateTimeOffset now) =>
        h.Status == HoldStatus.HELD && h.ExpiresAt <= now ? "EXPIRED" : h.Status.ToString();

    private static DomainException Invalid(string message) => new(400, "VALIDATION_ERROR", message);
    private static DomainException HoldNotFound() => new(404, "HOLD_NOT_FOUND", "Application not found.");

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..(prefix.Length + 1 + 10)];
}