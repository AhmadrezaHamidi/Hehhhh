
using Demtists.Controllers;
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
    public DbSet<SpecialtyWorkingHour> SpecialtyWorkingHours { get; set; }
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

        // SpecialtyWorkingHour Configuration
        modelBuilder.Entity<SpecialtyWorkingHour>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.SpecialtyId).IsRequired();
            entity.Property(w => w.DayOfWeek).HasConversion<int>().IsRequired();
            entity.Property(w => w.StartTime).HasColumnType("time").IsRequired();
            entity.Property(w => w.EndTime).HasColumnType("time").IsRequired();
            entity.Property(w => w.SlotDurationMinutes).IsRequired().HasDefaultValue(30);
            entity.Property(w => w.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(w => w.CreatedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            // روابط
            entity.HasOne(w => w.Specialty)
                .WithMany()
                .HasForeignKey(w => w.SpecialtyId)
                .OnDelete(DeleteBehavior.Cascade);

            // ایندکس‌ها
            entity.HasIndex(w => new { w.SpecialtyId, w.DayOfWeek })
                .HasDatabaseName("IX_SpecialtyWorkingHours_SpecialtyId_DayOfWeek");
            entity.HasIndex(w => w.SpecialtyId)
                .HasDatabaseName("IX_SpecialtyWorkingHours_SpecialtyId");
            entity.HasIndex(w => w.IsActive)
                .HasDatabaseName("IX_SpecialtyWorkingHours_IsActive");

            // محدودیت‌ها
            entity.HasCheckConstraint("CK_SpecialtyWorkingHours_SlotDuration",
                "[SlotDurationMinutes] > 0 AND [SlotDurationMinutes] <= 480");
            entity.HasCheckConstraint("CK_SpecialtyWorkingHours_TimeRange",
                "[StartTime] < [EndTime]");
        });

        // Seed Specialties
        //modelBuilder.Entity<Specialty>().HasData(
        //    new Specialty
        //    { Id = 1, Name = "ایمپلنت", Description = "کاشت دندان", HasInstallments = true, IsActive = true },
        //    new Specialty
        //    { Id = 2, Name = "لمینت", Description = "روکش زیبایی", HasInstallments = false, IsActive = true },
        //    new Specialty
        //    { Id = 3, Name = "درمان ریشه", Description = "عصب کشی", HasInstallments = false, IsActive = true },
        //    new Specialty
        //    { Id = 4, Name = "ترمیم", Description = "پر کردن دندان", HasInstallments = false, IsActive = true },
        //    new Specialty
        //    {
        //        Id = 5,
        //        Name = "پروتز ثابت و متحرک",
        //        Description = "دندان مصنوعی",
        //        HasInstallments = false,
        //        IsActive = true
        //    },
        //    new Specialty
        //    { Id = 6, Name = "ارتودنسی", Description = "تقویم دندان", HasInstallments = false, IsActive = true },
        //    new Specialty
        //    { Id = 7, Name = "بلیچینگ", Description = "سفید کردن", HasInstallments = false, IsActive = true },
        //    new Specialty
        //    { Id = 8, Name = "کامپوزیت", Description = "ترمیم زیبایی", HasInstallments = false, IsActive = true }
        //);
    }
}


    //    modelBuilder.Entity<SpecialtyWorkingHour>().HasData(
    //       // ایمپلنت - دوشنبه تا پنج‌شنبه
    //       new SpecialtyWorkingHour { Id = 1, SpecialtyId = 1, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(17, 0), SlotDurationMinutes = 60 },
    //       new SpecialtyWorkingHour { Id = 2, SpecialtyId = 1, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(17, 0), SlotDurationMinutes = 60 },
    //       new SpecialtyWorkingHour { Id = 3, SpecialtyId = 1, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(17, 0), SlotDurationMinutes = 60 },
    //       new SpecialtyWorkingHour { Id = 4, SpecialtyId = 1, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(17, 0), SlotDurationMinutes = 60 },
    //       new SpecialtyWorkingHour { Id = 5, SpecialtyId = 1, DayOfWeek = DayOfWeek.Saturday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(14, 0), SlotDurationMinutes = 60 },

    //       // لمینت - سه‌شنبه و پنج‌شنبه
    //       new SpecialtyWorkingHour { Id = 6, SpecialtyId = 2, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0), SlotDurationMinutes = 45 },
    //       new SpecialtyWorkingHour { Id = 7, SpecialtyId = 2, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0), SlotDurationMinutes = 45 },

    //       // درمان ریشه - دوشنبه، چهارشنبه، شنبه
    //       new SpecialtyWorkingHour { Id = 8, SpecialtyId = 3, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(8, 30), EndTime = new TimeOnly(16, 30), SlotDurationMinutes = 30 },
    //       new SpecialtyWorkingHour { Id = 9, SpecialtyId = 3, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(8, 30), EndTime = new TimeOnly(16, 30), SlotDurationMinutes = 30 },
    //       new SpecialtyWorkingHour { Id = 10, SpecialtyId = 3, DayOfWeek = DayOfWeek.Saturday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(15, 0), SlotDurationMinutes = 30 },

    //       // ترمیم - همه روزه
    //       new SpecialtyWorkingHour { Id = 11, SpecialtyId = 4, DayOfWeek = DayOfWeek.Sunday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0), SlotDurationMinutes = 30 },
    //       new SpecialtyWorkingHour { Id = 12, SpecialtyId = 4, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0), SlotDurationMinutes = 30 },
    //       new SpecialtyWorkingHour { Id = 13, SpecialtyId = 4, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0), SlotDurationMinutes = 30 },
    //       new SpecialtyWorkingHour { Id = 14, SpecialtyId = 4, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0), SlotDurationMinutes = 30 },
    //       new SpecialtyWorkingHour { Id = 15, SpecialtyId = 4, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0), SlotDurationMinutes = 30 },
    //       new SpecialtyWorkingHour { Id = 16, SpecialtyId = 4, DayOfWeek = DayOfWeek.Saturday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(14, 0), SlotDurationMinutes = 30 },

    //       // ارتودنسی - دوشنبه و چهارشنبه
    //       new SpecialtyWorkingHour { Id = 17, SpecialtyId = 6, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(19, 0), SlotDurationMinutes = 90 },
    //       new SpecialtyWorkingHour { Id = 18, SpecialtyId = 6, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(19, 0), SlotDurationMinutes = 90 }
    //   );
    //}
