namespace Backend;

public enum HoldStatus { HELD, CONFIRMED, CANCELLED }

public class SchoolClass
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int TotalSeats { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class SeatHold
{
    public string Id { get; set; } = "";
    public string ClassId { get; set; } = "";
    public string StudentId { get; set; } = "";
    public HoldStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public SchoolClass Class { get; set; } = null!;
}

public record CreateClassRequest(string? Name, int TotalSeats);
public record ClassDto(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);
public record HoldDto(string HoldId, string ClassId, string ClassName, string StudentId, string Status, DateTimeOffset ExpiresAt);
public record ApiError(string Error, string Message);

public class DomainException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public static class StudentId
{
    public static string From(HttpRequest request)
    {
        var value = request.Headers["X-Student-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value) || value.Length != 17 || !value.All(char.IsAsciiDigit))
            throw new DomainException(400, "INVALID_STUDENT_ID",
                "X-Student-Id must be the 17-digit birth registration number.");
        return value;
    }
}