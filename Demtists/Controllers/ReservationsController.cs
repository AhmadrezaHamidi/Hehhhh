using Demtists.Domian;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Demtists.Controllers
{

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

        // GET: api/reservations - فقط رزروهای خود کاربر
        [HttpGet]
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
        [Authorize(Roles = "Admin")] // فقط ادمین
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
                .Where(r => r.Id == id && r.UserId == currentUserId) // فقط رزرو خود کاربر
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

        // POST: api/reservations
        [HttpPost]
        public async Task<ActionResult<ReservationDto>> CreateReservation([FromBody]CreateReservationDto createDto)
        {
            var currentUserId = GetCurrentUserId();


            var specialtyExists = await _context.Specialties.AnyAsync(s => s.Id == createDto.SpecialtyId && s.IsActive);
            if (!specialtyExists)
                return BadRequest(new { message = "تخصص پیدا نشد یا غیرفعال است" });

            var hasConflict = await _context.Reservations
                .AnyAsync(r => r.ReservationDate.Date == createDto.ReservationDate.Date &&
                              r.SpecialtyId == createDto.SpecialtyId &&
                              r.Status != ReservationStatus.Cancelled &&
                              ((createDto.StartTime >= r.StartTime && createDto.StartTime < r.EndTime) ||
                               (createDto.EndTime > r.StartTime && createDto.EndTime <= r.EndTime) ||
                               (createDto.StartTime <= r.StartTime && createDto.EndTime >= r.EndTime)));

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
                UserId = currentUserId, // از JWT گرفته می‌شود
                SpecialtyId = createDto.SpecialtyId,
                ReservationDate = createDto.ReservationDate,
                StartTime = createDto.StartTime,
                EndTime = createDto.EndTime,
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

        // GET: api/reservations/available-times
        [HttpGet("available-times")]
        [AllowAnonymous] // این endpoint نیاز به احراز هویت ندارد
        public async Task<ActionResult<IEnumerable<TimeSlot>>> GetAvailableTimes(
            [FromQuery] DateTime date,
            [FromQuery] int specialtyId,
            [FromQuery] int durationMinutes = 30)
        {
            // بررسی وجود تخصص
            var specialtyExists = await _context.Specialties.AnyAsync(s => s.Id == specialtyId && s.IsActive);
            if (!specialtyExists)
                return BadRequest(new { message = "تخصص پیدا نشد یا غیرفعال است" });

            // ساعت کاری کلینیک (8 صبح تا 8 شب)
            var workStartTime = new TimeOnly(8, 0);
            var workEndTime = new TimeOnly(20, 0);

            // رزروهای موجود در این روز
            var existingReservations = await _context.Reservations
                .Where(r => r.ReservationDate.Date == date.Date &&
                           r.SpecialtyId == specialtyId &&
                           r.Status != ReservationStatus.Cancelled)
                .Select(r => new { r.StartTime, r.EndTime })
                .ToListAsync();

            var availableSlots = new List<TimeSlot>();
            var currentTime = workStartTime;
            var duration = TimeSpan.FromMinutes(durationMinutes);

            while (currentTime.Add(duration) <= workEndTime)
            {
                var endTime = TimeOnly.FromTimeSpan(currentTime.ToTimeSpan().Add(duration));

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

                currentTime = TimeOnly.FromTimeSpan(currentTime.ToTimeSpan().Add(TimeSpan.FromMinutes(30)));
            }

            return Ok(availableSlots);
        }

        // GET: api/reservations/user/{userId} - حذف شد چون دیگه نیاز نیست
        // کاربر از GET /api/reservations استفاده می‌کندTimeSpan().Add(TimeSpan.FromMinutes(30)));

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<ReservationDto>>> GetUserReservations(int userId)
        {
            var reservations = await _context.Reservations
                .Include(r => r.Specialty)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.ReservationDate)
                .ThenByDescending(r => r.StartTime)
                .Select(r => new ReservationDto
                {
                    Id = r.Id,
                    UserName = "", // خالی چون خود کاربر است
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

        private async Task<bool> ReservationExists(int id)
        {
            return await _context.Reservations.AnyAsync(e => e.Id == id);
        }
    }
}


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
    // UserId حذف شد چون از JWT گرفته می‌شود

    [Required]
    public int SpecialtyId { get; set; }

    [Required]
    public DateTime ReservationDate { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }
}

public class UpdateReservationDto
{
    public DateTime? ReservationDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    // Status حذف شد چون کاربر عادی نمی‌تواند status تغییر دهد
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
