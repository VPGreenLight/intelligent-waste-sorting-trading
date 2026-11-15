using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Phân hệ Chấm công &amp; Nhật ký Ca trực Hàng ngày của Nhân viên Vận hành (FE-17 / M-15: UC S-11).
/// Bao phủ các endpoint STT 165 - STT 168 trong 00_api_inventory_and_endpoint_statistics.md.
/// </summary>
[ApiController]
[Produces("application/json")]
public class StaffAttendanceController : ControllerBase
{
    private readonly IStaffAttendanceService _attendanceService;
    private readonly IValidator<StaffCheckInRequest> _checkInValidator;
    private readonly IValidator<StaffCheckOutRequest> _checkOutValidator;

    public StaffAttendanceController(
        IStaffAttendanceService attendanceService,
        IValidator<StaffCheckInRequest> checkInValidator,
        IValidator<StaffCheckOutRequest> checkOutValidator)
    {
        _attendanceService = attendanceService;
        _checkInValidator = checkInValidator;
        _checkOutValidator = checkOutValidator;
    }

    /// <summary>
    /// [STT 165] [S-11] Nhân viên chấm công bắt đầu ca làm việc hàng ngày để ghi nhận chuyên cần (không cộng vào ví điểm thưởng công dân theo BR-GAM-09).
    /// </summary>
    [HttpPost("api/v1/staff/attendance/check-in")]
    [Authorize(Roles = "Staff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<StaffAttendanceStatusDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StaffAttendanceStatusDto>>> CheckInShift(
        [FromBody] StaffCheckInRequest? request,
        CancellationToken ct)
    {
        var payload = request ?? new StaffCheckInRequest();
        var validation = await _checkInValidator.ValidateAsync(payload, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu điểm danh đầu ca không hợp lệ.", "MSG-GEN-01", errors));
        }

        var staffId = GetCurrentUserId();
        var status = await _attendanceService.CheckInAsync(staffId, payload, ct);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<StaffAttendanceStatusDto>.Ok(
            status,
            $"Điểm danh đầu ca thành công! Bạn đang ở ngày thứ {status.CurrentStreakDay} của chuỗi chuyên cần (+{status.PointsEarnedToday} điểm nhận việc).",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 166] [S-11] Nhân viên chấm công kết thúc ca trực vận hành và lưu ghi chú bàn giao ca.
    /// </summary>
    [HttpPost("api/v1/staff/attendance/check-out")]
    [Authorize(Roles = "Staff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<StaffAttendanceStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StaffAttendanceStatusDto>>> CheckOutShift(
        [FromBody] StaffCheckOutRequest? request,
        CancellationToken ct)
    {
        var payload = request ?? new StaffCheckOutRequest();
        var validation = await _checkOutValidator.ValidateAsync(payload, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu kết thúc ca trực không hợp lệ.", "MSG-GEN-01", errors));
        }

        var staffId = GetCurrentUserId();
        var status = await _attendanceService.CheckOutAsync(staffId, payload, ct);

        return Ok(ApiResponse<StaffAttendanceStatusDto>.Ok(
            status,
            "Đã kết thúc ca trực và ghi nhận bàn giao ca thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 167] [S-11] Nhân viên tra cứu bảng chấm công cá nhân, chuỗi chuyên cần và lịch sử các ca trực trong tháng.
    /// </summary>
    [HttpGet("api/v1/staff/attendance/me")]
    [Authorize(Roles = "Staff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<StaffAttendanceStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StaffAttendanceStatusDto>>> GetMyAttendanceLogs(
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        CancellationToken ct = default)
    {
        var staffId = GetCurrentUserId();
        var status = await _attendanceService.GetMyAttendanceAsync(staffId, month, year, ct);

        return Ok(ApiResponse<StaffAttendanceStatusDto>.Ok(
            status,
            "Lấy thông tin chấm công cá nhân thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 168] [S-11 / A-03] Quản trị viên theo dõi báo cáo chuyên cần và nhật ký ca trực hàng ngày của toàn bộ đội ngũ Nhân viên.
    /// </summary>
    [HttpGet("api/v1/admin/staff/attendance")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminStaffAttendanceItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminStaffAttendanceItemDto>>>> GetAllStaffAttendance(
        [FromQuery] AdminStaffAttendanceFilterRequest filter,
        CancellationToken ct)
    {
        var result = await _attendanceService.GetAllStaffAttendanceAsync(filter, ct);
        return Ok(ApiResponse<PagedResult<AdminStaffAttendanceItemDto>>.Ok(
            result,
            "Lấy báo cáo chuyên cần nhân sự thành công.",
            "MSG-GEN-01"));
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Không xác định được định danh nhân sự từ Token.");
        }
        return userId;
    }
}
