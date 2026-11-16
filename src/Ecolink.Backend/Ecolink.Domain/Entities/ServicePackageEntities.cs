using Ecolink.Domain.Common;
using Ecolink.Domain.Enums;

namespace Ecolink.Domain.Entities;

/// <summary>
/// Định nghĩa 3 gói dịch vụ chuẩn khởi tạo hệ thống (BASIC - PREMIUM - PRO).
/// </summary>
public static class CanonicalServicePackages
{
    public static readonly Guid BasicId = Guid.Parse("51000001-0000-0000-0000-000000000001");
    public static readonly Guid PremiumId = Guid.Parse("51000001-0000-0000-0000-000000000002");
    public static readonly Guid ProId = Guid.Parse("51000002-0000-0000-0000-000000000001");

    public const string BasicCode = "BASIC";
    public const string PremiumCode = "PREMIUM";
    public const string ProCode = "PRO";

    public const string BasicFeaturesJson =
        """{"tierName":"Basic","adFree":false,"dailyPointMultiplier":1.0,"ecoPlusBadge":false,"verifiedBlueBadge":false,"featuredMapMarker":false,"mapPriorityBoost":0}""";

    public const string PremiumFeaturesJson =
        """{"tierName":"Premium","adFree":true,"dailyPointMultiplier":1.2,"ecoPlusBadge":true,"verifiedBlueBadge":false,"featuredMapMarker":false,"mapPriorityBoost":0}""";

    public const string ProFeaturesJson =
        """{"tierName":"Pro","adFree":true,"dailyPointMultiplier":1.2,"ecoPlusBadge":true,"verifiedBlueBadge":true,"featuredMapMarker":true,"mapPriorityBoost":100}""";

    public static readonly DateTime SeedTimestampUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}

/// <summary>
/// Danh mục gói dịch vụ nội bộ nền tảng EcoLink (UC A-10, U-16, R-07).
/// </summary>
public class ServicePackage : BaseAuditableEntity
{
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public ServiceTargetRole TargetRole { get; set; } = ServiceTargetRole.All;
    public string? Description { get; set; }
    public bool IsDefaultFree { get; set; } = false;
    public bool RequiresVerifiedKyb { get; set; } = false;
    public decimal PriceVnd { get; set; }
    public int DurationDays { get; set; }
    public string FeaturesJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 100;

    public ICollection<ServiceOrder> Orders { get; set; } = new List<ServiceOrder>();
    public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
}

/// <summary>
/// Đơn thanh toán gói dịch vụ qua VietQR động / PayOS (UC U-16, R-07, A-11 - BR-PAY-01, BR-PAY-05).
/// </summary>
public class ServiceOrder : BaseAuditableEntity
{
    public string OrderCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid PackageId { get; set; }
    public ServicePackage? Package { get; set; }

    // Bản chụp giá & đặc quyền tại thời điểm tạo đơn (Grandfathering BR-PAY-05)
    public string PackageCodeSnapshot { get; set; } = string.Empty;
    public string PackageNameSnapshot { get; set; } = string.Empty;
    public int DurationDaysSnapshot { get; set; }
    public string FeaturesSnapshot { get; set; } = "{}";

    public decimal ExpectedAmountVnd { get; set; }
    public decimal? PaidAmountVnd { get; set; }
    public string PaymentMethod { get; set; } = "VIETQR";
    public string Description { get; set; } = string.Empty;
    public string? QrPayload { get; set; }
    public string QrCodeUrl { get; set; } = string.Empty;
    public string? CheckoutUrl { get; set; }
    public string TransferContent { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;

    public long? GatewayOrderCode { get; set; }
    public string? PaymentReference { get; set; }
    public string? GatewayTransactionNo { get; set; }

    public ServiceOrderStatus OrderStatus { get; set; } = ServiceOrderStatus.Pending;
    public ServicePaymentStatus PaymentStatus { get; set; } = ServicePaymentStatus.Pending;
    public ReconciliationStatus ReconciliationStatus { get; set; } = ReconciliationStatus.Pending;
    public PaymentAnomalyType AnomalyType { get; set; } = PaymentAnomalyType.None;
    public string? AnomalyNote { get; set; }

    public Guid? ResolvedBy { get; set; }
    public User? ResolverUser { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public string? ResolutionBankRef { get; set; }

    public DateTime QrExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }

    public UserSubscription? Subscription { get; set; }
    public ICollection<PaymentWebhookLog> WebhookLogs { get; set; } = new List<PaymentWebhookLog>();
}

/// <summary>
/// Thuê bao đặc quyền đang hiệu lực của Người dân hoặc Cơ sở thu mua (BR-PAY-04).
/// </summary>
public class UserSubscription : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid PackageId { get; set; }
    public ServicePackage? Package { get; set; }
    public Guid SourceOrderId { get; set; }
    public ServiceOrder? SourceOrder { get; set; }

    public string PackageCode { get; set; } = string.Empty;
    public string FeaturesSnapshot { get; set; } = "{}";
    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }

    /// <summary>Kiểm tra thuê bao còn hiệu lực tại thời điểm UTC hiện hành.</summary>
    public bool IsCurrentlyActive(DateTime utcNow) =>
        !IsDeleted && Status == SubscriptionStatus.Active && StartsAt <= utcNow && ExpiresAt > utcNow;
}

/// <summary>
/// Nhật ký callback ngân hàng & webhook thô phục vụ chống xử lý trùng (BR-PAY-02) và đối soát (UC A-11).
/// Không xóa mềm hay xóa vật lý; dữ liệu ngân hàng gốc là bất biến.
/// </summary>
public class PaymentWebhookLog : BaseEntity
{
    public string WebhookCode { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string GatewaySource { get; set; } = "VIETQR";
    public Guid? MatchedOrderId { get; set; }
    public ServiceOrder? MatchedOrder { get; set; }
    public string? ParsedOrderCode { get; set; }
    public decimal ReceivedAmountVnd { get; set; }
    public string? BankReference { get; set; }
    public string? TransferDescription { get; set; }
    public bool SignatureValid { get; set; } = false;
    public string RawPayload { get; set; } = "{}";
    public string ProcessingResult { get; set; } = string.Empty;
    public PaymentAnomalyType AnomalyType { get; set; } = PaymentAnomalyType.None;

    public Guid? ResolvedBy { get; set; }
    public User? ResolverUser { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public string? ResolutionBankRef { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
