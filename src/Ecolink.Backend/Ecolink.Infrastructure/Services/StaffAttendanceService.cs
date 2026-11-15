using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Hiện thực hóa nghiệp vụ Chấm công &amp; Nhật ký ca trực hàng ngày của Nhân viên vận hành (UC S-11).
/// Tuân thủ BR-GAM-09 (1 lần/ngày theo UTC+7, đứt chuỗi về Ngày 1) và BR-GAM-08 (tách biệt hoàn toàn với ví điểm công dân).
/// </summary>
public class StaffAttendanceService : IStaffAttendanceService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;

    public StaffAttendanceService(
        ApplicationDbContext dbContext,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
    }

    public async Task<StaffAttendanceStatusDto> CheckInAsync(
        Guid staffUserId,
        StaffCheckInRequest request,
        CancellationToken ct = default)
    {
        var profile = await EnsureStaffProfileExistsAsync(staffUserId, ct);
        var nowUtc = DateTime.UtcNow;
        var todayVn = DateOnly.FromDateTime(nowUtc.Add(VietnamOffset));

        var alreadyCheckedIn = await _dbContext.StaffAttendances
            .AnyAsync(a => a.StaffId == profile.UserId && a.WorkDate == todayVn, ct);

        if (alreadyCheckedIn)
        {
            throw new BusinessRuleException("BR-GAM-09", "MSG-GEN-01",
                "Bạn đã điểm danh ca làm việc hôm nay rồi. Mỗi ngày chỉ điểm danh tối đa 1 lần (BR-GAM-09).");
        }

        // Kiểm tra ngày làm việc liền trước để tính chuỗi 7 ngày (BR-GAM-09)
        var lastAttendance = await _dbContext.StaffAttendances
            .AsNoTracking()
            .Where(a => a.StaffId == profile.UserId && a.WorkDate < todayVn)
            .OrderByDescending(a => a.WorkDate)
            .FirstOrDefaultAsync(ct);

        int streakDay = 1;
        if (lastAttendance != null && lastAttendance.WorkDate == todayVn.AddDays(-1))
        {
            streakDay = (lastAttendance.StreakDay % 7) + 1;
        }

        // Ngày 1..6: +10 điểm nhận việc; Ngày 7: thưởng gấp rưỡi (+15 điểm nhận việc)
        int pointsEarned = streakDay == 7 ? 15 : 10;

        var attendance = new StaffAttendance
        {
            Id = Guid.NewGuid(),
            StaffId = profile.UserId,
            WorkDate = todayVn,
            CheckInAt = nowUtc,
            CheckOutAt = null,
            WorkedMinutes = null,
            StreakDay = streakDay,
            WorkPointsEarned = pointsEarned,
            TasksCompletedSnapshot = 0,
            ShiftNote = string.IsNullOrWhiteSpace(request.ShiftNote) ? null : request.ShiftNote.Trim()
        };

        _dbContext.StaffAttendances.Add(attendance);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            action: "STAFF_CHECK_IN",
            entityType: "StaffAttendance",
            entityId: attendance.Id.ToString(),
            newValues: new { workDate = todayVn.ToString("yyyy-MM-dd"), streakDay, pointsEarned },
            reason: attendance.ShiftNote,
            explicitActorId: staffUserId,
            ct: ct);

        return await GetMyAttendanceAsync(staffUserId, todayVn.Month, todayVn.Year, ct);
    }

    public async Task<StaffAttendanceStatusDto> CheckOutAsync(
        Guid staffUserId,
        StaffCheckOutRequest request,
        CancellationToken ct = default)
    {
        var profile = await EnsureStaffProfileExistsAsync(staffUserId, ct);
        var nowUtc = DateTime.UtcNow;
        var todayVn = DateOnly.FromDateTime(nowUtc.Add(VietnamOffset));

        var todayRecord = await _dbContext.StaffAttendances
            .FirstOrDefaultAsync(a => a.StaffId == profile.UserId && a.WorkDate == todayVn, ct);

        if (todayRecord == null)
        {
            throw new BusinessRuleException("BR-GAM-09", "MSG-GEN-01",
                "Bạn chưa điểm danh đầu ca hôm nay nên chưa thể kết thúc ca trực.");
        }

        if (todayRecord.CheckOutAt.HasValue)
        {
            throw new BusinessRuleException("BR-GAM-09", "MSG-GEN-01",
                "Bạn đã kết thúc ca trực hôm nay trước đó rồi.");
        }

        todayRecord.CheckOutAt = nowUtc;
        todayRecord.WorkedMinutes = Math.Max(1, (int)Math.Round((nowUtc - todayRecord.CheckInAt).TotalMinutes));

        if (!string.IsNullOrWhiteSpace(request.ShiftNote))
        {
            todayRecord.ShiftNote = request.ShiftNote.Trim();
        }

        // Đếm số tác vụ vận hành đã thực hiện trong ca trực từ bảng audit_logs
        var tasksInShift = await _dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(a =>
                a.ActorUserId == staffUserId &&
                a.CreatedAt >= todayRecord.CheckInAt &&
                a.CreatedAt <= nowUtc &&
                a.Action != "STAFF_CHECK_IN" &&
                a.Action != "STAFF_CHECK_OUT", ct);

        todayRecord.TasksCompletedSnapshot = tasksInShift;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            action: "STAFF_CHECK_OUT",
            entityType: "StaffAttendance",
            entityId: todayRecord.Id.ToString(),
            newValues: new
            {
                workDate = todayVn.ToString("yyyy-MM-dd"),
                workedMinutes = todayRecord.WorkedMinutes,
                tasksCompleted = tasksInShift
            },
            reason: todayRecord.ShiftNote,
            explicitActorId: staffUserId,
            ct: ct);

        return await GetMyAttendanceAsync(staffUserId, todayVn.Month, todayVn.Year, ct);
    }

    public async Task<StaffAttendanceStatusDto> GetMyAttendanceAsync(
        Guid staffUserId,
        int? month = null,
        int? year = null,
        CancellationToken ct = default)
    {
        var profile = await EnsureStaffProfileExistsAsync(staffUserId, ct);
        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.Add(VietnamOffset));
        var targetYear = year ?? todayVn.Year;
        var targetMonth = month is >= 1 and <= 12 ? month.Value : todayVn.Month;

        var monthStart = new DateOnly(targetYear, targetMonth, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var monthLogs = await _dbContext.StaffAttendances
            .AsNoTracking()
            .Where(a => a.StaffId == profile.UserId && a.WorkDate >= monthStart && a.WorkDate <= monthEnd)
            .OrderByDescending(a => a.WorkDate)
            .ToListAsync(ct);

        var todayRecord = await _dbContext.StaffAttendances
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.StaffId == profile.UserId && a.WorkDate == todayVn, ct);

        int currentStreak = 0;
        if (todayRecord != null)
        {
            currentStreak = todayRecord.StreakDay;
        }
        else
        {
            var yesterdayRecord = await _dbContext.StaffAttendances
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.StaffId == profile.UserId && a.WorkDate == todayVn.AddDays(-1), ct);
            currentStreak = yesterdayRecord?.StreakDay ?? 0;
        }

        // Tính tổng số tác vụ đã thực hiện trong tháng của nhân viên
        var monthStartUtc = monthStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Add(-VietnamOffset);
        var monthEndUtc = monthEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Add(-VietnamOffset);

        var monthlyAuditTasks = await _dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(a =>
                a.ActorUserId == staffUserId &&
                a.CreatedAt >= monthStartUtc &&
                a.CreatedAt < monthEndUtc &&
                a.Action != "STAFF_CHECK_IN" &&
                a.Action != "STAFF_CHECK_OUT", ct);

        return new StaffAttendanceStatusDto
        {
            AttendanceId = todayRecord?.Id,
            WorkDate = todayVn,
            CheckedInToday = todayRecord != null,
            CheckedOutToday = todayRecord?.CheckOutAt != null,
            CheckInAt = todayRecord?.CheckInAt,
            CheckOutAt = todayRecord?.CheckOutAt,
            WorkedMinutesToday = todayRecord?.WorkedMinutes,
            CurrentStreakDay = currentStreak,
            PointsEarnedToday = todayRecord?.WorkPointsEarned ?? 0,
            MonthlyWorkPoints = monthLogs.Sum(x => x.WorkPointsEarned),
            MonthlyTasksCompleted = Math.Max(monthlyAuditTasks, monthLogs.Sum(x => x.TasksCompletedSnapshot)),
            TodayShiftNote = todayRecord?.ShiftNote,
            RecentLogs = monthLogs.Select(x => new StaffAttendanceLogDto
            {
                AttendanceId = x.Id,
                WorkDate = x.WorkDate,
                CheckInAt = x.CheckInAt,
                CheckOutAt = x.CheckOutAt,
                WorkedMinutes = x.WorkedMinutes,
                StreakDay = x.StreakDay,
                WorkPointsEarned = x.WorkPointsEarned,
                TasksCompletedSnapshot = x.TasksCompletedSnapshot,
                ShiftNote = x.ShiftNote
            }).ToList()
        };
    }

    public async Task<PagedResult<AdminStaffAttendanceItemDto>> GetAllStaffAttendanceAsync(
        AdminStaffAttendanceFilterRequest filter,
        CancellationToken ct = default)
    {
        var query = _dbContext.StaffAttendances
            .Include(a => a.StaffProfile)
                .ThenInclude(s => s.User)
            .AsNoTracking()
            .AsQueryable();

        if (filter.StaffId.HasValue)
        {
            query = query.Where(a => a.StaffId == filter.StaffId.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(a => a.WorkDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(a => a.WorkDate <= filter.ToDate.Value);
        }

        var totalRecords = await query.CountAsync(ct);
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(a => a.WorkDate)
            .ThenByDescending(a => a.CheckInAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AdminStaffAttendanceItemDto
            {
                AttendanceId = a.Id,
                StaffId = a.StaffId,
                StaffCode = a.StaffProfile.StaffCode,
                StaffName = a.StaffProfile.User.FullName,
                Department = a.StaffProfile.Department,
                WorkDate = a.WorkDate,
                CheckInAt = a.CheckInAt,
                CheckOutAt = a.CheckOutAt,
                WorkedMinutes = a.WorkedMinutes,
                StreakDay = a.StreakDay,
                WorkPointsEarned = a.WorkPointsEarned,
                TasksCompletedSnapshot = a.TasksCompletedSnapshot,
                ShiftNote = a.ShiftNote
            })
            .ToListAsync(ct);

        return new PagedResult<AdminStaffAttendanceItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords
        };
    }

    private async Task<StaffProfile> EnsureStaffProfileExistsAsync(Guid userId, CancellationToken ct)
    {
        var profile = await _dbContext.StaffProfiles
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (profile != null) return profile;

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null || user.Role is not (UserRole.Staff or UserRole.Admin))
        {
            throw new ForbiddenAccessException("BR-ADM-02", "MSG-ADM-04",
                "Chỉ tài khoản Nhân viên vận hành mới được phép thực hiện chấm công ca trực.");
        }

        var count = await _dbContext.StaffProfiles.IgnoreQueryFilters().CountAsync(ct);
        profile = new StaffProfile
        {
            UserId = user.Id,
            StaffCode = user.Role == UserRole.Admin ? $"ADM-{count + 1:D2}" : $"NV-{count + 1:D3}",
            Department = user.Role == UserRole.Admin ? "Ban Quản Trị" : "Vận hành tổng hợp",
            Position = user.Role == UserRole.Admin ? "Quản trị viên" : "Nhân viên vận hành",
            Status = "ACTIVE"
        };

        _dbContext.StaffProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(ct);
        return profile;
    }
}
