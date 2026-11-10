using Microsoft.EntityFrameworkCore;

namespace Backend;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<SeatHold> Holds => Set<SeatHold>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<SchoolClass>(e =>
        {
            e.ToTable("classes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        model.Entity<SeatHold>(e =>
        {
            e.ToTable("holds");
            e.HasKey(x => x.Id);
            e.Property(x => x.StudentId).HasMaxLength(17).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId);
            e.HasIndex(x => new { x.ClassId, x.StudentId });
        });
    }
}