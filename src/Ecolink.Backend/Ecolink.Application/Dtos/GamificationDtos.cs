using System.ComponentModel.DataAnnotations;

namespace Ecolink.Application.Dtos;

public class GamPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

// ============================================================ Ví điểm
public class WalletDto
{
    public long Balance { get; set; }
    public long TotalEarned { get; set; }
    public long TotalSpent { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PointTransactionDto
{
    public Guid Id { get; set; }
    public long Points { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================================ Check-in (U-13)
public class CheckinDayDto
{
    public DateOnly Date { get; set; }
    public bool CheckedIn { get; set; }
    public long PointsAwarded { get; set; }
}

public class CheckinStatusDto
{
    public bool CheckedInToday { get; set; }
    public int CurrentStreak { get; set; }
    /// <summary>Số ngày liên tiếp nếu điểm danh ngay bây giờ.</summary>
    public int NextStreak { get; set; }
    /// <summary>true nếu hôm qua bỏ lỡ: chuỗi sẽ bắt đầu lại từ ngày 1 (AF-01).</summary>
    public bool StreakWillReset { get; set; }
    public long TodayRewardPreview { get; set; }
    public long DailyCapRemaining { get; set; }
    public long WalletBalance { get; set; }
    public List<CheckinDayDto> Calendar { get; set; } = new();
}

public class CheckinResultDto
{
    public long PointsAwarded { get; set; }
    public int StreakCount { get; set; }
    public long WalletBalance { get; set; }
    /// <summary>true nếu điểm bị cắt bớt vì chạm trần điểm phi giao dịch trong ngày.</summary>
    public bool LimitedByDailyCap { get; set; }
}

// ============================================================ Quà (U-14, S-10)
public class GiftDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public long PointsRequired { get; set; }
    public int StockQuantity { get; set; }
    public bool InStock { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RedeemGiftRequest
{
    [Range(1, 10, ErrorMessage = "Số lượng phải từ 1 đến 10.")]
    public int Quantity { get; set; } = 1;

    [MaxLength(500, ErrorMessage = "Địa chỉ giao hàng tối đa 500 ký tự.")]
    public string? DeliveryAddress { get; set; }
}

public class RedemptionGiftInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public class RedemptionDto
{
    public Guid Id { get; set; }
    public string RedemptionCode { get; set; } = string.Empty;
    public RedemptionGiftInfoDto Gift { get; set; } = new();
    public long PointsSpent { get; set; }
    public int Quantity { get; set; }
    public string? VoucherCode { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? DeliveryAddress { get; set; }
    public DateTime RedeemedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
    /// <summary>Chỉ trả cho Staff.</summary>
    public string? UserName { get; set; }
}

public class RedeemResultDto
{
    public RedemptionDto Redemption { get; set; } = new();
    public long WalletBalance { get; set; }
}

public class SaveGiftRequest
{
    // Kiểm tra thiếu/độ dài trong service để trả đúng MSG-AUTH-02 / MSG-NTF-03 của báo cáo
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;      // Physical | Voucher

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    [Range(0, long.MaxValue, ErrorMessage = "Điểm quy đổi phải là số không âm.")]
    public long PointsRequired { get; set; }

    /// <summary>Tồn kho ban đầu (chỉ dùng khi tạo mới; muốn đổi tồn kho dùng restock).</summary>
    [Range(0, 1000000, ErrorMessage = "Số lượng tồn kho phải là số không âm.")]
    public int StockQuantity { get; set; }
}

public class RestockGiftRequest
{
    [Range(1, 1000000, ErrorMessage = "Số lượng nhập thêm phải lớn hơn 0.")]
    public int Quantity { get; set; }
}

public class SetStatusRequest
{
    public string Status { get; set; } = string.Empty;    // ACTIVE | INACTIVE
}

// ============================================================ Cây ảo (U-15) + Eco Fact (S-09)
public class EcoFactDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string? ImageUrl { get; set; }
    public int UnlockTreeLevel { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveEcoFactRequest
{
    // Thiếu trường -> MSG-AUTH-02, vượt độ dài -> MSG-NTF-03 (xử lý trong service)
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string? ImageUrl { get; set; }
    public int UnlockTreeLevel { get; set; } = 1;
}

public class TreeLevelInfoDto
{
    public int Level { get; set; }
    public string Title { get; set; } = string.Empty;
    public long RequiredPoints { get; set; }
    public bool Reached { get; set; }
}

public class VirtualTreeDto
{
    public int Level { get; set; }
    public string LevelTitle { get; set; } = string.Empty;
    public int MaxLevel { get; set; }
    public bool IsMaxLevel { get; set; }
    public long GrowthPoints { get; set; }
    public int TotalWatered { get; set; }
    public int DailyWaterCount { get; set; }
    public int DailyLimit { get; set; }
    public int? NextLevel { get; set; }
    public string? NextLevelTitle { get; set; }
    public long? UpgradeCost { get; set; }
    public long WalletBalance { get; set; }
    /// <summary>Điểm hiện có so với chi phí lên cấp, tối đa 100.</summary>
    public int ProgressPercent { get; set; }
    public bool CanUpgrade { get; set; }
    public List<TreeLevelInfoDto> Levels { get; set; } = new();
    public int UnlockedFactCount { get; set; }
    public int TotalFactCount { get; set; }
}

public class WaterTreeResultDto
{
    public VirtualTreeDto Tree { get; set; } = new();
    public long PointsSpent { get; set; }
    public List<EcoFactDto> UnlockedFacts { get; set; } = new();
}

// ============================================================ Cấu hình (A-07)
public class PointRateDto
{
    [Required, MaxLength(50)]
    public string CategoryCode { get; set; } = string.Empty;
    [Range(0, 1000000, ErrorMessage = "Điểm trên mỗi kg phải là số không âm.")]
    public long PointsPerKg { get; set; }
}

public class TreeLevelConfigDto
{
    [Range(1, 100)]
    public int Level { get; set; }
    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;
    [Range(0, long.MaxValue, ErrorMessage = "Ngưỡng điểm phải là số không âm.")]
    public long RequiredPoints { get; set; }
}

public class GamificationConfigDto
{
    [Range(0, 1000000, ErrorMessage = "Giá trị phải là số không âm.")]
    public long DailyNonTransactionCap { get; set; }
    [Range(0, 1000000, ErrorMessage = "Giá trị phải là số không âm.")]
    public long CheckinBasePoints { get; set; }
    [Range(0, 1000000, ErrorMessage = "Giá trị phải là số không âm.")]
    public long StreakBonusIncrement { get; set; }
    [Range(0, 1000000, ErrorMessage = "Giá trị phải là số không âm.")]
    public long StreakBonusCap { get; set; }
    [Range(1, 100, ErrorMessage = "Giới hạn chăm cây mỗi ngày phải từ 1 đến 100.")]
    public int DailyTreeCareLimit { get; set; }
    [Range(1, 3650, ErrorMessage = "Hạn dùng voucher phải từ 1 đến 3650 ngày.")]
    public int VoucherValidityDays { get; set; }
    [Range(0, 1000000, ErrorMessage = "Giá trị phải là số không âm.")]
    public long DefaultPointsPerKg { get; set; }
    public List<PointRateDto> PointRates { get; set; } = new();
    public List<TreeLevelConfigDto> TreeLevels { get; set; } = new();
    public DateTime? UpdatedAt { get; set; }
}

// ============================================================ Ca làm Staff (6.2)
public class ShiftDto
{
    public Guid Id { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTime CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public decimal? WorkingHours { get; set; }
    public int StreakCount { get; set; }
}

public class ShiftStatusDto
{
    public DateOnly Today { get; set; }
    public bool CanCheckIn { get; set; }
    public bool CanCheckOut { get; set; }
    public int CurrentStreak { get; set; }
    public ShiftDto? TodayShift { get; set; }
}

/// <summary>Bộ tham số gamification đã nạp (đọc nội bộ giữa các service).</summary>
public sealed record GamSettings(
    long DailyCap, long CheckinBase, long StreakIncrement, long StreakCap,
    int TreeLimit, int VoucherDays, long DefaultPerKg,
    IReadOnlyDictionary<string, long> Rates, IReadOnlyList<TreeLevelConfigDto> Levels)
{
    public int MaxLevel => Levels.Count == 0 ? 1 : Levels.Max(l => l.Level);

    public long RateFor(string? categoryCode) =>
        categoryCode != null && Rates.TryGetValue(categoryCode.ToLowerInvariant(), out var r) ? r : DefaultPerKg;

    public long CheckinReward(int streak) =>
        CheckinBase + Math.Min(Math.Max(0, streak - 1) * StreakIncrement, StreakCap);

    public TreeLevelConfigDto? LevelInfo(int level) => Levels.FirstOrDefault(l => l.Level == level);
}
