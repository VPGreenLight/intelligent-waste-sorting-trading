using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Yêu cầu bàn giao phế liệu phía người dân (UC U-06, U-07).</summary>
[ApiController]
[Route("api/v1/handover-requests")]
[Authorize(Roles = "User,Citizen")]
[Produces("application/json")]
public class HandoverRequestController : ControllerBase
{
    private readonly IHandoverRequestService _service;
    private readonly ICurrentUserService _currentUser;

    public HandoverRequestController(IHandoverRequestService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid UserId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Tạo yêu cầu bàn giao tới một Recycler (U-06). Trả 201 kèm mã yêu cầu HND-yyyyMMdd-NNNN.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<HandoverRequestDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateHandoverRequest request, CancellationToken ct)
    {
        var data = await _service.CreateAsync(UserId, request, ct);
        var body = ApiResponse<HandoverRequestDto>.Ok(data, "Yêu cầu bàn giao của bạn đã được gửi.", "MSG-HND-01");
        return CreatedAtAction(nameof(GetById), new { id = data.Id }, body);
    }

    /// <summary>Danh sách yêu cầu của tôi, lọc theo trạng thái (Pending, Accepted, Cancelled...), có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HandoverPagedResult<HandoverRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine([FromQuery] string? status, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var data = await _service.GetUserRequestsAsync(UserId, status, page, pageSize, ct);
        return Ok(ApiResponse<HandoverPagedResult<HandoverRequestDto>>.Ok(data));
    }

    /// <summary>Chi tiết một yêu cầu của tôi. Thông tin liên hệ Recycler chỉ hiện sau khi được chấp nhận (BR-HND-08).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<HandoverRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var data = await _service.GetUserRequestAsync(UserId, id, ct);
        return Ok(ApiResponse<HandoverRequestDto>.Ok(data));
    }

    /// <summary>Chỉnh sửa yêu cầu khi còn Pending (U-07).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<HandoverRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHandoverRequest request, CancellationToken ct)
    {
        var data = await _service.UpdateAsync(UserId, id, request, ct);
        return Ok(ApiResponse<HandoverRequestDto>.Ok(data, "Cập nhật yêu cầu bàn giao thành công."));
    }

    /// <summary>Hủy yêu cầu kèm lý do bắt buộc (U-07). Hủy sát giờ hẹn được ghi nhận là hủy muộn (BR-HND-06).</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<HandoverRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] HandoverReasonRequest request, CancellationToken ct)
    {
        var data = await _service.CancelByUserAsync(UserId, id, request.Reason, ct);
        var msg = data.IsLateCancellation
            ? $"Đã hủy. Việc hủy trong vòng {Ecolink.Infrastructure.Services.HandoverRequestService.LateCancelWindowHours} giờ trước lịch hẹn được ghi nhận là hủy muộn."
            : "Yêu cầu bàn giao đã được hủy.";
        return Ok(ApiResponse<HandoverRequestDto>.Ok(data, msg, data.IsLateCancellation ? "MSG-HND-06" : "MSG-HND-05"));
    }
}
