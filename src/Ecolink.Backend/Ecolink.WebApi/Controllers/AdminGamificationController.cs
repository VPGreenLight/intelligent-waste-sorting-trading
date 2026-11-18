using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Admin cấu hình gamification (A-07). Mọi thay đổi ghi audit log (BR-ADM-05).</summary>
[ApiController]
[Route("api/v1/admin/gamification/config")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminGamificationController : ControllerBase
{
    private readonly IGamificationConfigService _config;
    private readonly ICurrentUserService _currentUser;

    public AdminGamificationController(IGamificationConfigService config, ICurrentUserService currentUser)
    {
        _config = config;
        _currentUser = currentUser;
    }

    private Guid AdminId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GamificationConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(ApiResponse<GamificationConfigDto>.Ok(await _config.GetAsync(ct)));

    /// <summary>Lưu toàn bộ cấu hình (thay thế). Điểm không âm; cấp cây liên tục từ 1, ngưỡng tăng dần nghiêm ngặt.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<GamificationConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update([FromBody] GamificationConfigDto request, CancellationToken ct) =>
        Ok(ApiResponse<GamificationConfigDto>.Ok(await _config.UpdateAsync(AdminId, request, ct), "Đã lưu cấu hình gamification."));

    /// <summary>Khôi phục tham số mặc định.</summary>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(ApiResponse<GamificationConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reset(CancellationToken ct) =>
        Ok(ApiResponse<GamificationConfigDto>.Ok(await _config.ResetToDefaultsAsync(AdminId, ct), "Đã khôi phục cấu hình mặc định."));
}
