namespace Ecolink.Application.Dtos;

public enum ServicePackageType
{
    VerifiedBadge, // Gói Tích xanh xác minh cơ sở uy tín
    NoAds,         // Gói Tắt quảng cáo
    MapBoost       // Gói Đẩy vị trí hiển thị ưu tiên trên bản đồ
}

public class CreatePayInRequest
{
    /// <summary>
    /// Mã gói dịch vụ nội bộ (VERIFIED_BADGE, NO_ADS, MAP_BOOST)
    /// </summary>
    public string PackageCode { get; set; } = "VERIFIED_BADGE";

    /// <summary>
    /// Số tiền cần thanh toán (VNĐ)
    /// </summary>
    public decimal Amount { get; set; } = 50000;

    /// <summary>
    /// Nội dung thanh toán
    /// </summary>
    public string Description { get; set; } = "Thanh toan goi Tich Xanh EcoLink";

    /// <summary>
    /// ID tài khoản User hoặc Recycler đăng ký mua gói
    /// </summary>
    public Guid? UserId { get; set; }
}

public class PayInOrderDto
{
    public string OrderCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string TransferContent { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING"; // PENDING, PAID, CANCELLED, EXPIRED
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? TransactionReference { get; set; }
}

public class VietQrWebhookPayload
{
    public string? OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public string? TransactionDateTime { get; set; }
    public string? Signature { get; set; }
}

public class WebhookResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}
