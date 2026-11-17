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
/// Cổng vận hành Staff thẩm định báo cáo AI nhận diện sai (S-01) và gán nhãn dữ liệu chuẩn (S-02).
/// Bao phủ STT 131–136 trong 00_api_inventory_and_endpoint_statistics.md.
/// </summary>
[ApiController]
[Route("api/v1/staff")]
[Authorize(Roles = "Staff,Admin")]
[HasPermission(StaffPermissions.ReviewAi)]
[Produces("application/json")]
public class StaffAiFeedbackController : ControllerBase
{
    private readonly IAiFeedbackService _feedbackService;
    private readonly IAiDatasetCurationService _curationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<ReviewAiFeedbackRequest> _reviewValidator;
    private readonly IValidator<AssignGroundTruthLabelRequest> _assignValidator;
    private readonly IValidator<BatchAssignLabelsRequest> _batchAssignValidator;

    public StaffAiFeedbackController(
        IAiFeedbackService feedbackService,
        IAiDatasetCurationService curationService,
        ICurrentUserService currentUserService,
        IValidator<ReviewAiFeedbackRequest> reviewValidator,
        IValidator<AssignGroundTruthLabelRequest> assignValidator,
        IValidator<BatchAssignLabelsRequest> batchAssignValidator)
    {
        _feedbackService = feedbackService;
        _curationService = curationService;
        _currentUserService = currentUserService;
        _reviewValidator = reviewValidator;
        _assignValidator = assignValidator;
        _batchAssignValidator = batchAssignValidator;
    }

    /// <summary>
    /// [STT 131] [S-01] Lọc và phân trang hàng đợi báo cáo AI nhận diện sai kèm bộ đếm tồn đọng.
    /// </summary>
    [HttpGet("ai-feedback")]
    [ProducesResponseType(typeof(ApiResponse<StaffAiFeedbackQueueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StaffAiFeedbackQueueDto>>> GetFeedbackQueue(
        [FromQuery] StaffAiFeedbackFilterRequest filter,
        CancellationToken ct)
    {
        var result = await _feedbackService.GetStaffFeedbackQueueAsync(filter, ct);
        return Ok(ApiResponse<StaffAiFeedbackQueueDto>.Ok(
            result,
            "Tải hàng đợi báo cáo AI nhận diện sai thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 132] [S-01] Xem chi tiết ảnh gốc, bounding boxes lúc quét và lịch sử người báo cáo AI sai.
    /// </summary>
    [HttpGet("ai-feedback/{reportId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StaffAiFeedbackDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StaffAiFeedbackDetailDto>>> GetFeedbackDetail(
        Guid reportId,
        CancellationToken ct)
    {
        var detail = await _feedbackService.GetStaffFeedbackDetailAsync(reportId, ct);
        return Ok(ApiResponse<StaffAiFeedbackDetailDto>.Ok(
            detail,
            "Tải chi tiết báo cáo AI nhận diện sai thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 133] [S-01] Staff thẩm định báo cáo sai: ApproveAndLabel (1 chạm), ApproveToQueue hoặc Reject (BR-CLS-06).
    /// </summary>
    [HttpPatch("ai-feedback/{reportId:guid}/review")]
    [ProducesResponseType(typeof(ApiResponse<StaffAiFeedbackDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<StaffAiFeedbackDetailDto>>> ReviewFeedback(
        Guid reportId,
        [FromBody] ReviewAiFeedbackRequest request,
        CancellationToken ct)
    {
        var validation = await _reviewValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu thẩm định báo cáo AI không hợp lệ.", "MSG-GEN-01", errors));
        }

        var staffId = ResolveCallerId();
        var updated = await _feedbackService.ReviewFeedbackAsync(staffId, reportId, request, ct);
        return Ok(ApiResponse<StaffAiFeedbackDetailDto>.Ok(
            updated,
            "Đã cập nhật kết quả thẩm định và chuẩn hóa nhãn Ground-Truth cho mẫu ảnh AI.",
            "MSG-CLS-07"));
    }

    /// <summary>
    /// [STT 134] [S-02] Hàng đợi ảnh chờ gán nhãn chuẩn &amp; ca OOD thu thập tự động (< 60%).
    /// </summary>
    [HttpGet("ai-labeling/queue")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AiDatasetItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AiDatasetItemDto>>>> GetLabelingQueue(
        [FromQuery] AiLabelingQueueFilterRequest filter,
        CancellationToken ct)
    {
        var queue = await _curationService.GetLabelingQueueAsync(filter, ct);
        return Ok(ApiResponse<PagedResult<AiDatasetItemDto>>.Ok(
            queue,
            "Tải kho ảnh gán nhãn Ground-Truth thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 135] [S-02] Gán nhãn danh mục chuẩn &amp; tọa độ hộp bao YOLO chuẩn hóa [0.0, 1.0] cho từng ảnh.
    /// </summary>
    [HttpPut("ai-labeling/{itemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AiDatasetItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AiDatasetItemDto>>> AssignGroundTruthLabel(
        Guid itemId,
        [FromBody] AssignGroundTruthLabelRequest request,
        CancellationToken ct)
    {
        var validation = await _assignValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu gán nhãn không hợp lệ.", "MSG-GEN-01", errors));
        }

        var staffId = ResolveCallerId();
        var updated = await _curationService.AssignGroundTruthLabelAsync(staffId, itemId, request, ct);
        return Ok(ApiResponse<AiDatasetItemDto>.Ok(
            updated,
            "Đã cập nhật kết quả thẩm định và chuẩn hóa nhãn Ground-Truth cho mẫu ảnh AI.",
            "MSG-CLS-07"));
    }

    /// <summary>
    /// [STT 136] [S-02] Gán nhãn chuẩn hàng loạt nguyên tử cho nhiều ảnh đồng nhất trong 1 giao dịch DB.
    /// </summary>
    [HttpPost("ai-labeling/batch-assign")]
    [ProducesResponseType(typeof(ApiResponse<BatchAssignLabelsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BatchAssignLabelsResultDto>>> BatchAssignLabels(
        [FromBody] BatchAssignLabelsRequest request,
        CancellationToken ct)
    {
        var validation = await _batchAssignValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Danh sách hoặc nhãn gán hàng loạt không hợp lệ.", "MSG-GEN-01", errors));
        }

        var staffId = ResolveCallerId();
        var result = await _curationService.BatchAssignLabelsAsync(staffId, request, ct);
        return Ok(ApiResponse<BatchAssignLabelsResultDto>.Ok(
            result,
            "Đã cập nhật kết quả thẩm định và chuẩn hóa nhãn Ground-Truth cho mẫu ảnh AI.",
            "MSG-CLS-07"));
    }

    private Guid ResolveCallerId()
    {
        if (_currentUserService.UserId is { } uid && uid != Guid.Empty)
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
