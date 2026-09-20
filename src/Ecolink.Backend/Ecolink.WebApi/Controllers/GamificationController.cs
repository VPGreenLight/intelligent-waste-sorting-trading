using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

[ApiController]
[Route("api/v1/gamification")]
public class GamificationController : ControllerBase
{
    private readonly IGamificationService _gamificationService;

    public GamificationController(IGamificationService gamificationService)
    {
        _gamificationService = gamificationService;
    }

    /// <summary>
    /// Lấy thông tin chỉ số sống xanh, cấp độ cây ảo và điểm tích lũy của người dùng (UC6, UC12)
    /// </summary>
    [HttpGet("profile/{userId:guid}")]
    public async Task<IActionResult> GetProfile(Guid userId, CancellationToken ct)
    {
        var profile = await _gamificationService.GetUserProfileAsync(userId, ct);
        return Ok(profile);
    }

    /// <summary>
    /// Xem bảng xếp hạng đóng góp sống xanh (UC23)
    /// </summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int top = 10, CancellationToken ct = default)
    {
        var board = await _gamificationService.GetLeaderboardAsync(top, ct);
        return Ok(board);
    }
}
