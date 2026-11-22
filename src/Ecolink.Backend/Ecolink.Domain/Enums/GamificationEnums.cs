namespace Ecolink.Domain.Enums;

/// <summary>Loại sự kiện tạo biến động điểm (point_action_type, báo cáo 3.5.2). Lưu DB dạng chuỗi.</summary>
public enum PointActionType
{
    HandoverReward = 1,
    DailyCheckin = 2,
    RedeemGift = 3,
    TreeCare = 4,
    LeaderboardBonus = 5,
    Adjustment = 6
}

/// <summary>gift_type (báo cáo 3.5.5): Physical = quà hiện vật, Voucher = mã giảm giá điện tử.</summary>
public enum GiftType
{
    Physical = 1,
    Voucher = 2
}

/// <summary>redemption_status (báo cáo 3.5.6): Pending, Redeemed, Delivered, Expired, Cancelled.</summary>
public enum RedemptionStatus
{
    Pending = 1,
    Redeemed = 2,
    Delivered = 3,
    Expired = 4,
    Cancelled = 5
}

/// <summary>Chu kỳ bảng xếp hạng thi đua (UC A-08): Weekly, Monthly, Quarterly.</summary>
public enum LeaderboardPeriodType
{
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3
}

/// <summary>Trạng thái chu kỳ bảng xếp hạng (UC A-08): Upcoming, Active, Finalized, Cancelled.</summary>
public enum LeaderboardCycleStatus
{
    Upcoming = 1,
    Active = 2,
    Finalized = 3,
    Cancelled = 4
}
