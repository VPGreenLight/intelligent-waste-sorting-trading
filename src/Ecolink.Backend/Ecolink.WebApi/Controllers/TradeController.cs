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
    private readonly ILogger<TradeController> _logger;

    public TradeController(
        IGeoMatchingService geoMatchingService,
        ITradeTransactionService tradeService,
        ILogger<TradeController> logger)
    {
        _geoMatchingService = geoMatchingService;
        _tradeService = tradeService;
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
    /// Tạo yêu cầu bàn giao rác đến cơ sở thu gom (UC7)
    /// </summary>
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateTradeOrderRequest request, CancellationToken ct)
    {
        var order = await _tradeService.CreateTradeOrderAsync(request, ct);
        return Ok(new
        {
            success = true,
            order,
            message = "Tạo đơn hẹn bàn giao rác thành công. Vui lòng đưa mã QR cho bên thu gom quét khi mang rác tới."
        });
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
