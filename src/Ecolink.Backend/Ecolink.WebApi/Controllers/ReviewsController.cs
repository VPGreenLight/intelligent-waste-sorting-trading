using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// API Đánh giá Cơ sở thu mua 1–5 sao, Tự xóa &amp; Báo cáo vi phạm (U-10: STT 103–107).
/// </summary>
[ApiController]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateReviewRequest> _createValidator;
    private readonly IValidator<ReportReviewRequest> _reportValidator;

    public ReviewsController(
        IReviewService reviewService,
        ICurrentUserService currentUser,
        IValidator<CreateReviewRequest> createValidator,
        IValidator<ReportReviewRequest> reportValidator)
    {
        _reviewService = reviewService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _reportValidator = reportValidator;
    }

    /// <summary>[STT 103] [U-10] Người dân đánh giá 1-5 sao &amp; nhận xét sau khi đơn Completed trong 14 ngày (BR-RVW-01).</summary>
    [HttpPost("api/v1/handover-requests/{requestId:guid}/reviews")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TransactionReviewDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<TransactionReviewDto>>> CreateReview(
        Guid requestId,
        [FromBody] CreateReviewRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu đánh giá không hợp lệ.", "MSG-RVW-02", errors));
        }

        var userId = ResolveCallerId();
        var created = await _reviewService.CreateReviewAsync(userId, requestId, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<TransactionReviewDto>.Ok(
            created,
            "Cảm ơn bạn đã đánh giá! Điểm uy tín của cơ sở thu mua đã được cập nhật.",
            "MSG-RVW-01"));
    }

    /// <summary>[STT 104] [U-10] Xem danh sách đánh giá công khai &amp; phân bố sao của cơ sở thu mua.</summary>
    [HttpGet("api/v1/recyclers/{recyclerId:guid}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<RecyclerReviewsSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RecyclerReviewsSummaryDto>>> GetRecyclerReviews(
        Guid recyclerId,
        [FromQuery] RecyclerReviewFilterRequest filter,
        CancellationToken ct)
    {
        var summary = await _reviewService.GetRecyclerReviewsAsync(recyclerId, filter, ct);
        return Ok(ApiResponse<RecyclerReviewsSummaryDto>.Ok(
            summary,
            "Tải danh sách đánh giá cơ sở thu mua thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>[STT 105] [U-10] Người dân tra cứu lịch sử đánh giá đã gửi.</summary>
    [HttpGet("api/v1/reviews/me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TransactionReviewDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<TransactionReviewDto>>>> GetMyReviews(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var userId = ResolveCallerId();
        var result = await _reviewService.GetMyReviewsAsync(userId, pageNumber, pageSize, ct);
        return Ok(ApiResponse<PagedResult<TransactionReviewDto>>.Ok(
            result,
            "Tải lịch sử đánh giá cá nhân thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>[STT 106] [U-10] Tác giả tự xóa vĩnh viễn bài đánh giá của mình (BR-RVW-03).</summary>
    [HttpDelete("api/v1/reviews/{reviewId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TransactionReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TransactionReviewDto>>> DeleteMyReview(
        Guid reviewId,
        CancellationToken ct)
    {
        var userId = ResolveCallerId();
        var updated = await _reviewService.DeleteMyReviewAsync(userId, reviewId, ct);
        return Ok(ApiResponse<TransactionReviewDto>.Ok(
            updated,
            "Đánh giá đã được cập nhật trạng thái và tính lại điểm sao trung bình của cơ sở.",
            "MSG-RVW-03"));
    }

    /// <summary>[STT 107] [U-10] Báo cáo đánh giá có nội dung vi phạm / spam để Staff kiểm duyệt (BR-RVW-03).</summary>
    [HttpPost("api/v1/reviews/{reviewId:guid}/report")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TransactionReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TransactionReviewDto>>> ReportReview(
        Guid reviewId,
        [FromBody] ReportReviewRequest request,
        CancellationToken ct)
    {
        var validation = await _reportValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Lý do báo cáo đánh giá không hợp lệ.", "MSG-GEN-01", errors));
        }

        var userId = ResolveCallerId();
        var updated = await _reviewService.ReportAbusiveReviewAsync(userId, reviewId, request, ct);
        return Ok(ApiResponse<TransactionReviewDto>.Ok(
            updated,
            "Báo cáo vi phạm đánh giá đã được gửi tới bộ phận kiểm duyệt.",
            "MSG-GEN-01"));
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

        throw new UnauthorizedAccessException("Không xác định được danh tính người dùng từ phiên đăng nhập.");
    }
}
