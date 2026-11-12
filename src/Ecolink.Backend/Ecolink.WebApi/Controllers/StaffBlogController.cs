using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Staff soạn bài, lưu nháp, nộp duyệt, thu hồi (UC S-08). Staff chỉ thao tác trên bài của chính mình.</summary>
[ApiController]
[Route("api/v1/staff/blog-posts")]
[Authorize(Roles = "Staff")]
[Produces("application/json")]
public class StaffBlogController : ControllerBase
{
    private readonly IBlogPostService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffBlogController(IBlogPostService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid StaffId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Bài viết của tôi, lọc theo trạng thái (Draft / Pending_Approval / Approved) và từ khóa tiêu đề.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<BlogPagedResult<BlogPostSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var data = await _service.ListMineAsync(StaffId, status, search, page, pageSize, ct);
        return Ok(ApiResponse<BlogPagedResult<BlogPostSummaryDto>>.Ok(data));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var data = await _service.GetMineAsync(StaffId, id, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data));
    }

    /// <summary>Tạo bài. <c>submitForApproval=false</c> lưu nháp; <c>true</c> nộp duyệt ngay (cần ảnh bìa + nội dung).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] SaveBlogPostRequest request, CancellationToken ct)
    {
        var data = await _service.CreateAsync(StaffId, request, ct);
        var submitted = request.SubmitForApproval;
        var body = submitted
            ? ApiResponse<BlogPostDto>.Ok(data, "Bài viết đã được gửi Admin xét duyệt.", "MSG-ADM-06")
            : ApiResponse<BlogPostDto>.Ok(data, "Đã lưu bản nháp.");
        return StatusCode(StatusCodes.Status201Created, body);
    }

    /// <summary>Sửa bài ở trạng thái Draft (kể cả bài bị Admin trả về). Bài đang chờ duyệt phải thu hồi trước.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveBlogPostRequest request, CancellationToken ct)
    {
        var data = await _service.UpdateAsync(StaffId, id, request, ct);
        return request.SubmitForApproval
            ? Ok(ApiResponse<BlogPostDto>.Ok(data, "Bài viết đã được gửi Admin xét duyệt.", "MSG-ADM-06"))
            : Ok(ApiResponse<BlogPostDto>.Ok(data, "Đã lưu bản nháp."));
    }

    /// <summary>Nộp bài nháp đã lưu để Admin duyệt (NF-05/06).</summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var data = await _service.SubmitAsync(StaffId, id, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data, "Bài viết đã được gửi Admin xét duyệt.", "MSG-ADM-06"));
    }

    /// <summary>Thu hồi bài đang chờ duyệt về bản nháp (EF-02 của A-09).</summary>
    [HttpPost("{id:guid}/recall")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Recall(Guid id, CancellationToken ct)
    {
        var data = await _service.RecallAsync(StaffId, id, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data, "Đã thu hồi bài viết về bản nháp."));
    }

    /// <summary>Xóa bài nháp. Bài đang chờ duyệt hoặc đã xuất bản không xóa được.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(StaffId, id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Đã xóa bản nháp."));
    }
}
