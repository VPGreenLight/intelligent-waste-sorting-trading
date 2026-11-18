using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Cấu hình gamification (UC A-07): điểm/kg theo loại phế liệu, trần điểm phi giao dịch, thưởng check-in và chuỗi ngày,
/// giới hạn chăm cây, bảng ngưỡng cấp cây. Lần đọc đầu tiên tự tạo giá trị mặc định. Mọi thay đổi ghi audit log (BR-ADM-05).
/// </summary>
public class GamificationConfigService : IGamificationConfigService
{
    private const string Rule = "BR-GAM-02";
    private const int MaxLevels = 10;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly (int Level, string Title, long Points)[] DefaultLevels =
    {
        (1, "Seedling", 0), (2, "Sprout", 100), (3, "Young Tree", 300), (4, "Ancient Canopy", 700)
    };

    private readonly ApplicationDbContext _db;

    public GamificationConfigService(ApplicationDbContext db) => _db = db;

    public async Task<GamificationConfigDto> GetAsync(CancellationToken ct = default) =>
        ToDto(await GetSettingsAsync(ct), (await _db.GamificationConfigs.AsNoTracking().FirstAsync(c => c.Id == 1, ct)).UpdatedAt);

    public async Task<GamSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var c = await _db.GamificationConfigs.AsNoTracking().FirstAsync(x => x.Id == 1, ct);
        var rates = await _db.PointRateRules.AsNoTracking().ToListAsync(ct);
        var levels = await _db.TreeLevelThresholds.AsNoTracking().OrderBy(l => l.Level).ToListAsync(ct);
        return new GamSettings(c.DailyNonTransactionCap, c.CheckinBasePoints, c.StreakBonusIncrement, c.StreakBonusCap,
            c.DailyTreeCareLimit, c.VoucherValidityDays, c.DefaultPointsPerKg,
            rates.ToDictionary(r => r.WasteCategoryCode.ToLowerInvariant(), r => r.PointsPerKg),
            levels.Select(l => new TreeLevelConfigDto { Level = l.Level, Title = l.Title, RequiredPoints = l.RequiredPoints }).ToList());
    }

    public async Task<GamificationConfigDto> UpdateAsync(Guid adminId, GamificationConfigDto r, CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var rates = await ValidateAsync(r, ct);
        var before = await GetAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await ApplyAsync(adminId, r, rates, ct);
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = adminId,
            Action = "GAMIFICATION_CONFIG_UPDATE",
            EntityType = "GamificationConfig",
            EntityId = "1",
            OldValues = JsonSerializer.Serialize(before, Json),
            NewValues = JsonSerializer.Serialize(r, Json)
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(ct);
    }

    public async Task<GamificationConfigDto> ResetToDefaultsAsync(Guid adminId, CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var before = await GetAsync(ct);
        var defaults = new GamificationConfigDto
        {
            DailyNonTransactionCap = 50, CheckinBasePoints = 5, StreakBonusIncrement = 1, StreakBonusCap = 20,
            DailyTreeCareLimit = 3, VoucherValidityDays = 30, DefaultPointsPerKg = 10,
            PointRates = new List<PointRateDto>(),
            TreeLevels = DefaultLevels.Select(l => new TreeLevelConfigDto { Level = l.Level, Title = l.Title, RequiredPoints = l.Points }).ToList()
        };
        var rates = await ValidateAsync(defaults, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await ApplyAsync(adminId, defaults, rates, ct);
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = adminId,
            Action = "GAMIFICATION_CONFIG_RESET",
            EntityType = "GamificationConfig",
            EntityId = "1",
            OldValues = JsonSerializer.Serialize(before, Json),
            NewValues = JsonSerializer.Serialize(defaults, Json)
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(ct);
    }

    // ------------------------------------------------------------------
    private async Task<List<PointRateDto>> ValidateAsync(GamificationConfigDto r, CancellationToken ct)
    {
        if (r.StreakBonusCap < 0 || r.StreakBonusIncrement < 0 || r.CheckinBasePoints < 0 || r.DailyNonTransactionCap < 0
            || r.DefaultPointsPerKg < 0)
            throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Các giá trị điểm phải là số không âm.");

        // Điểm/kg theo loại phế liệu: mã phải tồn tại, không trùng
        var rates = new List<PointRateDto>();
        foreach (var p in r.PointRates ?? new List<PointRateDto>())
        {
            var code = (p.CategoryCode ?? string.Empty).Trim().ToLowerInvariant();
            if (code.Length == 0 || p.PointsPerKg < 0)
                throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Mỗi loại phế liệu cần mã hợp lệ và điểm/kg không âm.");
            if (rates.Any(x => x.CategoryCode == code))
                throw GamErrors.Rule(Rule, "MSG-AUTH-02", $"Loại phế liệu '{code}' bị khai báo trùng.");
            rates.Add(new PointRateDto { CategoryCode = code, PointsPerKg = p.PointsPerKg });
        }
        if (rates.Count > 0)
        {
            var codes = rates.Select(x => x.CategoryCode).ToList();
            var known = await _db.WasteCategories.AsNoTracking().Where(w => codes.Contains(w.Code.ToLower()))
                .Select(w => w.Code.ToLower()).ToListAsync(ct);
            var unknown = codes.Except(known).ToList();
            if (unknown.Count > 0)
                throw GamErrors.Rule(Rule, "MSG-AUTH-02", $"Không tìm thấy loại phế liệu: {string.Join(", ", unknown)}.");
        }

        // Bảng cấp cây: bắt đầu từ cấp 1 (0 điểm), liên tục, ngưỡng tăng dần nghiêm ngặt (EF-01 của A-07)
        var levels = (r.TreeLevels ?? new List<TreeLevelConfigDto>()).OrderBy(l => l.Level).ToList();
        if (levels.Count == 0 || levels.Count > MaxLevels)
            throw GamErrors.Rule("BR-GAM-06", "MSG-AUTH-02", $"Bảng cấp cây phải có từ 1 đến {MaxLevels} cấp.");
        for (var i = 0; i < levels.Count; i++)
        {
            var l = levels[i];
            if (l.Level != i + 1)
                throw GamErrors.Rule("BR-GAM-06", "MSG-AUTH-02", "Các cấp cây phải liên tục bắt đầu từ 1.");
            if (string.IsNullOrWhiteSpace(l.Title) || l.Title.Trim().Length > 100)
                throw GamErrors.Rule("BR-GAM-06", "MSG-AUTH-02", "Tên cấp cây không được để trống và tối đa 100 ký tự.");
            if (l.RequiredPoints < 0)
                throw GamErrors.Rule("BR-GAM-06", "MSG-AUTH-02", "Ngưỡng điểm phải là số không âm.");
            if (i == 0 && l.RequiredPoints != 0)
                throw GamErrors.Rule("BR-GAM-06", "MSG-AUTH-02", "Cấp 1 là cấp khởi đầu nên ngưỡng điểm phải bằng 0.");
            if (i > 0 && l.RequiredPoints <= levels[i - 1].RequiredPoints)
                throw GamErrors.Rule("BR-GAM-06", "MSG-AUTH-02", "Ngưỡng điểm các cấp phải tăng dần nghiêm ngặt.");
        }

        // Không được bỏ bớt cấp nếu còn Eco Fact đang hoạt động gắn với cấp đó
        var maxLevel = levels.Count;
        var orphan = await _db.EcoFacts.AsNoTracking().AnyAsync(f => f.Status == "ACTIVE" && f.UnlockTreeLevel > maxLevel, ct);
        if (orphan)
            throw GamErrors.Rule("BR-GAM-06", "MSG-AUTH-02",
                $"Còn Eco Fact đang hoạt động gắn với cấp cây lớn hơn {maxLevel}. Hãy đổi cấp hoặc tắt các Eco Fact đó trước.");

        r.TreeLevels = levels;
        return rates;
    }

    private async Task ApplyAsync(Guid adminId, GamificationConfigDto r, List<PointRateDto> rates, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await _db.GamificationConfigs.Where(c => c.Id == 1).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.DailyNonTransactionCap, r.DailyNonTransactionCap)
            .SetProperty(c => c.CheckinBasePoints, r.CheckinBasePoints)
            .SetProperty(c => c.StreakBonusIncrement, r.StreakBonusIncrement)
            .SetProperty(c => c.StreakBonusCap, r.StreakBonusCap)
            .SetProperty(c => c.DailyTreeCareLimit, r.DailyTreeCareLimit)
            .SetProperty(c => c.VoucherValidityDays, r.VoucherValidityDays)
            .SetProperty(c => c.DefaultPointsPerKg, r.DefaultPointsPerKg)
            .SetProperty(c => c.UpdatedAt, now)
            .SetProperty(c => c.UpdatedBy, (Guid?)adminId), ct);

        await _db.PointRateRules.ExecuteDeleteAsync(ct);
        await _db.TreeLevelThresholds.ExecuteDeleteAsync(ct);
        _db.PointRateRules.AddRange(rates.Select(x => new PointRateRule { WasteCategoryCode = x.CategoryCode, PointsPerKg = x.PointsPerKg }));
        _db.TreeLevelThresholds.AddRange(r.TreeLevels.Select(l => new TreeLevelThreshold
            { Level = l.Level, Title = l.Title.Trim(), RequiredPoints = l.RequiredPoints }));
    }

    private async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        // INSERT ... ON CONFLICT DO NOTHING: an toàn khi nhiều request cùng khởi tạo lần đầu
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO gamification_config (id, daily_non_transaction_cap, checkin_base_points, streak_bonus_increment,
                streak_bonus_cap, daily_tree_care_limit, voucher_validity_days, default_points_per_kg, updated_at)
            VALUES (1, 50, 5, 1, 20, 3, 30, 10, NOW())
            ON CONFLICT (id) DO NOTHING", ct);

        if (!await _db.TreeLevelThresholds.AnyAsync(ct))
        {
            foreach (var l in DefaultLevels)
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO tree_level_thresholds (level, title, required_points)
                    VALUES ({l.Level}, {l.Title}, {l.Points}) ON CONFLICT (level) DO NOTHING", ct);
        }
    }

    private static GamificationConfigDto ToDto(GamSettings s, DateTime updatedAt) => new()
    {
        DailyNonTransactionCap = s.DailyCap,
        CheckinBasePoints = s.CheckinBase,
        StreakBonusIncrement = s.StreakIncrement,
        StreakBonusCap = s.StreakCap,
        DailyTreeCareLimit = s.TreeLimit,
        VoucherValidityDays = s.VoucherDays,
        DefaultPointsPerKg = s.DefaultPerKg,
        PointRates = s.Rates.OrderBy(x => x.Key).Select(x => new PointRateDto { CategoryCode = x.Key, PointsPerKg = x.Value }).ToList(),
        TreeLevels = s.Levels.ToList(),
        UpdatedAt = updatedAt
    };
}
