using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Điểm danh hằng ngày (UC U-13, BR-GAM-09, BR-GAM-02). Một lần mỗi ngày theo giờ ICT (reset 00:00), thưởng = điểm cơ bản
/// + thưởng chuỗi (tăng theo ngày liên tiếp, có trần), bỏ lỡ một ngày thì chuỗi về 1. Điểm bị cắt theo trần điểm phi giao dịch.
/// </summary>
public class CheckinService : ICheckinService
{
    private readonly ApplicationDbContext _db;
    private readonly IPointsService _points;
    private readonly IGamificationConfigService _config;

    public CheckinService(ApplicationDbContext db, IPointsService points, IGamificationConfigService config)
    {
        _db = db;
        _points = points;
        _config = config;
    }

    public async Task<CheckinStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var s = await _config.GetSettingsAsync(ct);
        var today = GamTime.Today();
        var logs = await _db.DailyCheckinLogs.AsNoTracking()
            .Where(l => l.UserId == userId && l.CheckinDate >= today.AddDays(-6))
            .OrderByDescending(l => l.CheckinDate).ToListAsync(ct);
        var last = await _db.DailyCheckinLogs.AsNoTracking().Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CheckinDate).FirstOrDefaultAsync(ct);

        var checkedToday = last != null && last.CheckinDate == today;
        var continuing = last != null && last.CheckinDate >= today.AddDays(-1);
        var current = last == null ? 0 : (continuing ? last.StreakCount : 0);
        var next = checkedToday ? current : (continuing ? current + 1 : 1);
        var remaining = await CapRemainingAsync(userId, s, today, ct);

        return new CheckinStatusDto
        {
            CheckedInToday = checkedToday,
            CurrentStreak = current,
            NextStreak = next,
            StreakWillReset = !checkedToday && last != null && last.CheckinDate < today.AddDays(-1),
            TodayRewardPreview = checkedToday ? 0 : Math.Min(s.CheckinReward(next), remaining),
            DailyCapRemaining = remaining,
            WalletBalance = (await _points.GetWalletAsync(userId, ct)).Balance,
            Calendar = Enumerable.Range(0, 7).Select(i => today.AddDays(i - 6)).Select(d =>
            {
                var log = logs.FirstOrDefault(l => l.CheckinDate == d);
                return new CheckinDayDto { Date = d, CheckedIn = log != null, PointsAwarded = log?.PointsAwarded ?? 0 };
            }).ToList()
        };
    }

    public async Task<CheckinResultDto> CheckInAsync(Guid userId, CancellationToken ct = default)
    {
        var s = await _config.GetSettingsAsync(ct);
        var today = GamTime.Today();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var last = await _db.DailyCheckinLogs.AsNoTracking().Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CheckinDate).FirstOrDefaultAsync(ct);
        if (last != null && last.CheckinDate == today) throw AlreadyCheckedIn();

        var streak = last != null && last.CheckinDate == today.AddDays(-1) ? last.StreakCount + 1 : 1;
        var reward = s.CheckinReward(streak);

        var remaining = await CapRemainingAsync(userId, s, today, ct);
        if (remaining <= 0)
            throw GamErrors.Rule("BR-GAM-02", "MSG-GAM-05", "Bạn đã đạt giới hạn điểm trong ngày.");
        var award = Math.Min(reward, remaining);

        _db.DailyCheckinLogs.Add(new DailyCheckinLog { UserId = userId, CheckinDate = today, StreakCount = streak, PointsAwarded = award });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (GamErrors.IsUniqueViolation(ex, "ux_daily_checkin_user_date"))
        {
            throw AlreadyCheckedIn();      // bấm đúp / hai request cùng lúc: chỉ một request thắng
        }

        if (award > 0)
            await _points.CreditAsync(userId, award, PointActionType.DailyCheckin, "DailyCheckin", $"{userId}:{today:yyyy-MM-dd}",
                $"CHECKIN:{userId}:{today:yyyy-MM-dd}", $"Điểm danh ngày {today:dd/MM/yyyy} (chuỗi {streak} ngày)", ct);
        await tx.CommitAsync(ct);

        return new CheckinResultDto
        {
            PointsAwarded = award,
            StreakCount = streak,
            LimitedByDailyCap = award < reward,
            WalletBalance = (await _points.GetWalletAsync(userId, ct)).Balance
        };
    }

    /// <summary>Phần còn lại của trần điểm phi giao dịch hôm nay (chỉ tính các khoản cộng từ check-in).</summary>
    private async Task<long> CapRemainingAsync(Guid userId, GamSettings s, DateOnly today, CancellationToken ct)
    {
        var start = GamTime.StartOfDayUtc(today);
        var earned = await _db.PointTransactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.ActionType == PointActionType.DailyCheckin && t.Points > 0 && t.CreatedAt >= start)
            .SumAsync(t => (long?)t.Points, ct) ?? 0;
        return Math.Max(0, s.DailyCap - earned);
    }

    private static Ecolink.Domain.Exceptions.BusinessRuleException AlreadyCheckedIn() =>
        GamErrors.Rule("BR-GAM-09", "MSG-GEN-01", "Bạn đã điểm danh hôm nay rồi.");
}
