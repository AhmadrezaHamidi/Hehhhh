using Demtists.Domian;
using Demtists.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Demtists.Services;
using ErrorOr;
using static Demtists.Controllers.AuthController;

public interface IAuthService
{
    Task<ErrorOr<string>> SendVerificationCodeAsync(string phoneNumber);
    Task<ErrorOr<string>> RegisterUserAsync(RegisterDto registerDto);
    Task<ErrorOr<AuthResponseDto>> VerifyPhoneAsync(VerifyPhoneDto verifyDto);
    Task<ErrorOr<AuthResponseDto>> LoginAsync(string phoneNumber);
    Task<ErrorOr<string>> RegisterAdminAsync(RegisterAdminDto registerDto);
}


public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly ISmsService _smsService;
    private readonly IConfiguration _configuration;

    public AuthService(ApplicationDbContext context, ISmsService smsService, IConfiguration configuration)
    {
        _context = context;
        _smsService = smsService;
        _configuration = configuration;
    }

    public async Task<ErrorOr<string>> RegisterAdminAsync(RegisterAdminDto registerDto)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == registerDto.PhoneNumber || u.NationalId == registerDto.NationalId);

        if (existingUser != null)
        {
            existingUser.Role = UserRole.Admin;
            _context.Users.Update(existingUser); 
        }

        var user = new User
        {
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName,
            NationalId = registerDto.NationalId,
            PhoneNumber = registerDto.PhoneNumber,
            IsPhoneVerified = false,
            Role = UserRole.Admin // نقش ادمین
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // ارسال کد تأیید
        var smsResult = await SendVerificationCodeAsync(registerDto.PhoneNumber);
        if (smsResult.IsError)
        {
            return Error.Failure("Sms.Failed", "ادمین ثبت شد اما کد تأیید ارسال نشد");
        }
        return user.PhoneNumber;
    }


    public async Task<ErrorOr<string>> SendVerificationCodeAsync(string phoneNumber)
    {
        // حذف کدهای قبلی
        var existingCodes = _context.SmsVerifications
            .Where(s => s.PhoneNumber == phoneNumber).ToList();

        _context.SmsVerifications.RemoveRange(existingCodes);

        // تولید کد جدید
        var code = new Random().Next(100000, 999999).ToString();
        var verification = new SmsVerification
        {
            PhoneNumber = phoneNumber,
            VerificationCode = code,
            ExpiresAt = DateTime.Now.AddMinutes(5), // 5 دقیقه مناسب‌تر است
            IsUsed = false // باید false باشد تا قابل استفاده باشد
        };

        _context.SmsVerifications.Add(verification);
        await _context.SaveChangesAsync();

        // ارسال SMS
        var message = $"کد تأیید شما: {code}";
        var smsResult = await _smsService.SendSmsAsync(message, new List<string> { phoneNumber });

        if (smsResult.IsError)
        {
            return Error.Failure("Sms.Failed", "خطا در ارسال پیامک");
        }

        return "کد تأیید ارسال شد";
    }

    public async Task<ErrorOr<string>> RegisterUserAsync(RegisterDto registerDto)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == registerDto.PhoneNumber || u.NationalId == registerDto.NationalId);

        if (existingUser != null)
        {
            return Error.Conflict("User.Exists", "کاربر با این شماره موبایل یا کد ملی قبلاً ثبت نام کرده است");
        }

        var user = new User
        {
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName,
            NationalId = registerDto.NationalId,
            PhoneNumber = registerDto.PhoneNumber,
            IsPhoneVerified = false,
            Role = UserRole.User // پیش‌فرض User
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // ارسال کد تأیید
        var smsResult = await SendVerificationCodeAsync(registerDto.PhoneNumber);
        if (smsResult.IsError)
        {
            return Error.Failure("Sms.Failed", "کاربر ثبت شد اما کد تأیید ارسال نشد");
        }
        return user.PhoneNumber;
    }

    public async Task<ErrorOr<AuthResponseDto>> VerifyPhoneAsync(VerifyPhoneDto verifyDto)
    {
        try
        {
            var verification = await _context.SmsVerifications
            .FirstOrDefaultAsync(s => s.PhoneNumber == verifyDto.PhoneNumber
                && s.VerificationCode == verifyDto.VerificationCode
                && !s.IsUsed // باید استفاده نشده باشد
                && s.ExpiresAt > DateTime.Now);

        if (verification == null)
        {
            return Error.NotFound("Code.Invalid", "کد تأیید نامعتبر یا منقضی شده است");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == verifyDto.PhoneNumber);

        if (user == null)
        {
            return Error.NotFound("User.NotFound", "کاربر یافت نشد");
        }

        user.IsPhoneVerified = true;
        user.UpdatedAt = DateTime.Now;

        verification.IsUsed = true;

        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user);
        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = DateTime.Now.AddMinutes(_configuration.GetValue<int>("Authentication:JwtExpireMins")),
            User = new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                NationalId = user.NationalId,
                IsPhoneVerified = user.IsPhoneVerified,
                Role = user.Role.ToString() // اضافه کردن نقش
            }
        };
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return Error.Failure("Verification.Failed", $"{ex.Message}");
        }

    }

    public async Task<ErrorOr<AuthResponseDto>> LoginAsync(string phoneNumber)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

        if (user == null)
        {
            return Error.NotFound("User.NotFound", "کاربر با این شماره موبایل یافت نشد");
        }

        if (!user.IsPhoneVerified)
        {
            return Error.Validation("Phone.NotVerified", "شماره موبایل تأیید نشده است");
        }

        // ارسال کد تأیید جدید
        var smsResult = await SendVerificationCodeAsync(phoneNumber);
        if (smsResult.IsError)
        {
            return Error.Failure("Sms.Failed", "خطا در ارسال کد تأیید");
        }

        var token = GenerateJwtToken(user);
        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("Authentication:JwtExpireMins")),
            User = new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                NationalId = user.NationalId,
                IsPhoneVerified = user.IsPhoneVerified,
                Role = user.Role.ToString() // اضافه کردن نقش
            }
        };
    }

    private string GenerateJwtToken(User user)
    {
        var jwtKey = _configuration["Authentication:JwtKey"]!;
        var jwtIssuer = _configuration["Authentication:JwtIssuer"]!;
        var jwtAudience = _configuration["Authentication:JwtAudience"]!;
        var jwtExpireMins = _configuration.GetValue<int>("Authentication:JwtExpireMins");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.MobilePhone, user.PhoneNumber),
            new("NationalId", user.NationalId),
            new("IsPhoneVerified", user.IsPhoneVerified.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()) // اضافه کردن نقش به Claims
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwtExpireMins),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
