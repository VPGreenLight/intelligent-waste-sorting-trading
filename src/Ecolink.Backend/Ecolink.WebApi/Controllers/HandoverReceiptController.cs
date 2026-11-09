using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Biên nhận bàn giao phía người dân (UC U-09).</summary>
[ApiController]
[Route("api/v1/handover-requests/{requestId:guid}/receipt")]
[Authorize(Roles = "User,Citizen")]
[Produces("application/json")]
public class HandoverReceiptController : ControllerBase
{
    private readonly IHandoverReceiptService _service;
    private readonly ICurrentUserService _currentUser;

    public HandoverReceiptController(IHandoverReceiptService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid UserId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Xem biên nhận: khối lượng cân thực tế, đơn giá, tổng tiền, ảnh cân, cảnh báo lệch khối lượng (MSG-HND-04) và điểm sẽ nhận.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid requestId, CancellationToken ct)
    {
        var data = await _service.GetForUserAsync(UserId, requestId, ct);
        return Ok(ApiResponse<ReceiptDto>.Ok(data, data.DeviationMessage, data.HasDeviationWarning ? "MSG-HND-04" : null));
    }

    /// <summary>
    /// Xác nhận đã nhận đủ tiền và hoàn tất giao dịch (U-09). Giao dịch bị khóa vĩnh viễn (BR-HND-07)
    /// và điểm xanh được cộng cho cả người dân và Recycler (BR-GAM-01). Tiền được thanh toán trực tiếp ngoài hệ thống.
    /// </summary>
    [HttpPost("confirm")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm(Guid requestId, CancellationToken ct)
    {
        var data = await _service.ConfirmAsync(UserId, requestId, ct);
        return Ok(ApiResponse<ReceiptDto>.Ok(data, $"Giao dịch #{data.ReceiptCode} đã được đánh dấu hoàn tất.", "MSG-HND-03"));
    }
}

/// <summary>Lịch sử giao dịch của người dân (UC U-12).</summary>
[ApiController]
[Route("api/v1/transactions")]
[Authorize(Roles = "User,Citizen")]
[Produces("application/json")]
public class TransactionHistoryController : ControllerBase
{
    private readonly IHandoverReceiptService _service;
    private readonly ICurrentUserService _currentUser;

    public TransactionHistoryController(IHandoverReceiptService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid UserId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Các giao dịch đã hoàn tất: khối lượng, số tiền đã nhận, điểm xanh. Lọc theo khoảng ngày hoàn tất, có phân trang và tổng cộng.</summary>
    /// <param name="from">Từ ngày (yyyy-MM-dd).</param>
    /// <param name="to">Đến hết ngày (yyyy-MM-dd).</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<TransactionHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var data = await _service.GetUserHistoryAsync(UserId, from, to, page, pageSize, ct);
        return Ok(ApiResponse<TransactionHistoryDto>.Ok(data));
    }
}
