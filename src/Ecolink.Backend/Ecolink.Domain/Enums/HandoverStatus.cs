namespace Ecolink.Domain.Enums;

/// <summary>
/// Trạng thái yêu cầu bàn giao phế liệu (báo cáo mục 3.3.2, BR-HND-02).
/// Luồng một chiều: Pending → (Negotiating) → Accepted → Scheduled → Completed;
/// trạng thái kết thúc: Rejected / Cancelled / Expired / Disputed.
/// </summary>
public enum HandoverStatus
{
    Pending = 1,
    Negotiating = 2,
    Accepted = 3,
    Scheduled = 4,
    Completed = 5,
    Rejected = 6,
    Cancelled = 7,
    Expired = 8,
    Disputed = 9,

    /// <summary>[Bổ sung] Recycler đã lập biên nhận, chờ người dân xác nhận (U-09, JOB-01). Báo cáo gọi là Awaiting/Pending_User_Confirmation.</summary>
    AwaitingConfirmation = 10
}
