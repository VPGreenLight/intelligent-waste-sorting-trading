using System.Collections.Concurrent;
using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

public class WasteRuleEngineService : IWasteRuleEngineService
{
    private sealed record DefaultRuleSeed(
        string Code,
        string Name,
        string BinColor,
        bool IsRecyclable,
        string Title,
        string PreparationSteps,
        string SortingInstruction,
        string HandlingAction,
        string HazardWarning);

    private static readonly DefaultRuleSeed[] DefaultSeeds =
    {
        new(
            "plastic",
            "Nhựa tái chế (Plastic)",
            "Cam",
            true,
            "Hướng dẫn phân loại Nhựa tái chế (PET/HDPE)",
            "1. Tráng sạch cặn nước ngọt, dầu mỡ.\n2. Tháo nắp chai và gỡ màng co ni-lông.\n3. Bẹp chai để tiết kiệm thể tích lưu trữ.",
            "Tách riêng chai nhựa PET, can HDPE sạch khỏi nhựa bẩn bám dầu mỡ hoặc xốp hộp.",
            "Bỏ vào thùng rác màu Cam hoặc mang đến các điểm đối tác thu mua phế liệu.",
            "Không trộn lẫn các loại chai nhựa chứa hóa chất độc hại, thuốc trừ sâu."),
        new(
            "paper",
            "Giấy báo & Tài liệu (Paper)",
            "Xanh Dương",
            true,
            "Hướng dẫn phân loại Giấy báo & Tài liệu",
            "1. Giữ giấy khô ráo, tránh dính nước mưa.\n2. Gỡ bỏ ghim kim bấm, băng dính nhựa.\n3. Xếp phẳng và buộc thành từng bó.",
            "Phân tách giấy văn phòng, sách báo khô ráo khỏi giấy thấm dầu hoặc giấy cán màng nhựa.",
            "Bỏ vào thùng rác màu Xanh Dương hoặc liên hệ đối tác EcoLink để bàn giao nhận điểm xanh.",
            "Giấy vệ sinh, khăn ướt, giấy bóng đã cán màng không thể tái chế."),
        new(
            "glass",
            "Thủy tinh (Glass)",
            "Trắng",
            true,
            "Hướng dẫn phân loại Thủy tinh",
            "1. Rửa sạch cặn thực phẩm bên trong chai lọ.\n2. Tháo nắp kim loại hoặc nắp bần.\n3. Phân loại riêng thủy tinh màu và thủy tinh trong suốt.",
            "Phân loại chai lọ thủy tinh còn nguyên vẹn hoặc mảnh vỡ được bọc kín an toàn.",
            "Bỏ cẩn thận vào thùng rác màu Trắng chuyên biệt.",
            "Nếu chai vỡ, bọc kỹ bằng nhiều lớp giấy báo dày để tránh gây thương tích cho nhân viên thu gom."),
        new(
            "metal",
            "Kim loại & Vỏ lon (Metal)",
            "Xám Kim Loại",
            true,
            "Hướng dẫn phân loại Kim loại & Vỏ lon",
            "1. Dốc sạch chất lỏng bên trong vỏ lon.\n2. Có thể giẫm bẹp vỏ lon bia/nước ngọt.\n3. Để riêng đồ nhôm, sắt thép vụn.",
            "Tách riêng lon nhôm, hộp thiếc, sắt thép phế liệu sạch khỏi bình xịt áp suất chứa khí dễ cháy.",
            "Mang đến cơ sở thu mua phế liệu có giá thu mua cao nhất trên EcoLink.",
            "Cẩn thận mép lon sắc nhọn có thể cứa đứt tay."),
        new(
            "cardboard",
            "Bìa Carton (Cardboard)",
            "Nâu Vàng",
            true,
            "Hướng dẫn phân loại Bìa Carton",
            "1. Rạch băng keo dán thùng.\n2. Gập phẳng thùng giấy để tiết kiệm diện tích.\n3. Tránh để ẩm ướt.",
            "Gỡ bỏ băng dính, xốp lót trước khi bó phẳng thùng carton sạch.",
            "Tập kết số lượng lớn để cơ sở thu gom đến tận nơi thu mua.",
            "Thùng carton dính nhiều dầu mỡ (như hộp pizza) phải bỏ vào rác thông thường."),
        new(
            "trash",
            "Rác thải thông thường / Không thể tái chế",
            "Đen",
            false,
            "Hướng dẫn xử lý Rác thải thông thường",
            "1. Buộc chặt miệng túi rác.\n2. Không đổ nước lỏng vào túi rác.",
            "Phân loại riêng khỏi các luồng phế liệu có thể tái chế để tránh nhiễm bẩn.",
            "Bỏ vào thùng rác màu Đen hoặc điểm tập kết rác sinh hoạt đô thị.",
            "Không đốt rác tự phát gây ô nhiễm không khí.")
    };

