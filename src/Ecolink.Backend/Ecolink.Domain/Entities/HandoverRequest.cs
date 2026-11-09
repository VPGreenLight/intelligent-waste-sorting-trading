using Ecolink.Domain.Enums;

namespace Ecolink.Domain.Entities;

/// <summary>
/// Yêu cầu bàn giao phế liệu của người dân gửi tới một Recycler (UC U-06, U-07, R-03, R-04).
/// Ánh xạ bảng HandoverRequest (báo cáo mục 3.3.2). Cột bổ sung duy nhất: is_late_cancellation (BR-HND-06).
/// </summary>
public class HandoverRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();                 // request_id
    public string RequestCode { get; set; } = string.Empty;        // request_code, vd HND-20261103-0001

    public Guid UserId { get; set; }                               // user_id (người dân)
    public Guid RecyclerId { get; set; }                           // recycler_id = RecyclerProfile.UserId
    public RecyclerProfile? Recycler { get; set; }

    public string PickupAddress { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    /// <summary>Cột waste_photo_url (TEXT): lưu mảng JSON các URL ảnh (1–5 ảnh theo SCR-USR-06).</summary>
    public string? WastePhotoUrl { get; set; }

    public DateTime ProposedTimeSlot { get; set; }                 // UTC
    public DateTime? ConfirmedTimeSlot { get; set; }
    public DateTime? ActualHandoverAt { get; set; }

    public HandoverStatus Status { get; set; } = HandoverStatus.Pending;

    public string? UserNote { get; set; }
    public string? RecyclerNote { get; set; }
    public string? CancelReason { get; set; }                      // lý do hủy / từ chối
    public Guid? CancelledBy { get; set; }                         // user_id người hủy

    /// <summary>[Cột bổ sung] Hủy trong vòng {X} giờ trước lịch hẹn đã xác nhận (BR-HND-06, MSG-HND-06).</summary>
    public bool IsLateCancellation { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<HandoverRequestItem> Items { get; set; } = new List<HandoverRequestItem>();
}

/// <summary>Dòng phế liệu của yêu cầu bàn giao (báo cáo mục 3.3.3).</summary>
public class HandoverRequestItem
{
    public Guid Id { get; set; } = Guid.NewGuid();                 // item_id
    public Guid RequestId { get; set; }
    public HandoverRequest? Request { get; set; }

    public Guid WasteCategoryId { get; set; }                      // category_id
    public WasteCategory? WasteCategory { get; set; }

    public decimal EstimatedWeightKg { get; set; }
    public decimal? ActualWeightKg { get; set; }                   // Recycler nhập khi lập biên nhận (module khác)
    public decimal? AgreedPricePerKg { get; set; }                 // chốt khi thương lượng / lập biên nhận
    public string? ItemNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
