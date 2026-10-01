using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

[ApiController]
[Route("api/v1/payment")]
[Produces("application/json")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Tạo yêu cầu Pay-in và sinh mã VietQR động chuẩn Napas 247
    /// </summary>
    /// <param name="request">Thông tin số tiền, người nạp và tài khoản nhận</param>
    [HttpPost("payin/create")]
    [ProducesResponseType(typeof(PayInOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePayInOrder([FromBody] CreatePayInRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Số tiền thanh toán phải lớn hơn 0 VNĐ." });
        }

        var order = await _paymentService.CreatePayInOrderAsync(request, ct);
        return Ok(new
        {
            success = true,
            order,
            instruction = $"Quét mã QR tại link '{order.QrCodeUrl}' bằng bất kỳ app ngân hàng nào (MBBank, Vietcombank, Momo, BIDV...). " +
                          $"Nội dung chuyển khoản phải là '{order.TransferContent}'."
        });
    }

    /// <summary>
    /// Tra cứu trạng thái đơn thanh toán Pay-in (Dành cho Mobile/Web Poll kiểm tra khi khách quét mã)
    /// </summary>
    /// <param name="orderCode">Mã đơn hàng (Ví dụ: ECO09281234)</param>
    [HttpGet("orders/{orderCode}")]
    [ProducesResponseType(typeof(PayInOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderStatus(string orderCode, CancellationToken ct)
    {
        var order = await _paymentService.GetOrderAsync(orderCode, ct);
        if (order == null)
        {
            return NotFound(new { message = $"Không tìm thấy đơn hàng '{orderCode}'." });
        }
        return Ok(order);
    }

    /// <summary>
    /// Danh sách toàn bộ lịch sử đơn thanh toán Pay-in
    /// </summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetAllOrders(CancellationToken ct)
    {
        var orders = await _paymentService.GetAllOrdersAsync(ct);
        return Ok(orders);
    }

    /// <summary>
    /// Webhook tiếp nhận biến động số dư và thông báo thanh toán từ VietQR / payOS / Ngân hàng
    /// </summary>
    /// <remarks>
    /// URL Webhook công khai khi chạy ngrok: https://subarctic-sneer-debug.ngrok-free.dev/api/v1/payment/webhook
    /// </remarks>
    [HttpPost("webhook")]
    [ProducesResponseType(typeof(WebhookResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceivePaymentWebhook([FromBody] VietQrWebhookPayload payload, CancellationToken ct)
    {
        var result = await _paymentService.ProcessWebhookAsync(payload, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// [MOCK TEST] Giả lập chuyển khoản thành công để kiểm thử luồng Webhook tức thì mà không cần chuyển tiền thật
    /// </summary>
    /// <param name="orderCode">Mã đơn hàng cần giả lập thanh toán (Ví dụ: ECO09281234)</param>
    [HttpPost("simulate-transfer/{orderCode}")]
    public async Task<IActionResult> SimulateTransfer(string orderCode, CancellationToken ct)
    {
        try
        {
            var updatedOrder = await _paymentService.SimulateTransferSuccessAsync(orderCode, ct);
            return Ok(new
            {
                success = true,
                message = $"Đã giả lập thanh toán thành công cho đơn {orderCode}! Trạng thái hiện tại: {updatedOrder.Status}",
                order = updatedOrder
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
