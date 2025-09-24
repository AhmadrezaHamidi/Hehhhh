using Demtists.Domian;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Demtists.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // اضافه کردن Authorization
public class ReservationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReservationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Helper method برای گرفتن User ID از JWT
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }

    // Helper method برای بررسی اینکه آیا کاربر verified هست
    private bool IsCurrentUserPhoneVerified()
    {
        var isVerifiedClaim = User.FindFirst("IsPhoneVerified")?.Value;
        return bool.TryParse(isVerifiedClaim, out bool isVerified) && isVerified;
    }

    // GET: api/reservations/statuses-auto
    [HttpGet("statuses")]
    [AllowAnonymous]
    public ActionResult<IEnumerable<ReservationStatusDto>> GetReservationStatuses()
    {
        var statuses = Enum.GetValues<ReservationStatus>()
            .Select(status => new ReservationStatusDto
            {
                Value = (int)status,
                Name = status.ToString(),
                Description = GetStatusDescription(status),
                DisplayName = GetStatusDisplayName(status)
            })
            .ToList();

        return Ok(statuses);
    }

    // GET: api/reservations/specialty/{specialtyId}/reserved-times
    [HttpGet("specialty/{specialtyId}/reserved-times")]
    [AllowAnonymous]
    public async Task<ActionResult<SpecialtyReservedTimesDto>> GetSpecialtyReservedTimes(
        int specialtyId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        // بررسی معتبر بودن بازه زمانی
        if (fromDate > toDate)
            return BadRequest(new { message = "تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد" });

        // محدود کردن بازه زمانی (مثلاً حداکثر 30 روز)
        if ((toDate - fromDate).TotalDays > 30)
            return BadRequest(new { message = "بازه زمانی نمی‌تواند بیشتر از 30 روز باشد" });

        // بررسی وجود specialty
        var specialty = await _context.Specialties
            .Where(s => s.Id == specialtyId && s.IsActive)
            .Select(s => new { s.Id, s.Name })
            .FirstOrDefaultAsync();

        if (specialty == null)
            return NotFound(new { message = "تخصص پیدا نشد یا غیرفعال است" });

        // گرفتن رزروهای غیر کنسل شده در بازه زمانی مشخص
        var reservedTimes = await _context.Reservations
            .Where(r => r.SpecialtyId == specialtyId &&
                       r.ReservationDate.Date >= fromDate.Date &&
                       r.ReservationDate.Date <= toDate.Date &&
                       r.Status != ReservationStatus.Cancelled)
            .OrderBy(r => r.ReservationDate)
            .ThenBy(r => r.StartTime)
            .Select(r => new ReservedTimeDto
            {
                Id = r.Id,
                Date = r.ReservationDate,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                Status = r.Status
            })
            .ToListAsync();

        // گروه‌بندی بر اساس تاریخ
        var dailyReservedTimes = reservedTimes
            .GroupBy(r => r.Date.Date)
            .Select(g => new DailyReservedTimesDto
            {
                Date = g.Key,
                ReservedTimes = g.ToList(),
                TotalReservations = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        var result = new SpecialtyReservedTimesDto
        {
            SpecialtyId = specialtyId,
            SpecialtyName = specialty.Name,
            FromDate = fromDate,
            ToDate = toDate,
            DailyReservedTimes = dailyReservedTimes,
            TotalReservations = reservedTimes.Count,
            Summary = new ReservationSummaryDto
            {
                PendingCount = reservedTimes.Count(r => r.Status == ReservationStatus.Pending),
                ConfirmedCount = reservedTimes.Count(r => r.Status == ReservationStatus.Confirmed),
                CompletedCount = reservedTimes.Count(r => r.Status == ReservationStatus.Completed)
            }
        };

        return Ok(result);
    }

    // GET: api/reservations/MyReserves - فقط رزروهای خود کاربر
    [HttpGet("MyReserves")]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetMyReservations(
        [FromQuery] int? specialtyId = null,
        [FromQuery] ReservationStatus? status = null,
        [FromQuery] DateTime? date = null)
    {
        var currentUserId = GetCurrentUserId();

        var query = _context.Reservations
            .Include(r => r.User)
            .Include(r => r.Specialty)
            .Where(r => r.UserId == currentUserId);

        if (specialtyId.HasValue)
            query = query.Where(r => r.SpecialtyId == specialtyId.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (date.HasValue)
            query = query.Where(r => r.ReservationDate.Date == date.Value.Date);

        var reservations = await query
            .OrderBy(r => r.ReservationDate)
            .ThenBy(r => r.StartTime)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                UserName = $"{r.User.FirstName} {r.User.LastName}",
                SpecialtyId = r.SpecialtyId,
                SpecialtyName = r.Specialty.Name,
                ReservationDate = r.ReservationDate,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();

        return Ok(reservations);
    }

    // GET: api/reservations/all - برای ادمین (همه رزروها)
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetAllReservations(
        [FromQuery] int? userId = null,
        [FromQuery] int? specialtyId = null,
        [FromQuery] ReservationStatus? status = null,
        [FromQuery] DateTime? date = null)
    {
        var query = _context.Reservations
            .Include(r => r.User)
            .Include(r => r.Specialty)
            .AsQueryable();

        if (userId.HasValue)
            query = query.Where(r => r.UserId == userId.Value);

        if (specialtyId.HasValue)
            query = query.Where(r => r.SpecialtyId == specialtyId.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (date.HasValue)
            query = query.Where(r => r.ReservationDate.Date == date.Value.Date);

        var reservations = await query
            .OrderBy(r => r.ReservationDate)
            .ThenBy(r => r.StartTime)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                UserName = $"{r.User.FirstName} {r.User.LastName}",
                SpecialtyId = r.SpecialtyId,
                SpecialtyName = r.Specialty.Name,
                ReservationDate = r.ReservationDate,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();

        return Ok(reservations);
    }

    // GET: api/reservations/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ReservationDto>> GetReservation(int id)
    {
        var currentUserId = GetCurrentUserId();

        var reservation = await _context.Reservations
            .Include(r => r.User)
            .Include(r => r.Specialty)
            .Where(r => r.Id == id && r.UserId == currentUserId)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                UserName = $"{r.User.FirstName} {r.User.LastName}",
                SpecialtyId = r.SpecialtyId,
                SpecialtyName = r.Specialty.Name,
                ReservationDate = r.ReservationDate,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (reservation == null)
            return NotFound(new { message = "رزرو پیدا نشد" });

        return Ok(reservation);
    }

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> CreateReservation([FromBody] CreateReservationDto createDto)
    {
        var currentUserId = GetCurrentUserId();

        // تبدیل string به TimeOnly
        if (!TimeOnly.TryParse(createDto.StartTime, out TimeOnly startTime))
            return BadRequest(new { message = "فرمت ساعت شروع نامعتبر است" });

        if (!TimeOnly.TryParse(createDto.EndTime, out TimeOnly endTime))
            return BadRequest(new { message = "فرمت ساعت پایان نامعتبر است" });

        var specialtyExists = await _context.Specialties.AnyAsync(s => s.Id == createDto.SpecialtyId && s.IsActive);
        if (!specialtyExists)
            return BadRequest(new { message = "تخصص پیدا نشد یا غیرفعال است" });

        var hasConflict = await _context.Reservations
            .AnyAsync(r => r.ReservationDate.Date == createDto.ReservationDate.Date &&
                          r.SpecialtyId == createDto.SpecialtyId &&
                          r.Status != ReservationStatus.Cancelled &&
                          ((startTime >= r.StartTime && startTime < r.EndTime) ||
                           (endTime > r.StartTime && endTime <= r.EndTime) ||
                           (startTime <= r.StartTime && endTime >= r.EndTime)));

        if (hasConflict)
            return BadRequest(new { message = "در این زمان رزرو دیگری وجود دارد" });

        // بررسی اینکه کاربر در همان روز رزرو دیگری نداشته باشد
        var hasExistingReservationOnSameDay = await _context.Reservations
            .AnyAsync(r => r.UserId == currentUserId &&
                          r.ReservationDate.Date == createDto.ReservationDate.Date &&
                          r.Status != ReservationStatus.Cancelled);

        if (hasExistingReservationOnSameDay)
            return BadRequest(new { message = "شما در این روز قبلاً رزرو دارید" });

        var reservation = new Reservation
        {
            UserId = currentUserId,
            SpecialtyId = createDto.SpecialtyId,
            ReservationDate = createDto.ReservationDate,
            StartTime = startTime, // استفاده از TimeOnly تبدیل شده
            EndTime = endTime,     // استفاده از TimeOnly تبدیل شده
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // دریافت رزرو کامل با اطلاعات مرتبط
        var createdReservation = await _context.Reservations
            .Include(r => r.User)
            .Include(r => r.Specialty)
            .Where(r => r.Id == reservation.Id)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                UserName = $"{r.User.FirstName} {r.User.LastName}",
                SpecialtyId = r.SpecialtyId,
                SpecialtyName = r.Specialty.Name,
                ReservationDate = r.ReservationDate,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .FirstAsync();

        return CreatedAtAction(nameof(GetReservation), new { id = reservation.Id }, createdReservation);
    }


    // PUT: api/reservations/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateReservation(int id, UpdateReservationDto updateDto)
    {
        var currentUserId = GetCurrentUserId();

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == currentUserId);

        if (reservation == null)
            return NotFound(new { message = "رزرو پیدا نشد یا شما مجاز به ویرایش آن نیستید" });

        // فقط رزروهای Pending قابل ویرایش هستند
        if (reservation.Status != ReservationStatus.Pending)
            return BadRequest(new { message = "فقط رزروهای در انتظار تایید قابل ویرایش هستند" });

        // اگر زمان تغییر کند، بررسی تداخل
        if (updateDto.ReservationDate.HasValue || updateDto.StartTime.HasValue || updateDto.EndTime.HasValue)
        {
            var newDate = updateDto.ReservationDate ?? reservation.ReservationDate;
            var newStartTime = updateDto.StartTime ?? reservation.StartTime;
            var newEndTime = updateDto.EndTime ?? reservation.EndTime;

            var hasConflict = await _context.Reservations
                .AnyAsync(r => r.Id != id &&
                              r.ReservationDate.Date == newDate.Date &&
                              r.SpecialtyId == reservation.SpecialtyId &&
                              r.Status != ReservationStatus.Cancelled &&
                              ((newStartTime >= r.StartTime && newStartTime < r.EndTime) ||
                               (newEndTime > r.StartTime && newEndTime <= r.EndTime) ||
                               (newStartTime <= r.StartTime && newEndTime >= r.EndTime)));

            if (hasConflict)
                return BadRequest(new { message = "در این زمان رزرو دیگری وجود دارد" });

            reservation.ReservationDate = newDate;
            reservation.StartTime = newStartTime;
            reservation.EndTime = newEndTime;
        }

        reservation.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
            return Ok(new { message = "رزرو با موفقیت به‌روزرسانی شد" });
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await ReservationExists(id))
                return NotFound(new { message = "رزرو پیدا نشد" });
            throw;
        }
    }

    // DELETE: api/reservations/{id} - فقط کنسل کردن
    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelReservation(int id)
    {
        var currentUserId = GetCurrentUserId();

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == currentUserId);

        if (reservation == null)
            return NotFound(new { message = "رزرو پیدا نشد" });

        // فقط رزروهای Pending و Confirmed قابل کنسل هستند
        if (reservation.Status == ReservationStatus.Cancelled)
            return BadRequest(new { message = "این رزرو قبلاً کنسل شده است" });

        if (reservation.Status == ReservationStatus.Completed)
            return BadRequest(new { message = "نمی‌توان رزرو تکمیل شده را کنسل کرد" });

        // بررسی زمان کنسلی (مثلاً حداقل 24 ساعت قبل)
        var minCancelTime = DateTime.Now.AddHours(24);
        var reservationDateTime = reservation.ReservationDate.Add(reservation.StartTime.ToTimeSpan());

        if (reservationDateTime <= minCancelTime)
            return BadRequest(new { message = "حداقل 24 ساعت قبل از موعد رزرو باید کنسل کنید" });

        reservation.Status = ReservationStatus.Cancelled;
        reservation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = "رزرو با موفقیت کنسل شد" });
    }

    // PATCH: api/reservations/{id}/status
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateReservationStatus(int id, [FromBody] UpdateStatusDto statusDto)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null)
            return NotFound(new { message = "رزرو پیدا نشد" });

        reservation.Status = statusDto.Status;
        reservation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = "وضعیت رزرو با موفقیت تغییر کرد" });
    }

    // GET: api/reservations/available-times - بر اساس ساعات کاری تعریف شده
    [HttpGet("available-times")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TimeSlot>>> GetAvailableTimes(
        [FromQuery] DateTime date,
        [FromQuery] int specialtyId)
    {
        // بررسی وجود تخصص
        var specialtyExists = await _context.Specialties.AnyAsync(s => s.Id == specialtyId && s.IsActive);
        if (!specialtyExists)
            return BadRequest(new { message = "تخصص پیدا نشد یا غیرفعال است" });

        // گرفتن ساعات کاری برای این تخصص و روز هفته
        var dayOfWeek = date.DayOfWeek;

        var workingHours = await _context.SpecialtyWorkingHours
            .Where(w => w.SpecialtyId == specialtyId &&
                       w.DayOfWeek == dayOfWeek &&
                       w.IsActive)
            .ToListAsync();

        // اگر برای این روز ساعت کاری تعریف نشده باشد
        if (!workingHours.Any())
        {
            return Ok(new List<TimeSlot>());
        }

        // رزروهای موجود در این روز
        var existingReservations = await _context.Reservations
            .Where(r => r.ReservationDate.Date == date.Date &&
                       r.SpecialtyId == specialtyId &&
                       r.Status != ReservationStatus.Cancelled)
            .Select(r => new { r.StartTime, r.EndTime })
            .ToListAsync();

        var availableSlots = new List<TimeSlot>();

        // برای هر بازه ساعت کاری
        foreach (var workingHour in workingHours)
        {
            var currentTime = workingHour.StartTime;
            var workEndTime = workingHour.EndTime;
            var slotDuration = TimeSpan.FromMinutes(workingHour.SlotDurationMinutes);

            while (currentTime.Add(slotDuration) <= workEndTime)
            {
                var endTime = TimeOnly.FromTimeSpan(currentTime.ToTimeSpan().Add(slotDuration));

                // بررسی اینکه این اسلات رزرو شده یا نه
                var isAvailable = !existingReservations.Any(r =>
                    (currentTime >= r.StartTime && currentTime < r.EndTime) ||
                    (endTime > r.StartTime && endTime <= r.EndTime) ||
                    (currentTime <= r.StartTime && endTime >= r.EndTime));

                if (isAvailable)
                {
                    availableSlots.Add(new TimeSlot
                    {
                        StartTime = currentTime,
                        EndTime = endTime,
                        IsAvailable = true
                    });
                }

                currentTime = TimeOnly.FromTimeSpan(currentTime.ToTimeSpan().Add(slotDuration));
            }
        }

        // مرتب‌سازی بر اساس زمان شروع
        var sortedSlots = availableSlots.OrderBy(s => s.StartTime).ToList();
        return Ok(sortedSlots);
    }

    // GET: api/reservations/working-hours/{specialtyId}
    [HttpGet("working-hours/{specialtyId}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<WorkingHourInfoDto>>> GetSpecialtyWorkingHours(int specialtyId)
    {
        var specialty = await _context.Specialties
            .Where(s => s.Id == specialtyId && s.IsActive)
            .FirstOrDefaultAsync();

        if (specialty == null)
            return NotFound(new { message = "تخصص پیدا نشد" });

        var workingHours = await _context.SpecialtyWorkingHours
            .Where(w => w.SpecialtyId == specialtyId && w.IsActive)
            .OrderBy(w => w.DayOfWeek)
            .ThenBy(w => w.StartTime)
            .Select(w => new WorkingHourInfoDto
            {
                DayOfWeek = w.DayOfWeek,
                DayName = GetDayName(w.DayOfWeek),
                StartTime = w.StartTime,
                EndTime = w.EndTime,
                SlotDurationMinutes = w.SlotDurationMinutes
            })
            .ToListAsync();

        return Ok(workingHours);
    }

    // GET: api/reservations/user/{userId} - برای ادمین
    [HttpGet("user/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetUserReservations(int userId)
    {
        var reservations = await _context.Reservations
            .Include(r => r.User)
            .Include(r => r.Specialty)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReservationDate)
            .ThenByDescending(r => r.StartTime)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                UserName = $"{r.User.FirstName} {r.User.LastName}",
                SpecialtyId = r.SpecialtyId,
                SpecialtyName = r.Specialty.Name,
                ReservationDate = r.ReservationDate,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();

        return Ok(reservations);
    }

    // Helper methods
    private async Task<bool> ReservationExists(int id)
    {
        return await _context.Reservations.AnyAsync(e => e.Id == id);
    }

    private string GetStatusDescription(ReservationStatus status)
    {
        return status switch
        {
            ReservationStatus.Pending => "رزرو ثبت شده اما هنوز تایید نشده است",
            ReservationStatus.Confirmed => "رزرو تایید شده و قطعی است",
            ReservationStatus.Cancelled => "رزرو لغو شده است",
            ReservationStatus.Completed => "رزرو انجام شده و تکمیل گردیده است",
            _ => "وضعیت نامشخص"
        };
    }

    private string GetStatusDisplayName(ReservationStatus status)
    {
        return status switch
        {
            ReservationStatus.Pending => "منتظر تایید",
            ReservationStatus.Confirmed => "تایید شده",
            ReservationStatus.Cancelled => "لغو شده",
            ReservationStatus.Completed => "انجام شده",
            _ => "نامشخص"
        };
    }

    private string GetDayName(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Sunday => "یکشنبه",
            DayOfWeek.Monday => "دوشنبه",
            DayOfWeek.Tuesday => "سه‌شنبه",
            DayOfWeek.Wednesday => "چهارشنبه",
            DayOfWeek.Thursday => "پنج‌شنبه",
            DayOfWeek.Friday => "جمعه",
            DayOfWeek.Saturday => "شنبه",
            _ => "نامشخص"
        };
    }
}

// DTOs
public class ReservationDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int SpecialtyId { get; set; }
    public string SpecialtyName { get; set; } = string.Empty;
    public DateTime ReservationDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateReservationDto
{
    [Required]
    public int SpecialtyId { get; set; }

    [Required]
    public DateTime ReservationDate { get; set; }

    [Required]
    public string StartTime { get; set; }  // انتظار string دارد، نه object

    [Required]
    public string EndTime { get; set; }    // انتظار string دارد، نه object
}




public class UpdateReservationDto
{
    public DateTime? ReservationDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
}

public class UpdateStatusDto
{
    [Required]
    public ReservationStatus Status { get; set; }
}

public class TimeSlot
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsAvailable { get; set; }
}

public class ReservationStatusDto
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class SpecialtyReservedTimesDto
{
    public int SpecialtyId { get; set; }
    public string SpecialtyName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<DailyReservedTimesDto> DailyReservedTimes { get; set; } = new();
    public int TotalReservations { get; set; }
    public ReservationSummaryDto Summary { get; set; } = new();
}

public class DailyReservedTimesDto
{
    public DateTime Date { get; set; }
    public List<ReservedTimeDto> ReservedTimes { get; set; } = new();
    public int TotalReservations { get; set; }
}

public class ReservedTimeDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public ReservationStatus Status { get; set; }
}

public class ReservationSummaryDto
{
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int CompletedCount { get; set; }
}

public class WorkingHourInfoDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int SlotDurationMinutes { get; set; }
}