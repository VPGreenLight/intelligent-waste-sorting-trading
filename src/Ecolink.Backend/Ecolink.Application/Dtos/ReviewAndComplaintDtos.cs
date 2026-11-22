namespace Ecolink.Application.Dtos;

// =========================================================================
// USE CASE U-10: RATE RECYCLER, PUBLIC REVIEWS & ABUSE MODERATION (STT 103-108)
// =========================================================================

public class CreateReviewRequest
{
    public int RatingStars { get; set; }
    public string? Comment { get; set; }
}

public class RecyclerReviewFilterRequest
{
    public int? RatingStars { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class ReportReviewRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class ModerateReviewRequest
{
    /// <summary>Hide | Restore</summary>
    public string Action { get; set; } = string.Empty;
    public string ModerationReason { get; set; } = string.Empty;
}

public class TransactionReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid RequestId { get; set; }
    public string? RequestCode { get; set; }
    public Guid ReviewerId { get; set; }
    public string ReviewerDisplayName { get; set; } = string.Empty;
    public Guid RecyclerId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public int RatingStars { get; set; }
    public string? Comment { get; set; }
    public string Status { get; set; } = "Visible";
    public int ReportCount { get; set; }
    public string? LastReportReason { get; set; }
    public string? ModerationReason { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal? UpdatedRecyclerAverageRating { get; set; }
    public int? UpdatedRecyclerTotalReviews { get; set; }
}

public class StarDistributionDto
{
    public int FiveStars { get; set; }
    public int FourStars { get; set; }
    public int ThreeStars { get; set; }
    public int TwoStars { get; set; }
    public int OneStar { get; set; }
}

public class RecyclerReviewsSummaryDto
{
    public Guid RecyclerId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public StarDistributionDto StarDistribution { get; set; } = new();
    public PagedResult<TransactionReviewDto> Reviews { get; set; } = new();
}

// =========================================================================
// USE CASE U-11 & S-05: BILATERAL COMPLAINTS & 360 ARBITRATION (STT 109-114)
// =========================================================================

public class SubmitComplaintRequest
{
    public Guid HandoverRequestId { get; set; }
    /// <summary>ScaleDiscrepancy | PaymentIssue | NoShow | Misconduct | Other</summary>
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string>? EvidencePhotoUrls { get; set; }
}

public class MyComplaintFilterRequest
{
    /// <summary>all | submitted | against</summary>
    public string? Role { get; set; } = "all";
    /// <summary>Open | Investigating | Resolved | Dismissed</summary>
    public string? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class StaffComplaintFilterRequest
{
    public string? Status { get; set; }
    public string? Category { get; set; }
    public string? Search { get; set; }
    public bool IncludeReportedReviews { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class ComplaintSummaryDto
{
    public Guid ComplaintId { get; set; }
    public string ComplaintCode { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string HandoverStatus { get; set; } = string.Empty;
    public Guid? ReceiptId { get; set; }
    public string? ReceiptCode { get; set; }
    public Guid ComplainantId { get; set; }
    public string ComplainantName { get; set; } = string.Empty;
    public string ComplainantRole { get; set; } = string.Empty;
    public Guid RespondentId { get; set; }
    public string RespondentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> EvidencePhotoUrls { get; set; } = new();
    public string Status { get; set; } = "Open";
    public Guid? AssignedStaffId { get; set; }
    public string? ResolutionOutcome { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool PointsRevoked { get; set; }
    public bool WarningLogged { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ComplaintDetailDto : ComplaintSummaryDto
{
}

public class StaffComplaintQueueResponseDto
{
    public PagedResult<ComplaintSummaryDto> Complaints { get; set; } = new();
    public int OpenCount { get; set; }
    public int InvestigatingCount { get; set; }
    public int ResolvedCount { get; set; }
    public int DismissedCount { get; set; }
    public int ReportedReviewsCount { get; set; }
    public List<TransactionReviewDto> ReportedReviews { get; set; } = new();
}

public class DossierPartyInfoDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int WarningCount { get; set; }
}

public class DossierHandoverItemDto
{
    public Guid ItemId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal EstimatedWeightKg { get; set; }
    public decimal? ActualWeightKg { get; set; }
    public decimal? AgreedPricePerKg { get; set; }
}

public class DossierReceiptSnapshotDto
{
    public Guid ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public decimal TotalWeightKg { get; set; }
    public decimal TotalAmountCash { get; set; }
    public long PointsAwarded { get; set; }
    public List<string> ScaleProofPhotoUrls { get; set; } = new();
    public string? ReceiptImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedByUserAt { get; set; }
}

public class DossierChatMessageDto
{
    public Guid MessageId { get; set; }
    public Guid SenderUserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? AttachmentImageUrl { get; set; }
    public decimal? ProposedPricePerKg { get; set; }
    public decimal? ProposedWeightKg { get; set; }
    public string? ProposalStatus { get; set; }
    public DateTime SentAt { get; set; }
}

public class StaffComplaintDossierDto
{
    public ComplaintSummaryDto Complaint { get; set; } = new();
    public DossierPartyInfoDto Complainant { get; set; } = new();
    public DossierPartyInfoDto Respondent { get; set; } = new();
    public string HandoverRequestCode { get; set; } = string.Empty;
    public string HandoverStatus { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string? PreferredTimeSlot { get; set; }
    public List<string> WastePhotoUrls { get; set; } = new();
    public List<DossierHandoverItemDto> Items { get; set; } = new();
    public DossierReceiptSnapshotDto? Receipt { get; set; }
    public List<DossierChatMessageDto> NegotiationMessages { get; set; } = new();
}

public class ResolveComplaintRequest
{
    /// <summary>ConfirmReceiptAsIs | CompensateUser | UpholdComplainant | CancelDisputedOrder | Dismissed</summary>
    public string ResolutionOutcome { get; set; } = string.Empty;
    public string ResolutionNote { get; set; } = string.Empty;
    public bool IssueWarningToRespondent { get; set; }
    public string? WarningReason { get; set; }
}
