using Ecolink.Domain.Enums;

namespace Ecolink.Domain.Entities;

// Các entity của module Gamification & Rewards (báo cáo mục 3.5, 3.6.1).
// Khóa chính dùng Guid theo quy ước chung của dự án. Cột thêm ngoài báo cáo được ghi chú riêng.

/// <summary>Ví điểm 1-1 với User (user_point_wallets, 3.5.1). Dùng cho cả User và Recycler (BR-GAM-08).</summary>
public class UserPointWallet
{
    public Guid Id { get; set; } = Guid.NewGuid();            // wallet_id
    public Guid UserId { get; set; }
    public long CurrentBalance { get; set; }                  // không bao giờ âm (BR-GAM-03)
    public long TotalEarned { get; set; }                     // tổng điểm tích lũy trọn đời
    public long TotalSpent { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Sổ cái biến động điểm, chỉ thêm không sửa (point_transactions, 3.5.2).</summary>
public class PointTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();            // transaction_id
    public Guid WalletId { get; set; }
    public Guid UserId { get; set; }
    public long Points { get; set; }                          // + cộng, - trừ
    public PointActionType ActionType { get; set; }
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }                     // báo cáo: BIGINT; dự án dùng Guid nên lưu chuỗi
    public string? IdempotencyKey { get; set; }               // UNIQUE: chống xử lý trùng (BR-GAM-01)
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Nhật ký điểm danh (daily_checkin_logs, 3.5.3). Mỗi User một dòng mỗi ngày (theo giờ ICT).</summary>
public class DailyCheckinLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly CheckinDate { get; set; }
    public int StreakCount { get; set; } = 1;
    public long PointsAwarded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Cây ảo 1-1 với User (virtual_tree_progress, 3.5.4).</summary>
public class VirtualTreeProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();            // progress_id
    public Guid UserId { get; set; }
    public int TreeLevel { get; set; } = 1;
    public long GrowthPoints { get; set; }                    // tổng điểm đã đầu tư cho cây
    public int TotalWatered { get; set; }
    public int DailyWaterCount { get; set; }
    public DateOnly? LastWaterDate { get; set; }
    public DateTime? LastWateredAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Kho quà (gift_inventory, 3.5.5).</summary>
public class Gift
{
    public Guid Id { get; set; } = Guid.NewGuid();            // gift_id
    public string Name { get; set; } = string.Empty;
    public GiftType Type { get; set; } = GiftType.Physical;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public long PointsRequired { get; set; }
    public int StockQuantity { get; set; }
    public string Status { get; set; } = "ACTIVE";            // ACTIVE | INACTIVE
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Lượt đổi quà (gift_redemptions, 3.5.6).</summary>
public class GiftRedemption
{
    public Guid Id { get; set; } = Guid.NewGuid();            // redemption_id
    public Guid UserId { get; set; }
    public Guid GiftId { get; set; }
    public Gift? Gift { get; set; }
    public long PointsSpent { get; set; }
    public int Quantity { get; set; } = 1;
    public string RedemptionCode { get; set; } = string.Empty; // UNIQUE
    public string? VoucherCode { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public RedemptionStatus Status { get; set; } = RedemptionStatus.Pending;
    public string? DeliveryAddress { get; set; }
    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FulfilledAt { get; set; }
}

/// <summary>Kiến thức môi trường gắn với cấp cây (eco_facts, 3.6.1).</summary>
public class EcoFact
{
    public Guid Id { get; set; } = Guid.NewGuid();            // fact_id
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string? ImageUrl { get; set; }
    public int UnlockTreeLevel { get; set; } = 1;
    public string Status { get; set; } = "ACTIVE";            // ACTIVE | INACTIVE
    public Guid? CreatedByStaffId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tham số gamification (UC A-07). Một dòng duy nhất Id = 1. BẢNG THÊM NGOÀI BÁO CÁO: báo cáo mô tả màn hình
/// cấu hình nhưng không có bảng lưu trữ.
/// </summary>
public class GamificationConfig
{
    public int Id { get; set; } = 1;
    public long DailyNonTransactionCap { get; set; } = 50;       // BR-GAM-02, ví dụ 50 điểm/ngày
    public long CheckinBasePoints { get; set; } = 5;             // mặc định 5 điểm
    public long StreakBonusIncrement { get; set; } = 1;          // cộng thêm mỗi ngày liên tiếp
    public long StreakBonusCap { get; set; } = 20;               // tối đa 20 điểm/ngày
    public int DailyTreeCareLimit { get; set; } = 3;             // BR-GAM-06
    public int VoucherValidityDays { get; set; } = 30;           // BR-GAM-05
    public long DefaultPointsPerKg { get; set; } = 10;           // dùng khi loại phế liệu chưa có mức riêng
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
}

/// <summary>Điểm thưởng theo kg cho từng loại phế liệu (A-07). BẢNG THÊM NGOÀI BÁO CÁO.</summary>
public class PointRateRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string WasteCategoryCode { get; set; } = string.Empty;   // UNIQUE, khớp waste_categories.code
    public long PointsPerKg { get; set; }
}

/// <summary>Bảng ngưỡng cấp cây (A-07). BẢNG THÊM NGOÀI BÁO CÁO. RequiredPoints = điểm phải trả để lên cấp này.</summary>
public class TreeLevelThreshold
{
    public int Level { get; set; }                                  // khóa chính: 1, 2, 3...
    public string Title { get; set; } = string.Empty;
    public long RequiredPoints { get; set; }
}

/// <summary>Chu kỳ bảng xếp hạng thi đua (UC A-08, bảng leaderboard_cycles).</summary>
public class LeaderboardCycle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public LeaderboardPeriodType PeriodType { get; set; } = LeaderboardPeriodType.Monthly;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; }
    public LeaderboardCycleStatus Status { get; set; } = LeaderboardCycleStatus.Active;
    public long Top1BonusPoints { get; set; } = 500;
    public long Top2BonusPoints { get; set; } = 300;
    public long Top3BonusPoints { get; set; } = 150;
    public string? WinnersSnapshotJson { get; set; }
    public Guid? CreatedByAdminId { get; set; }
    public Guid? FinalizedByAdminId { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
