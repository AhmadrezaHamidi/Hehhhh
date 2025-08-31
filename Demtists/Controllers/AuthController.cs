using Demtists.Dtos;
using Demtists.Services;
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

    //[HttpPost("login")]
    //public async Task<IActionResult> Login([FromBody] SendVerificationDto dto)
    //{
    //    var result = await _authService.LoginAsync(dto.PhoneNumber);
        
    //    if (result.IsError)
    //        return BadRequest(new { message = result.FirstError.Description });

    //    return Ok(result.Value);
    //}
}