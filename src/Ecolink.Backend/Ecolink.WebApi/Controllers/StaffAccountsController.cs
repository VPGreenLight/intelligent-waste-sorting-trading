using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Constants;
using Ecolink.WebApi.Security;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Phân hệ Kiểm duyệt Tài khoản Người dân &amp; Cơ sở thu mua dành cho Staff/Admin (FE-17 / M-15: UC S-06, S-07).
/// Bao phủ các endpoint STT 154 - STT 157 trong 00_api_inventory_and_endpoint_statistics.md.
/// </summary>
[ApiController]
[Route("api/v1/staff/accounts")]
[HasPermission(StaffPermissions.ManageAccounts)]
[Produces("application/json")]
public class StaffAccountsController : ControllerBase
{
    private readonly IAccountGovernanceService _accountService;
    private readonly IValidator<LockOrUnlockAccountRequest> _lockValidator;
    private readonly IValidator<DeactivateAccountRequest> _deactivateValidator;

    public StaffAccountsController(
        IAccountGovernanceService accountService,
        IValidator<LockOrUnlockAccountRequest> lockValidator,
        IValidator<DeactivateAccountRequest> deactivateValidator)
    {
        _accountService = accountService;
        _lockValidator = lockValidator;
        _deactivateValidator = deactivateValidator;
    }

    /// <summary>
    /// [STT 154] [S-06] Nhân viên xem danh sách tài khoản Người dân và Cơ sở thu gom (lọc vai trò, trạng thái, số lần cảnh cáo).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ManagedAccountSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ManagedAccountSummaryDto>>>> GetAccounts(
        [FromQuery] AccountGovernanceFilterRequest filter,
        CancellationToken ct)
    {
        var result = await _accountService.GetAccountsAsync(filter, ct);
        return Ok(ApiResponse<PagedResult<ManagedAccountSummaryDto>>.Ok(
            result,
            "Lấy danh sách tài khoản thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 155] [S-06] Xem chi tiết hồ sơ tài khoản, thống kê giao dịch và nhật ký bị cảnh cáo/khóa trước đó.
    /// </summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ManagedAccountDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ManagedAccountDetailDto>>> GetAccountDetail(
        Guid userId,
        CancellationToken ct)
    {
        var detail = await _accountService.GetAccountDetailAsync(userId, ct);
        return Ok(ApiResponse<ManagedAccountDetailDto>.Ok(
            detail,
            "Lấy thông tin chi tiết tài khoản thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 156] [S-07] Tạm khóa (Suspended) hoặc mở khóa tài khoản vi phạm chính sách kèm lý do bắt buộc và thu hồi phiên đăng nhập (BR-ADM-03).
    /// </summary>
    [HttpPatch("{userId:guid}/lock")]
    [ProducesResponseType(typeof(ApiResponse<ManagedAccountSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<ManagedAccountSummaryDto>>> LockOrUnlockAccount(
        Guid userId,
        [FromBody] LockOrUnlockAccountRequest request,
        CancellationToken ct)
    {
        var validation = await _lockValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu kiểm duyệt tài khoản không hợp lệ.", "MSG-GEN-01", errors));
        }

        var actorId = GetCurrentUserId();
        var updated = await _accountService.LockOrUnlockAccountAsync(actorId, userId, request, ct);

        return Ok(ApiResponse<ManagedAccountSummaryDto>.Ok(
            updated,
            "Trạng thái tài khoản đã được cập nhật và người dùng đã được thông báo.",
            "MSG-ADM-03"));
    }

    /// <summary>
    /// [STT 157] [S-07] Vô hiệu hóa mềm (Soft-Deactivate) tài khoản vi phạm nghiêm trọng kèm biên bản lý do, bảo toàn biên nhận cũ (BR-ADM-04).
    /// </summary>
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ManagedAccountSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<ManagedAccountSummaryDto>>> DeactivateAccount(
        Guid userId,
        [FromBody] DeactivateAccountRequest request,
        CancellationToken ct)
    {
        var validation = await _deactivateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Biên bản lý do vô hiệu hóa không hợp lệ.", "MSG-GEN-01", errors));
        }

        var actorId = GetCurrentUserId();
        var updated = await _accountService.DeactivateAccountAsync(actorId, userId, request, ct);

        return Ok(ApiResponse<ManagedAccountSummaryDto>.Ok(
            updated,
            "Trạng thái tài khoản đã được cập nhật và người dùng đã được thông báo.",
            "MSG-ADM-03"));
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Không xác định được định danh nhân viên từ Token.");
        }
        return userId;
    }
}
