using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Yêu cầu bàn giao đến cơ sở Recycler (UC R-03, R-04). Recycler chỉ thấy yêu cầu gửi cho mình (BR-HND-03).</summary>
[ApiController]
[Route("api/v1/recycler/handover-requests")]
[Authorize(Roles = "Recycler,Collector")]
[Produces("application/json")]
public class RecyclerHandoverController : ControllerBase
{
    private readonly IHandoverRequestService _service;
    private readonly ICurrentUserService _currentUser;

    public RecyclerHandoverController(IHandoverRequestService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid RecyclerId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Danh sách yêu cầu đến, lọc theo trạng thái, có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HandoverPagedResult<HandoverRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncoming([FromQuery] string? status, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var data = await _service.GetRecyclerRequestsAsync(RecyclerId, status, page, pageSize, ct);
        return Ok(ApiResponse<HandoverPagedResult<HandoverRequestDto>>.Ok(data));
    }

    /// <summary>Chi tiết yêu cầu. Địa chỉ chính xác và SĐT người dân chỉ hiện sau khi Accepted (BR-HND-08); không bao giờ trả tọa độ GPS.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<HandoverRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var data = await _service.GetRecyclerRequestAsync(RecyclerId, id, ct);
        return Ok(ApiResponse<HandoverRequestDto>.Ok(data));
    }

    /// <summary>Chấp nhận yêu cầu (R-03): chốt khung giờ đề xuất và mở thông tin liên hệ.</summary>
    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(typeof(ApiResponse<HandoverRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(Guid id, [FromBody] AcceptHandoverRequest? request, CancellationToken ct)
    {
        var data = await _service.AcceptAsync(RecyclerId, id, request?.Note, ct);
        return Ok(ApiResponse<HandoverRequestDto>.Ok(data, "Yêu cầu bàn giao đã được chấp nhận.", "MSG-HND-02"));
    }

    /// <summary>Từ chối yêu cầu kèm lý do bắt buộc (R-03).</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<HandoverRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] HandoverReasonRequest request, CancellationToken ct)
    {
        var data = await _service.RejectAsync(RecyclerId, id, request.Reason, ct);
        return Ok(ApiResponse<HandoverRequestDto>.Ok(data, "Đã từ chối yêu cầu bàn giao."));
    }

    /// <summary>Hủy lịch hẹn đã chấp nhận trước khi thu gom, kèm lý do bắt buộc (R-04, BR-HND-06).</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<HandoverRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] HandoverReasonRequest request, CancellationToken ct)
    {
        var data = await _service.CancelByRecyclerAsync(RecyclerId, id, request.Reason, ct);
        return Ok(ApiResponse<HandoverRequestDto>.Ok(data, "Lịch thu gom đã được hủy.",
            data.IsLateCancellation ? "MSG-HND-06" : "MSG-HND-05"));
    }
}
