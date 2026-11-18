using Ecolink.Domain.Common;
using Ecolink.Domain.Enums;

namespace Ecolink.Domain.Entities;

/// <summary>
/// Đánh giá 1–5 sao &amp; nhận xét của Citizen dành cho Recycler sau khi hoàn tất bàn giao (U-10).
/// Tuân thủ BR-RVW-01 (1 đánh giá / 1 đơn hoàn tất trong 14 ngày), BR-RVW-02 (không sửa sao), BR-RVW-03 (tác giả tự xóa / Staff ẩn vi phạm).
/// </summary>
public class TransactionReview : BaseAuditableEntity
{
    public Guid RequestId { get; set; }
    public HandoverRequest? Request { get; set; }

    public Guid ReviewerId { get; set; }
    public User? Reviewer { get; set; }

    public Guid RecyclerId { get; set; }
    public RecyclerProfile? Recycler { get; set; }

    public int RatingStars { get; set; }
    public string? Comment { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Visible;

    public int ReportCount { get; set; } = 0;
    public string? LastReportReason { get; set; }
    public Guid? ReportedByUserId { get; set; }
    public User? ReportedByUser { get; set; }

    public Guid? ModeratedByStaffId { get; set; }
    public User? ModeratedByStaff { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public string? ModerationReason { get; set; }
}

/// <summary>
/// Phiếu khiếu nại giao dịch 2 chiều giữa Citizen và Recycler (U-11) &amp; Hồ sơ Trọng tài của Staff (S-05).
/// Tuân thủ BR-DSP-01 (hạn 7 ngày, tối đa 1 phiếu active mỗi HandoverRequest), BR-DSP-02 (0..5 ảnh R2), BR-DSP-04 &amp; BR-DSP-06.
/// </summary>
public class TransactionComplaint : BaseAuditableEntity
{
    public string ComplaintCode { get; set; } = string.Empty;

    public Guid RequestId { get; set; }
    public HandoverRequest? Request { get; set; }

    public Guid? ReceiptId { get; set; }
    public HandoverReceipt? Receipt { get; set; }

    public Guid ComplainantId { get; set; }
    public User? Complainant { get; set; }

    public string ComplainantRole { get; set; } = "Citizen"; // Citizen | Recycler

    public Guid RespondentId { get; set; }
    public User? Respondent { get; set; }

    public ComplaintCategory Category { get; set; } = ComplaintCategory.ScaleDiscrepancy;
    public string Description { get; set; } = string.Empty;
    public string EvidenceUrlsJson { get; set; } = "[]";
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;

    public Guid? AssignedStaffId { get; set; }
    public User? AssignedStaff { get; set; }
    public DateTime? TakenAt { get; set; }

    public ComplaintResolutionOutcome? ResolutionOutcome { get; set; }
    public string? ResolutionNote { get; set; }
    public Guid? ResolvedByStaffId { get; set; }
    public User? ResolvedByStaff { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public bool PointsRevoked { get; set; } = false;
    public bool WarningLogged { get; set; } = false;
}
