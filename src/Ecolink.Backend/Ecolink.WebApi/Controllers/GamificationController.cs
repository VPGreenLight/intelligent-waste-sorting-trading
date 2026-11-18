using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Hồ sơ Sống Xanh (STT 076) và Bảng xếp hạng công khai theo chu kỳ (STT 077).</summary>
[ApiController]
[Route("api/v1/gamification")]
[Produces("application/json")]
public class GamificationController : ControllerBase
{
    private readonly IGamificationService _gamificationService;
    private readonly ILeaderboardService _leaderboardService;
    private readonly ICurrentUserService _currentUser;

    public GamificationController(
        IGamificationService gamificationService,
        ILeaderboardService leaderboardService,
        ICurrentUserService currentUser)
    {
        _gamificationService = gamificationService;
        _leaderboardService = leaderboardService;
        _currentUser = currentUser;
    }

    /// <summary>Hồ sơ Sống Xanh cá nhân của tài khoản đang đăng nhập.</summary>
    [HttpGet("profile/me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<EcoProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var userId = _currentUser.UserId
                     ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
        var profile = await _gamificationService.GetUserProfileAsync(userId, ct);
        return Ok(ApiResponse<EcoProfileDto>.Ok(profile));
    }

    /// <summary>[STT 076] Lấy thông tin chỉ số sống xanh, cấp độ cây ảo và điểm tích lũy của người dùng từ dữ liệu thật.</summary>
    [HttpGet("profile/{userId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<EcoProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(Guid userId, CancellationToken ct)
    {
        var profile = await _gamificationService.GetUserProfileAsync(userId, ct);
        return Ok(ApiResponse<EcoProfileDto>.Ok(profile));
    }

    /// <summary>[STT 077] Xem bảng xếp hạng đóng góp sống xanh theo chu kỳ đang bật hoặc kỳ chỉ định.</summary>
    [HttpGet("leaderboard")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LeaderboardViewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery] int top = 10,
        [FromQuery] string? period = null,
        [FromQuery] Guid? cycleId = null,
        CancellationToken ct = default)
    {
        var board = await _leaderboardService.GetLeaderboardViewAsync(top, period, cycleId, _currentUser.UserId, ct);
        return Ok(ApiResponse<LeaderboardViewDto>.Ok(board));
    }
}
