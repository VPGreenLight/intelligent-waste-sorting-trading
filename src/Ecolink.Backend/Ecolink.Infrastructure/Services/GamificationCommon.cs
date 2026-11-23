using Ecolink.Application.Dtos;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

internal static class TreeCareShared
{
    internal static async Task EnsureTreeRowAsync(ApplicationDbContext db, Guid userId, CancellationToken ct)
    {
        if (await db.VirtualTreeProgresses.AsNoTracking().AnyAsync(t => t.UserId == userId, ct)) return;
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        if (!db.Database.IsRelational())
        {
            db.VirtualTreeProgresses.Add(new VirtualTreeProgress
            {
                Id = id,
                UserId = userId,
                TreeLevel = 1,
                GrowthPoints = 0,
                TotalWatered = 0,
                DailyWaterCount = 0,
                HydrationPercent = 75,
                LastHydrationAt = now,
                CareExp = 0,
                HarvestReady = false,
                TotalFertilized = 0,
                DailyFertilizeCount = 0,
                FertilizerPacks = 0,
                TotalHarvested = 0,
                UpdatedAt = now
            });
            await db.SaveChangesAsync(ct);
            return;
        }

        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO virtual_tree_progress (
                progress_id, user_id, tree_level, growth_points, total_watered, daily_water_count,
                hydration_percent, last_hydration_at, care_exp, harvest_ready,
                total_fertilized, daily_fertilize_count, fertilizer_packs, total_harvested, updated_at
            )
            VALUES ({id}, {userId}, 1, 0, 0, 0, 75, NOW(), 0, FALSE, 0, 0, 0, 0, NOW())
            ON CONFLICT (user_id) DO NOTHING", ct);
    }

    /// <summary>Phần còn lại của trần điểm phi giao dịch hôm nay (BR-GAM-02: gộp Check-in + Thu hoạch Cây Ảo).</summary>
    internal static async Task<long> ComputeNonTransactionCapRemainingAsync(
        ApplicationDbContext db,
        Guid userId,
        GamSettings s,
        DateOnly today,
        CancellationToken ct)
    {
        var start = GamTime.StartOfDayUtc(today);
        var earned = await db.PointTransactions.AsNoTracking()
            .Where(t => t.UserId == userId
                && (t.ActionType == PointActionType.DailyCheckin || t.ActionType == PointActionType.TreeCare)
                && t.Points > 0
                && t.CreatedAt >= start)
            .SumAsync(t => (long?)t.Points, ct) ?? 0L;
        return Math.Max(0L, s.DailyCap - earned);
    }

    internal static async Task AwardHandoverFertilizerAsync(
        ApplicationDbContext db,
        Guid userId,
        int packs,
        CancellationToken ct)
    {
        if (packs <= 0) return;
        var isCitizen = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.Role == UserRole.User, ct);
        if (!isCitizen) return;

        await EnsureTreeRowAsync(db, userId, ct);
        var now = DateTime.UtcNow;
        if (db.Database.IsRelational())
        {
            await db.VirtualTreeProgresses
                .Where(t => t.UserId == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.FertilizerPacks, t => t.FertilizerPacks + packs)
                    .SetProperty(t => t.UpdatedAt, now), ct);
        }
        else
        {
            var tracked = await db.VirtualTreeProgresses.FirstAsync(t => t.UserId == userId, ct);
            tracked.FertilizerPacks += packs;
            tracked.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }
    }

    internal static async Task TryAwardDailyScanFertilizerAsync(
        ApplicationDbContext db,
        Guid userId,
        CancellationToken ct)
    {
        var isCitizen = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.Role == UserRole.User, ct);
        if (!isCitizen) return;

        await EnsureTreeRowAsync(db, userId, ct);
        var today = GamTime.Today();
        var now = DateTime.UtcNow;
        if (db.Database.IsRelational())
        {
            await db.VirtualTreeProgresses
                .Where(t => t.UserId == userId && (t.LastScanFertilizerDate == null || t.LastScanFertilizerDate != today))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.FertilizerPacks, t => t.FertilizerPacks + 1)
                    .SetProperty(t => t.LastScanFertilizerDate, (DateOnly?)today)
                    .SetProperty(t => t.UpdatedAt, now), ct);
        }
        else
        {
            var tracked = await db.VirtualTreeProgresses.FirstAsync(t => t.UserId == userId, ct);
            if (tracked.LastScanFertilizerDate == today) return;
            tracked.FertilizerPacks += 1;
            tracked.LastScanFertilizerDate = today;
            tracked.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }
    }
}

/// <summary>Múi giờ ICT (UTC+7) cho mọi mốc "ngày" của gamification (check-in reset lúc 00:00 ICT).</summary>
internal static class GamTime
{
    public static readonly TimeSpan Ict = TimeSpan.FromHours(7);
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow + Ict);
    public static DateTime StartOfDayUtc(DateOnly ictDate) =>
        DateTime.SpecifyKind(ictDate.ToDateTime(TimeOnly.MinValue) - Ict, DateTimeKind.Utc);   // Npgsql từ chối Kind=Unspecified với timestamptz
}

internal static class GamErrors
{
    public static BusinessRuleException Rule(string rule, string code, string message) => new(rule, code, message);

    public static bool IsUniqueViolation(DbUpdateException ex, string? constraint = null) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg &&
        (constraint == null || pg.ConstraintName == constraint);

    public static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    public static string Like(string s) => "%" + EscapeLike(s.Trim()) + "%";

    private static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png", ".webp" };

    /// <summary>Kiểm tra URL ảnh http(s) hoặc đường dẫn nội bộ /assets/ và định dạng (MSG-CLS-01); null/rỗng hợp lệ.</summary>
    public static string? CleanImageUrl(string? url, string rule)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var u = url.Trim();
        if (u.Length > 1000) throw Rule(rule, "MSG-NTF-03", "URL ảnh quá dài.");
        if (u.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
        {
            var relExt = Path.GetExtension(u.Split('?')[0]).ToLowerInvariant();
            if (relExt.Length == 0 || !ImageExt.Contains(relExt))
                throw Rule(rule, "MSG-CLS-01", "Định dạng ảnh không được hỗ trợ (chỉ JPG, PNG, WEBP).");
            return u;
        }
        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw Rule(rule, "MSG-AUTH-02", "URL ảnh không hợp lệ.");
        var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (ext.Length > 0 && !ImageExt.Contains(ext))
            throw Rule(rule, "MSG-CLS-01", "Định dạng ảnh không được hỗ trợ (chỉ JPG, PNG, WEBP).");
        return u;
    }

    public static string? CleanHttpUrl(string? url, string rule, string label)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var u = url.Trim();
        if (u.Length > 1000) throw Rule(rule, "MSG-NTF-03", $"{label} quá dài.");
        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw Rule(rule, "MSG-AUTH-02", $"{label} không hợp lệ.");
        return u;
    }
}