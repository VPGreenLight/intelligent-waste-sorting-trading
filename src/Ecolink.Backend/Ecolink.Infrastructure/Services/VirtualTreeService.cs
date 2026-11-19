using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Cây ảo (UC U-15, BR-GAM-06, BR-GAM-08: chỉ role User). "Tưới & nâng cấp" trả điểm bằng ngưỡng của cấp kế tiếp, lên đúng một
/// cấp và mở khóa Eco Fact của cấp đó. Cấp chỉ tăng (cập nhật có điều kiện theo cấp hiện tại nên bấm đúp chỉ thắng một lần),
/// số lần chăm mỗi ngày có giới hạn và reset theo ngày ICT.
/// </summary>
public class VirtualTreeService : IVirtualTreeService
{
    private const string Rule = "BR-GAM-06";

    private readonly ApplicationDbContext _db;
    private readonly IPointsService _points;
    private readonly IGamificationConfigService _config;

    public VirtualTreeService(ApplicationDbContext db, IPointsService points, IGamificationConfigService config)
    {
        _db = db;
        _points = points;
        _config = config;
    }

    public async Task<VirtualTreeDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var settings = await _config.GetSettingsAsync(ct);
        return await BuildAsync(userId, settings, ct);
    }

    public async Task<WaterTreeResultDto> WaterAndUpgradeAsync(Guid userId, CancellationToken ct = default)
    {
        var settings = await _config.GetSettingsAsync(ct);
        var today = GamTime.Today();
        await EnsureTreeAsync(userId, ct);
        var tree = await _db.VirtualTreeProgresses.AsNoTracking().FirstAsync(t => t.UserId == userId, ct);

        if (tree.TreeLevel >= settings.MaxLevel)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây của bạn đã đạt cấp tối đa.");

        var dailyCount = tree.LastWaterDate == today ? tree.DailyWaterCount : 0;
        if (dailyCount >= settings.TreeLimit)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", $"Bạn đã chăm cây tối đa {settings.TreeLimit} lần trong ngày hôm nay.");

        var next = settings.LevelInfo(tree.TreeLevel + 1)
                   ?? throw GamErrors.Rule(Rule, "MSG-GEN-01", "Chưa cấu hình ngưỡng cho cấp kế tiếp.");
        var cost = next.RequiredPoints;
        var currentLevel = tree.TreeLevel;
        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (_db.Database.IsRelational())
        {
            // Cập nhật có điều kiện theo cấp hiện tại: hai request cùng lúc chỉ một request lên cấp (cấp một chiều, BR-GAM-06)
            var changed = await _db.VirtualTreeProgresses
                .Where(t => t.UserId == userId && t.TreeLevel == currentLevel)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.TreeLevel, currentLevel + 1)
                    .SetProperty(t => t.GrowthPoints, t => t.GrowthPoints + cost)
                    .SetProperty(t => t.TotalWatered, t => t.TotalWatered + 1)
                    .SetProperty(t => t.DailyWaterCount, dailyCount + 1)
                    .SetProperty(t => t.LastWaterDate, (DateOnly?)today)
                    .SetProperty(t => t.LastWateredAt, (DateTime?)now)
                    .SetProperty(t => t.UpdatedAt, now), ct);
            if (changed == 0)
                throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây vừa được cập nhật bởi một thao tác khác. Vui lòng tải lại.");

            var paid = await _points.TryDebitAsync(userId, cost, PointActionType.TreeCare, "VirtualTree", $"{userId}:L{currentLevel + 1}",
                $"TREE:{userId}:L{currentLevel + 1}", $"Nâng cấp cây lên cấp {currentLevel + 1} ({next.Title})", ct);
            if (!paid)
                throw GamErrors.Rule("BR-GAM-03", "MSG-GAM-03", "Bạn không đủ điểm để nâng cấp cây.");
        }
        else
        {
            // Trên EF Core InMemory (không có rollback transaction thật), trừ điểm trước khi nâng cấp cây để giữ tính nguyên tử
            var paid = await _points.TryDebitAsync(userId, cost, PointActionType.TreeCare, "VirtualTree", $"{userId}:L{currentLevel + 1}",
                $"TREE:{userId}:L{currentLevel + 1}", $"Nâng cấp cây lên cấp {currentLevel + 1} ({next.Title})", ct);
            if (!paid)
                throw GamErrors.Rule("BR-GAM-03", "MSG-GAM-03", "Bạn không đủ điểm để nâng cấp cây.");

            var tracked = await _db.VirtualTreeProgresses.FirstOrDefaultAsync(t => t.UserId == userId && t.TreeLevel == currentLevel, ct)
                          ?? throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây vừa được cập nhật bởi một thao tác khác. Vui lòng tải lại.");
            tracked.TreeLevel = currentLevel + 1;
            tracked.GrowthPoints += cost;
            tracked.TotalWatered += 1;
            tracked.DailyWaterCount = dailyCount + 1;
            tracked.LastWaterDate = today;
            tracked.LastWateredAt = now;
            tracked.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);

        var facts = await _db.EcoFacts.AsNoTracking()
            .Where(f => f.Status == "ACTIVE" && f.UnlockTreeLevel == currentLevel + 1)
            .OrderBy(f => f.CreatedAt).ToListAsync(ct);
        return new WaterTreeResultDto
        {
            Tree = await BuildAsync(userId, settings, ct),
            PointsSpent = cost,
            UnlockedFacts = facts.Select(EcoFactService.ToDto).ToList()
        };
    }

    public async Task<List<EcoFactDto>> ListUnlockedFactsAsync(Guid userId, CancellationToken ct = default)
    {
        await EnsureTreeAsync(userId, ct);
        var level = await _db.VirtualTreeProgresses.AsNoTracking().Where(t => t.UserId == userId).Select(t => t.TreeLevel).FirstAsync(ct);
        var facts = await _db.EcoFacts.AsNoTracking()
            .Where(f => f.Status == "ACTIVE" && f.UnlockTreeLevel <= level)
            .OrderBy(f => f.UnlockTreeLevel).ThenBy(f => f.CreatedAt).ToListAsync(ct);
        return facts.Select(EcoFactService.ToDto).ToList();
    }

    public async Task<EcoFactDto> GetUnlockedFactAsync(Guid userId, Guid factId, CancellationToken ct = default)
    {
        await EnsureTreeAsync(userId, ct);
        var level = await _db.VirtualTreeProgresses.AsNoTracking().Where(t => t.UserId == userId).Select(t => t.TreeLevel).FirstAsync(ct);
        var f = await _db.EcoFacts.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == factId && x.Status == "ACTIVE" && x.UnlockTreeLevel <= level, ct)
                ?? throw new NotFoundException("Không tìm thấy Eco Fact hoặc bạn chưa mở khóa.");
        return EcoFactService.ToDto(f);
    }

    // ------------------------------------------------------------------
    private async Task<VirtualTreeDto> BuildAsync(Guid userId, GamSettings s, CancellationToken ct)
    {
        await EnsureTreeAsync(userId, ct);
        var t = await _db.VirtualTreeProgresses.AsNoTracking().FirstAsync(x => x.UserId == userId, ct);
        var balance = (await _points.GetWalletAsync(userId, ct)).Balance;
        var today = GamTime.Today();

        var isMax = t.TreeLevel >= s.MaxLevel;
        var next = isMax ? null : s.LevelInfo(t.TreeLevel + 1);
        var daily = t.LastWaterDate == today ? t.DailyWaterCount : 0;
        var cost = next?.RequiredPoints;
        var progress = cost is null ? 100 : cost.Value <= 0 ? 100 : (int)Math.Min(100, balance * 100 / cost.Value);

        var activeFacts = _db.EcoFacts.AsNoTracking().Where(f => f.Status == "ACTIVE");
        return new VirtualTreeDto
        {
            Level = t.TreeLevel,
            LevelTitle = s.LevelInfo(t.TreeLevel)?.Title ?? string.Empty,
            MaxLevel = s.MaxLevel,
            IsMaxLevel = isMax,
            GrowthPoints = t.GrowthPoints,
            TotalWatered = t.TotalWatered,
            DailyWaterCount = daily,
            DailyLimit = s.TreeLimit,
            NextLevel = next?.Level,
            NextLevelTitle = next?.Title,
            UpgradeCost = cost,
            WalletBalance = balance,
            ProgressPercent = progress,
            CanUpgrade = next != null && balance >= next.RequiredPoints && daily < s.TreeLimit,
            Levels = s.Levels.Select(l => new TreeLevelInfoDto
                { Level = l.Level, Title = l.Title, RequiredPoints = l.RequiredPoints, Reached = l.Level <= t.TreeLevel }).ToList(),
            UnlockedFactCount = await activeFacts.CountAsync(f => f.UnlockTreeLevel <= t.TreeLevel, ct),
            TotalFactCount = await activeFacts.CountAsync(ct)
        };
    }

    private async Task EnsureTreeAsync(Guid userId, CancellationToken ct)
    {
        if (await _db.VirtualTreeProgresses.AsNoTracking().AnyAsync(t => t.UserId == userId, ct)) return;
        var id = Guid.NewGuid();
        if (!_db.Database.IsRelational())
        {
            _db.VirtualTreeProgresses.Add(new Domain.Entities.VirtualTreeProgress
            {
                Id = id,
                UserId = userId,
                TreeLevel = 1,
                GrowthPoints = 0,
                TotalWatered = 0,
                DailyWaterCount = 0,
                UpdatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            return;
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO virtual_tree_progress (progress_id, user_id, tree_level, growth_points, total_watered, daily_water_count, updated_at)
            VALUES ({id}, {userId}, 1, 0, 0, 0, NOW()) ON CONFLICT (user_id) DO NOTHING", ct);
    }
}
