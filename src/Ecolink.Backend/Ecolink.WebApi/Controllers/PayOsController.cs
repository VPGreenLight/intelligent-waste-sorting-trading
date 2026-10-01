using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

[ApiController]
[Route("api/v1/payos")]
[Produces("application/json")]
public class PayOsController : ControllerBase
{
    private readonly IPayOsService _payOsService;
    private readonly ILogger<PayOsController> _logger;

    public PayOsController(IPayOsService payOsService, ILogger<PayOsController> logger)
    {
        _payOsService = payOsService;
        _logger = logger;
    }

    /// <summary>
    /// Tạo liên kết thanh toán VietQR Pay-in qua cổng payOS
    /// </summary>
    /// <param name="request">Thông tin số tiền, mô tả giao dịch và người thanh toán</param>
    [HttpPost("create-payment-link")]
    [ProducesResponseType(typeof(PayOsPaymentResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePaymentLink([FromBody] CreatePayOsPaymentRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Số tiền thanh toán phải lớn hơn 0 VNĐ." });
        }

        var result = await _payOsService.CreatePaymentLinkAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Tra cứu trạng thái đơn thanh toán payOS theo mã orderCode
    /// </summary>
    /// <param name="orderCode">Mã đơn hàng payOS (Số nguyên dài)</param>
    [HttpGet("orders/{orderCode}")]
    [ProducesResponseType(typeof(PayOsPaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(long orderCode, CancellationToken ct)
    {
        var order = await _payOsService.GetPaymentOrderAsync(orderCode, ct);
        if (order == null)
        {
            return NotFound(new { message = $"Không tìm thấy đơn hàng payOS với mã {orderCode}." });
        }
        return Ok(order);
    }

    /// <summary>
    /// Chi hộ (Pay-out): Chuyển tiền tự động Napas 24/7 tức thì đến tài khoản ngân hàng của Người dân bán rác
    /// </summary>
    /// <param name="request">Thông tin ngân hàng, số tài khoản người nhận và số tiền cần chi trả</param>
    [HttpPost("payout")]
    [ProducesResponseType(typeof(PayOsPayoutResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExecutePayout([FromBody] PayOsPayoutRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Số tiền chi hộ phải lớn hơn 0 VNĐ." });
        }

        if (string.IsNullOrWhiteSpace(request.ToAccountNumber) || string.IsNullOrWhiteSpace(request.ToBankCode))
        {
            return BadRequest(new { message = "Vui lòng cung cấp mã ngân hàng (ToBankCode) và số tài khoản người nhận (ToAccountNumber)." });
        }

        var result = await _payOsService.ExecutePayoutAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Webhook tiếp nhận thông báo thanh toán thành công tự động từ cổng payOS
    /// </summary>
    /// <remarks>
    /// Cấu hình Webhook URL trên payOS dashboard: https://subarctic-sneer-debug.ngrok-free.dev/api/v1/payos/webhook
    /// </remarks>
    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook([FromBody] JsonElement rawBody, CancellationToken ct)
    {
        string json = rawBody.ToString();
        _logger.LogInformation("==> [payOS Webhook Received]: {Json}", json);

        bool success = await _payOsService.ProcessWebhookAsync(json, ct);
        if (success)
        {
            return Ok(new { error = 0, message = "Webhook processed successfully" });
        }

        return Ok(new { error = 0, message = "Webhook received" });
    }

    /// <summary>
    /// Xác thực và đăng ký Webhook URL với máy chủ payOS
    /// </summary>
    [HttpPost("confirm-webhook")]
    public async Task<IActionResult> ConfirmWebhook([FromQuery] string webhookUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return BadRequest(new { message = "Vui lòng truyền URL webhook cần xác nhận." });
        }

        string result = await _payOsService.ConfirmWebhookAsync(webhookUrl, ct);
        return Ok(new
        {
            success = true,
            webhookUrl = result,
            message = "Đã gửi yêu cầu xác nhận Webhook URL đến máy chủ payOS."
        });
    }
}
