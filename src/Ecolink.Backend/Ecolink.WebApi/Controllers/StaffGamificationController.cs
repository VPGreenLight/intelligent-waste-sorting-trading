using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Constants;
using Ecolink.WebApi.Security;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Ecolink.WebApi.Controllers;

/// <summary>Staff: kho quà (S-10), Eco Fact (S-09). Phân quyền MANAGE_INVENTORY hoặc Admin (BR-ADM-02).</summary>
[ApiController]
[HasPermission(StaffPermissions.ManageInventory)]
[Produces("application/json")]
public class StaffGamificationController : ControllerBase
{
    private const long MaxGiftImageBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedGiftImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IGiftService _gifts;
    private readonly IEcoFactService _facts;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<SaveGiftRequest> _giftValidator;
    private readonly IValidator<SaveEcoFactRequest> _ecoFactValidator;
    private readonly IFileStorageService? _fileStorage;

    public StaffGamificationController(
        IGiftService gifts,
        IEcoFactService facts,
        ICurrentUserService currentUser,
        IValidator<SaveGiftRequest> giftValidator,
        IValidator<SaveEcoFactRequest> ecoFactValidator,
        IFileStorageService? fileStorage = null)
    {
        _gifts = gifts;
        _facts = facts;
        _currentUser = currentUser;
        _giftValidator = giftValidator;
        _ecoFactValidator = ecoFactValidator;
        _fileStorage = fileStorage;
    }

