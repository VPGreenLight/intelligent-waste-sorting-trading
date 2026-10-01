namespace Ecolink.Application.Dtos;

public class CreatePayOsPaymentRequest
{
    /// <summary>
    /// Số tiền cần thanh toán (VNĐ)
    /// </summary>
    public int Amount { get; set; } = 20000;

    /// <summary>
    /// Mô tả giao dịch (Tối đa 25 ký tự không dấu hoặc ngắn gọn theo chuẩn payOS)
    /// </summary>
    public string Description { get; set; } = "EcoLink thanh toan rac";

    /// <summary>
    /// Tên người thanh toán (tùy chọn)
    /// </summary>
    public string? BuyerName { get; set; }

    /// <summary>
    /// Số điện thoại người thanh toán (tùy chọn)
    /// </summary>
    public string? BuyerPhone { get; set; }

    /// <summary>
    /// Email người thanh toán (tùy chọn)
    /// </summary>
    public string? BuyerEmail { get; set; }
}

public class PayOsPaymentResponseDto
{
    public bool Success { get; set; }
    public long OrderCode { get; set; }
    public int Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public string Message { get; set; } = string.Empty;
}

public class PayOsPayoutRequest
{
    /// <summary>
    /// Mã ngân hàng nhận tiền (VD: MB, TPB, VCB, TCB, VPB, ICB...)
    /// </summary>
    public string ToBankCode { get; set; } = "TPB";

    /// <summary>
    /// Số tài khoản người nhận
    /// </summary>
    public string ToAccountNumber { get; set; } = "05773216801";

    /// <summary>
    /// Tên chủ tài khoản người nhận
    /// </summary>
    public string ToAccountName { get; set; } = "NGUYEN HOANG THANG";

    /// <summary>
    /// Số tiền chi trả (VNĐ)
    /// </summary>
    public int Amount { get; set; } = 20000;

    /// <summary>
    /// Lý do chi trả (Ví dụ: "EcoLink chi tra tien phe lieu")
    /// </summary>
    public string Description { get; set; } = "EcoLink tra tien phe lieu";

    /// <summary>
    /// ID người nhận trong hệ thống (tùy chọn)
    /// </summary>
    public Guid? RecipientUserId { get; set; }
}

public class PayOsPayoutResponseDto
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string ToBankCode { get; set; } = string.Empty;
    public string ToAccountNumber { get; set; } = string.Empty;
    public string ToAccountName { get; set; } = string.Empty;
    public string Status { get; set; } = "SUCCESS";
    public DateTime TransactedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
