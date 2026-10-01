namespace Ecolink.Application.Dtos;

public class CreatePayInRequest
{
    /// <summary>
    /// Số tiền cần nạp / thanh toán (VNĐ), ví dụ: 20000
    /// </summary>
    public decimal Amount { get; set; } = 20000;

    /// <summary>
    /// Nội dung thanh toán, ví dụ: "Thu gom phe lieu EcoLink"
    /// </summary>
    public string Description { get; set; } = "Thanh toan phe lieu EcoLink";

    /// <summary>
    /// ID người dùng hoặc đối tác vựa thu gom (tùy chọn)
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
    /// <summary>
    /// Mã đơn hàng hoặc mã giao dịch phía EcoLink
    /// </summary>
    public string? OrderCode { get; set; }

    /// <summary>
    /// Số tiền thực nhận qua ngân hàng
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Nội dung tin nhắn biến động số dư hoặc ghi chú
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Mã giao dịch tham chiếu từ phía ngân hàng (FT number)
    /// </summary>
    public string? Reference { get; set; }

    public string? TransactionDateTime { get; set; }

    /// <summary>
    /// Chữ ký bảo mật từ cổng thanh toán (HMAC-SHA256)
    /// </summary>
    public string? Signature { get; set; }
}

public class WebhookResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string? Status { get; set; }
}
