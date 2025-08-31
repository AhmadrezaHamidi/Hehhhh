using System.ComponentModel.DataAnnotations;

namespace Demtists.Dtos;

public class SendVerificationDto
{
    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;
}