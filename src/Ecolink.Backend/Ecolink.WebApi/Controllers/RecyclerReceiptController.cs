using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Lập biên nhận bàn giao phía Recycler (UC R-06).</summary>
[ApiController]
[Route("api/v1/recycler/handover-requests/{requestId:guid}/receipt")]
[Authorize(Roles = "Recycler,Collector")]
[Produces("application/json")]
public class RecyclerReceiptController : ControllerBase
{
    private readonly IHandoverReceiptService _service;
    private readonly ICurrentUserService _currentUser;

    public RecyclerReceiptController(IHandoverReceiptService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid RecyclerId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Xem trước biên nhận (không lưu): tổng khối lượng, tổng tiền theo đơn giá đã chốt, độ lệch so với ước tính.</summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(Guid requestId, [FromBody] CreateReceiptRequest request, CancellationToken ct)
    {
        var data = await _service.PreviewAsync(RecyclerId, requestId, request, ct);
        return Ok(ApiResponse<ReceiptDto>.Ok(data, data.DeviationMessage, data.HasDeviationWarning ? "MSG-HND-04" : null));
    }

    /// <summary>
    /// Phát hành biên nhận số (R-06): lưu khối lượng thực tế, khóa đơn giá, chuyển yêu cầu sang AwaitingConfirmation
    /// để người dân xác nhận. Bắt buộc có ít nhất 1 ảnh cân (BR-HND-04).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid requestId, [FromBody] CreateReceiptRequest request, CancellationToken ct)
    {
        var data = await _service.CreateAsync(RecyclerId, requestId, request, ct);
        var body = ApiResponse<ReceiptDto>.Ok(data,
            data.DeviationMessage ?? "Biên nhận đã được gửi để người dân xác nhận.",
            data.HasDeviationWarning ? "MSG-HND-04" : null);
        return CreatedAtAction(nameof(Get), new { requestId }, body);
    }

    /// <summary>Xem biên nhận đã phát hành của yêu cầu.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid requestId, CancellationToken ct)
    {
        var data = await _service.GetForRecyclerAsync(RecyclerId, requestId, ct);
        return Ok(ApiResponse<ReceiptDto>.Ok(data));
    }
}

/// <summary>Lịch sử giao dịch của Recycler (màn hình Transaction History, UC U-12 phía cơ sở).</summary>
[ApiController]
[Route("api/v1/recycler/transactions")]
[Authorize(Roles = "Recycler,Collector")]
[Produces("application/json")]
public class RecyclerTransactionController : ControllerBase
{
    private readonly IHandoverReceiptService _service;
    private readonly ICurrentUserService _currentUser;

    public RecyclerTransactionController(IHandoverReceiptService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid RecyclerId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Các giao dịch đã hoàn tất của cơ sở: khối lượng, giá trị, điểm. Lọc theo khoảng ngày, có phân trang và tổng cộng.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<TransactionHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var data = await _service.GetRecyclerHistoryAsync(RecyclerId, from, to, page, pageSize, ct);
        return Ok(ApiResponse<TransactionHistoryDto>.Ok(data));
    }
}
