using System.Globalization;
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
/// Cổng Staff quản trị phiên bản tập dữ liệu kiểm chứng &amp; huấn luyện AI (S-03: Sticky Stratified Split 70/20/10, ZIP Export).
/// Bao phủ STT 137–139 trong 00_api_inventory_and_endpoint_statistics.md.
/// </summary>
[ApiController]
[Route("api/v1/staff/ai-datasets")]
[Authorize(Roles = "Staff,Admin")]
[HasPermission(StaffPermissions.CurateDataset)]
[Produces("application/json")]
public class StaffDatasetCurationController : ControllerBase
{
    private readonly IAiDatasetCurationService _curationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateDatasetVersionRequest> _createVersionValidator;

    public StaffDatasetCurationController(
        IAiDatasetCurationService curationService,
        ICurrentUserService currentUserService,
        IValidator<CreateDatasetVersionRequest> createVersionValidator)
    {
        _curationService = curationService;
        _currentUserService = currentUserService;
        _createVersionValidator = createVersionValidator;
    }

    /// <summary>
    /// [STT 137] [S-03] Tổng quan kho dữ liệu sạch, phân bố Train/Val/Test theo danh mục, chỉ số cân bằng lớp &amp; lịch sử phiên bản.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AiDatasetDashboardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AiDatasetDashboardDto>>> GetDatasetDashboard(CancellationToken ct)
    {
        var overview = await _curationService.GetDatasetOverviewAsync(ct);
        return Ok(ApiResponse<AiDatasetDashboardDto>.Ok(
            overview,
            "Tải tổng quan kho dữ liệu kiểm chứng AI thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 138] [S-03] Đóng gói phiên bản Dataset chính thức theo thuật toán Sticky Stratified Split (70/20/10) chống rò rỉ (BR-CLS-07).
    /// </summary>
    [HttpPost("versions")]
    [ProducesResponseType(typeof(ApiResponse<AiDatasetVersionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AiDatasetVersionDto>>> CreateDatasetVersion(
        [FromBody] CreateDatasetVersionRequest request,
        CancellationToken ct)
    {
        var validation = await _createVersionValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Cấu hình đóng gói phiên bản Dataset không hợp lệ.", "MSG-GEN-01", errors));
        }

        var staffId = ResolveCallerId();
        var created = await _curationService.CreateDatasetVersionAsync(staffId, request, ct);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<AiDatasetVersionDto>.Ok(
            created,
            $"Đã đóng gói phiên bản Dataset '{created.VersionCode}' ({created.TotalSamples} ảnh chuẩn hóa: {created.TrainCount}/{created.ValCount}/{created.TestCount}).",
            "MSG-CLS-08"));
    }

    /// <summary>
    /// [STT 139] [S-03] Xuất gói .zip chuẩn YOLO (data.yaml + labels/*.txt + manifest.json) hoặc ImageFolder phục vụ huấn luyện lại.
    /// </summary>
    [HttpGet("versions/{versionId:guid}/export")]
    [Produces("application/zip", "application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportDatasetVersion(
        Guid versionId,
        [FromQuery] string format = "yolo",
        CancellationToken ct = default)
    {
        var staffId = ResolveCallerId();
        var archive = await _curationService.ExportDatasetVersionAsync(staffId, versionId, format, ct);

        Response.Headers["X-EcoLink-Message-Code"] = "MSG-ADM-02";
        Response.Headers["X-EcoLink-Sample-Count"] = archive.SampleCount.ToString(CultureInfo.InvariantCulture);
        return File(archive.Content, archive.ContentType, archive.FileName);
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
