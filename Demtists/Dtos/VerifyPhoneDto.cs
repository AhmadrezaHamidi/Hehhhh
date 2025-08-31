using System.ComponentModel.DataAnnotations;

namespace Demtists.Dtos;

public class VerifyPhoneDto
{
    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required]
    [StringLength(6)]
    public string VerificationCode { get; set; } = string.Empty;
}