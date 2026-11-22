using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Constants;
using Ecolink.WebApi.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Cổng vận hành Staff kiểm duyệt đánh giá vi phạm (STT 108) &amp; giải quyết khiếu nại trọng tài 360 (S-05: STT 112–114).
/// Yêu cầu quyền hạt mịn HANDLE_COMPLAINT (hoặc Admin).
/// </summary>
[ApiController]
[Route("api/v1/staff")]
[Authorize(Roles = "Staff,Admin")]
[HasPermission(StaffPermissions.HandleComplaint)]
[Produces("application/json")]
public class StaffComplaintsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly IComplaintService _complaintService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<ModerateReviewRequest> _moderateValidator;
    private readonly IValidator<ResolveComplaintRequest> _resolveValidator;

    public StaffComplaintsController(
        IReviewService reviewService,
        IComplaintService complaintService,
        ICurrentUserService currentUser,
        IValidator<ModerateReviewRequest> moderateValidator,
        IValidator<ResolveComplaintRequest> resolveValidator)
    {
        _reviewService = reviewService;
        _complaintService = complaintService;
        _currentUser = currentUser;
        _moderateValidator = moderateValidator;
        _resolveValidator = resolveValidator;
    }

    /// <summary>[STT 108] [U-10 Moderation] Staff ẩn hoặc khôi phục đánh giá bị báo cáo vi phạm (BR-RVW-03, BR-ADM-05).</summary>
    [HttpPatch("reviews/{reviewId:guid}/moderate")]
    [ProducesResponseType(typeof(ApiResponse<TransactionReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TransactionReviewDto>>> ModerateReview(
        Guid reviewId,
        [FromBody] ModerateReviewRequest request,
        CancellationToken ct)
    {
        var validation = await _moderateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu kiểm duyệt đánh giá không hợp lệ.", "MSG-GEN-01", errors));
        }

        var staffId = ResolveCallerId();
        var updated = await _reviewService.ModerateReviewAsync(staffId, reviewId, request, ct);
        return Ok(ApiResponse<TransactionReviewDto>.Ok(
            updated,
            "Đánh giá đã được cập nhật trạng thái và tính lại điểm sao trung bình của cơ sở.",
            "MSG-RVW-03"));
    }

    /// <summary>[STT 112] [S-05] Hàng đợi khiếu nại giao dịch kèm bộ đếm tồn đọng &amp; đánh giá bị báo cáo.</summary>
    [HttpGet("complaints")]
    [ProducesResponseType(typeof(ApiResponse<StaffComplaintQueueResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StaffComplaintQueueResponseDto>>> GetComplaintQueue(
        [FromQuery] StaffComplaintFilterRequest filter,
        CancellationToken ct)
    {
        var result = await _complaintService.GetStaffComplaintQueueAsync(filter, ct);
        return Ok(ApiResponse<StaffComplaintQueueResponseDto>.Ok(
            result,
            "Tải hàng đợi khiếu nại giao dịch thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>[STT 113] [S-05] Mở Hồ sơ Trọng tài 360 độ (ảnh cân + bằng chứng R2 + lịch sử chat bất biến BR-DSP-06).</summary>
    [HttpGet("complaints/{complaintId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StaffComplaintDossierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StaffComplaintDossierDto>>> GetComplaintDossier(
        Guid complaintId,
        [FromQuery] bool claim = true,
        CancellationToken ct = default)
    {
        var staffId = ResolveCallerId();
        var dossier = await _complaintService.GetStaffComplaintDossierAsync(staffId, complaintId, claim, ct);
        return Ok(ApiResponse<StaffComplaintDossierDto>.Ok(
            dossier,
            "Tải hồ sơ trọng tài khiếu nại thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>[STT 114] [S-05] Ban hành phán quyết trọng tài ràng buộc, thu hồi điểm &amp; cảnh cáo vi phạm (BR-DSP-04, BR-GAM-01, BR-ADM-05).</summary>
    [HttpPatch("complaints/{complaintId:guid}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<ComplaintDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ComplaintDetailDto>>> ResolveComplaint(
        Guid complaintId,
        [FromBody] ResolveComplaintRequest request,
        CancellationToken ct)
    {
        var validation = await _resolveValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Biên bản giải quyết khiếu nại không hợp lệ.", "MSG-GEN-01", errors));
        }

        var staffId = ResolveCallerId();
        var resolved = await _complaintService.ResolveComplaintAsync(staffId, complaintId, request, ct);
        return Ok(ApiResponse<ComplaintDetailDto>.Ok(
            resolved,
            $"Khiếu nại của bạn đã được giải quyết: {resolved.ResolutionOutcome}.",
            "MSG-DSP-02"));
    }

    private Guid ResolveCallerId()
    {
        if (_currentUser.UserId is { } uid && uid != Guid.Empty)
        {
            return uid;
        }

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        throw new UnauthorizedAccessException("Không xác định được danh tính nhân viên vận hành từ phiên đăng nhập.");
    }
}
