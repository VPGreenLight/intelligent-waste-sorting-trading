using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Cây ảo (UC U-15, BR-GAM-06, BR-GAM-08: chỉ role User).
/// - Độ ẩm (HydrationPercent) hao hụt lười -2%/giờ; về 0% sẽ héo tạm thời (không tụt cấp, cần tưới để hồi sinh).
/// - Tưới nước &amp; Bón phân có hạn mức ngày riêng, rào chắn sinh học (chặn tưới >= 95%, chặn bón &lt; 30%, x1.4 EXP tại ẩm vàng 60–90%),
///   và dùng túi phân hữu cơ (tặng khi Quét AI / Hoàn tất bàn giao) khi hết lượt miễn phí.
/// - Thanh CareExp tích đủ sẽ cho Thu hoạch Điểm Xanh thật vào ví (kiểm soát bởi BR-GAM-02 DailyNonTransactionCap).
/// - Nâng cấp cấp cây chính thức (WaterAndUpgradeAsync) tiêu Điểm Xanh để mở khóa Eco Fact.
/// </summary>
public class VirtualTreeService : IVirtualTreeService
{
    private const string Rule = "BR-GAM-06";
    private const double HydrationDecayPerHour = 2.0;

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

    public async Task<VirtualTreeDto> CareAsync(Guid userId, string? actionType, CancellationToken ct = default)
    {
        var action = string.IsNullOrWhiteSpace(actionType)
            ? "water"
            : actionType.Trim().ToLowerInvariant();
        if (action is not ("water" or "fertilize"))
            throw GamErrors.Rule(Rule, "MSG-GEN-01", "Hành động chăm sóc cây chỉ hỗ trợ tưới nước (water) hoặc bón phân (fertilize).");

        var settings = await _config.GetSettingsAsync(ct);
        var today = GamTime.Today();
        var now = DateTime.UtcNow;
        await EnsureTreeAsync(userId, ct);
        var tree = await _db.VirtualTreeProgresses.AsNoTracking().FirstAsync(t => t.UserId == userId, ct);

        var dailyWater = tree.LastWaterDate == today ? tree.DailyWaterCount : 0;
        var dailyFert = tree.LastFertilizeDate == today ? tree.DailyFertilizeCount : 0;
        var waterLimit = DailyWaterLimit(settings);
        var fertFreeLimit = DailyFreeFertilizeLimit(settings);
        var effectiveHydration = ComputeEffectiveHydration(tree.HydrationPercent, tree.LastHydrationAt, now);
        var isWilted = effectiveHydration == 0;
        var maxExp = MaxCareExpForLevel(tree.TreeLevel);
        var alreadyHarvestReady = tree.HarvestReady || tree.CareExp >= maxExp;

        if (action == "water")
        {
            if (dailyWater >= waterLimit)
                throw GamErrors.Rule(Rule, "MSG-GEN-01", $"Bạn đã tưới cây tối đa {waterLimit} lần trong ngày hôm nay.");
            if (effectiveHydration >= 95)
                throw GamErrors.Rule(Rule, "MSG-GEN-01", "Độ ẩm đất đã đạt mức bão hòa (≥ 95%), không cần tưới thêm lúc này.");

            var newHydration = Math.Min(100, effectiveHydration + 35);
            var expGain = alreadyHarvestReady ? 0 : 10;
            var newExp = Math.Min(maxExp, tree.CareExp + expGain);
            var newHarvestReady = alreadyHarvestReady || newExp >= maxExp;

            if (_db.Database.IsRelational())
            {
                var changed = await _db.VirtualTreeProgresses
                    .Where(t => t.UserId == userId &&
                                t.TotalWatered == tree.TotalWatered &&
                                t.CareExp == tree.CareExp &&
                                ((t.LastWaterDate == today && t.DailyWaterCount == dailyWater) || t.LastWaterDate != today))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.TotalWatered, t => t.TotalWatered + 1)
                        .SetProperty(t => t.DailyWaterCount, dailyWater + 1)
                        .SetProperty(t => t.LastWaterDate, (DateOnly?)today)
                        .SetProperty(t => t.LastWateredAt, (DateTime?)now)
                        .SetProperty(t => t.HydrationPercent, newHydration)
                        .SetProperty(t => t.LastHydrationAt, now)
                        .SetProperty(t => t.CareExp, newExp)
                        .SetProperty(t => t.HarvestReady, newHarvestReady)
                        .SetProperty(t => t.UpdatedAt, now), ct);
                if (changed == 0)
                    throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây vừa được cập nhật. Vui lòng thử lại.");
            }
            else
            {
                var tracked = await _db.VirtualTreeProgresses.FirstAsync(t => t.UserId == userId, ct);
                tracked.TotalWatered += 1;
                tracked.DailyWaterCount = dailyWater + 1;
                tracked.LastWaterDate = today;
                tracked.LastWateredAt = now;
                tracked.HydrationPercent = newHydration;
                tracked.LastHydrationAt = now;
                tracked.CareExp = newExp;
                tracked.HarvestReady = newHarvestReady;
                tracked.UpdatedAt = now;
                await _db.SaveChangesAsync(ct);
            }

