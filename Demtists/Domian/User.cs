using System.ComponentModel.DataAnnotations;

namespace Demtists.Domian;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [StringLength(11)]
    public string NationalId { get; set; } = string.Empty;

    [Required]
    [StringLength(11)]
    public string PhoneNumber { get; set; } = string.Empty;

    public bool IsPhoneVerified { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

public class Reservation
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public int SpecialtyId { get; set; }

    public DateTime ReservationDate { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;


    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    public virtual User User { get; set; } = null!;
    public virtual Specialty Specialty { get; set; } = null!;
}

public class Specialty
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool HasInstallments { get; set; } // برای ایمپلنت اقساط

    public bool IsActive { get; set; } = true;

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

public enum ReservationStatus
{
    Pending = 1,
    Confirmed = 2,
    Cancelled = 3,
    Completed = 4
}