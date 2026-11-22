using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Constants;
using Ecolink.Domain.Exceptions;
using Ecolink.WebApi.Security;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Staff soạn bài, tải ảnh R2, lưu nháp, nộp duyệt, thu hồi (UC S-08). Staff chỉ thao tác trên bài của chính mình.</summary>
[ApiController]
[HasPermission(StaffPermissions.ManageBlog)]
[Produces("application/json")]
public class StaffBlogController : ControllerBase
{
    private const long MaxBlogMediaBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedMediaExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IBlogPostService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;

    public StaffBlogController(
        IBlogPostService service,
        ICurrentUserService currentUser,
        IFileStorageService fileStorage)
    {
        _service = service;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    private Guid StaffId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>[STT 142] [S-08] Tải ảnh bìa hoặc ảnh minh họa bài viết lên Cloudflare R2.</summary>
    [HttpPost("api/v1/staff/blogs/upload-media")]
    [HttpPost("api/v1/staff/blog-posts/upload-media")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<BlogMediaUploadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadMedia(IFormFile? file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            throw new BusinessRuleException("BR-ADM-08", "MSG-AUTH-02", "Vui lòng chọn tệp hình ảnh để tải lên.");

        if (file.Length > MaxBlogMediaBytes)
            throw new BusinessRuleException("BR-ADM-08", "MSG-NTF-03", "Dung lượng ảnh bài viết tối đa 5 MB.");

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedMediaExtensions.Contains(ext))
            throw new BusinessRuleException("BR-ADM-08", "MSG-CLS-01", "Định dạng ảnh không được hỗ trợ (chỉ JPG, PNG, WEBP).");

        await using var stream = file.OpenReadStream();
        var uploaded = await _fileStorage.UploadRecyclerDocumentAsync(
            stream,
            file.FileName,
            file.ContentType,
            "blog-media",
            ct);

        return Ok(ApiResponse<BlogMediaUploadResponseDto>.Ok(
            new BlogMediaUploadResponseDto
            {
                Url = uploaded.FileUrl,
                FileUrl = uploaded.FileUrl,
                ObjectKey = uploaded.ObjectKey,
                ContentType = uploaded.ContentType,
                SizeBytes = uploaded.SizeBytes
            },
            "Tải ảnh bài viết thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>Bài viết của tôi, lọc theo trạng thái (Draft / Pending_Approval / Approved / Published) và từ khóa tiêu đề.</summary>
    [HttpGet("api/v1/staff/blogs")]
    [HttpGet("api/v1/staff/blog-posts")]
    [ProducesResponseType(typeof(ApiResponse<BlogPagedResult<BlogPostSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var data = await _service.ListMineAsync(StaffId, status, search, page, pageSize, ct);
        return Ok(ApiResponse<BlogPagedResult<BlogPostSummaryDto>>.Ok(data));
    }

    [HttpGet("api/v1/staff/blogs/{id:guid}")]
    [HttpGet("api/v1/staff/blog-posts/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var data = await _service.GetMineAsync(StaffId, id, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data));
    }

    /// <summary>Tạo bài. <c>submitForApproval=false</c> lưu nháp; <c>true</c> nộp duyệt ngay (cần ảnh bìa + nội dung).</summary>
    [HttpPost("api/v1/staff/blogs")]
    [HttpPost("api/v1/staff/blog-posts")]
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
    [HttpPut("api/v1/staff/blogs/{id:guid}")]
    [HttpPut("api/v1/staff/blog-posts/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveBlogPostRequest request, CancellationToken ct)
    {
        var data = await _service.UpdateAsync(StaffId, id, request, ct);
        return request.SubmitForApproval
            ? Ok(ApiResponse<BlogPostDto>.Ok(data, "Bài viết đã được gửi Admin xét duyệt.", "MSG-ADM-06"))
            : Ok(ApiResponse<BlogPostDto>.Ok(data, "Đã lưu bản nháp."));
    }

    /// <summary>Nộp bài nháp đã lưu để Admin duyệt (NF-05/06).</summary>
    [HttpPost("api/v1/staff/blogs/{id:guid}/submit")]
    [HttpPost("api/v1/staff/blog-posts/{id:guid}/submit")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var data = await _service.SubmitAsync(StaffId, id, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data, "Bài viết đã được gửi Admin xét duyệt.", "MSG-ADM-06"));
    }

    /// <summary>Thu hồi bài đang chờ duyệt về bản nháp (EF-02 của A-09).</summary>
    [HttpPost("api/v1/staff/blogs/{id:guid}/recall")]
    [HttpPost("api/v1/staff/blog-posts/{id:guid}/recall")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Recall(Guid id, CancellationToken ct)
    {
        var data = await _service.RecallAsync(StaffId, id, ct);
        return Ok(ApiResponse<BlogPostDto>.Ok(data, "Đã thu hồi bài viết về bản nháp."));
    }

    /// <summary>Xóa bài nháp. Bài đang chờ duyệt hoặc đã xuất bản không xóa được.</summary>
    [HttpDelete("api/v1/staff/blogs/{id:guid}")]
    [HttpDelete("api/v1/staff/blog-posts/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(StaffId, id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Đã xóa bản nháp."));
    }
}
