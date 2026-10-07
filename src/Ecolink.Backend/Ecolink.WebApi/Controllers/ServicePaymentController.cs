using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Quản lý thanh toán các gói dịch vụ nội bộ EcoLink qua VietQR động (UC-PAY-01, BR-PAY-01, BR-PAY-02)
/// Các gói hỗ trợ: Huy hiệu Tích xanh xác minh cơ sở uy tín, Gói tắt quảng cáo, Gói ưu tiên hiển thị trên bản đồ.
/// </summary>
[ApiController]
[Route("api/v1/service-payments")]
[Produces("application/json")]
public class ServicePaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<ServicePaymentController> _logger;

    public ServicePaymentController(IPaymentService paymentService, ILogger<ServicePaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Đăng ký mua gói dịch vụ nội bộ và sinh mã VietQR thanh toán (BR-PAY-01)
    /// </summary>
    [HttpPost("create")]
    [ProducesResponseType(typeof(ApiResponse<PayInOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateServicePayment([FromBody] CreatePayInRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Số tiền thanh toán phải lớn hơn 0 VNĐ.", "MSG-PAY-03"));
        }

        var order = await _paymentService.CreatePayInOrderAsync(request, ct);

        return Ok(ApiResponse<PayInOrderDto>.Ok(
            data: order,
            message: $"Tạo đơn thanh toán gói dịch vụ thành công. Quét mã QR tại link '{order.QrCodeUrl}' bằng bất kỳ app ngân hàng nào (MBBank, Vietcombank, Momo, BIDV...).",
            messageCode: "MSG-PAY-01"
        ));
    }

    /// <summary>
    /// Tra cứu trạng thái đơn thanh toán dịch vụ theo mã đơn hàng
    /// </summary>
    [HttpGet("orders/{orderCode}")]
    [ProducesResponseType(typeof(ApiResponse<PayInOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderStatus(string orderCode, CancellationToken ct)
    {
        var order = await _paymentService.GetOrderAsync(orderCode, ct);
        if (order == null)
        {
            return NotFound(ApiResponse<object>.Fail($"Không tìm thấy đơn hàng '{orderCode}'.", "MSG-PAY-04"));
        }

        return Ok(ApiResponse<PayInOrderDto>.Ok(order));
    }

    /// <summary>
    /// Danh sách toàn bộ lịch sử đơn thanh toán gói dịch vụ
    /// </summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PayInOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllOrders(CancellationToken ct)
    {
        var orders = await _paymentService.GetAllOrdersAsync(ct);
        return Ok(ApiResponse<IEnumerable<PayInOrderDto>>.Ok(orders));
    }

    /// <summary>
    /// Webhook tiếp nhận biến động số dư và thông báo thanh toán từ VietQR / Ngân hàng (BR-PAY-02)
    /// </summary>
    [HttpPost("webhook")]
    [ProducesResponseType(typeof(ApiResponse<WebhookResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<WebhookResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceivePaymentWebhook([FromBody] VietQrWebhookPayload payload, CancellationToken ct)
    {
        var result = await _paymentService.ProcessWebhookAsync(payload, ct);
        if (!result.Success)
        {
            return BadRequest(ApiResponse<WebhookResponseDto>.Fail(result.Message, "MSG-PAY-05"));
        }

        return Ok(ApiResponse<WebhookResponseDto>.Ok(result, "Xác minh thanh toán và kích hoạt gói dịch vụ thành công.", "MSG-PAY-02"));
    }

    /// <summary>
    /// [MOCK TEST] Giả lập chuyển khoản thành công để kiểm thử luồng Webhook mà không cần chuyển tiền thật
    /// </summary>
    [HttpPost("simulate-transfer/{orderCode}")]
    [ProducesResponseType(typeof(ApiResponse<PayInOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SimulateTransfer(string orderCode, CancellationToken ct)
    {
        try
        {
            var updatedOrder = await _paymentService.SimulateTransferSuccessAsync(orderCode, ct);
            return Ok(ApiResponse<PayInOrderDto>.Ok(
                data: updatedOrder,
                message: $"Đã giả lập thanh toán thành công cho đơn {orderCode}! Gói dịch vụ đã được kích hoạt.",
                messageCode: "MSG-PAY-02"
            ));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, "MSG-PAY-04"));
        }
    }
}
