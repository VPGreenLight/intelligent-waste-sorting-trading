using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Danh mục gói dịch vụ nội bộ, thuê bao người dùng và cấu hình biểu phí Admin (M-12 / FE-14: U-16, R-07, A-10).
/// Bao phủ STT 120, 121, 123, 124, 125, 126 trong 00_api_inventory_and_endpoint_statistics.md.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
[Produces("application/json")]
public class ServicePackagesController : ControllerBase
{
    private readonly IServicePackageService _packageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<UpsertServicePackageRequest> _upsertValidator;

    public ServicePackagesController(
        IServicePackageService packageService,
        ICurrentUserService currentUserService,
        IValidator<UpsertServicePackageRequest> upsertValidator)
    {
        _packageService = packageService;
        _currentUserService = currentUserService;
        _upsertValidator = upsertValidator;
    }

    /// <summary>
    /// [STT 120] Lấy bảng giá 3 gói dịch vụ (Basic - Premium - Pro) đang mở bán kèm trạng thái đủ điều kiện KYB/Vai trò.
    /// </summary>
    [HttpGet("service-packages")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ServicePackageCatalogDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ServicePackageCatalogDto>>>> GetAvailablePackages(
        [FromQuery] string? targetRole = null,
        CancellationToken ct = default)
    {
        var (userId, role) = ResolveCallerIdentity();
        var packages = await _packageService.GetAvailablePackagesAsync(userId, role, targetRole, ct);

        return Ok(ApiResponse<IReadOnlyList<ServicePackageCatalogDto>>.Ok(
            packages,
            "Tải danh mục gói dịch vụ thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 121] Xem bậc thuê bao hiện tại, các đặc quyền đang hoạt động, thời hạn còn lại và lịch sử gia hạn (BR-PAY-04).
    /// </summary>
    [HttpGet("service-packages/subscriptions/me")]
    [ProducesResponseType(typeof(ApiResponse<MySubscriptionsResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MySubscriptionsResponseDto>>> GetMySubscriptions(CancellationToken ct)
    {
        var (userId, _) = ResolveCallerIdentity();
        var subs = await _packageService.GetMySubscriptionsAsync(userId, ct);

        return Ok(ApiResponse<MySubscriptionsResponseDto>.Ok(
            subs,
            "Lấy thông tin gói đặc quyền cá nhân thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 123] [A-10] Quản trị viên tra cứu toàn bộ danh mục gói dịch vụ thương mại, biểu phí và thống kê thuê bao.
    /// </summary>
    [HttpGet("admin/service-packages")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminServicePackageDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminServicePackageDto>>>> GetAllServicePackages(
        [FromQuery] string? search = null,
        [FromQuery] string? targetRole = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var list = await _packageService.GetAllPackagesForAdminAsync(search, targetRole, isActive, ct);
        return Ok(ApiResponse<IReadOnlyList<AdminServicePackageDto>>.Ok(
            list,
            "Lấy danh sách cấu hình gói dịch vụ thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 124] [A-10] Quản trị viên tạo gói dịch vụ mới (BR-PAY-05, BR-ADM-05).
    /// </summary>
    [HttpPost("admin/service-packages")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<AdminServicePackageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AdminServicePackageDto>>> CreateServicePackage(
        [FromBody] UpsertServicePackageRequest request,
        CancellationToken ct)
    {
        var validation = await _upsertValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu cấu hình gói dịch vụ không hợp lệ.", "MSG-GEN-01", errors));
        }

        var (adminId, _) = ResolveCallerIdentity();
        var created = await _packageService.CreatePackageAsync(adminId, request, ct);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<AdminServicePackageDto>.Ok(
            created,
            "Tạo gói dịch vụ thành công.",
            "MSG-PAY-07"));
    }

    /// <summary>
    /// [STT 125] [A-10] Quản trị viên cập nhật đơn giá và chu kỳ gói dịch vụ cho lượt đăng ký mới (giữ nguyên giá thuê bao cũ theo BR-PAY-05).
    /// </summary>
    [HttpPut("admin/service-packages/{packageId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<AdminServicePackageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AdminServicePackageDto>>> UpdateServicePackage(
        Guid packageId,
        [FromBody] UpsertServicePackageRequest request,
        CancellationToken ct)
    {
        var validation = await _upsertValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu cập nhật gói dịch vụ không hợp lệ.", "MSG-GEN-01", errors));
        }

        var (adminId, _) = ResolveCallerIdentity();
        var updated = await _packageService.UpdatePackageAsync(adminId, packageId, request, ct);

        return Ok(ApiResponse<AdminServicePackageDto>.Ok(
            updated,
            "Cập nhật cấu hình gói dịch vụ thành công.",
            "MSG-PAY-07"));
    }

    /// <summary>
    /// [STT 126] [A-10] Mở bán công khai hoặc ngừng cung cấp gói dịch vụ (khóa tắt gói mặc định BASIC).
    /// </summary>
    [HttpPatch("admin/service-packages/{packageId:guid}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<AdminServicePackageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AdminServicePackageDto>>> TogglePackageStatus(
        Guid packageId,
        [FromBody] ToggleServicePackageStatusRequest request,
        CancellationToken ct)
    {
        var (adminId, _) = ResolveCallerIdentity();
        var updated = await _packageService.TogglePackageStatusAsync(adminId, packageId, request, ct);

        return Ok(ApiResponse<AdminServicePackageDto>.Ok(
            updated,
            "Cập nhật trạng thái mở bán gói dịch vụ thành công.",
            "MSG-PAY-07"));
    }

    private (Guid UserId, UserRole Role) ResolveCallerIdentity()
    {
        var userId = _currentUserService.UserId
                     ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");

        var roleStr = _currentUserService.Role ?? nameof(UserRole.User);
        if (!Enum.TryParse<UserRole>(roleStr, true, out var role))
        {
            role = UserRole.User;
        }

        return (userId, role);
    }
}
