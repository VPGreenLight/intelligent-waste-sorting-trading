using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Recycler Profile, Pricing &amp; Availability dành cho đối tác thu gom (UC R-01, R-02).
/// Mọi endpoint chỉ thao tác trên hồ sơ của chính Recycler đang đăng nhập (UserId lấy từ JWT).
/// </summary>
[ApiController]
[Route("api/v1/recycler")]
[Authorize(Roles = "Recycler,Collector")] // Enum UserRole có alias Recycler/Collector nên chấp nhận cả hai tên
[Produces("application/json")]
public class RecyclerController : ControllerBase
{
    private readonly IRecyclerManagementService _service;
    private readonly ICurrentUserService _currentUser;

    public RecyclerController(IRecyclerManagementService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid RecyclerId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    // ============================================================
    // Recycler Profile
    // ============================================================

    /// <summary>Xem hồ sơ cơ sở của chính mình (kèm trạng thái duyệt và lý do từ chối nếu có).</summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ApiResponse<RecyclerProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var data = await _service.GetProfileAsync(RecyclerId, ct);
        return Ok(ApiResponse<RecyclerProfileDto>.Ok(data));
    }

    /// <summary>
    /// Cập nhật hồ sơ cơ sở (tên, địa chỉ, tọa độ GPS, liên hệ, ảnh, mô tả).
    /// Hồ sơ đang bị Rejected sẽ chuyển về Pending để Staff duyệt lại (BR-MTH-02).
    /// </summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ApiResponse<RecyclerProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateRecyclerProfileRequest request, CancellationToken ct)
    {
        var data = await _service.UpdateProfileAsync(RecyclerId, request, ct);
        return Ok(ApiResponse<RecyclerProfileDto>.Ok(data, "Cập nhật hồ sơ cơ sở thành công."));
    }

    // ============================================================
    // Pricing (R-01)
    // ============================================================

    /// <summary>Danh sách loại phế liệu và giá thu mua của cơ sở. Hỗ trợ tìm theo tên/mã và lọc theo trạng thái.</summary>
    /// <param name="keyword">Từ khóa tìm theo tên hoặc mã loại phế liệu.</param>
    /// <param name="isActive">true = đang thu mua, false = tạm ngưng, bỏ trống = tất cả.</param>
    [HttpGet("prices")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RecyclerPriceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrices([FromQuery] string? keyword, [FromQuery] bool? isActive, CancellationToken ct)
    {
        var data = await _service.GetPricesAsync(RecyclerId, keyword, isActive, ct);
        return Ok(ApiResponse<IReadOnlyList<RecyclerPriceDto>>.Ok(data));
    }

    /// <summary>Thêm một loại phế liệu nhận mua kèm giá (VND/kg) và khối lượng tối thiểu. Yêu cầu hồ sơ đã được duyệt.</summary>
    [HttpPost("prices")]
    [ProducesResponseType(typeof(ApiResponse<RecyclerPriceDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddPrice([FromBody] CreateRecyclerPriceRequest request, CancellationToken ct)
    {
        var data = await _service.AddPriceAsync(RecyclerId, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<RecyclerPriceDto>.Ok(data, "Đã thêm loại phế liệu vào bảng giá."));
    }

    /// <summary>Sửa giá, khối lượng tối thiểu, điều kiện thu gom hoặc tạm ngưng/mở lại việc thu mua một loại phế liệu.</summary>
    [HttpPut("prices/{priceId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RecyclerPriceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePrice(Guid priceId, [FromBody] UpdateRecyclerPriceRequest request, CancellationToken ct)
    {
        var data = await _service.UpdatePriceAsync(RecyclerId, priceId, request, ct);
        return Ok(ApiResponse<RecyclerPriceDto>.Ok(data, "Cập nhật bảng giá thành công."));
    }

    /// <summary>
    /// Lưu cả bảng giá một lần (nút "Save price sheet" ở SCR-RCY-01). Các loại phế liệu không nằm trong danh sách được giữ nguyên.
    /// Dòng đã gửi sẽ được làm mới mốc "cập nhật lần cuối".
    /// </summary>
    [HttpPut("prices")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RecyclerPriceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkSavePrices([FromBody] BulkSavePricesRequest request, CancellationToken ct)
    {
        var data = await _service.BulkSavePricesAsync(RecyclerId, request, ct);
        return Ok(ApiResponse<IReadOnlyList<RecyclerPriceDto>>.Ok(data, "Đã lưu và công bố bảng giá."));
    }

    // ============================================================
    // Availability (R-02)
    // ============================================================

    /// <summary>Xem trạng thái Online/Offline, lịch hoạt động theo tuần và ghi chú bận.</summary>
    [HttpGet("availability")]
    [ProducesResponseType(typeof(ApiResponse<AvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailability(CancellationToken ct)
    {
        var data = await _service.GetAvailabilityAsync(RecyclerId, ct);
        return Ok(ApiResponse<AvailabilityDto>.Ok(data));
    }

    /// <summary>
    /// Bật/tắt trạng thái hoạt động. Bật Online yêu cầu hồ sơ đã được duyệt, có ít nhất một loại phế liệu đang nhận mua và vị trí hợp lệ.
    /// Offline chỉ ẩn khỏi bản đồ, đơn đang xử lý không bị ảnh hưởng (BR-MTH-06).
    /// </summary>
    [HttpPatch("availability")]
    [ProducesResponseType(typeof(ApiResponse<AvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAvailability([FromBody] UpdateAvailabilityRequest request, CancellationToken ct)
    {
        var data = await _service.UpdateAvailabilityAsync(RecyclerId, request, ct);
        var message = data.IsOnline ? "Cơ sở đã chuyển sang trạng thái hoạt động." : "Cơ sở đã chuyển sang trạng thái tạm nghỉ.";
        return Ok(ApiResponse<AvailabilityDto>.Ok(data, message));
    }

    /// <summary>Cập nhật lịch hoạt động cố định theo tuần (dayOfWeek: 0 = Chủ nhật ... 6 = Thứ bảy; giờ dạng HH:mm).</summary>
    [HttpPut("availability/schedule")]
    [ProducesResponseType(typeof(ApiResponse<AvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSchedule([FromBody] UpdateScheduleRequest request, CancellationToken ct)
    {
        var data = await _service.UpdateScheduleAsync(RecyclerId, request, ct);
        return Ok(ApiResponse<AvailabilityDto>.Ok(data, "Đã cập nhật lịch hoạt động."));
    }
}
