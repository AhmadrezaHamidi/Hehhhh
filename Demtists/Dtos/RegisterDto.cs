using System.ComponentModel.DataAnnotations;

namespace Demtists.Dtos;

public class RegisterDto
{
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
    [Phone]
    [StringLength(11)]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}