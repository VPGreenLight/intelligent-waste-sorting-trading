using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Admin duyệt / từ chối bài viết do Staff gửi (UC A-09, BR-ADM-02, BR-ADM-05, BR-ADM-08).</summary>
[ApiController]
[Route("api/v1/admin/blog-posts")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminBlogController : ControllerBase
{
    private readonly IBlogPostService _service;
    private readonly ICurrentUserService _currentUser;

    public AdminBlogController(IBlogPostService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid AdminId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Hàng đợi duyệt (chỉ bài Pending_Approval). <c>sort</c> = oldest (mặc định) | newest theo ngày nộp.</summary>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(ApiResponse<BlogPagedResult<BlogPostSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Queue([FromQuery] string? search, [FromQuery] string? sort,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var data = await _service.ListQueueAsync(search, sort, page, pageSize, ct);
        return Ok(ApiResponse<BlogPagedResult<BlogPostSummaryDto>>.Ok(data));
    }

    /// <summary>Xem toàn bộ bài viết để duyệt (nội dung, ảnh bìa, thẻ).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var data = await _service.GetForReviewAsync(id, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data));
    }

    /// <summary>Duyệt và xuất bản bài viết; ghi nhật ký kiểm toán và thông báo cho Staff.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var data = await _service.ApproveAsync(AdminId, id, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data, $"Bài viết '{data.Title}' đã được duyệt và xuất bản.", "MSG-ADM-05"));
    }

    /// <summary>Từ chối: bài quay về Draft kèm góp ý bắt buộc, thông báo cho Staff.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBlogPostRequest request, CancellationToken ct)
    {
        var data = await _service.RejectAsync(AdminId, id, request.Feedback, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data, "Đã trả bài viết về bản nháp kèm góp ý."));
    }
}
