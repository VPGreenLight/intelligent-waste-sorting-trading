using System.Collections.Concurrent;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

public class WasteRuleEngineService : IWasteRuleEngineService
{
    private readonly ILogger<WasteRuleEngineService> _logger;
    private static readonly ConcurrentDictionary<string, WasteSortingGuideDto> _ruleCache = new();

    public WasteRuleEngineService(ILogger<WasteRuleEngineService> logger)
    {
        _logger = logger;
        InitializeDefaultRules();
    }

    private void InitializeDefaultRules()
    {
        var defaultRules = new List<WasteSortingGuideDto>
        {
            new()
            {
                CategoryCode = "plastic",
                CategoryName = "Nhựa tái chế (Plastic)",
                StandardBinColor = "Cam",
                PreparationSteps = "1. Tráng sạch cặn nước ngọt, dầu mỡ.\n2. Tháo nắp chai và gỡ màng co ni-lông.\n3. Bẹp chai để tiết kiệm thể tích lưu trữ.",
                HandlingAction = "Bỏ vào thùng rác màu Cam hoặc mang đến các điểm đối tác thu mua phế liệu.",
                HazardWarning = "Không trộn lẫn các loại chai nhựa chứa hóa chất độc hại, thuốc trừ sâu."
            },
            new()
            {
                CategoryCode = "paper",
                CategoryName = "Giấy báo & Tài liệu (Paper)",
                StandardBinColor = "Xanh Dương",
                PreparationSteps = "1. Giữ giấy khô ráo, tránh dính nước mưa.\n2. Gỡ bỏ ghim kim bấm, băng dính nhựa.\n3. Xếp phẳng và buộc thành từng bó.",
                HandlingAction = "Bỏ vào thùng rác màu Xanh Dương hoặc liên hệ đối tác EcoLink để bàn giao nhận điểm xanh.",
                HazardWarning = "Giấy vệ sinh, khăn ướt, giấy bóng đã cán màng không thể tái chế."
            },
            new()
            {
                CategoryCode = "glass",
                CategoryName = "Thủy tinh (Glass)",
                StandardBinColor = "Trắng",
                PreparationSteps = "1. Rửa sạch cặn thực phẩm bên trong chai lọ.\n2. Tháo nắp kim loại hoặc nắp bần.\n3. Phân loại riêng thủy tinh màu và thủy tinh trong suốt.",
                HandlingAction = "Bỏ cẩn thận vào thùng rác màu Trắng chuyên biệt.",
                HazardWarning = "Nếu chai vỡ, bọc kỹ bằng nhiều lớp giấy báo dày để tránh gây thương tích cho nhân viên thu gom."
            },
            new()
            {
                CategoryCode = "metal",
                CategoryName = "Kim loại & Vỏ lon (Metal)",
                StandardBinColor = "Xám Kim Loại",
                PreparationSteps = "1. Dốc sạch chất lỏng bên trong vỏ lon.\n2. Có thể giẫm bẹp vỏ lon bia/nước ngọt.\n3. Để riêng đồ nhôm, sắt thép vụn.",
                HandlingAction = "Mang đến cơ sở thu mua phế liệu có giá thu mua cao nhất trên EcoLink.",
                HazardWarning = "Cẩn thận mép lon sắc nhọn có thể cứa đứt tay."
            },
            new()
            {
                CategoryCode = "cardboard",
                CategoryName = "Bìa Carton (Cardboard)",
                StandardBinColor = "Nâu Vàng",
                PreparationSteps = "1. Rạch băng keo dán thùng.\n2. Gập phẳng thùng giấy để tiết kiệm diện tích.\n3. Tránh để ẩm ướt.",
                HandlingAction = "Tập kết số lượng lớn để cơ sở thu gom đến tận nơi thu mua.",
                HazardWarning = "Thùng carton dính nhiều dầu mỡ (như hộp pizza) phải bỏ vào rác thông thường."
            },
            new()
            {
                CategoryCode = "trash",
                CategoryName = "Rác thải thông thường / Không thể tái chế",
                StandardBinColor = "Đen",
                PreparationSteps = "1. Buộc chặt miệng túi rác.\n2. Không đổ nước lỏng vào túi rác.",
                HandlingAction = "Bỏ vào thùng rác màu Đen hoặc điểm tập kết rác sinh hoạt đô thị.",
                HazardWarning = "Không đốt rác tự phát gây ô nhiễm không khí."
            }
        };

        foreach (var rule in defaultRules)
        {
            _ruleCache[rule.CategoryCode.ToLowerInvariant()] = rule;
        }
    }

    public Task<WasteSortingGuideDto?> GetSortingGuideAsync(string categoryCode, CancellationToken cancellationToken = default)
    {
        var key = categoryCode.ToLowerInvariant().Trim();
        if (_ruleCache.TryGetValue(key, out var guide))
        {
            _logger.LogInformation("Tìm thấy quy tắc phân loại cho loại rác: {CategoryCode}", key);
            return Task.FromResult<WasteSortingGuideDto?>(guide);
        }

        _logger.LogWarning("Không tìm thấy quy tắc cấu hình cho loại rác: {CategoryCode}", key);
        return Task.FromResult<WasteSortingGuideDto?>(null);
    }

    public Task<IEnumerable<WasteSortingGuideDto>> GetAllRulesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<WasteSortingGuideDto>>(_ruleCache.Values.ToList());
    }

    public Task InvalidateRuleCacheAsync(string? categoryCode = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(categoryCode))
        {
            _ruleCache.Clear();
            InitializeDefaultRules();
        }
        else
        {
            _ruleCache.TryRemove(categoryCode.ToLowerInvariant().Trim(), out _);
        }
        return Task.CompletedTask;
    }
}
