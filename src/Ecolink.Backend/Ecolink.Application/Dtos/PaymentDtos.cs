using Ecolink.Domain.Enums;

namespace Ecolink.Application.Dtos;

public enum ServicePackageType
{
    VerifiedBadge, // Gói Tích xanh xác minh cơ sở uy tín (Pro)
    NoAds,         // Gói Tắt quảng cáo (Premium)
    MapBoost       // Gói Đẩy vị trí hiển thị ưu tiên trên bản đồ (Pro)
}

/// <summary>
/// Đặc quyền kỹ thuật mở khóa theo gói dịch vụ (ánh xạ từ cột jsonb features_json / features_snapshot).
/// </summary>
public class ServicePackageFeaturesDto
{
    public string TierName { get; set; } = "Basic";
    public bool AdFree { get; set; }
    public decimal DailyPointMultiplier { get; set; } = 1.0m;
    public bool EcoPlusBadge { get; set; }
    public bool VerifiedBlueBadge { get; set; }
    public bool FeaturedMapMarker { get; set; }
    public int MapPriorityBoost { get; set; }
}

/// <summary>
/// Báo cáo thẩm định 4 tiêu chí đăng ký / đăng ký lại gói Tích xanh PRO cho Cơ sở thu mua (R-07).
/// </summary>
public class ProEligibilityReportDto
{
    public bool IsEligible { get; set; }
    public bool IsReRegistration { get; set; }
    public bool IsKybAndProfileValid { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public bool RequiresProfileReverification { get; set; }
    public bool IsWarningLimitOk { get; set; }
    public int CurrentWarningCount { get; set; }
    public int MaxAllowedWarnings { get; set; } = 5;
    public bool IsPositiveFeedbackMet { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public decimal MinRequiredRating { get; set; } = 4.0m;
    public int MinRequiredReviews { get; set; } = 50;
    public int MinRequiredReviewsForRenewal { get; set; } = 50;
    public bool IsNotCurrentlyActivePro { get; set; }
    public DateTime? ActiveProExpiresAt { get; set; }
    public List<string> BlockingReasons { get; set; } = new();
}

/// <summary>
/// Thông tin một gói dịch vụ hiển thị trên cửa hàng Citizen / Recycler (STT 120).
/// </summary>
public class ServicePackageCatalogDto
{
    public Guid Id { get; set; }
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string TargetRole { get; set; } = "All";
    public string? Description { get; set; }
    public bool IsDefaultFree { get; set; }
    public bool RequiresVerifiedKyb { get; set; }
    public decimal PriceVnd { get; set; }
    public int DurationDays { get; set; }
    public int DisplayOrder { get; set; }
    public ServicePackageFeaturesDto Features { get; set; } = new();
    public bool IsActive { get; set; }
    public bool IsEligible { get; set; } = true;
    public string? IneligibleReason { get; set; }
    public bool IsCurrentPlan { get; set; }
    public bool IsIncludedInHigherPlan { get; set; }
    public bool CanRenew { get; set; }
    public DateTime? CurrentActiveExpiresAt { get; set; }
    public ProEligibilityReportDto? ProEligibility { get; set; }
}

/// <summary>
/// DTO quản trị danh mục & biểu phí gói dịch vụ kèm thống kê doanh thu (STT 123 - UC A-10).
/// </summary>
public class AdminServicePackageDto : ServicePackageCatalogDto
{
    public int ActiveSubscribersCount { get; set; }
    public int TotalPaidOrdersCount { get; set; }
    public decimal TotalRevenueVnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class UpsertServicePackageRequest
{
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string TargetRole { get; set; } = "All";
    public string? Description { get; set; }
    public decimal PriceVnd { get; set; }
    public int DurationDays { get; set; }
    public bool RequiresVerifiedKyb { get; set; }
    public int DisplayOrder { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public ServicePackageFeaturesDto Features { get; set; } = new();
}

public class ToggleServicePackageStatusRequest
{
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Tổng hợp quyền lợi hiệu lực hiện tại của tài khoản (dùng cho UI, Auth & GeoMatchingService).
/// </summary>
public class UserEntitlementSummaryDto
{
    public string CurrentTier { get; set; } = "Basic";
    public bool IsAdFree { get; set; }
    public decimal DailyPointMultiplier { get; set; } = 1.0m;
    public bool HasEcoPlusBadge { get; set; }
    public bool HasVerifiedBlueBadge { get; set; }
    public bool IsFeaturedMapMarker { get; set; }
    public int MapPriorityBoost { get; set; }
}

public class ActiveSubscriptionDto
{
    public Guid SubscriptionId { get; set; }
    public Guid PackageId { get; set; }
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string SourceOrderCode { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int RemainingDays { get; set; }
    public string Status { get; set; } = "Active";
    public ServicePackageFeaturesDto Features { get; set; } = new();
}

public class MySubscriptionsResponseDto
{
    public string CurrentTier { get; set; } = "Basic";
    public UserEntitlementSummaryDto Entitlements { get; set; } = new();
    public IReadOnlyList<ActiveSubscriptionDto> ActiveSubscriptions { get; set; } = Array.Empty<ActiveSubscriptionDto>();
    public IReadOnlyList<ActiveSubscriptionDto> PastSubscriptions { get; set; } = Array.Empty<ActiveSubscriptionDto>();
}

public class CreatePayInRequest
{
    public Guid? PackageId { get; set; }

    /// <summary>
    /// Mã gói dịch vụ nội bộ (PREMIUM, PRO, hoặc gói mở rộng; chặn tạo đơn QR cho gói BASIC miễn phí).
    /// </summary>
    public string PackageCode { get; set; } = "PREMIUM";

    /// <summary>
    /// Số tiền thanh toán tùy chọn (Server luôn ép/đối chiếu theo giá gốc trong service_packages theo BR-PAY-01).
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Mô tả đơn thanh toán.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Trường tương thích cũ (khi đã xác thực JWT, Server luôn lấy UserId từ ICurrentUserService).
    /// </summary>
    public Guid? UserId { get; set; }
}

public class CancelPendingOrderRequest
{
    public string? Reason { get; set; }
}

public class PayInOrderDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PackageId { get; set; }
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? PaidAmount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string TransferContent { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public string? QrPayload { get; set; }
    public string? CheckoutUrl { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, PAID, CANCELLED, EXPIRED, REFUNDED
    public string ReconciliationStatus { get; set; } = "Pending";
    public string AnomalyType { get; set; } = "None";
    public string? AnomalyNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? TransactionReference { get; set; }
    public ActiveSubscriptionDto? ActivatedSubscription { get; set; }
}

public class PayOsWebhookInnerDataDto
{
    public long? OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public string? TransactionDateTime { get; set; }
    public string? AccountNumber { get; set; }
}

public class VietQrWebhookPayload
{
    public string? OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public string? TransactionDateTime { get; set; }
    public string? Signature { get; set; }

    // Hỗ trợ chuẩn PayOS v2 webhook envelope
    public string? Code { get; set; }
    public string? Desc { get; set; }
    public bool? Success { get; set; }
    public PayOsWebhookInnerDataDto? Data { get; set; }
}

public class SimulateTransferRequest
{
    /// <summary>Exact | Underpaid | Overpaid | LateTransfer | Unmatched</summary>
    public string Scenario { get; set; } = "Exact";
    public decimal? SimulatedAmount { get; set; }
    public string? SimulatedDescription { get; set; }
}

public class WebhookResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? OrderCode { get; set; }
    public string? WebhookCode { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReconciliationStatus { get; set; }
}

// ============================================================================
// ADMIN FINANCIAL RECONCILIATION DTOs (UC A-11 - STT 127, 128, 129)
// ============================================================================

public class ReconciliationLedgerQuery
{
    public string? ReconciliationStatus { get; set; }
    public string? AnomalyType { get; set; }
    public bool OnlyAnomalies { get; set; }
    public string? Search { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ReconciliationSummaryDto
{
    public decimal TotalMatchedRevenueVnd { get; set; }
    public int PendingOrdersCount { get; set; }
    public int OpenAnomaliesCount { get; set; }
    public decimal TotalDiscrepancyVnd { get; set; }
    public decimal TotalRefundedVnd { get; set; }
}

public class ReconciliationLedgerItemDto
{
    public string RecordType { get; set; } = "Order"; // Order | OrphanWebhook
    public string ReferenceCode { get; set; } = string.Empty; // ECO... hoặc WH-...
    public Guid? UserId { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public string? PayerEmail { get; set; }
    public string? PayerPhone { get; set; }
    public string? PayerRole { get; set; }
    public string? PackageCode { get; set; }
    public string? PackageName { get; set; }
    public decimal ExpectedAmountVnd { get; set; }
    public decimal PaidAmountVnd { get; set; }
    public decimal DiscrepancyVnd { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string ReconciliationStatus { get; set; } = string.Empty;
    public string AnomalyType { get; set; } = "None";
    public string? AnomalyNote { get; set; }
    public string? RawWebhookJson { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public string? ResolutionBankRef { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class ReconciliationLedgerResponseDto
{
    public ReconciliationSummaryDto Summary { get; set; } = new();
    public PagedResult<ReconciliationLedgerItemDto> Items { get; set; } = new();
}

public class ResolvePaymentAnomalyRequest
{
    public string Action { get; set; } = nameof(ReconciliationAction.ApproveAndActivate);
    public string? TargetOrderCode { get; set; }
    public string? ResolutionBankRef { get; set; }
    public string ResolutionNote { get; set; } = string.Empty;
}

public class ReconciliationExportQuery
{
    /// <summary>csv | xlsx</summary>
    public string Format { get; set; } = "csv";
    public string? ReconciliationStatus { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}
