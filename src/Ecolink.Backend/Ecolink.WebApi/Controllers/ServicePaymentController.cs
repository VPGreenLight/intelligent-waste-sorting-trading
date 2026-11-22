using System.Text.Json;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Quản lý thanh toán các gói dịch vụ nội bộ EcoLink qua VietQR động / PayOS (M-12 / FE-14: U-16, R-07).
/// Bao phủ STT 115, 116, 117, 118, 119, 122 trong 00_api_inventory_and_endpoint_statistics.md.
/// </summary>
[ApiController]
[Route("api/v1/service-payments")]
[Authorize]
[Produces("application/json")]
public class ServicePaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ServicePaymentController> _logger;

    private static readonly JsonSerializerOptions JsonCaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ServicePaymentController(
        IPaymentService paymentService,
        ICurrentUserService currentUserService,
        ILogger<ServicePaymentController> logger)
    {
        _paymentService = paymentService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// [STT 115] Khởi tạo đơn mua gói dịch vụ nội bộ (PREMIUM / PRO) và sinh mã VietQR động (BR-PAY-01, BR-PAY-03).
    /// </summary>
    [HttpPost("create")]
    [Authorize(Roles = "User,Citizen,Recycler,Collector,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PayInOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateServicePayment([FromBody] CreatePayInRequest request, CancellationToken ct)
    {
        if (request.Amount.HasValue && request.Amount.Value <= 0m)
        {
            return BadRequest(ApiResponse<object>.Fail("Số tiền thanh toán phải lớn hơn 0 VNĐ.", "MSG-PAY-03"));
        }

        var (userId, role) = ResolveCallerIdentity();
        var order = await _paymentService.CreatePayInOrderAsync(userId, role, request, ct);

        return Ok(ApiResponse<PayInOrderDto>.Ok(
            data: order,
            message: "Quét mã VietQR này để hoàn tất thanh toán dịch vụ EcoLink.",
            messageCode: "MSG-PAY-01"));
    }

    /// <summary>
    /// [STT 116] Tra cứu trạng thái đơn thanh toán dịch vụ thời gian thực theo mã đơn hàng.
    /// </summary>
    [HttpGet("orders/{orderCode}")]
    [ProducesResponseType(typeof(ApiResponse<PayInOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderStatus(string orderCode, CancellationToken ct)
    {
        var (userId, role) = ResolveCallerIdentity();
        var isStaffOrAdmin = role is UserRole.Admin or UserRole.Staff;
        var order = await _paymentService.GetOrderForUserAsync(userId, isStaffOrAdmin, orderCode, ct);

        return Ok(ApiResponse<PayInOrderDto>.Ok(order, "Lấy trạng thái đơn thanh toán thành công.", "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 117] Danh sách hóa đơn thanh toán gói dịch vụ của chính tài khoản đang đăng nhập.
    /// </summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PayInOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllOrders(
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var (userId, _) = ResolveCallerIdentity();
        var paged = await _paymentService.GetUserOrdersAsync(userId, status, pageNumber, pageSize, ct);
        return Ok(ApiResponse<PagedResult<PayInOrderDto>>.Ok(paged, "Tải lịch sử hóa đơn dịch vụ thành công.", "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 122] Hủy đơn thanh toán VietQR đang chờ chuyển khoản (Pending).
    /// </summary>
    [HttpPost("orders/{orderCode}/cancel")]
    [Authorize(Roles = "User,Citizen,Recycler,Collector,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PayInOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelPendingOrder(string orderCode, CancellationToken ct)
    {
        var cancelReq = await TryReadOptionalBodyAsync<CancelPendingOrderRequest>(ct);
        var (userId, _) = ResolveCallerIdentity();
        var updated = await _paymentService.CancelPendingOrderAsync(userId, orderCode, cancelReq?.Reason, ct);

        return Ok(ApiResponse<PayInOrderDto>.Ok(
            data: updated,
            message: "Đã hủy đơn thanh toán đang chờ. Bạn có thể tạo mã thanh toán mới bất kỳ lúc nào.",
            messageCode: "MSG-PAY-03"));
    }

    /// <summary>
    /// [STT 118] Webhook tiếp nhận biến động số dư và thông báo thanh toán từ VietQR / PayOS (BR-PAY-02).
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<WebhookResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<WebhookResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceivePaymentWebhook([FromBody] VietQrWebhookPayload payload, CancellationToken ct)
    {
        var headerSecret = Request.Headers["X-VietQr-Webhook-Secret"].FirstOrDefault();
        var result = await _paymentService.ProcessWebhookAsync(payload, headerSecret, ct);
        if (!result.Success)
        {
            return BadRequest(ApiResponse<WebhookResponseDto>.Fail(result.Message, "MSG-PAY-05"));
        }

        return Ok(ApiResponse<WebhookResponseDto>.Ok(
            data: result,
            message: string.IsNullOrWhiteSpace(result.Message)
                ? "Thanh toán dịch vụ của bạn đã được xác nhận thành công."
                : result.Message,
            messageCode: "MSG-PAY-02"));
    }

    /// <summary>
    /// [STT 119] [DEV/DEMO] Giả lập chuyển khoản VietQR (Exact / Underpaid / Overpaid / LateTransfer / Unmatched) qua đúng pipeline Webhook.
    /// </summary>
    [HttpPost("simulate-transfer/{orderCode}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PayInOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SimulateTransfer(string orderCode, CancellationToken ct)
    {
        var simReq = await TryReadOptionalBodyAsync<SimulateTransferRequest>(ct);
        var updatedOrder = await _paymentService.SimulateTransferAsync(orderCode, simReq, ct);

        return Ok(ApiResponse<PayInOrderDto>.Ok(
            data: updatedOrder,
            message: $"Đã giả lập chuyển khoản cho đơn {orderCode} (Trạng thái đối soát: {updatedOrder.ReconciliationStatus}).",
            messageCode: "MSG-PAY-02"));
    }

    private (Guid UserId, UserRole Role) ResolveCallerIdentity()
    {
        var userId = _currentUserService.UserId
                     ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");

        var roleStr = _currentUserService.Role ?? nameof(UserRole.User);
        if (!Enum.TryParse<UserRole>(roleStr, true, out var role))
        {
            role = UserRole.User;
        }

        return (userId, role);
    }

    private async Task<T?> TryReadOptionalBodyAsync<T>(CancellationToken ct) where T : class
    {
        if (!Request.HasJsonContentType() || Request.ContentLength is <= 0)
        {
            return null;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<T>(Request.Body, JsonCaseInsensitive, ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
