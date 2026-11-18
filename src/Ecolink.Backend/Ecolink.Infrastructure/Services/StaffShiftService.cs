using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Điểm danh ca làm của Staff (UC 6.2, BR-GAM-09): mỗi ngày một lần vào ca / ra ca, tính giờ làm và chuỗi ngày liên tiếp
/// (bỏ lỡ một ngày thì chuỗi về 1). Staff không nhận điểm thưởng từ việc này.
/// </summary>
public class StaffShiftService : IStaffShiftService
{
    private readonly ApplicationDbContext _db;

    public StaffShiftService(ApplicationDbContext db) => _db = db;

    public async Task<ShiftStatusDto> GetStatusAsync(Guid staffId, CancellationToken ct = default)
    {
        var today = GamTime.Today();
        var todayShift = await _db.StaffShifts.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == staffId && s.WorkDate == today, ct);
        var last = await _db.StaffShifts.AsNoTracking().Where(s => s.StaffId == staffId).OrderByDescending(s => s.WorkDate).FirstOrDefaultAsync(ct);
        var streak = last == null ? 0 : (last.WorkDate >= today.AddDays(-1) ? last.StreakCount : 0);

        return new ShiftStatusDto
        {
            Today = today,
            CanCheckIn = todayShift == null,
            CanCheckOut = todayShift != null && todayShift.CheckOutAt == null,
            CurrentStreak = streak,
            TodayShift = todayShift == null ? null : ToDto(todayShift)
        };
    }

    public async Task<ShiftDto> CheckInAsync(Guid staffId, CancellationToken ct = default)
    {
        var today = GamTime.Today();
        var last = await _db.StaffShifts.AsNoTracking().Where(s => s.StaffId == staffId).OrderByDescending(s => s.WorkDate).FirstOrDefaultAsync(ct);
        if (last != null && last.WorkDate == today)
            throw GamErrors.Rule("BR-GAM-09", "MSG-GEN-01", "Bạn đã vào ca hôm nay rồi.");

        var shift = new StaffShift
        {
            StaffId = staffId, WorkDate = today, CheckInAt = DateTime.UtcNow,
            StreakCount = last != null && last.WorkDate == today.AddDays(-1) ? last.StreakCount + 1 : 1
        };
        _db.StaffShifts.Add(shift);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (GamErrors.IsUniqueViolation(ex, "ux_staff_shifts_staff_date"))
        {
            throw GamErrors.Rule("BR-GAM-09", "MSG-GEN-01", "Bạn đã vào ca hôm nay rồi.");
        }
        return ToDto(shift);
    }

    public async Task<ShiftDto> CheckOutAsync(Guid staffId, CancellationToken ct = default)
    {
        var today = GamTime.Today();
        var shift = await _db.StaffShifts.FirstOrDefaultAsync(s => s.StaffId == staffId && s.WorkDate == today, ct)
                    ?? throw GamErrors.Rule("BR-GAM-09", "MSG-GEN-01", "Bạn chưa vào ca hôm nay nên không thể ra ca.");
        if (shift.CheckOutAt != null)
            throw GamErrors.Rule("BR-GAM-09", "MSG-GEN-01", "Bạn đã ra ca hôm nay rồi.");

        var now = DateTime.UtcNow;
        var hours = Math.Round((decimal)(now - shift.CheckInAt).TotalHours, 2);
        var shiftId = shift.Id;
        var n = await _db.StaffShifts.Where(s => s.Id == shiftId && s.CheckOutAt == null).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.CheckOutAt, (DateTime?)now)
            .SetProperty(x => x.WorkingHours, (decimal?)hours), ct);
        if (n == 0) throw GamErrors.Rule("BR-GAM-09", "MSG-GEN-01", "Bạn đã ra ca hôm nay rồi.");

        return ToDto(await _db.StaffShifts.AsNoTracking().FirstAsync(s => s.Id == shiftId, ct));
    }

    public async Task<GamPagedResult<ShiftDto>> GetHistoryAsync(Guid staffId, int? year, int? month, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.StaffShifts.AsNoTracking().Where(s => s.StaffId == staffId);
        if (year.HasValue)
        {
            var y = year.Value;
            if (y < 2000 || y > 2100) throw GamErrors.Rule("BR-GAM-09", "MSG-GEN-01", "Năm không hợp lệ.");
            if (month.HasValue)
            {
                if (month.Value < 1 || month.Value > 12) throw GamErrors.Rule("BR-GAM-09", "MSG-GEN-01", "Tháng không hợp lệ.");
                var start = new DateOnly(y, month.Value, 1);
                var end = start.AddMonths(1);
                q = q.Where(s => s.WorkDate >= start && s.WorkDate < end);
            }
            else
            {
                var start = new DateOnly(y, 1, 1);
                var end = new DateOnly(y + 1, 1, 1);
                q = q.Where(s => s.WorkDate >= start && s.WorkDate < end);
            }
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(s => s.WorkDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new GamPagedResult<ShiftDto> { Items = rows.Select(ToDto).ToList(), Page = page, PageSize = pageSize, TotalCount = total };
    }

    private static ShiftDto ToDto(StaffShift s) => new()
    {
        Id = s.Id, WorkDate = s.WorkDate, CheckInAt = s.CheckInAt, CheckOutAt = s.CheckOutAt,
        WorkingHours = s.WorkingHours, StreakCount = s.StreakCount
    };
}
