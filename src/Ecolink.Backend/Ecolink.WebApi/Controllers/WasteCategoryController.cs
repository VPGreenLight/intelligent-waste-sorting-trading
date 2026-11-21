using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Quản trị danh mục phế liệu / loại rác chuẩn (MasterWasteCategory) (UC A-05 - STT 025, 027, 028, 029).
/// </summary>
[ApiController]
[Route("api/v1/waste-categories")]
[Produces("application/json")]
public class WasteCategoryController : ControllerBase
{
    private readonly IRecyclerManagementService _service;
    private readonly IValidator<CreateWasteCategoryRequest>? _createValidator;
    private readonly IValidator<UpdateWasteCategoryRequest>? _updateValidator;
    private readonly IValidator<ToggleWasteCategoryStatusRequest>? _statusValidator;

    public WasteCategoryController(
        IRecyclerManagementService service,
        IValidator<CreateWasteCategoryRequest>? createValidator = null,
        IValidator<UpdateWasteCategoryRequest>? updateValidator = null,
        IValidator<ToggleWasteCategoryStatusRequest>? statusValidator = null)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
    }

    /// <summary>
    /// [STT 025] Lấy danh sách loại phế liệu chuẩn (công khai danh mục Active; Admin có thể truyền includeInactive/includeNonRecyclable).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WasteCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool includeNonRecyclable = false,
        CancellationToken ct = default)
    {
        var isAdminOrStaff = User.IsInRole("Admin") || User.IsInRole("Staff");
        if (isAdminOrStaff || includeNonRecyclable)
        {
            var data = await _service.GetAllWasteCategoriesAsync(
                includeInactive: isAdminOrStaff && includeInactive,
                includeNonRecyclable: includeNonRecyclable,
                ct);
            return Ok(ApiResponse<IReadOnlyList<WasteCategoryDto>>.Ok(
                data,
                message: "Tải danh mục phế liệu thành công.",
                messageCode: "MSG-SRT-LIST-OK"));
        }

        var recyclableActive = await _service.GetWasteCategoriesAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<WasteCategoryDto>>.Ok(
            recyclableActive,
            message: "Tải danh mục phế liệu thành công.",
            messageCode: "MSG-SRT-LIST-OK"));
    }

    /// <summary>
    /// [STT 027] Quản trị viên thêm mới loại phế liệu chuẩn vào danh mục gốc hệ thống (A-05, BR-SRT-02).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<WasteCategoryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateWasteCategoryRequest request,
        CancellationToken ct = default)
    {
        if (_createValidator != null)
        {
            var validation = await _createValidator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    "Dữ liệu tạo danh mục rác chưa hợp lệ.",
                    "MSG-GEN-01",
                    validation.Errors.Select(e => e.ErrorMessage).ToList()));
            }
        }

        var adminId = GetCurrentUserId();
        var dto = await _service.CreateWasteCategoryAsync(adminId, request, ct);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<WasteCategoryDto>.Ok(
                dto,
                message: $"Đã tạo danh mục rác '{dto.Code}' thành công.",
                messageCode: "MSG-SRT-CREATED"));
    }

    /// <summary>
    /// [STT 028] Quản trị viên cập nhật tên, mô tả, biểu tượng, khả năng tái chế hoặc màu thùng chuẩn (A-05, BR-SRT-02).
    /// </summary>
    [HttpPut("{categoryId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<WasteCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(
        Guid categoryId,
        [FromBody] UpdateWasteCategoryRequest request,
        CancellationToken ct = default)
    {
        if (_updateValidator != null)
        {
            var validation = await _updateValidator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    "Dữ liệu cập nhật danh mục rác chưa hợp lệ.",
                    "MSG-GEN-01",
                    validation.Errors.Select(e => e.ErrorMessage).ToList()));
            }
        }

        var adminId = GetCurrentUserId();
        var dto = await _service.UpdateWasteCategoryAsync(adminId, categoryId, request, ct);
        return Ok(ApiResponse<WasteCategoryDto>.Ok(
            dto,
            message: $"Đã cập nhật danh mục rác '{dto.Code}'.",
            messageCode: "MSG-SRT-UPDATED"));
    }

    /// <summary>
    /// [STT 029] Vô hiệu hóa mềm (Inactive) hoặc mở lại (Active) danh mục phế liệu mà vẫn bảo toàn biên nhận lịch sử (A-05, BR-SRT-04).
    /// </summary>
    [HttpPatch("{categoryId:guid}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<WasteCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleCategoryStatus(
        Guid categoryId,
        [FromBody] ToggleWasteCategoryStatusRequest request,
        CancellationToken ct = default)
    {
        if (_statusValidator != null)
        {
            var validation = await _statusValidator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    "Trạng thái cập nhật không hợp lệ.",
                    "MSG-GEN-01",
                    validation.Errors.Select(e => e.ErrorMessage).ToList()));
            }
        }

        var adminId = GetCurrentUserId();
        var dto = await _service.ToggleWasteCategoryStatusAsync(adminId, categoryId, request, ct);
        return Ok(ApiResponse<WasteCategoryDto>.Ok(
            dto,
            message: $"Đã chuyển trạng thái danh mục '{dto.Code}' thành {dto.Status}.",
            messageCode: "MSG-SRT-STATUS-UPDATED"));
    }

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? User.FindFirstValue("userId");

        if (!Guid.TryParse(raw, out var userId))
        {
            throw new UnauthorizedAccessException("Không xác định được danh tính quản trị viên.");
        }

        return userId;
    }
}
