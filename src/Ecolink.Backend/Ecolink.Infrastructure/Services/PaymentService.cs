using System.Collections.Concurrent;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly ILogger<PaymentService> _logger;
    private readonly IConfiguration _config;

    // Lưu trữ đơn hàng nạp tiền in-memory phục vụ demo & test webhook
    private readonly ConcurrentDictionary<string, PayInOrderDto> _orders = new(StringComparer.OrdinalIgnoreCase);

    public PaymentService(ILogger<PaymentService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public Task<PayInOrderDto> CreatePayInOrderAsync(CreatePayInRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Lấy thông tin tài khoản nhận tiền của hệ thống từ appsettings.json
        string bankCode = _config["VietQr:BankCode"] ?? "TPB";
        if (bankCode == "TP") bankCode = "TPB";

        string bankName = _config["VietQr:BankName"] ?? "TPBank";
        string accountNo = _config["VietQr:AccountNo"] ?? "05773216801";
        string accountName = _config["VietQr:AccountName"] ?? "NGUYEN HOANG THANG";

        // 2. Sinh mã đơn hàng duy nhất (VD: ECO281605 hoặc ECO + 6 số ngẫu nhiên)
        string orderCode = $"ECO{DateTime.UtcNow:MMdd}{Random.Shared.Next(1000, 9999)}";

        // Nội dung chuyển khoản bắt buộc khách hàng điền để khớp lệnh
        string transferContent = orderCode;

        // 3. Sinh link ảnh VietQR động chuẩn EMVCo Napas 24/7 (sử dụng cổng VietQR quốc gia)
        // Cú pháp: https://img.vietqr.io/image/<BANK_ID>-<ACCOUNT_NO>-<TEMPLATE>.png?amount=<AMOUNT>&addInfo=<CONTENT>&accountName=<NAME>
        string qrCodeUrl = $"https://img.vietqr.io/image/{bankCode}-{accountNo}-compact2.png" +
                           $"?amount={(long)request.Amount}" +
                           $"&addInfo={Uri.EscapeDataString(transferContent)}" +
                           $"&accountName={Uri.EscapeDataString(accountName)}";

        var order = new PayInOrderDto
        {
            OrderCode = orderCode,
            Amount = request.Amount,
            Description = request.Description,
            TransferContent = transferContent,
            BankCode = bankCode,
            BankName = bankName,
            AccountNo = accountNo,
            AccountName = accountName,
            QrCodeUrl = qrCodeUrl,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15) // Hết hạn sau 15 phút
        };

        _orders[orderCode] = order;

        _logger.LogInformation("Đã khởi tạo đơn Pay-in VietQR: {OrderCode} - Số tiền: {Amount:N0} VNĐ", orderCode, request.Amount);

        return Task.FromResult(order);
    }

    public Task<PayInOrderDto?> GetOrderAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        _orders.TryGetValue(orderCode, out var order);
        return Task.FromResult<PayInOrderDto?>(order);
    }

    public Task<IEnumerable<PayInOrderDto>> GetAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<PayInOrderDto>>(_orders.Values.OrderByDescending(o => o.CreatedAt));
    }

    public Task<WebhookResponseDto> ProcessWebhookAsync(VietQrWebhookPayload payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("==> Tiếp nhận Webhook thanh toán: OrderCode=[{OrderCode}], Amount={Amount}, Desc=[{Desc}], Ref=[{Ref}]",
            payload.OrderCode, payload.Amount, payload.Description, payload.Reference);

        string? targetOrderCode = payload.OrderCode;

        // Nếu cổng không truyền thẳng orderCode mà nằm trong Description (nội dung SMS ngân hàng)
        if (string.IsNullOrWhiteSpace(targetOrderCode) && !string.IsNullOrWhiteSpace(payload.Description))
        {
            foreach (var key in _orders.Keys)
            {
                if (payload.Description.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    targetOrderCode = key;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(targetOrderCode) || !_orders.TryGetValue(targetOrderCode, out var order))
        {
            _logger.LogWarning("Không tìm thấy đơn hàng khớp với Webhook: OrderCode=[{OrderCode}]", targetOrderCode);
            return Task.FromResult(new WebhookResponseDto
            {
                Success = false,
                Message = $"Không tìm thấy đơn hàng tương ứng với mã '{targetOrderCode}'."
            });
        }

        if (order.Status == "PAID")
        {
            return Task.FromResult(new WebhookResponseDto
            {
                Success = true,
                OrderCode = order.OrderCode,
                Amount = order.Amount,
                Status = "ALREADY_PAID",
                Message = "Đơn hàng này đã được xác nhận thanh toán trước đó."
            });
        }

        // Cập nhật trạng thái thành công
        order.Status = "PAID";
        order.PaidAt = DateTime.UtcNow;
        order.TransactionReference = payload.Reference ?? $"REF_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        _logger.LogInformation("✅ Xác nhận thanh toán thành công cho đơn: {OrderCode} - {Amount:N0} VNĐ", order.OrderCode, order.Amount);

        return Task.FromResult(new WebhookResponseDto
        {
            Success = true,
            OrderCode = order.OrderCode,
            Amount = order.Amount,
            Status = "PAID",
            Message = "Giao dịch thanh toán Pay-in qua VietQR đã được xử lý và ghi nhận thành công!"
        });
    }

    public Task<PayInOrderDto> SimulateTransferSuccessAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        if (!_orders.TryGetValue(orderCode, out var order))
        {
            throw new KeyNotFoundException($"Không tìm thấy đơn hàng có mã '{orderCode}'.");
        }

        order.Status = "PAID";
        order.PaidAt = DateTime.UtcNow;
        order.TransactionReference = $"SIM_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        _logger.LogInformation("⚡ [SIMULATOR] Đã giả lập thanh toán thành công đơn: {OrderCode}", orderCode);

        return Task.FromResult(order);
    }
}
