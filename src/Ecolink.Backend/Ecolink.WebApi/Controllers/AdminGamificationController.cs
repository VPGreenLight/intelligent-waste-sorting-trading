using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Admin cấu hình gamification (A-07) &amp; chu kỳ bảng xếp hạng (A-08). Mọi thay đổi ghi audit log (BR-ADM-05).</summary>
[ApiController]
[Route("api/v1/admin/gamification")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminGamificationController : ControllerBase
{
    private readonly IGamificationConfigService _config;
    private readonly ILeaderboardService _leaderboard;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<GamificationConfigDto> _configValidator;
    private readonly IValidator<SaveLeaderboardCycleRequest> _cycleValidator;
    private readonly IPointsService? _points;
    private readonly IAuditLogService? _auditLog;

    public AdminGamificationController(
        IGamificationConfigService config,
        ILeaderboardService leaderboard,
        ICurrentUserService currentUser,
        IValidator<GamificationConfigDto> configValidator,
        IValidator<SaveLeaderboardCycleRequest> cycleValidator,
        IPointsService? points = null,
        IAuditLogService? auditLog = null)
    {
        _config = config;
        _leaderboard = leaderboard;
        _currentUser = currentUser;
        _configValidator = configValidator;
        _cycleValidator = cycleValidator;
        _points = points;
        _auditLog = auditLog;
    }

    private Guid AdminId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    [HttpGet("config")]
    [ProducesResponseType(typeof(ApiResponse<GamificationConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(ApiResponse<GamificationConfigDto>.Ok(await _config.GetAsync(ct)));

    /// <summary>Lưu toàn bộ cấu hình (thay thế). Điểm không âm; cấp cây liên tục từ 1, ngưỡng tăng dần nghiêm ngặt.</summary>
    [HttpPut("config")]
    [ProducesResponseType(typeof(ApiResponse<GamificationConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update([FromBody] GamificationConfigDto request, CancellationToken ct)
    {
        var invalid = await ValidateAsync(_configValidator, request, ct);
        if (invalid != null) return invalid;
        return Ok(ApiResponse<GamificationConfigDto>.Ok(await _config.UpdateAsync(AdminId, request, ct), "Đã lưu cấu hình gamification."));
    }

    /// <summary>Khôi phục tham số mặc định.</summary>
    [HttpPost("config/reset")]
    [ProducesResponseType(typeof(ApiResponse<GamificationConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reset(CancellationToken ct) =>
        Ok(ApiResponse<GamificationConfigDto>.Ok(await _config.ResetToDefaultsAsync(AdminId, ct), "Đã khôi phục cấu hình mặc định."));

    // ============================================================
    // Chu kỳ Bảng xếp hạng (A-08: STT 100–102)
    // ============================================================

    /// <summary>[STT 100] [A-08] Danh sách các chu kỳ thi đua Bảng xếp hạng (Tuần / Tháng / Quý).</summary>
    [HttpGet("leaderboard-cycles")]
    [ProducesResponseType(typeof(ApiResponse<List<LeaderboardCycleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCycles(CancellationToken ct) =>
        Ok(ApiResponse<List<LeaderboardCycleDto>>.Ok(await _leaderboard.GetCyclesAsync(ct)));

    /// <summary>[STT 101] [A-08] Khởi tạo chu kỳ Bảng xếp hạng mới kèm mức thưởng Top 1/2/3.</summary>
    [HttpPost("leaderboard-cycles")]
    [ProducesResponseType(typeof(ApiResponse<LeaderboardCycleDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCycle([FromBody] SaveLeaderboardCycleRequest request, CancellationToken ct)
    {
        var invalid = await ValidateAsync(_cycleValidator, request, ct);
        if (invalid != null) return invalid;

        var created = await _leaderboard.CreateCycleAsync(AdminId, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<LeaderboardCycleDto>.Ok(created, "Đã tạo chu kỳ bảng xếp hạng mới."));
    }

    /// <summary>Cập nhật thông tin chu kỳ bảng xếp hạng chưa chốt.</summary>
    [HttpPut("leaderboard-cycles/{cycleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LeaderboardCycleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCycle(Guid cycleId, [FromBody] SaveLeaderboardCycleRequest request, CancellationToken ct)
    {
        var invalid = await ValidateAsync(_cycleValidator, request, ct);
        if (invalid != null) return invalid;

        return Ok(ApiResponse<LeaderboardCycleDto>.Ok(
            await _leaderboard.UpdateCycleAsync(AdminId, cycleId, request, ct),
            "Đã cập nhật chu kỳ bảng xếp hạng."));
    }

    private async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (validation.IsValid) return null;

        var errors = validation.Errors
            .Select(e => string.IsNullOrWhiteSpace(e.PropertyName) ? e.ErrorMessage : $"{e.PropertyName}: {e.ErrorMessage}")
            .ToList();
        return BadRequest(ApiResponse<object>.Fail(validation.Errors[0].ErrorMessage, "MSG-AUTH-02", errors));
    }

    /// <summary>Chuyển chu kỳ đang bật (chỉ một chu kỳ được bật tại một thời điểm).</summary>
    [HttpPatch("leaderboard-cycles/{cycleId:guid}/activate")]
    [HttpPost("leaderboard-cycles/{cycleId:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<LeaderboardCycleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateCycle(Guid cycleId, CancellationToken ct) =>
        Ok(ApiResponse<LeaderboardCycleDto>.Ok(
            await _leaderboard.ActivateCycleAsync(AdminId, cycleId, ct),
            "Đã chuyển chu kỳ bảng xếp hạng đang áp dụng."));

    /// <summary>[STT 102] [A-08] Chốt bảng tổng sắp cuối kỳ và tự động cộng điểm thưởng cho Top 3.</summary>
    [HttpPost("leaderboard-cycles/{cycleId:guid}/finalize")]
    [ProducesResponseType(typeof(ApiResponse<LeaderboardCycleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FinalizeCycle(Guid cycleId, CancellationToken ct) =>
        Ok(ApiResponse<LeaderboardCycleDto>.Ok(
            await _leaderboard.FinalizeCycleAsync(AdminId, cycleId, ct),
            "Đã chốt chu kỳ bảng xếp hạng và trao thưởng Top dẫn đầu."));

    /// <summary>Admin cấp điểm điều chỉnh thủ công cho một tài khoản (không thông qua giao dịch bàn giao).</summary>
    [HttpPost("points/grant")]
    [ProducesResponseType(typeof(ApiResponse<WalletDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GrantPoints([FromBody] AdminGrantPointsRequest request, CancellationToken ct)
    {
        if (request == null || request.UserId == Guid.Empty || request.Points <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("UserId và số điểm cấp phát phải lớn hơn 0.", "MSG-AUTH-02"));
        }
        if (_points == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Dịch vụ điểm thưởng chưa khả dụng.", "MSG-GEN-01"));
        }

        var desc = string.IsNullOrWhiteSpace(request.Description)
            ? "Cấp điểm điều chỉnh hệ thống"
            : request.Description.Trim();

        await _points.CreditAsync(
            request.UserId,
            request.Points,
            PointActionType.Adjustment,
            "AdminGrant",
            request.UserId.ToString(),
            $"ADMIN_GRANT:{Guid.NewGuid():N}",
            desc,
            ct);

        if (_auditLog != null)
        {
            await _auditLog.LogAsync(
                action: "ADMIN_POINTS_GRANT",
                entityType: "UserPointWallet",
                entityId: request.UserId.ToString(),
                oldValues: null,
                newValues: new { request.UserId, request.Points, Description = desc },
                explicitActorId: AdminId,
                ct: ct);
        }

        var wallet = await _points.GetWalletAsync(request.UserId, ct);
        return Ok(ApiResponse<WalletDto>.Ok(wallet, "MSG-GAM-01", "Đã cấp điểm điều chỉnh thành công."));
    }
}