    private static readonly ConcurrentDictionary<string, WasteSortingGuideDto> _ruleCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<WasteRuleEngineService> _logger;
    private readonly ApplicationDbContext? _db;
    private readonly IAuditLogService? _auditLogService;
    private readonly IAiVisionClient? _aiVisionClient;

    public WasteRuleEngineService(
        ILogger<WasteRuleEngineService> logger,
        ApplicationDbContext? db = null,
        IAuditLogService? auditLogService = null,
        IAiVisionClient? aiVisionClient = null)
    {
        _logger = logger;
        _db = db;
        _auditLogService = auditLogService;
        _aiVisionClient = aiVisionClient;
        InitializeDefaultRulesInMemory();
    }

    private static void InitializeDefaultRulesInMemory()
    {
        foreach (var seed in DefaultSeeds)
        {
            _ruleCache[seed.Code] = new WasteSortingGuideDto
            {
                CategoryCode = seed.Code,
                CategoryName = seed.Name,
                StandardBinColor = seed.BinColor,
                PreparationSteps = seed.PreparationSteps,
                HandlingAction = seed.HandlingAction,
                HazardWarning = seed.HazardWarning
            };
        }
    }

    private async Task EnsureDatabaseDefaultsSeededAsync(CancellationToken ct)
    {
        if (_db == null) return;

        var existingCategories = await _db.WasteCategories
            .Include(c => c.SortingRule)
            .ToListAsync(ct);

        var byCode = existingCategories.ToDictionary(c => c.Code.ToLowerInvariant(), c => c);
        var changed = false;

        foreach (var seed in DefaultSeeds)
        {
            if (!byCode.TryGetValue(seed.Code, out var cat))
            {
                cat = new WasteCategory
                {
                    Code = seed.Code,
                    Name = seed.Name,
                    StandardBinColor = seed.BinColor,
                    IsRecyclable = seed.IsRecyclable,
                    Status = "Active"
                };
                _db.WasteCategories.Add(cat);
                byCode[seed.Code] = cat;
                changed = true;
            }

            if (cat.SortingRule == null)
            {
                var rule = new SortingRule
                {
                    WasteCategoryId = cat.Id,
                    WasteCategory = cat,
                    Title = seed.Title,
                    PreparationSteps = seed.PreparationSteps,
                    SortingInstruction = seed.SortingInstruction,
                    HandlingAction = seed.HandlingAction,
                    HazardWarning = seed.HazardWarning,
                    ApplicableRegion = "VN_NATIONAL",
                    Status = "ACTIVE"
                };
                _db.SortingRules.Add(rule);
                cat.SortingRule = rule;
                changed = true;
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Đã đảm bảo dữ liệu mặc định cho bảng waste_categories và sorting_guidance_rules.");
        }
    }

    public async Task<WasteSortingGuideDto?> GetSortingGuideAsync(string categoryCode, CancellationToken cancellationToken = default)
    {
        var key = (categoryCode ?? string.Empty).ToLowerInvariant().Trim();
        if (string.IsNullOrEmpty(key)) return null;

        if (_db == null)
        {
            if (_ruleCache.TryGetValue(key, out var cachedGuide))
            {
                _logger.LogInformation("Tìm thấy quy tắc phân loại cho loại rác: {CategoryCode}", key);
                return cachedGuide;
            }

            _logger.LogWarning("Không tìm thấy quy tắc cấu hình cho loại rác: {CategoryCode}", key);
            return null;
        }

        await EnsureDatabaseDefaultsSeededAsync(cancellationToken);

        var rule = await _db.SortingRules
            .AsNoTracking()
            .Include(r => r.WasteCategory)
            .FirstOrDefaultAsync(
                r => r.WasteCategory != null
                     && r.WasteCategory.Code == key
                     && r.WasteCategory.Status == "Active"
                     && r.Status == "ACTIVE",
                cancellationToken);

        if (rule?.WasteCategory == null)
        {
            _ruleCache.TryRemove(key, out _);
            _logger.LogWarning("Không tìm thấy quy tắc ACTIVE hoặc danh mục đang Inactive cho loại rác: {CategoryCode}", key);
            return null;
        }

        var dto = MapGuide(rule.WasteCategory, rule);
        _ruleCache[key] = dto;
        return dto;
    }

    public async Task<IEnumerable<WasteSortingGuideDto>> GetAllRulesAsync(CancellationToken cancellationToken = default)
    {
        if (_db == null)
        {
            return _ruleCache.Values.ToList();
        }

        await EnsureDatabaseDefaultsSeededAsync(cancellationToken);

        var activeRules = await _db.SortingRules
            .AsNoTracking()
            .Include(r => r.WasteCategory)
            .Where(r => r.WasteCategory != null && r.WasteCategory.Status == "Active" && r.Status == "ACTIVE")
            .OrderBy(r => r.WasteCategory!.Name)
            .ToListAsync(cancellationToken);

        var list = new List<WasteSortingGuideDto>(activeRules.Count);
        foreach (var r in activeRules)
        {
            var dto = MapGuide(r.WasteCategory!, r);
            _ruleCache[dto.CategoryCode.ToLowerInvariant()] = dto;
            list.Add(dto);
        }

        return list;
    }

    public async Task<IReadOnlyList<SortingRuleDetailDto>> GetAllRuleDetailsAsync(
        bool includeDraft = true,
        CancellationToken cancellationToken = default)
    {
        if (_db == null)
        {
            return _ruleCache.Values.Select(g => new SortingRuleDetailDto
            {
                RuleId = Guid.Empty,
                WasteCategoryId = Guid.Empty,
                CategoryCode = g.CategoryCode,
                CategoryName = g.CategoryName,
                CategoryStatus = "Active",
                StandardBinColor = g.StandardBinColor,
                Title = g.CategoryName,
                PreparationSteps = g.PreparationSteps,
                SortingInstruction = g.PreparationSteps,
                HandlingAction = g.HandlingAction,
                HazardWarning = g.HazardWarning,
                ApplicableRegion = "VN_NATIONAL",
                Status = "ACTIVE"
            }).ToList();
        }

        await EnsureDatabaseDefaultsSeededAsync(cancellationToken);

        if (includeDraft)
        {
            var categories = await _db.WasteCategories
                .AsNoTracking()
                .Include(c => c.SortingRule)
                .OrderBy(c => c.Code)
                .ToListAsync(cancellationToken);

            return categories
                .Select(c => c.SortingRule != null ? MapDetail(c, c.SortingRule) : MapUnconfiguredDetail(c))
                .ToList();
        }

        var rules = await _db.SortingRules
            .AsNoTracking()
            .Include(r => r.WasteCategory)
            .Where(r => r.WasteCategory != null && r.Status == "ACTIVE" && r.WasteCategory!.Status == "Active")
            .OrderBy(r => r.WasteCategory!.Code)
            .ToListAsync(cancellationToken);

        return rules.Select(r => MapDetail(r.WasteCategory!, r)).ToList();
    }

    public async Task<SortingRuleDetailDto> UpsertSortingRuleAsync(
        Guid adminUserId,
        Guid categoryId,
        UpsertSortingRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_db == null)
        {
            throw new BusinessRuleException(
                "BR-SRT-01",
                "MSG-GEN-01",
                "Rule Engine đang chạy ở chế độ bộ nhớ tạm, không hỗ trợ lưu DB.");
        }

        var category = await _db.WasteCategories
            .Include(c => c.SortingRule)
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy danh mục rác với ID '{categoryId}'.");

        var now = DateTime.UtcNow;
        var region = string.IsNullOrWhiteSpace(request.ApplicableRegion)
            ? "VN_NATIONAL"
            : request.ApplicableRegion.Trim().ToUpperInvariant();

        var existing = category.SortingRule
            ?? await _db.SortingRules.FirstOrDefaultAsync(r => r.WasteCategoryId == categoryId, cancellationToken);

        bool isNew = existing == null;
        string? oldSnapshot = null;

        if (existing == null)
        {
            existing = new SortingRule
            {
                WasteCategoryId = category.Id,
                Title = request.Title.Trim(),
                PreparationSteps = request.PreparationSteps.Trim(),
                SortingInstruction = request.SortingInstruction.Trim(),
                HandlingAction = request.HandlingAction.Trim(),
                HazardWarning = string.IsNullOrWhiteSpace(request.HazardWarning) ? null : request.HazardWarning.Trim(),
                ApplicableRegion = region,
                Status = "DRAFT",
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.SortingRules.Add(existing);
        }
        else
        {
            oldSnapshot = JsonSerializer.Serialize(new
            {
                existing.Title,
                existing.PreparationSteps,
                existing.SortingInstruction,
                existing.HandlingAction,
                existing.HazardWarning,
                existing.ApplicableRegion,
                existing.Status
            });

            existing.Title = request.Title.Trim();
            existing.PreparationSteps = request.PreparationSteps.Trim();
            existing.SortingInstruction = request.SortingInstruction.Trim();
            existing.HandlingAction = request.HandlingAction.Trim();
            existing.HazardWarning = string.IsNullOrWhiteSpace(request.HazardWarning) ? null : request.HazardWarning.Trim();
            existing.ApplicableRegion = region;
            existing.Status = "DRAFT"; // Theo BR-SRT-03: bản chỉnh sửa mới ở trạng thái DRAFT cho tới khi được Publish chính thức
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Nếu rule chuyển về DRAFT thì gỡ khỏi cache để không phục vụ bản nháp chưa duyệt
        _ruleCache.TryRemove(category.Code.ToLowerInvariant(), out _);

        if (_auditLogService != null)
        {
            await _auditLogService.LogAsync(
                action: isNew ? "CREATE_SORTING_RULE" : "UPDATE_SORTING_RULE",
                entityType: nameof(SortingRule),
                entityId: existing.Id.ToString(),
                oldValues: oldSnapshot,
                newValues: new
                {
                    existing.Title,
                    existing.PreparationSteps,
                    existing.SortingInstruction,
                    existing.HandlingAction,
                    existing.HazardWarning,
                    existing.ApplicableRegion,
                    existing.Status
                },
                reason: $"Admin cập nhật quy tắc phân loại cho danh mục '{category.Code}' (trạng thái DRAFT).",
                explicitActorId: adminUserId,
                ct: cancellationToken);
        }

        return MapDetail(category, existing);
    }

    public async Task<PublishSortingRuleResultDto> PublishSortingRuleAsync(
        Guid adminUserId,
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        if (_db == null)
        {
            throw new BusinessRuleException(
                "BR-SRT-03",
                "MSG-GEN-01",
                "Rule Engine đang chạy ở chế độ bộ nhớ tạm, không hỗ trợ Publish DB.");
        }

        var rule = await _db.SortingRules
            .Include(r => r.WasteCategory)
            .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy quy tắc phân loại với ID '{ruleId}'.");

        var category = rule.WasteCategory
            ?? throw new NotFoundException("Quy tắc phân loại không gắn với danh mục rác hợp lệ.");

        if (string.IsNullOrWhiteSpace(rule.PreparationSteps) || string.IsNullOrWhiteSpace(rule.HandlingAction))
        {
            throw new BusinessRuleException(
                "BR-SRT-03",
                "MSG-SRT-03",
                "Quy tắc phân loại chưa đủ nội dung hướng dẫn sơ chế hoặc xử lý để phát hành.");
        }

        var oldStatus = rule.Status;
        rule.Status = "ACTIVE";
        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var key = category.Code.ToLowerInvariant().Trim();
        if (string.Equals(category.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            _ruleCache[key] = MapGuide(category, rule);
        }
        else
        {
            _ruleCache.TryRemove(key, out _);
        }

        if (_auditLogService != null)
        {
            await _auditLogService.LogAsync(
                action: "PUBLISH_SORTING_RULE",
                entityType: nameof(SortingRule),
                entityId: rule.Id.ToString(),
                oldValues: new { Status = oldStatus },
                newValues: new { Status = rule.Status, CategoryCode = category.Code },
                reason: $"Phát hành chính thức quy tắc phân loại cho danh mục '{category.Code}'.",
                explicitActorId: adminUserId,
                ct: cancellationToken);
        }

        var instructionsText = string.Join(
            " | ",
            new[] { rule.PreparationSteps, rule.SortingInstruction, rule.HandlingAction }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        bool aiSynced = false;
        string? aiMessage = null;

        if (_aiVisionClient != null)
        {
            try
            {
                var syncResp = await _aiVisionClient.SyncSortingRuleAsync(
                    key,
                    category.StandardBinColor,
                    instructionsText,
                    cancellationToken);
                aiSynced = syncResp.Success;
                aiMessage = syncResp.Message;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Đồng bộ quy tắc {CategoryCode} sang AI Service thất bại nhưng bản DB đã Publish.", key);
                aiSynced = false;
                aiMessage = $"AI Service sync warning: {ex.Message}";
            }
        }

        return new PublishSortingRuleResultDto
        {
            Rule = MapDetail(category, rule),
            AiServiceSynced = aiSynced,
            AiSyncMessage = aiMessage
        };
    }

    public async Task InvalidateRuleCacheAsync(string? categoryCode = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(categoryCode))
        {
            _ruleCache.Clear();
            if (_db == null)
            {
                InitializeDefaultRulesInMemory();
                return;
            }

            await GetAllRulesAsync(cancellationToken);
            return;
        }

        var key = categoryCode.ToLowerInvariant().Trim();
        _ruleCache.TryRemove(key, out _);

        if (_db != null)
        {
            await GetSortingGuideAsync(key, cancellationToken);
        }
    }

    private static WasteSortingGuideDto MapGuide(WasteCategory category, SortingRule rule) => new()
    {
        CategoryCode = category.Code,
        CategoryName = category.Name,
        StandardBinColor = category.StandardBinColor,
        PreparationSteps = string.IsNullOrWhiteSpace(rule.SortingInstruction)
            ? rule.PreparationSteps
            : $"{rule.PreparationSteps}\n• Lưu ý phân loại: {rule.SortingInstruction}",
        HandlingAction = rule.HandlingAction,
        HazardWarning = rule.HazardWarning
    };

    private static SortingRuleDetailDto MapDetail(WasteCategory category, SortingRule rule) => new()
    {
        RuleId = rule.Id,
        WasteCategoryId = category.Id,
        CategoryCode = category.Code,
        CategoryName = category.Name,
        CategoryStatus = category.Status,
        StandardBinColor = category.StandardBinColor,
        Title = rule.Title,
        PreparationSteps = rule.PreparationSteps,
        SortingInstruction = rule.SortingInstruction,
        HandlingAction = rule.HandlingAction,
        HazardWarning = rule.HazardWarning,
        ApplicableRegion = rule.ApplicableRegion,
        Status = rule.Status,
        CreatedAt = rule.CreatedAt,
        UpdatedAt = rule.UpdatedAt
    };

    private static SortingRuleDetailDto MapUnconfiguredDetail(WasteCategory category) => new()
    {
        RuleId = Guid.Empty,
        WasteCategoryId = category.Id,
        CategoryCode = category.Code,
        CategoryName = category.Name,
        CategoryStatus = category.Status,
        StandardBinColor = category.StandardBinColor,
        Title = $"Hướng dẫn phân loại {category.Name}",
        PreparationSteps = category.IsRecyclable
            ? "1. Làm sạch sơ bộ và để khô ráo trước khi phân loại.\n2. Xếp gọn để tối ưu thể tích lưu trữ."
            : "1. Buộc kín miệng túi rác.\n2. Không trộn lẫn vào nhóm phế liệu tái chế.",
        SortingInstruction = !string.IsNullOrWhiteSpace(category.Description)
            ? category.Description!
            : $"Tách riêng {category.Name} khỏi các nhóm rác khác để tránh lẫn tạp chất.",
        HandlingAction = category.IsRecyclable
            ? $"Bỏ vào thùng rác màu {category.StandardBinColor} hoặc đặt lịch thu gom trên EcoLink."
            : $"Bỏ vào thùng rác màu {category.StandardBinColor} theo quy định thu gom rác sinh hoạt.",
        HazardWarning = null,
        ApplicableRegion = "VN_NATIONAL",
        Status = "DRAFT",
        CreatedAt = category.CreatedAt,
        UpdatedAt = category.UpdatedAt
    };
}
