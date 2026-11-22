using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Quản trị và công bố Điều khoản dịch vụ, Chính sách môi trường &amp; Quyền riêng tư (/terms).
/// </summary>
[ApiController]
[Produces("application/json")]
public class PlatformPoliciesController : ControllerBase
{
    private readonly IPlatformPolicyService _policyService;

    public PlatformPoliciesController(IPlatformPolicyService policyService)
    {
        _policyService = policyService;
    }

    /// <summary>
    /// Lấy danh sách điều khoản/chính sách đang hoạt động (ACTIVE) hiển thị công khai trên trang /terms.
    /// </summary>
    [HttpGet("api/v1/policies")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlatformPolicySectionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicPolicies(
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        var list = await _policyService.GetPublicPoliciesAsync(category, ct);
        return Ok(ApiResponse<IReadOnlyList<PlatformPolicySectionDto>>.Ok(
            list,
            message: "Tải danh sách điều khoản và chính sách thành công.",
            messageCode: "MSG-POL-LIST-OK"));
    }

    /// <summary>
    /// Admin / Staff xem toàn bộ điều khoản và chính sách (bao gồm DRAFT và INACTIVE).
    /// </summary>
    [HttpGet("api/v1/admin/policies")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlatformPolicySectionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminPolicies(
        [FromQuery] string? category = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var list = await _policyService.GetAdminPoliciesAsync(category, status, search, ct);
        return Ok(ApiResponse<IReadOnlyList<PlatformPolicySectionDto>>.Ok(
            list,
            message: "Tải danh sách quản trị điều khoản thành công.",
            messageCode: "MSG-POL-LIST-OK"));
    }

    /// <summary>
    /// Admin tạo mới mục điều khoản hoặc chính sách nền tảng.
    /// </summary>
    [HttpPost("api/v1/admin/policies")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<PlatformPolicySectionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePolicy(
        [FromBody] SavePlatformPolicyRequest request,
        CancellationToken ct = default)
    {
        var adminId = ResolveCurrentUserId();
        var created = await _policyService.CreatePolicyAsync(adminId, request, ct);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<PlatformPolicySectionDto>.Ok(
                created,
                message: $"Đã tạo điều khoản '{created.Code}' thành công.",
                messageCode: "MSG-POL-CREATED"));
    }

    /// <summary>
    /// Admin cập nhật nội dung, nhóm, thứ tự hoặc trạng thái mục điều khoản.
    /// </summary>
    [HttpPut("api/v1/admin/policies/{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<PlatformPolicySectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePolicy(
        Guid id,
        [FromBody] SavePlatformPolicyRequest request,
        CancellationToken ct = default)
    {
        var adminId = ResolveCurrentUserId();
        var updated = await _policyService.UpdatePolicyAsync(adminId, id, request, ct);
        return Ok(ApiResponse<PlatformPolicySectionDto>.Ok(
            updated,
            message: $"Đã cập nhật điều khoản '{updated.Code}'.",
            messageCode: "MSG-POL-UPDATED"));
    }

    /// <summary>
    /// Admin bật/tắt nhanh trạng thái ACTIVE / DRAFT / INACTIVE cho mục điều khoản.
    /// </summary>
    [HttpPatch("api/v1/admin/policies/{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<PlatformPolicySectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPolicyStatus(
        Guid id,
        [FromBody] SetPlatformPolicyStatusRequest request,
        CancellationToken ct = default)
    {
        var adminId = ResolveCurrentUserId();
        var updated = await _policyService.SetPolicyStatusAsync(adminId, id, request.Status, ct);
        return Ok(ApiResponse<PlatformPolicySectionDto>.Ok(
            updated,
            message: $"Đã chuyển trạng thái điều khoản '{updated.Code}' thành {updated.Status}.",
            messageCode: "MSG-POL-STATUS-UPDATED"));
    }

    /// <summary>
    /// Admin xóa mục điều khoản khỏi hệ thống.
    /// </summary>
    [HttpDelete("api/v1/admin/policies/{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePolicy(Guid id, CancellationToken ct = default)
    {
        var adminId = ResolveCurrentUserId();
        await _policyService.DeletePolicyAsync(adminId, id, ct);
        return Ok(ApiResponse<object>.Ok(
            new { id },
            message: "Đã xóa mục điều khoản thành công.",
            messageCode: "MSG-POL-DELETED"));
    }

    private Guid ResolveCurrentUserId()
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
