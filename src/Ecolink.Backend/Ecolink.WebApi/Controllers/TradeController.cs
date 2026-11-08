using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

[ApiController]
[Route("api/v1/trade")]
public class TradeController : ControllerBase
{
    private readonly IGeoMatchingService _geoMatchingService;
    private readonly ITradeTransactionService _tradeService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<TradeController> _logger;

    public TradeController(
        IGeoMatchingService geoMatchingService,
        ITradeTransactionService tradeService,
        ICurrentUserService currentUser,
        ILogger<TradeController> logger)
    {
        _geoMatchingService = geoMatchingService;
        _tradeService = tradeService;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Tìm điểm thu gom phế liệu gần nhất theo vị trí GPS của người dùng (UC5)
    /// </summary>
    [HttpGet("partners/nearby")]
    public async Task<IActionResult> GetNearbyPartners(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] string? category,
        [FromQuery] double radiusKm = 10.0,
        CancellationToken ct = default)
    {
        var partners = await _geoMatchingService.FindNearestPartnersAsync(lat, lng, category, radiusKm, ct);
        return Ok(partners);
    }

    /// <summary>
    /// Lấy danh sách yêu cầu bàn giao rác của người dân hoặc cơ sở thu gom đang đăng nhập (UC U-07, R-03)
    /// </summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery] Guid? userId, CancellationToken ct = default)
    {
        var effectiveUserId = _currentUser.UserId ?? userId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        var orders = await _tradeService.GetUserOrdersAsync(effectiveUserId, ct);
        return Ok(ApiResponse<IEnumerable<TradeOrderDto>>.Ok(orders));
    }

    /// <summary>
    /// Tạo yêu cầu bàn giao rác đến cơ sở thu gom kèm tọa độ điểm hẹn đầu ngõ (UC U-07, BR-MTH-05)
    /// </summary>
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateTradeOrderRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId.HasValue)
        {
            request.UserId = _currentUser.UserId.Value;
        }

        var order = await _tradeService.CreateTradeOrderAsync(request, ct);
        return Ok(new
        {
            success = true,
            data = order,
            order,
            message = "Tạo đơn hẹn bàn giao rác thành công. Vui lòng đưa mã QR cho bên thu gom quét khi mang rác tới."
        });
    }

    /// <summary>
    /// Cập nhật trạng thái yêu cầu bàn giao: Nhận đơn, Từ chối hoặc Huỷ kèm lý do (UC U-08, R-03, R-04)
    /// </summary>
    [HttpPatch("orders/{orderId:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(
        Guid orderId,
        [FromBody] UpdateTradeOrderStatusRequest request,
        CancellationToken ct = default)
    {
        var callerId = _currentUser.UserId ?? Guid.Empty;
        var updated = await _tradeService.UpdateOrderStatusAsync(orderId, callerId, request, ct);
        return Ok(ApiResponse<TradeOrderDto>.Ok(updated, "Cập nhật trạng thái đơn bàn giao thành công."));
    }

    /// <summary>
    /// Đổi khung giờ thu gom đề xuất khi đơn đang chờ xác nhận (UC U-08)
    /// </summary>
    [HttpPatch("orders/{orderId:guid}/time-slot")]
    public async Task<IActionResult> UpdateOrderTimeSlot(
        Guid orderId,
        [FromBody] UpdateTradeOrderTimeSlotRequest request,
        CancellationToken ct = default)
    {
        var callerId = _currentUser.UserId ?? Guid.Empty;
        var updated = await _tradeService.UpdateOrderTimeSlotAsync(orderId, callerId, request.ProposedTimeSlot, ct);
        return Ok(ApiResponse<TradeOrderDto>.Ok(updated, "Đã cập nhật khung giờ thu gom đề xuất."));
    }

    /// <summary>
    /// Xác nhận biên nhận thu mua qua quét mã QR và chốt giao dịch (UC9, UC10)
    /// </summary>
    [HttpPost("receipts/confirm-qr")]
    public async Task<IActionResult> ConfirmReceipt([FromBody] ConfirmQrReceiptRequest request, CancellationToken ct)
    {
        try
        {
            var receipt = await _tradeService.ConfirmReceiptByQrAsync(request, ct);
            return Ok(new
            {
                success = true,
                receipt,
                message = $"Giao dịch thành công! Đã chốt {receipt.ActualWeightKg}kg rác, thanh toán {receipt.TotalAmountCash:N0} VNĐ và cộng {receipt.PointsAwarded} Điểm Xanh!"
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
