using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Staff: kho quà (S-10), Eco Fact (S-09), điểm danh ca làm (6.2). Vai trò Staff (BR-ADM-02).</summary>
[ApiController]
[Authorize(Roles = "Staff")]
[Produces("application/json")]
public class StaffGamificationController : ControllerBase
{
    private readonly IGiftService _gifts;
    private readonly IEcoFactService _facts;
    private readonly IStaffShiftService _shifts;
    private readonly ICurrentUserService _currentUser;

    public StaffGamificationController(IGiftService gifts, IEcoFactService facts, IStaffShiftService shifts, ICurrentUserService currentUser)
    {
        _gifts = gifts;
        _facts = facts;
        _shifts = shifts;
        _currentUser = currentUser;
    }

    private Guid StaffId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    // ---------------- Kho quà (S-10)
    [HttpGet("api/v1/staff/gifts")]
    [ProducesResponseType(typeof(ApiResponse<GamPagedResult<GiftDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Gifts([FromQuery] string? type, [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<GamPagedResult<GiftDto>>.Ok(await _gifts.ListInventoryAsync(type, status, search, page, pageSize, ct)));

    [HttpGet("api/v1/staff/gifts/{id:guid}")]
    public async Task<IActionResult> Gift(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<GiftDto>.Ok(await _gifts.GetInventoryGiftAsync(id, ct)));

    [HttpPost("api/v1/staff/gifts")]
    [ProducesResponseType(typeof(ApiResponse<GiftDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateGift([FromBody] SaveGiftRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<GiftDto>.Ok(await _gifts.CreateAsync(request, ct), "Đã thêm quà vào kho."));

    /// <summary>Sửa thông tin quà (tên, loại, mô tả, ảnh, điểm). Tồn kho đổi bằng /restock.</summary>
    [HttpPut("api/v1/staff/gifts/{id:guid}")]
    public async Task<IActionResult> UpdateGift(Guid id, [FromBody] SaveGiftRequest request, CancellationToken ct) =>
        Ok(ApiResponse<GiftDto>.Ok(await _gifts.UpdateAsync(id, request, ct), "Đã cập nhật quà."));

    [HttpPost("api/v1/staff/gifts/{id:guid}/restock")]
    public async Task<IActionResult> Restock(Guid id, [FromBody] RestockGiftRequest request, CancellationToken ct) =>
        Ok(ApiResponse<GiftDto>.Ok(await _gifts.RestockAsync(id, request.Quantity, ct), "Đã cập nhật tồn kho."));

    /// <summary>Đặt ACTIVE / INACTIVE. Quà INACTIVE hoặc hết hàng không thể đổi (MSG-GAM-04).</summary>
    [HttpPost("api/v1/staff/gifts/{id:guid}/status")]
    public async Task<IActionResult> GiftStatus(Guid id, [FromBody] SetStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<GiftDto>.Ok(await _gifts.SetStatusAsync(id, request.Status, ct), "Đã cập nhật trạng thái quà."));

    [HttpGet("api/v1/staff/gifts/{id:guid}/redemptions")]
    public async Task<IActionResult> GiftRedemptions(Guid id, [FromQuery] string? status, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<GamPagedResult<RedemptionDto>>.Ok(await _gifts.ListGiftRedemptionsAsync(id, status, page, pageSize, ct)));

    /// <summary>Xác nhận đã giao quà hiện vật / đã phát voucher cho người đổi.</summary>
    [HttpPost("api/v1/staff/gift-redemptions/{id:guid}/fulfill")]
    public async Task<IActionResult> Fulfill(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<RedemptionDto>.Ok(await _gifts.FulfillAsync(id, ct), "Đã xác nhận bàn giao quà."));

    // ---------------- Eco Fact (S-09)
    [HttpGet("api/v1/staff/eco-facts")]
    public async Task<IActionResult> Facts([FromQuery] int? treeLevel, [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<GamPagedResult<EcoFactDto>>.Ok(await _facts.ListAsync(treeLevel, status, search, page, pageSize, ct)));

    [HttpGet("api/v1/staff/eco-facts/{id:guid}")]
    public async Task<IActionResult> Fact(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<EcoFactDto>.Ok(await _facts.GetAsync(id, ct)));

    [HttpPost("api/v1/staff/eco-facts")]
    [ProducesResponseType(typeof(ApiResponse<EcoFactDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateFact([FromBody] SaveEcoFactRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<EcoFactDto>.Ok(await _facts.CreateAsync(StaffId, request, ct), "Đã thêm Eco Fact."));

    [HttpPut("api/v1/staff/eco-facts/{id:guid}")]
    public async Task<IActionResult> UpdateFact(Guid id, [FromBody] SaveEcoFactRequest request, CancellationToken ct) =>
        Ok(ApiResponse<EcoFactDto>.Ok(await _facts.UpdateAsync(id, request, ct), "Đã cập nhật Eco Fact."));

    [HttpPost("api/v1/staff/eco-facts/{id:guid}/status")]
    public async Task<IActionResult> FactStatus(Guid id, [FromBody] SetStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<EcoFactDto>.Ok(await _facts.SetStatusAsync(id, request.Status, ct), "Đã cập nhật trạng thái Eco Fact."));

    // ---------------- Ca làm (6.2)
    [HttpGet("api/v1/staff/shift")]
    public async Task<IActionResult> ShiftStatus(CancellationToken ct) =>
        Ok(ApiResponse<ShiftStatusDto>.Ok(await _shifts.GetStatusAsync(StaffId, ct)));

    [HttpPost("api/v1/staff/shift/check-in")]
    public async Task<IActionResult> ShiftCheckIn(CancellationToken ct)
    {
        var data = await _shifts.CheckInAsync(StaffId, ct);
        return Ok(ApiResponse<ShiftDto>.Ok(data, $"Vào ca thành công! (Chuỗi: {data.StreakCount} ngày).", "MSG-GAM-08"));
    }

    [HttpPost("api/v1/staff/shift/check-out")]
    public async Task<IActionResult> ShiftCheckOut(CancellationToken ct) =>
        Ok(ApiResponse<ShiftDto>.Ok(await _shifts.CheckOutAsync(StaffId, ct), "Đã ra ca."));

    [HttpGet("api/v1/staff/shift/history")]
    public async Task<IActionResult> ShiftHistory([FromQuery] int? year, [FromQuery] int? month, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 31, CancellationToken ct = default) =>
        Ok(ApiResponse<GamPagedResult<ShiftDto>>.Ok(await _shifts.GetHistoryAsync(StaffId, year, month, page, pageSize, ct)));
}
