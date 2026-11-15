using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Phân hệ Quản lý Nhân sự Vận hành &amp; Phân quyền RBAC chi tiết dành cho Admin (FE-17 / M-15: UC A-03, A-04).
/// Bao phủ các endpoint STT 158 - STT 164 trong 00_api_inventory_and_endpoint_statistics.md.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminStaffController : ControllerBase
{
    private readonly IStaffManagementService _staffService;
    private readonly IValidator<CreateStaffAccountRequest> _createValidator;
    private readonly IValidator<UpdateStaffAccountRequest> _updateValidator;
    private readonly IValidator<UpdateStaffStatusRequest> _statusValidator;
    private readonly IValidator<UpdateStaffPermissionsRequest> _permissionsValidator;

    public AdminStaffController(
        IStaffManagementService staffService,
        IValidator<CreateStaffAccountRequest> createValidator,
        IValidator<UpdateStaffAccountRequest> updateValidator,
        IValidator<UpdateStaffStatusRequest> statusValidator,
        IValidator<UpdateStaffPermissionsRequest> permissionsValidator)
    {
        _staffService = staffService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
        _permissionsValidator = permissionsValidator;
    }

    /// <summary>
    /// [STT 158] [A-03] Quản trị viên tra cứu danh sách tài khoản Nhân viên vận hành (Staff), trạng thái công tác và danh sách quyền hiện có.
    /// </summary>
    [HttpGet("staff")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffAccountDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StaffAccountDto>>>> GetStaffAccounts(
        [FromQuery] StaffAccountFilterRequest filter,
        CancellationToken ct)
    {
        var list = await _staffService.GetStaffAccountsAsync(filter, ct);
        return Ok(ApiResponse<IReadOnlyList<StaffAccountDto>>.Ok(
            list,
            "Lấy danh sách nhân viên vận hành thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 159] [A-03] Quản trị viên tạo mới tài khoản Nhân viên vận hành nội bộ (chỉ Admin được phép tạo theo BR-ADM-01).
    /// </summary>
    [HttpPost("staff")]
    [ProducesResponseType(typeof(ApiResponse<StaffAccountDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StaffAccountDto>>> CreateStaffAccount(
        [FromBody] CreateStaffAccountRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu tạo tài khoản nhân viên không hợp lệ.", "MSG-GEN-01", errors));
        }

        var adminId = GetCurrentUserId();
        var created = await _staffService.CreateStaffAccountAsync(adminId, request, ct);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<StaffAccountDto>.Ok(
            created,
            $"Đã tạo tài khoản nhân viên {created.StaffCode} thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 160] [A-03] Quản trị viên cập nhật thông tin nhân sự, bộ phận chuyên trách hoặc cấp lại mật khẩu tạm cho Nhân viên.
    /// </summary>
    [HttpPut("staff/{staffId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StaffAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StaffAccountDto>>> UpdateStaffAccount(
        Guid staffId,
        [FromBody] UpdateStaffAccountRequest request,
        CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu cập nhật nhân viên không hợp lệ.", "MSG-GEN-01", errors));
        }

        var adminId = GetCurrentUserId();
        var updated = await _staffService.UpdateStaffAccountAsync(adminId, staffId, request, ct);

        return Ok(ApiResponse<StaffAccountDto>.Ok(
            updated,
            "Cập nhật thông tin nhân viên thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 161] [A-03] Đình chỉ, ngừng kích hoạt hoặc mở lại tài khoản Nhân viên kèm lý do bắt buộc và hủy phiên truy cập (BR-ADM-03).
    /// </summary>
    [HttpPatch("staff/{staffId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<StaffAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<StaffAccountDto>>> UpdateStaffStatus(
        Guid staffId,
        [FromBody] UpdateStaffStatusRequest request,
        CancellationToken ct)
    {
        var validation = await _statusValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu trạng thái nhân sự không hợp lệ.", "MSG-GEN-01", errors));
        }

        var adminId = GetCurrentUserId();
        var updated = await _staffService.UpdateStaffStatusAsync(adminId, staffId, request, ct);

        return Ok(ApiResponse<StaffAccountDto>.Ok(
            updated,
            "Trạng thái tài khoản đã được cập nhật và người dùng đã được thông báo.",
            "MSG-ADM-03"));
    }

    /// <summary>
    /// [STT 162] [A-04] Lấy danh mục chuẩn toàn bộ mã quyền RBAC nghiệp vụ của Nhân viên vận hành.
    /// </summary>
    [HttpGet("permissions/catalog")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PermissionCatalogItemDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<IReadOnlyList<PermissionCatalogItemDto>>> GetPermissionCatalog()
    {
        var catalog = _staffService.GetPermissionCatalog();
        return Ok(ApiResponse<IReadOnlyList<PermissionCatalogItemDto>>.Ok(
            catalog,
            "Lấy danh mục quyền chuẩn RBAC thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 163] [A-04] Xem chi tiết tập quyền RBAC hiện đang được gán cho một tài khoản Nhân viên cụ thể.
    /// </summary>
    [HttpGet("staff/{staffId:guid}/permissions")]
    [ProducesResponseType(typeof(ApiResponse<StaffPermissionsDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StaffPermissionsDetailDto>>> GetStaffPermissions(
        Guid staffId,
        CancellationToken ct)
    {
        var detail = await _staffService.GetStaffPermissionsAsync(staffId, ct);
        return Ok(ApiResponse<StaffPermissionsDetailDto>.Ok(
            detail,
            "Lấy thông tin phân quyền nhân viên thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 164] [A-04] Cấu hình cấp mới hoặc thu hồi các mã quyền RBAC cho Nhân viên và áp dụng kiểm soát truy cập tức thời (BR-ADM-02, MSG-ADM-01).
    /// </summary>
    [HttpPut("staff/{staffId:guid}/permissions")]
    [ProducesResponseType(typeof(ApiResponse<StaffAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StaffAccountDto>>> UpdateStaffPermissions(
        Guid staffId,
        [FromBody] UpdateStaffPermissionsRequest request,
        CancellationToken ct)
    {
        var validation = await _permissionsValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Danh sách mã quyền cập nhật không hợp lệ.", "MSG-GEN-01", errors));
        }

        var adminId = GetCurrentUserId();
        var updated = await _staffService.UpdateStaffPermissionsAsync(adminId, staffId, request, ct);

        return Ok(ApiResponse<StaffAccountDto>.Ok(
            updated,
            "Cập nhật quyền nhân viên thành công.",
            "MSG-ADM-01"));
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Không xác định được định danh quản trị viên từ Token.");
        }
        return userId;
    }
}