    private Guid StaffId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    // ---------------- Kho quà (S-10)
    [HttpPost("api/v1/staff/gifts/upload-image")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<RecyclerDocumentUploadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadGiftImage(IFormFile? file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn tệp ảnh quà tặng để tải lên.", "MSG-AUTH-02"));

        if (file.Length > MaxGiftImageBytes)
            return BadRequest(ApiResponse<object>.Fail("Dung lượng ảnh quà tặng tối đa 5 MB.", "MSG-CLS-02"));

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedGiftImageExtensions.Contains(ext))
            return BadRequest(ApiResponse<object>.Fail("Định dạng ảnh không được hỗ trợ (chỉ JPG, PNG, WEBP).", "MSG-CLS-01"));

        if (_fileStorage == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Dịch vụ lưu trữ ảnh chưa được cấu hình."));

        await using var stream = file.OpenReadStream();
        var uploaded = await _fileStorage.UploadRecyclerDocumentAsync(
            stream,
            file.FileName,
            file.ContentType,
            "gamification-gifts",
            ct);

        return Ok(ApiResponse<RecyclerDocumentUploadResponseDto>.Ok(uploaded, "Đã tải ảnh quà tặng thành công."));
    }
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
    public async Task<IActionResult> CreateGift([FromBody] SaveGiftRequest request, CancellationToken ct)
    {
        var invalid = await ValidateWithErrorCodeAsync(_giftValidator, request, ct);
        if (invalid != null) return invalid;
        return StatusCode(StatusCodes.Status201Created, ApiResponse<GiftDto>.Ok(await _gifts.CreateAsync(request, ct), "Đã thêm quà vào kho."));
    }

    /// <summary>Sửa thông tin quà (tên, loại, mô tả, ảnh, điểm). Tồn kho đổi bằng /restock.</summary>
    [HttpPut("api/v1/staff/gifts/{id:guid}")]
    public async Task<IActionResult> UpdateGift(Guid id, [FromBody] SaveGiftRequest request, CancellationToken ct)
    {
        var invalid = await ValidateWithErrorCodeAsync(_giftValidator, request, ct);
        if (invalid != null) return invalid;
        return Ok(ApiResponse<GiftDto>.Ok(await _gifts.UpdateAsync(id, request, ct), "Đã cập nhật quà."));
    }

    [HttpPost("api/v1/staff/gifts/{id:guid}/restock")]
    public async Task<IActionResult> Restock(Guid id, [FromBody] RestockGiftRequest request, CancellationToken ct) =>
        Ok(ApiResponse<GiftDto>.Ok(await _gifts.RestockAsync(id, request.Quantity, ct), "Đã cập nhật tồn kho."));

    /// <summary>[STT 095] Nhập hàng loạt mã E-Voucher hoặc số lượng bổ sung vào kho.</summary>
    [HttpPost("api/v1/staff/gifts/{id:guid}/vouchers/import")]
    public async Task<IActionResult> ImportVouchers(Guid id, [FromBody] ImportVouchersRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ImportVouchersResultDto>.Ok(await _gifts.ImportVouchersAsync(id, request, ct), "Đã nhập bổ sung voucher vào kho."));

    /// <summary>[STT 094] Đặt ACTIVE / INACTIVE. Quà INACTIVE hoặc hết hàng không thể đổi (MSG-GAM-04).</summary>
    [HttpPatch("api/v1/staff/gifts/{id:guid}/status")]
    [HttpPost("api/v1/staff/gifts/{id:guid}/status")]
    public async Task<IActionResult> GiftStatus(Guid id, [FromBody] SetStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<GiftDto>.Ok(await _gifts.SetStatusAsync(id, request.Status, ct), "Đã cập nhật trạng thái quà."));

    [HttpGet("api/v1/staff/gifts/{id:guid}/redemptions")]
    public async Task<IActionResult> GiftRedemptions(Guid id, [FromQuery] string? status, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<GamPagedResult<RedemptionDto>>.Ok(await _gifts.ListGiftRedemptionsAsync(id, status, page, pageSize, ct)));

    /// <summary>[STT 096] Danh sách toàn bộ đơn đổi quà cần xử lý theo dõi.</summary>
    [HttpGet("api/v1/staff/redemptions")]
    public async Task<IActionResult> AllRedemptions([FromQuery] string? status, [FromQuery] string? type,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<GamPagedResult<RedemptionDto>>.Ok(
            await _gifts.ListAllRedemptionsAsync(status, type, search, page, pageSize, ct)));

    /// <summary>[STT 097] Xác nhận đã giao quà hiện vật / phát voucher (Fulfill) hoặc hủy đơn kèm hoàn điểm (Cancel).</summary>
    [HttpPatch("api/v1/staff/redemptions/{id:guid}/fulfill")]
    [HttpPost("api/v1/staff/redemptions/{id:guid}/fulfill")]
    [HttpPost("api/v1/staff/gift-redemptions/{id:guid}/fulfill")]
    public async Task<IActionResult> Fulfill(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] FulfillRedemptionRequest? request = null,
        CancellationToken ct = default)
    {
        var data = await _gifts.FulfillAsync(id, request, ct);
        var msg = string.Equals(data.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            ? "Đã hủy đơn đổi quà và hoàn điểm."
            : "Đã xác nhận bàn giao quà.";
        return Ok(ApiResponse<RedemptionDto>.Ok(data, msg));
    }

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
    public async Task<IActionResult> CreateFact([FromBody] SaveEcoFactRequest request, CancellationToken ct)
    {
        var invalid = await ValidateWithErrorCodeAsync(_ecoFactValidator, request, ct);
        if (invalid != null) return invalid;
        return StatusCode(StatusCodes.Status201Created, ApiResponse<EcoFactDto>.Ok(await _facts.CreateAsync(StaffId, request, ct), "Đã thêm Eco Fact."));
    }

    [HttpPut("api/v1/staff/eco-facts/{id:guid}")]
    public async Task<IActionResult> UpdateFact(Guid id, [FromBody] SaveEcoFactRequest request, CancellationToken ct)
    {
        var invalid = await ValidateWithErrorCodeAsync(_ecoFactValidator, request, ct);
        if (invalid != null) return invalid;
        return Ok(ApiResponse<EcoFactDto>.Ok(await _facts.UpdateAsync(id, request, ct), "Đã cập nhật Eco Fact."));
    }

    private async Task<IActionResult?> ValidateWithErrorCodeAsync<T>(
        IValidator<T> validator,
        T request,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (validation.IsValid) return null;

        var code = validation.Errors
            .FirstOrDefault(e => !string.IsNullOrEmpty(e.ErrorCode) && e.ErrorCode.StartsWith("MSG-", StringComparison.Ordinal))
            ?.ErrorCode ?? "MSG-AUTH-02";
        var errors = validation.Errors
            .Select(e => string.IsNullOrWhiteSpace(e.PropertyName) ? e.ErrorMessage : $"{e.PropertyName}: {e.ErrorMessage}")
            .ToList();
        return BadRequest(ApiResponse<object>.Fail(validation.Errors[0].ErrorMessage, code, errors));
    }

    [HttpPatch("api/v1/staff/eco-facts/{id:guid}/status")]
    [HttpPost("api/v1/staff/eco-facts/{id:guid}/status")]
    public async Task<IActionResult> FactStatus(Guid id, [FromBody] SetStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<EcoFactDto>.Ok(await _facts.SetStatusAsync(id, request.Status, ct), "Đã cập nhật trạng thái Eco Fact."));

    /// <summary>[STT 090] Xóa Eco Fact nháp (hoặc vô hiệu hóa mềm nếu đã có người mở khóa).</summary>
    [HttpDelete("api/v1/staff/eco-facts/{id:guid}")]
    public async Task<IActionResult> DeleteFact(Guid id, CancellationToken ct)
    {
        await _facts.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Đã xóa Eco Fact."));
    }
}
