using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Admin duyệt / từ chối bài viết do Staff gửi (UC A-09, BR-ADM-02, BR-ADM-05, BR-ADM-08).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminBlogController : ControllerBase
{
    private readonly IBlogPostService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<ReviewBlogPostRequest> _reviewValidator;
    private readonly IValidator<RejectBlogPostRequest> _rejectValidator;

    public AdminBlogController(
        IBlogPostService service,
        ICurrentUserService currentUser,
        IValidator<ReviewBlogPostRequest> reviewValidator,
        IValidator<RejectBlogPostRequest> rejectValidator)
    {
        _service = service;
        _currentUser = currentUser;
        _reviewValidator = reviewValidator;
        _rejectValidator = rejectValidator;
    }

    private Guid AdminId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>[STT 148] Hàng đợi duyệt (chỉ bài Pending_Approval). <c>sort</c> = oldest (mặc định) | newest theo ngày nộp.</summary>
    [HttpGet("api/v1/admin/blogs/pending")]
    [HttpGet("api/v1/admin/blog-posts/queue")]
    [ProducesResponseType(typeof(ApiResponse<BlogPagedResult<BlogPostSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Queue([FromQuery] string? search, [FromQuery] string? sort,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var data = await _service.ListQueueAsync(search, sort, page, pageSize, ct);
        return Ok(ApiResponse<BlogPagedResult<BlogPostSummaryDto>>.Ok(data));
    }

    /// <summary>Xem toàn bộ bài viết để duyệt (nội dung, ảnh bìa, thẻ).</summary>
    [HttpGet("api/v1/admin/blogs/{id:guid}")]
    [HttpGet("api/v1/admin/blog-posts/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var data = await _service.GetForReviewAsync(id, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data));
    }

    /// <summary>[STT 149] Phê duyệt hoặc từ chối bài viết trên một endpoint thống nhất.</summary>
    [HttpPatch("api/v1/admin/blogs/{id:guid}/review")]
    [HttpPatch("api/v1/admin/blog-posts/{id:guid}/review")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewBlogPostRequest request, CancellationToken ct)
    {
        var validation = await _reviewValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .Select(e => string.IsNullOrWhiteSpace(e.PropertyName) ? e.ErrorMessage : $"{e.PropertyName}: {e.ErrorMessage}")
                .ToList();
            return BadRequest(ApiResponse<object>.Fail(validation.Errors[0].ErrorMessage, "MSG-AUTH-02", errors));
        }

        var act = (request.Action ?? string.Empty).Trim().ToLowerInvariant();
        bool isApprove;
        if (request.Approve.HasValue && act.Length == 0)
        {
            isApprove = request.Approve.Value;
        }
        else if (act is "approve" or "approved" or "publish" or "published")
        {
            isApprove = true;
        }
        else if (act is "reject" or "rejected")
        {
            isApprove = false;
        }
        else
        {
            throw new BusinessRuleException("BR-ADM-08", "MSG-AUTH-02", "Quyết định duyệt không hợp lệ (Approve hoặc Reject).");
        }

        if (isApprove)
        {
            var approved = await _service.ApproveAsync(AdminId, id, ct);
            return Ok(ApiResponse<BlogPostDto>.Ok(approved, $"Bài viết '{approved.Title}' đã được duyệt và xuất bản.", "MSG-ADM-05"));
        }

        var rejected = await _service.RejectAsync(AdminId, id, request.Feedback ?? string.Empty, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(rejected, "Đã trả bài viết về bản nháp kèm góp ý."));
    }

    /// <summary>Duyệt và xuất bản bài viết; ghi nhật ký kiểm toán và thông báo cho Staff.</summary>
    [HttpPost("api/v1/admin/blogs/{id:guid}/approve")]
    [HttpPost("api/v1/admin/blog-posts/{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var data = await _service.ApproveAsync(AdminId, id, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data, $"Bài viết '{data.Title}' đã được duyệt và xuất bản.", "MSG-ADM-05"));
    }

    /// <summary>Từ chối: bài quay về Draft kèm góp ý bắt buộc, thông báo cho Staff.</summary>
    [HttpPost("api/v1/admin/blogs/{id:guid}/reject")]
    [HttpPost("api/v1/admin/blog-posts/{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBlogPostRequest request, CancellationToken ct)
    {
        var validation = await _rejectValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .Select(e => string.IsNullOrWhiteSpace(e.PropertyName) ? e.ErrorMessage : $"{e.PropertyName}: {e.ErrorMessage}")
                .ToList();
            return BadRequest(ApiResponse<object>.Fail(validation.Errors[0].ErrorMessage, "MSG-AUTH-02", errors));
        }

        var data = await _service.RejectAsync(AdminId, id, request.Feedback, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data, "Đã trả bài viết về bản nháp kèm góp ý."));
    }
}
