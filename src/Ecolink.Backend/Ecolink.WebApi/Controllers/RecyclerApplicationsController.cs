using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Phân hệ Đăng ký Nâng cấp & Thẩm định Hồ sơ Cơ sở thu gom (Module: Recycler Onboarding & Verification)
/// Hiện thực hóa Use Case U-17 (Apply for Recycler Upgrade) và S-04 (Verify Recycler Application)
/// Tích hợp lưu trữ chứng từ/ảnh cơ sở trên Cloudflare R2 Object Storage.
/// </summary>
[ApiController]
[Route("api/v1/recycler-applications")]
[Produces("application/json")]
public class RecyclerApplicationsController : ControllerBase
{
    private readonly IRecyclerOnboardingService _onboardingService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IValidator<ApplyRecyclerUpgradeRequest> _applyValidator;
    private readonly IValidator<VerifyRecyclerApplicationRequest> _verifyValidator;

    public RecyclerApplicationsController(
        IRecyclerOnboardingService onboardingService,
        IFileStorageService fileStorageService,
        IValidator<ApplyRecyclerUpgradeRequest> applyValidator,
        IValidator<VerifyRecyclerApplicationRequest> verifyValidator)
    {
        _onboardingService = onboardingService;
        _fileStorageService = fileStorageService;
        _applyValidator = applyValidator;
        _verifyValidator = verifyValidator;
    }

    /// <summary>
    /// [U-17] Tải tệp Giấy phép kinh doanh hoặc Ảnh chụp mặt tiền kho bãi lên Cloudflare R2 Object Storage
    /// </summary>
    /// <param name="file">Tệp hình ảnh (.jpg, .png, .webp) hoặc .pdf (tối đa 10 MB)</param>
    /// <param name="documentType">Loại tài liệu: permit (Giấy phép pháp lý) hoặc facility-photo (Ảnh mặt tiền kho bãi)</param>
    /// <param name="ct">CancellationToken</param>
    [HttpPost("upload-document")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<RecyclerDocumentUploadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RecyclerDocumentUploadResponseDto>>> UploadDocument(
        IFormFile? file,
        [FromForm] string? documentType = "permit",
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn tệp chứng từ hoặc ảnh cơ sở để tải lên.", "MSG-GEN-01"));
        }

        var folderPrefix = string.Equals(documentType, "facility-photo", StringComparison.OrdinalIgnoreCase)
            ? "recycler-kyb/facility-photos"
            : "recycler-kyb/legal-permits";

        using var stream = file.OpenReadStream();
        var result = await _fileStorageService.UploadRecyclerDocumentAsync(
            stream,
            file.FileName,
            file.ContentType,
            folderPrefix,
            ct);

        return Ok(ApiResponse<RecyclerDocumentUploadResponseDto>.Ok(
            result,
            message: "Tải chứng từ lên Cloudflare R2 thành công.",
            messageCode: "MSG-GEN-01"));
    }

    /// <summary>
    /// 1. [U-17] Người dân gửi hồ sơ đăng ký nâng cấp lên Cơ sở thu gom (hoặc nộp lại sau khi bị từ chối - BR-MTH-02)
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<RecyclerApplicationDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RecyclerApplicationDetailDto>>> ApplyForUpgrade(
        [FromBody] ApplyRecyclerUpgradeRequest request,
        CancellationToken ct)
    {
        var validationResult = await _applyValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu hồ sơ đăng ký không hợp lệ.", "MSG-GEN-01", errors));
        }

        var userId = GetCurrentUserId();
        var result = await _onboardingService.SubmitUpgradeApplicationAsync(userId, request, ct);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<RecyclerApplicationDetailDto>.Ok(
            result,
            message: "Hồ sơ cơ sở của bạn đang được xem xét và sẽ xuất hiện trong tìm kiếm sau khi được duyệt.",
            messageCode: "MSG-MTH-03"));
    }

    /// <summary>
    /// 2. [U-17] Người dùng xem trạng thái hồ sơ nâng cấp Recycler của chính mình (Pending / Verified / Rejected kèm lý do)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<RecyclerApplicationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RecyclerApplicationDetailDto>>> GetMyApplication(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _onboardingService.GetMyApplicationAsync(userId, ct);
        return Ok(ApiResponse<RecyclerApplicationDetailDto>.Ok(result, "Lấy thông tin hồ sơ thành công.", "MSG-GEN-01"));
    }

    /// <summary>
    /// 3. [S-04] Staff lấy danh sách hồ sơ đăng ký Recycler (hỗ trợ lọc theo status=Pending/Verified/Rejected và từ khóa)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Staff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RecyclerApplicationDetailDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<RecyclerApplicationDetailDto>>>> GetApplications(
        [FromQuery] RecyclerApplicationFilterRequest filter,
        CancellationToken ct)
    {
        var result = await _onboardingService.GetApplicationsForStaffAsync(filter, ct);
        return Ok(ApiResponse<PagedResult<RecyclerApplicationDetailDto>>.Ok(result));
    }

    /// <summary>
    /// 4. [S-04] Staff xem chi tiết giấy phép, ảnh kho bãi và tọa độ GPS của một hồ sơ theo UserId
    /// </summary>
    [HttpGet("{userId:guid}")]
    [Authorize(Roles = "Staff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<RecyclerApplicationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RecyclerApplicationDetailDto>>> GetApplicationDetail(
        Guid userId,
        CancellationToken ct)
    {
        var result = await _onboardingService.GetApplicationByUserIdAsync(userId, ct);
        return Ok(ApiResponse<RecyclerApplicationDetailDto>.Ok(result));
    }

    /// <summary>
    /// 5. [S-04] Staff phê duyệt (Approve) hoặc từ chối (Reject kèm lý do - BR-MTH-02) hồ sơ đăng ký Recycler
    /// </summary>
    [HttpPut("{userId:guid}/verification")]
    [Authorize(Roles = "Staff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<RecyclerApplicationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RecyclerApplicationDetailDto>>> VerifyApplication(
        Guid userId,
        [FromBody] VerifyRecyclerApplicationRequest request,
        CancellationToken ct)
    {
        var validationResult = await _verifyValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu thẩm định không hợp lệ.", "MSG-MTH-05", errors));
        }

        var staffUserId = GetCurrentUserId();
        var result = await _onboardingService.VerifyApplicationAsync(staffUserId, userId, request, ct);

        if (request.IsApproved)
        {
            return Ok(ApiResponse<RecyclerApplicationDetailDto>.Ok(
                result,
                message: "Hồ sơ cơ sở của bạn đã được duyệt và hiện hiển thị với người dùng.",
                messageCode: "MSG-MTH-04"));
        }

        return Ok(ApiResponse<RecyclerApplicationDetailDto>.Ok(
            result,
            message: $"Hồ sơ cơ sở của bạn chưa được duyệt: {result.RejectionReason}. Vui lòng cập nhật và gửi lại.",
            messageCode: "MSG-MTH-05"));
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Không xác định được định danh người dùng từ Token.");
        }
        return userId;
    }
}

