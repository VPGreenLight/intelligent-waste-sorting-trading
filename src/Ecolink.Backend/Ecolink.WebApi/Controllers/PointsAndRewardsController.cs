using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Ecolink.WebApi.Controllers;

/// <summary>Ví điểm, lịch sử điểm và cửa hàng đổi quà (U-14). Dùng cho cả User và Recycler (BR-GAM-08).</summary>
[ApiController]
[Authorize(Roles = "User,Citizen,Recycler,Collector")]
[Produces("application/json")]
public class PointsAndRewardsController : ControllerBase
{
    private readonly IPointsService _points;
    private readonly IGiftService _gifts;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<RedeemGiftRequest> _redeemValidator;
    private readonly IValidator<CreateRedemptionRequest> _createRedemptionValidator;

    public PointsAndRewardsController(
        IPointsService points,
        IGiftService gifts,
        ICurrentUserService currentUser,
        IValidator<RedeemGiftRequest> redeemValidator,
        IValidator<CreateRedemptionRequest> createRedemptionValidator)
    {
        _points = points;
        _gifts = gifts;
        _currentUser = currentUser;
        _redeemValidator = redeemValidator;
        _createRedemptionValidator = createRedemptionValidator;
    }

    private Guid UserId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Số dư điểm hiện tại.</summary>
    [HttpGet("api/v1/gamification/points/wallet")]
    [HttpGet("api/v1/points/wallet")]
    [ProducesResponseType(typeof(ApiResponse<WalletDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Wallet(CancellationToken ct) =>
        Ok(ApiResponse<WalletDto>.Ok(await _points.GetWalletAsync(UserId, ct)));

    /// <summary>[STT 080] Lịch sử biến động điểm (lọc theo thời gian, loại sự kiện).</summary>
    [HttpGet("api/v1/gamification/points/ledger")]
    [HttpGet("api/v1/points/transactions")]
    [ProducesResponseType(typeof(ApiResponse<GamPagedResult<PointTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Transactions([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? actionType, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<GamPagedResult<PointTransactionDto>>.Ok(
            await _points.GetTransactionsAsync(UserId, from, to, actionType, page, pageSize, ct)));

    /// <summary>[STT 081] Danh mục quà đang mở bán cho công chúng &amp; người dùng. type = Physical | Voucher; sort = points_asc | points_desc | newest.</summary>
    [HttpGet("api/v1/gamification/gifts")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<GiftDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublicGifts([FromQuery] string? type, [FromQuery] string? sort, [FromQuery] string? search, CancellationToken ct) =>
        Ok(ApiResponse<List<GiftDto>>.Ok(await _gifts.ListStoreAsync(type, sort, search, ct)));

    /// <summary>[STT 081] Danh mục quà đang mở bán (phiên bản yêu cầu đăng nhập Citizen/Recycler). type = Physical | Voucher; sort = points_asc | points_desc | newest.</summary>
    [HttpGet("api/v1/gifts")]
    [ProducesResponseType(typeof(ApiResponse<List<GiftDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Gifts([FromQuery] string? type, [FromQuery] string? sort, [FromQuery] string? search, CancellationToken ct) =>
        Ok(ApiResponse<List<GiftDto>>.Ok(await _gifts.ListStoreAsync(type, sort, search, ct)));

    [HttpGet("api/v1/gamification/gifts/{id:guid}")]
    [HttpGet("api/v1/gifts/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GiftDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Gift(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<GiftDto>.Ok(await _gifts.GetStoreGiftAsync(id, ct)));

    /// <summary>Đổi quà: trừ điểm + trừ tồn kho trong một transaction. Voucher trả mã ngay; quà hiện vật cần địa chỉ giao hàng.</summary>
    [HttpPost("api/v1/gifts/{id:guid}/redeem")]
    [ProducesResponseType(typeof(ApiResponse<RedeemResultDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Redeem(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RedeemGiftRequest? request = null,
        CancellationToken ct = default)
    {
        var effective = request ?? new RedeemGiftRequest();
        var invalid = await ValidateAsync(_redeemValidator, effective, ct);
        if (invalid != null) return invalid;

        var data = await _gifts.RedeemAsync(UserId, id, effective, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<RedeemResultDto>.Ok(data, "Đổi quà thành công.", "MSG-GAM-02"));
    }

    /// <summary>[STT 082] Đổi quà nhận giftId từ body request.</summary>
    [HttpPost("api/v1/gamification/redemptions")]
    [ProducesResponseType(typeof(ApiResponse<RedeemResultDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> RedeemFromBody([FromBody] CreateRedemptionRequest request, CancellationToken ct)
    {
        var invalid = await ValidateAsync(_createRedemptionValidator, request, ct);
        if (invalid != null) return invalid;

        var data = await _gifts.RedeemAsync(UserId, request.GiftId, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<RedeemResultDto>.Ok(data, "Đổi quà thành công.", "MSG-GAM-02"));
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

    [HttpGet("api/v1/gamification/redemptions/me")]
    [HttpGet("api/v1/gift-redemptions")]
    [ProducesResponseType(typeof(ApiResponse<GamPagedResult<RedemptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MyRedemptions([FromQuery] string? status, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<GamPagedResult<RedemptionDto>>.Ok(await _gifts.ListMyRedemptionsAsync(UserId, status, page, pageSize, ct)));

    [HttpGet("api/v1/gamification/redemptions/{id:guid}")]
    [HttpGet("api/v1/gift-redemptions/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RedemptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MyRedemption(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<RedemptionDto>.Ok(await _gifts.GetMyRedemptionAsync(UserId, id, ct)));
}
