using System.ComponentModel.DataAnnotations;

namespace Demtists.Domian;

public class SmsVerification
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(11)]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required]
    [StringLength(6)]
    public string VerificationCode { get; set; } = string.Empty;
    
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}