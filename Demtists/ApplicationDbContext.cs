
using Demtists.Domian;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Specialty> Specialties { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<SmsVerification> SmsVerifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(u => u.LastName).IsRequired().HasMaxLength(50);
            entity.Property(u => u.NationalId).IsRequired().HasMaxLength(11);
            entity.Property(u => u.PhoneNumber).IsRequired().HasMaxLength(11);

            entity.HasIndex(u => u.NationalId).IsUnique();
            entity.HasIndex(u => u.PhoneNumber).IsUnique();

            entity.HasMany(u => u.Reservations)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Specialty Configuration
        modelBuilder.Entity<Specialty>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
            entity.Property(s => s.Description).HasMaxLength(500);

            entity.HasMany(s => s.Reservations)
                .WithOne(r => r.Specialty)
                .HasForeignKey(r => r.SpecialtyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Reservation Configuration
        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Status).HasConversion<int>();
            entity.Property(r => r.ReservationDate).HasColumnType("date");
            entity.Property(r => r.StartTime).HasColumnType("time");
            entity.Property(r => r.EndTime).HasColumnType("time");

            entity.HasIndex(r => new { r.ReservationDate, r.StartTime, r.SpecialtyId });
        });

        // SmsVerification Configuration
        modelBuilder.Entity<SmsVerification>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.PhoneNumber).IsRequired().HasMaxLength(11);
            entity.Property(s => s.VerificationCode).IsRequired().HasMaxLength(6);
        });

        // Seed Specialties
        modelBuilder.Entity<Specialty>().HasData(
            new Specialty
                { Id = 1, Name = "ایمپلنت", Description = "کاشت دندان", HasInstallments = true, IsActive = true },
            new Specialty
                { Id = 2, Name = "لمینت", Description = "روکش زیبایی", HasInstallments = false, IsActive = true },
            new Specialty
                { Id = 3, Name = "درمان ریشه", Description = "عصب کشی", HasInstallments = false, IsActive = true },
            new Specialty
                { Id = 4, Name = "ترمیم", Description = "پر کردن دندان", HasInstallments = false, IsActive = true },
            new Specialty
            {
                Id = 5, Name = "پروتز ثابت و متحرک", Description = "دندان مصنوعی", HasInstallments = false,
                IsActive = true
            },
            new Specialty
                { Id = 6, Name = "ارتودنسی", Description = "تقویم دندان", HasInstallments = false, IsActive = true },
            new Specialty
                { Id = 7, Name = "بلیچینگ", Description = "سفید کردن", HasInstallments = false, IsActive = true },
            new Specialty
                { Id = 8, Name = "کامپوزیت", Description = "ترمیم زیبایی", HasInstallments = false, IsActive = true }
        );
    }
}