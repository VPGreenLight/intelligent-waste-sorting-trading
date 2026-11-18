using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Điểm danh (U-13: Citizen &amp; Recycler), cây ảo (U-15) và Eco Fact đã mở khóa (chỉ Citizen, BR-GAM-08).</summary>
[ApiController]
[Produces("application/json")]
public class UserGamificationController : ControllerBase
{
    private readonly ICheckinService _checkin;
    private readonly IVirtualTreeService _tree;
    private readonly ICurrentUserService _currentUser;

    public UserGamificationController(ICheckinService checkin, IVirtualTreeService tree, ICurrentUserService currentUser)
    {
        _checkin = checkin;
        _tree = tree;
        _currentUser = currentUser;
    }

    private Guid UserId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>[STT 078] Trạng thái điểm danh: lịch 7 ngày, chuỗi hiện tại, điểm thưởng hôm nay, cảnh báo chuỗi sẽ bị reset.</summary>
    [HttpGet("api/v1/gamification/check-in/status")]
    [HttpGet("api/v1/check-in")]
    [Authorize(Roles = "User,Citizen,Recycler,Collector")]
    [ProducesResponseType(typeof(ApiResponse<CheckinStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckinStatus(CancellationToken ct) =>
        Ok(ApiResponse<CheckinStatusDto>.Ok(await _checkin.GetStatusAsync(UserId, ct)));

    /// <summary>[STT 079] Điểm danh hôm nay (một lần mỗi ngày theo giờ ICT).</summary>
    [HttpPost("api/v1/gamification/check-in")]
    [HttpPost("api/v1/check-in")]
    [Authorize(Roles = "User,Citizen,Recycler,Collector")]
    [ProducesResponseType(typeof(ApiResponse<CheckinResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckIn(CancellationToken ct)
    {
        var data = await _checkin.CheckInAsync(UserId, ct);
        return Ok(ApiResponse<CheckinResultDto>.Ok(data,
            $"Điểm danh thành công! Bạn nhận được {data.PointsAwarded} điểm (chuỗi: {data.StreakCount} ngày).", "MSG-GAM-08"));
    }

    [HttpGet("api/v1/gamification/tree/me")]
    [HttpGet("api/v1/virtual-tree")]
    [Authorize(Roles = "User,Citizen")]
    [ProducesResponseType(typeof(ApiResponse<VirtualTreeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Tree(CancellationToken ct) =>
        Ok(ApiResponse<VirtualTreeDto>.Ok(await _tree.GetAsync(UserId, ct)));

    /// <summary>[STT 085] Tưới &amp; nâng cấp cây: trả điểm theo ngưỡng cấp kế tiếp, lên một cấp, trả về Eco Fact vừa mở khóa (MSG-GAM-06 / MSG-GAM-07).</summary>
    [HttpPost("api/v1/gamification/tree/upgrade")]
    [HttpPost("api/v1/virtual-tree/water")]
    [Authorize(Roles = "User,Citizen")]
    [ProducesResponseType(typeof(ApiResponse<WaterTreeResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Water(CancellationToken ct)
    {
        var data = await _tree.WaterAndUpgradeAsync(UserId, ct);
        return Ok(ApiResponse<WaterTreeResultDto>.Ok(data, "Cây của bạn đã lên cấp mới!", "MSG-GAM-06"));
    }

    /// <summary>[STT 086] Thư viện kiến thức: các Eco Fact đã mở khóa theo cấp cây hiện tại.</summary>
    [HttpGet("api/v1/gamification/eco-facts/me")]
    [HttpGet("api/v1/eco-facts")]
    [Authorize(Roles = "User,Citizen")]
    [ProducesResponseType(typeof(ApiResponse<List<EcoFactDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Facts(CancellationToken ct) =>
        Ok(ApiResponse<List<EcoFactDto>>.Ok(await _tree.ListUnlockedFactsAsync(UserId, ct)));

    [HttpGet("api/v1/gamification/eco-facts/{id:guid}")]
    [HttpGet("api/v1/eco-facts/{id:guid}")]
    [Authorize(Roles = "User,Citizen")]
    [ProducesResponseType(typeof(ApiResponse<EcoFactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Fact(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<EcoFactDto>.Ok(await _tree.GetUnlockedFactAsync(UserId, id, ct)));
}
