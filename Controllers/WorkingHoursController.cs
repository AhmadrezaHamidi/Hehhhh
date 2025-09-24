using System;
using Demtists.Domian;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace Demtists.Controllers;

// مدل برای ساعات کاری


// کنترلر برای مدیریت ساعات کاری
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")] // فقط ادمین
public class WorkingHoursController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public WorkingHoursController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/workinghours/specialty/{specialtyId}
    [HttpGet("specialty/{specialtyId}")]
    public async Task<ActionResult<SpecialtyWorkingHoursDto>> GetSpecialtyWorkingHours(int specialtyId)
    {
        var specialty = await _context.Specialties
            .Where(s => s.Id == specialtyId && s.IsActive)
            .Select(s => new { s.Id, s.Name })
            .FirstOrDefaultAsync();

        if (specialty == null)
            return NotFound(new { message = "تخصص پیدا نشد" });

        var workingHours = await _context.SpecialtyWorkingHours
            .Where(w => w.SpecialtyId == specialtyId && w.IsActive)
            .OrderBy(w => w.DayOfWeek)
            .ThenBy(w => w.StartTime)
            .Select(w => new WorkingHourDto
            {
                Id = w.Id,
                DayOfWeek = w.DayOfWeek,
                DayName = GetDayName(w.DayOfWeek),
                StartTime = w.StartTime,
                EndTime = w.EndTime,
                SlotDurationMinutes = w.SlotDurationMinutes
            })
            .ToListAsync();

        return Ok(new SpecialtyWorkingHoursDto
        {
            SpecialtyId = specialtyId,
            SpecialtyName = specialty.Name,
            WorkingHours = workingHours
        });
    }

    // POST: api/workinghours
    [HttpPost]
    public async Task<ActionResult> CreateWorkingHours([FromBody] CreateWorkingHoursDto dto)
    {
        // بررسی وجود تخصص
        var specialtyExists = await _context.Specialties
            .AnyAsync(s => s.Id == dto.SpecialtyId && s.IsActive);

        if (!specialtyExists)
            return BadRequest(new { message = "تخصص پیدا نشد" });

        // بررسی تداخل زمانی در همان روز
        var hasConflict = await _context.SpecialtyWorkingHours
            .AnyAsync(w => w.SpecialtyId == dto.SpecialtyId &&
                          w.DayOfWeek == dto.DayOfWeek &&
                          w.IsActive &&
                          ((dto.StartTime >= w.StartTime && dto.StartTime < w.EndTime) ||
                           (dto.EndTime > w.StartTime && dto.EndTime <= w.EndTime) ||
                           (dto.StartTime <= w.StartTime && dto.EndTime >= w.EndTime)));

        if (hasConflict)
            return BadRequest(new { message = "در این زمان ساعت کاری دیگری تعریف شده است" });

        var workingHour = new SpecialtyWorkingHour
        {
            SpecialtyId = dto.SpecialtyId,
            DayOfWeek = dto.DayOfWeek,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            SlotDurationMinutes = dto.SlotDurationMinutes
        };

        _context.SpecialtyWorkingHours.Add(workingHour);
        await _context.SaveChangesAsync();

        return Ok(new { message = "ساعت کاری با موفقیت اضافه شد", id = workingHour.Id });
    }

    // PUT: api/workinghours/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateWorkingHours(int id, [FromBody] UpdateWorkingHoursDto dto)
    {
        var workingHour = await _context.SpecialtyWorkingHours.FindAsync(id);
        if (workingHour == null)
            return NotFound(new { message = "ساعت کاری پیدا نشد" });

        // بررسی تداخل (غیر از خود رکورد فعلی)
        var hasConflict = await _context.SpecialtyWorkingHours
            .AnyAsync(w => w.Id != id &&
                          w.SpecialtyId == workingHour.SpecialtyId &&
                          w.DayOfWeek == (dto.DayOfWeek ?? workingHour.DayOfWeek) &&
                          w.IsActive &&
                          (((dto.StartTime ?? workingHour.StartTime) >= w.StartTime &&
                            (dto.StartTime ?? workingHour.StartTime) < w.EndTime) ||
                           ((dto.EndTime ?? workingHour.EndTime) > w.StartTime &&
                            (dto.EndTime ?? workingHour.EndTime) <= w.EndTime) ||
                           ((dto.StartTime ?? workingHour.StartTime) <= w.StartTime &&
                            (dto.EndTime ?? workingHour.EndTime) >= w.EndTime)));

        if (hasConflict)
            return BadRequest(new { message = "در این زمان ساعت کاری دیگری تعریف شده است" });

        // به‌روزرسانی فیلدها
        if (dto.DayOfWeek.HasValue) workingHour.DayOfWeek = dto.DayOfWeek.Value;
        if (dto.StartTime.HasValue) workingHour.StartTime = dto.StartTime.Value;
        if (dto.EndTime.HasValue) workingHour.EndTime = dto.EndTime.Value;
        if (dto.SlotDurationMinutes.HasValue) workingHour.SlotDurationMinutes = dto.SlotDurationMinutes.Value;

        await _context.SaveChangesAsync();
        return Ok(new { message = "ساعت کاری با موفقیت به‌روزرسانی شد" });
    }

    // DELETE: api/workinghours/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteWorkingHours(int id)
    {
        var workingHour = await _context.SpecialtyWorkingHours.FindAsync(id);
        if (workingHour == null)
            return NotFound(new { message = "ساعت کاری پیدا نشد" });

        workingHour.IsActive = false; // Soft delete
        await _context.SaveChangesAsync();

        return Ok(new { message = "ساعت کاری با موفقیت حذف شد" });
    }

    // GET: api/workinghours/generate-slots/{specialtyId}
    [HttpGet("generate-slots/{specialtyId}")]
    [AllowAnonymous] // برای استفاده عمومی
    public async Task<ActionResult<List<DailyAvailableSlotsDto>>> GenerateAvailableSlots(
        int specialtyId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        if (fromDate > toDate)
            return BadRequest(new { message = "تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد" });

        if ((toDate - fromDate).TotalDays > 7) // محدود به یک هفته
            return BadRequest(new { message = "بازه زمانی نمی‌تواند بیشتر از 7 روز باشد" });

        // گرفتن ساعات کاری
        var workingHours = await _context.SpecialtyWorkingHours
            .Where(w => w.SpecialtyId == specialtyId && w.IsActive)
            .ToListAsync();

        if (!workingHours.Any())
            return Ok(new List<DailyAvailableSlotsDto>()); // هیچ ساعت کاری تعریف نشده

        // گرفتن رزروهای موجود
        var existingReservations = await _context.Reservations
            .Where(r => r.SpecialtyId == specialtyId &&
                       r.ReservationDate.Date >= fromDate.Date &&
                       r.ReservationDate.Date <= toDate.Date &&
                       r.Status != ReservationStatus.Cancelled)
            .Select(r => new { r.ReservationDate, r.StartTime, r.EndTime })
            .ToListAsync();

        var result = new List<DailyAvailableSlotsDto>();

        for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
        {
            var dayOfWeek = date.DayOfWeek;
            var dayWorkingHours = workingHours.Where(w => w.DayOfWeek == dayOfWeek).ToList();

            if (!dayWorkingHours.Any())
                continue; // این روز ساعت کاری ندارد

            var dailySlots = new List<TimeSlotDto>();

            foreach (var workingHour in dayWorkingHours)
            {
                var currentTime = workingHour.StartTime;
                var slotDuration = TimeSpan.FromMinutes(workingHour.SlotDurationMinutes);

                while (currentTime.Add(slotDuration) <= workingHour.EndTime)
                {
                    var endTime = TimeOnly.FromTimeSpan(currentTime.ToTimeSpan().Add(slotDuration));

                    // بررسی اینکه این اسلات رزرو شده یا نه
                    var isReserved = existingReservations.Any(r =>
                        r.ReservationDate.Date == date &&
                        ((currentTime >= r.StartTime && currentTime < r.EndTime) ||
                         (endTime > r.StartTime && endTime <= r.EndTime) ||
                         (currentTime <= r.StartTime && endTime >= r.EndTime)));

                    if (!isReserved)
                    {
                        dailySlots.Add(new TimeSlotDto
                        {
                            StartTime = currentTime,
                            EndTime = endTime
                        });
                    }

                    currentTime = TimeOnly.FromTimeSpan(currentTime.ToTimeSpan().Add(slotDuration));
                }
            }

            if (dailySlots.Any())
            {
                result.Add(new DailyAvailableSlotsDto
                {
                    Date = date,
                    DayName = GetDayName(dayOfWeek),
                    AvailableSlots = dailySlots.OrderBy(s => s.StartTime).ToList(),
                    TotalSlots = dailySlots.Count
                });
            }
        }

        return Ok(result);
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
public class SpecialtyWorkingHoursDto
{
    public int SpecialtyId { get; set; }
    public string SpecialtyName { get; set; } = string.Empty;
    public List<WorkingHourDto> WorkingHours { get; set; } = new();
}

public class WorkingHourDto
{
    public int Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int SlotDurationMinutes { get; set; }
}

public class CreateWorkingHoursDto
{
    [Required]
    public int SpecialtyId { get; set; }

    [Required]
    public DayOfWeek DayOfWeek { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    public int SlotDurationMinutes { get; set; } = 30;
}

public class TimeSlotDto
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public class DailyAvailableSlotsDto
{
    public DateTime Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public List<TimeSlotDto> AvailableSlots { get; set; } = new();
    public int TotalSlots { get; set; }
}
public class UpdateWorkingHoursDto
{
    public DayOfWeek? DayOfWeek { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public int? SlotDurationMinutes { get; set; }
}