            var waterEffect = isWilted
                ? $"Đã tưới nước hồi sinh cây (+35% ẩm{(expGain > 0 ? $", +{expGain} EXP" : string.Empty)})."
                : $"Đã tưới nước (+35% ẩm{(expGain > 0 ? $", +{expGain} EXP" : ", cây đã đủ EXP thu hoạch")}).";
            return await BuildAsync(userId, settings, ct, waterEffect);
        }

        // Bón phân (fertilize)
        if (isWilted)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây đang khô héo (0% ẩm). Hãy tưới nước hồi sinh trước khi bón phân.");
        if (effectiveHydration < 30)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", "Độ ẩm dưới 30% rất dễ xót rễ! Hãy tưới nước đạt tối thiểu 30% trước khi bón phân.");
        if (alreadyHarvestReady)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây đã kết quả chín, hãy thu hoạch trước khi bón phân cho mùa vụ mới.");

        var usePack = dailyFert >= fertFreeLimit;
        if (usePack && tree.FertilizerPacks <= 0)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", $"Bạn đã dùng hết {fertFreeLimit} lượt bón phân miễn phí hôm nay và chưa còn túi phân hữu cơ dự trữ.");

        var inOptimal = effectiveHydration is >= 60 and <= 90;
        var fertExpGain = inOptimal ? 35 : 25;
        var nextHydration = Math.Max(0, effectiveHydration - 10);
        var nextExp = Math.Min(maxExp, tree.CareExp + fertExpGain);
        var nextHarvestReady = nextExp >= maxExp;
        var nextDailyFert = usePack ? dailyFert : dailyFert + 1;
        var nextFertDate = usePack ? tree.LastFertilizeDate : today;
        var nextPacks = usePack ? tree.FertilizerPacks - 1 : tree.FertilizerPacks;

        if (_db.Database.IsRelational())
        {
            var changed = await _db.VirtualTreeProgresses
                .Where(t => t.UserId == userId &&
                            t.TotalFertilized == tree.TotalFertilized &&
                            t.FertilizerPacks == tree.FertilizerPacks &&
                            t.CareExp == tree.CareExp &&
                            ((t.LastFertilizeDate == today && t.DailyFertilizeCount == dailyFert) || t.LastFertilizeDate != today))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.TotalFertilized, t => t.TotalFertilized + 1)
                    .SetProperty(t => t.DailyFertilizeCount, nextDailyFert)
                    .SetProperty(t => t.LastFertilizeDate, nextFertDate)
                    .SetProperty(t => t.FertilizerPacks, nextPacks)
                    .SetProperty(t => t.HydrationPercent, nextHydration)
                    .SetProperty(t => t.LastHydrationAt, now)
                    .SetProperty(t => t.CareExp, nextExp)
                    .SetProperty(t => t.HarvestReady, nextHarvestReady)
                    .SetProperty(t => t.UpdatedAt, now), ct);
            if (changed == 0)
                throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây vừa được cập nhật. Vui lòng thử lại.");
        }
        else
        {
            var tracked = await _db.VirtualTreeProgresses.FirstAsync(t => t.UserId == userId, ct);
            tracked.TotalFertilized += 1;
            tracked.DailyFertilizeCount = nextDailyFert;
            tracked.LastFertilizeDate = nextFertDate;
            tracked.FertilizerPacks = nextPacks;
            tracked.HydrationPercent = nextHydration;
            tracked.LastHydrationAt = now;
            tracked.CareExp = nextExp;
            tracked.HarvestReady = nextHarvestReady;
            tracked.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
        }

        var fertPrefix = usePack ? "Đã bón bằng 1 túi phân hữu cơ" : "Đã bón phân";
        var zoneNote = inOptimal
            ? $" (+{fertExpGain} EXP · Đất ẩm vàng x1.4, -10% ẩm)"
            : $" (+{fertExpGain} EXP, -10% ẩm)";
        return await BuildAsync(userId, settings, ct, $"{fertPrefix}{zoneNote}.");
    }

    public async Task<HarvestTreeResultDto> HarvestAsync(Guid userId, CancellationToken ct = default)
    {
        var settings = await _config.GetSettingsAsync(ct);
        var today = GamTime.Today();
        var now = DateTime.UtcNow;
        await EnsureTreeAsync(userId, ct);
        var tree = await _db.VirtualTreeProgresses.AsNoTracking().FirstAsync(t => t.UserId == userId, ct);

        var effectiveHydration = ComputeEffectiveHydration(tree.HydrationPercent, tree.LastHydrationAt, now);
        if (effectiveHydration == 0)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây đang khô héo (0% ẩm). Hãy tưới nước hồi sinh trước khi thu hoạch quả.");

        var maxExp = MaxCareExpForLevel(tree.TreeLevel);
        if (!tree.HarvestReady && tree.CareExp < maxExp)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây chưa tích đủ EXP để kết quả thu hoạch.");

        var rawReward = HarvestRewardForLevel(tree.TreeLevel);
        var remaining = await TreeCareShared.ComputeNonTransactionCapRemainingAsync(_db, userId, settings, today, ct);
        if (remaining <= 0)
            throw GamErrors.Rule("BR-GAM-02", "MSG-GAM-05", "Bạn đã đạt giới hạn điểm phi giao dịch hôm nay. Quả chín vẫn được giữ nguyên cho ngày mai.");

        var award = Math.Min(rawReward, remaining);
        var nextHarvestNo = tree.TotalHarvested + 1;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (_db.Database.IsRelational())
        {
            var changed = await _db.VirtualTreeProgresses
                .Where(t => t.UserId == userId &&
                            t.TotalHarvested == tree.TotalHarvested &&
                            (t.HarvestReady || t.CareExp >= maxExp))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.CareExp, 0)
                    .SetProperty(t => t.HarvestReady, false)
                    .SetProperty(t => t.TotalHarvested, nextHarvestNo)
                    .SetProperty(t => t.UpdatedAt, now), ct);
            if (changed == 0)
                throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây vừa được thu hoạch hoặc cập nhật. Vui lòng tải lại.");
        }
        else
        {
            var tracked = await _db.VirtualTreeProgresses.FirstOrDefaultAsync(
                              t => t.UserId == userId && t.TotalHarvested == tree.TotalHarvested && (t.HarvestReady || t.CareExp >= maxExp), ct)
                          ?? throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây vừa được thu hoạch hoặc cập nhật. Vui lòng tải lại.");
            tracked.CareExp = 0;
            tracked.HarvestReady = false;
            tracked.TotalHarvested = nextHarvestNo;
            tracked.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
        }

        var credited = await _points.CreditAsync(
            userId,
            award,
            PointActionType.TreeCare,
            "VirtualTreeHarvest",
            $"{userId}:{nextHarvestNo}",
            $"TREE_HARVEST:{userId}:{nextHarvestNo}",
            $"Thu hoạch mùa vụ Cây Ảo Cấp {tree.TreeLevel} (vụ #{nextHarvestNo})",
            ct);

        await tx.CommitAsync(ct);

        var effect = award < rawReward
            ? $"Thu hoạch thành công +{credited} Điểm Xanh (đã chạm trần điểm ngày)."
            : $"Thu hoạch thành công +{credited} Điểm Xanh vào ví!";
        return new HarvestTreeResultDto
        {
            Tree = await BuildAsync(userId, settings, ct, effect),
            PointsAwarded = credited,
            LimitedByDailyCap = award < rawReward
        };
    }

    public async Task<WaterTreeResultDto> WaterAndUpgradeAsync(Guid userId, CancellationToken ct = default)
    {
        var settings = await _config.GetSettingsAsync(ct);
        await EnsureTreeAsync(userId, ct);
        var tree = await _db.VirtualTreeProgresses.AsNoTracking().FirstAsync(t => t.UserId == userId, ct);

        if (tree.TreeLevel >= settings.MaxLevel)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", "Cây của bạn đã đạt cấp tối đa.");

        var next = settings.LevelInfo(tree.TreeLevel + 1)
                   ?? throw GamErrors.Rule(Rule, "MSG-GEN-01", "Chưa cấu hình ngưỡng cho cấp kế tiếp.");
        var cost = next.RequiredPoints;
        var currentLevel = tree.TreeLevel;
        var now = DateTime.UtcNow;
        var nextHydration = Math.Max(ComputeEffectiveHydration(tree.HydrationPercent, tree.LastHydrationAt, now), 80);
        var nextCareExp = tree.HarvestReady ? MaxCareExpForLevel(currentLevel + 1) : tree.CareExp;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (_db.Database.IsRelational())
        {
            // Cập nhật có điều kiện theo cấp hiện tại: hai request cùng lúc chỉ một request lên cấp (cấp một chiều, BR-GAM-06).
            // Nâng cấp bằng Điểm Xanh tách biệt với giới hạn lượt Tưới/Bón phân miễn phí trong ngày (DailyWaterCount).
            var changed = await _db.VirtualTreeProgresses
                .Where(t => t.UserId == userId && t.TreeLevel == currentLevel)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.TreeLevel, currentLevel + 1)
                    .SetProperty(t => t.GrowthPoints, t => t.GrowthPoints + cost)
                    .SetProperty(t => t.TotalWatered, t => t.TotalWatered + 1)
                    .SetProperty(t => t.HydrationPercent, nextHydration)
                    .SetProperty(t => t.LastHydrationAt, now)
                    .SetProperty(t => t.CareExp, nextCareExp)
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
            tracked.HydrationPercent = nextHydration;
            tracked.LastHydrationAt = now;
            tracked.CareExp = nextCareExp;
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
            Tree = await BuildAsync(userId, settings, ct, $"Cây đã thăng cấp {currentLevel + 1} ({next.Title})!"),
            PointsSpent = cost,
            UnlockedFacts = facts.Select(EcoFactService.ToDto).ToList()
        };
    }

    public Task<List<EcoFactDto>> ListUnlockedFactsAsync(Guid userId, CancellationToken ct = default) =>
        ListUnlockedFactsAsync(userId, includeLocked: false, ct);

    public async Task<List<EcoFactDto>> ListUnlockedFactsAsync(Guid userId, bool includeLocked, CancellationToken ct = default)
    {
        await EnsureTreeAsync(userId, ct);
        var level = await _db.VirtualTreeProgresses.AsNoTracking().Where(t => t.UserId == userId).Select(t => t.TreeLevel).FirstAsync(ct);
        var q = _db.EcoFacts.AsNoTracking().Where(f => f.Status == "ACTIVE");
        if (!includeLocked)
            q = q.Where(f => f.UnlockTreeLevel <= level);

        var facts = await q.OrderBy(f => f.UnlockTreeLevel).ThenBy(f => f.CreatedAt).ToListAsync(ct);
        return facts.Select(f =>
        {
            var dto = EcoFactService.ToDto(f);
            dto.IsUnlocked = f.UnlockTreeLevel <= level;
            if (!dto.IsUnlocked)
            {
                dto.Content = $"Chăm sóc và nâng cấp Cây Ảo lên Cấp {f.UnlockTreeLevel} để mở khóa kiến thức này.";
                dto.SourceUrl = null;
            }
            return dto;
        }).ToList();
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
    internal static int ComputeEffectiveHydration(int storedHydration, DateTime lastHydrationAt, DateTime nowUtc)
    {
        var clamped = Math.Clamp(storedHydration, 0, 100);
        if (clamped == 0 || lastHydrationAt == default || nowUtc <= lastHydrationAt) return clamped;
        var hours = (nowUtc - lastHydrationAt).TotalHours;
        var decay = (int)Math.Floor(hours * HydrationDecayPerHour);
        return Math.Max(0, clamped - decay);
    }

    internal static int MaxCareExpForLevel(int level) => 80 + Math.Max(0, level - 1) * 20;

    internal static long HarvestRewardForLevel(int level) => 10L + Math.Max(1, level) * 5L;

    internal static int DailyWaterLimit(GamSettings s) => Math.Max(0, s.TreeLimit);

    internal static int DailyFreeFertilizeLimit(GamSettings s) => s.TreeLimit <= 0 ? 0 : Math.Min(2, s.TreeLimit);

    private async Task<VirtualTreeDto> BuildAsync(Guid userId, GamSettings s, CancellationToken ct, string? lastCareEffect = null)
    {
        await EnsureTreeAsync(userId, ct);
        var t = await _db.VirtualTreeProgresses.AsNoTracking().FirstAsync(x => x.UserId == userId, ct);
        var balance = (await _points.GetWalletAsync(userId, ct)).Balance;
        var today = GamTime.Today();
        var now = DateTime.UtcNow;

        var isMax = t.TreeLevel >= s.MaxLevel;
        var next = isMax ? null : s.LevelInfo(t.TreeLevel + 1);
        var waterLimit = DailyWaterLimit(s);
        var fertFreeLimit = DailyFreeFertilizeLimit(s);
        var daily = t.LastWaterDate == today ? t.DailyWaterCount : 0;
        var dailyFert = t.LastFertilizeDate == today ? t.DailyFertilizeCount : 0;
        var hydration = ComputeEffectiveHydration(t.HydrationPercent, t.LastHydrationAt, now);
        var maxExp = MaxCareExpForLevel(t.TreeLevel);
        var harvestReady = hydration > 0 && (t.HarvestReady || t.CareExp >= maxExp);
        var careExp = (t.HarvestReady || t.CareExp >= maxExp) ? maxExp : Math.Max(0, t.CareExp);

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
            DailyLimit = waterLimit,
            HydrationPercent = hydration,
            IsWilted = hydration == 0,
            InOptimalHydrationZone = hydration is >= 60 and <= 90,
            CareExp = careExp,
            MaxCareExp = maxExp,
            HarvestReady = harvestReady,
            HarvestRewardPreview = HarvestRewardForLevel(t.TreeLevel),
            TotalFertilized = t.TotalFertilized,
            DailyFertilizeCount = dailyFert,
            DailyFertilizeLimit = fertFreeLimit,
            FertilizerPacks = t.FertilizerPacks,
            TotalHarvested = t.TotalHarvested,
            CanWater = daily < waterLimit && hydration < 95,
            CanFertilize = hydration >= 30 && !t.HarvestReady && t.CareExp < maxExp && (dailyFert < fertFreeLimit || t.FertilizerPacks > 0),
            LastCareEffect = lastCareEffect,
            NextLevel = next?.Level,
            NextLevelTitle = next?.Title,
            UpgradeCost = cost,
            WalletBalance = balance,
            ProgressPercent = progress,
            CanUpgrade = next != null && balance >= next.RequiredPoints,
            Levels = s.Levels.Select(l => new TreeLevelInfoDto
                { Level = l.Level, Title = l.Title, RequiredPoints = l.RequiredPoints, Reached = l.Level <= t.TreeLevel }).ToList(),
            UnlockedFactCount = await activeFacts.CountAsync(f => f.UnlockTreeLevel <= t.TreeLevel, ct),
            TotalFactCount = await activeFacts.CountAsync(ct)
        };
    }

    private Task EnsureTreeAsync(Guid userId, CancellationToken ct) =>
        TreeCareShared.EnsureTreeRowAsync(_db, userId, ct);
}
