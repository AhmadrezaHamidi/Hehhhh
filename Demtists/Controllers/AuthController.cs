using System.ComponentModel.DataAnnotations;
using System.Data;
using Demtists.Dtos;
using Demtists.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demtists.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("send-verification")]
    public async Task<IActionResult> SendVerification([FromBody] SendVerificationDto dto)
    {
        var result = await _authService.SendVerificationCodeAsync(dto.PhoneNumber);
        
        if (result.IsError)
            return BadRequest(new { message = result.FirstError.Description });

        return Ok(new { message = result.Value });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        var result = await _authService.RegisterUserAsync(registerDto);
        
        if (result.IsError)
            return BadRequest(new { message = result.FirstError.Description });

        return Ok(result.Value);
    }

    [HttpPost("verify-phone")]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyPhoneDto verifyDto)
    {
        var result = await _authService.VerifyPhoneAsync(verifyDto);
        
        if (result.IsError)
            return BadRequest(new { message = result.FirstError.Description });

        return Ok(result.Value);
    }
    [HttpPost("register-admin")]
    [Authorize(Roles = "Admin")] // فقط ادمین می‌تواند ادمین جدید ثبت کند
    public async Task<IActionResult> RegisterAdmin([FromBody] RegisterAdminDto registerDto)
    {
        var result = await _authService.RegisterAdminAsync(registerDto);

        if (result.IsError)
            return BadRequest(new { message = result.FirstError.Description });
        return Ok(new { phoneNumber = result.Value, message = "ادمین ثبت شد. کد تأیید ارسال شد" });
    }


    public class RegisterAdminDto
    {
        [Required(ErrorMessage = "نام الزامی است")]
        [StringLength(50, ErrorMessage = "نام نمی‌تواند بیش از 50 کاراکتر باشد")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام خانوادگی الزامی است")]
        [StringLength(50, ErrorMessage = "نام خانوادگی نمی‌تواند بیش از 50 کاراکتر باشد")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "کد ملی الزامی است")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "کد ملی باید 10 رقم باشد")]
        public string NationalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "شماره موبایل الزامی است")]
        [StringLength(11, MinimumLength = 11, ErrorMessage = "شماره موبایل باید 11 رقم باشد")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}