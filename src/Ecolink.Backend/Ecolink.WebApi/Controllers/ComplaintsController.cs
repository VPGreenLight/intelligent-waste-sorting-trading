using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// API Gửi Khiếu nại 2 chiều &amp; Tải ảnh bằng chứng lên Cloudflare R2 (U-11: STT 109–111).
/// </summary>
[ApiController]
[Route("api/v1/complaints")]
[Authorize]
[Produces("application/json")]
public class ComplaintsController : ControllerBase
{
    private readonly IComplaintService _complaintService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<SubmitComplaintRequest> _submitValidator;

    public ComplaintsController(
        IComplaintService complaintService,
        ICurrentUserService currentUser,
        IValidator<SubmitComplaintRequest> submitValidator)
    {
        _complaintService = complaintService;
        _currentUser = currentUser;
        _submitValidator = submitValidator;
    }

    /// <summary>[STT 109] [U-11] Tải ảnh bằng chứng khiếu nại lên Cloudflare R2 (thư mục complaint-evidences).</summary>
    [HttpPost("upload-evidence")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<RecyclerDocumentUploadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RecyclerDocumentUploadResponseDto>>> UploadEvidence(
        IFormFile? file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn tệp ảnh bằng chứng hợp lệ (tối đa 10 MB).", "MSG-GEN-01"));
        }

        await using var stream = file.OpenReadStream();
        var result = await _complaintService.UploadEvidenceAsync(stream, file.FileName, file.ContentType, ct);
        return Ok(ApiResponse<RecyclerDocumentUploadResponseDto>.Ok(
            result,
            "Tải ảnh bằng chứng khiếu nại thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>[STT 110] [U-11] Gửi phiếu khiếu nại giao dịch 2 chiều (Citizen hoặc Recycler) trong hạn 7 ngày.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ComplaintDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ComplaintDetailDto>>> SubmitComplaint(
        [FromBody] SubmitComplaintRequest request,
        CancellationToken ct)
    {
        var validation = await _submitValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu khiếu nại không hợp lệ.", "MSG-GEN-01", errors));
        }

        var userId = ResolveCallerId();
        var created = await _complaintService.SubmitComplaintAsync(userId, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ComplaintDetailDto>.Ok(
            created,
            "Khiếu nại của bạn đã được gửi và đang được xem xét.",
            "MSG-DSP-01"));
    }

    /// <summary>[STT 111] [U-11] Tra cứu danh sách khiếu nại liên quan đến tài khoản đang đăng nhập (BR-DSP-06).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ComplaintSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ComplaintSummaryDto>>>> GetMyComplaints(
        [FromQuery] MyComplaintFilterRequest filter,
        CancellationToken ct)
    {
        var userId = ResolveCallerId();
        var result = await _complaintService.GetMyComplaintsAsync(userId, filter, ct);
        return Ok(ApiResponse<PagedResult<ComplaintSummaryDto>>.Ok(
            result,
            "Tải danh sách khiếu nại cá nhân thành công.",
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
