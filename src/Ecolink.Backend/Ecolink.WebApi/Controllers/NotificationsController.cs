using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Quản lý kho thông báo hệ thống của người dùng (Module M-15: SU-04 - STT 169, 170, 171).
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// [STT 169] Lấy danh sách thông báo hệ thống của tài khoản hiện tại kèm số lượng chưa đọc (SU-04).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<NotificationListResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] NotificationFilterRequest filter,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var data = await _notificationService.GetMyNotificationsAsync(userId, filter, ct);
        return Ok(ApiResponse<NotificationListResponseDto>.Ok(
            data,
            message: "Tải danh sách thông báo hệ thống thành công.",
            messageCode: "MSG-NTF-LIST-OK"));
    }

    /// <summary>
    /// [STT 170] Đánh dấu một thông báo cụ thể là đã đọc (is_read = true) (SU-04).
    /// </summary>
    [HttpPatch("{notificationId:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<MarkNotificationReadResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkNotificationRead(Guid notificationId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var data = await _notificationService.MarkAsReadAsync(userId, notificationId, ct);
        return Ok(ApiResponse<MarkNotificationReadResultDto>.Ok(
            data,
            message: "Đã đánh dấu thông báo là đã đọc.",
            messageCode: "MSG-NTF-READ-OK"));
    }

    /// <summary>
    /// [STT 171] Đánh dấu toàn bộ thông báo chưa đọc của tài khoản hiện tại là đã xem (SU-04).
    /// </summary>
    [HttpPut("read-all")]
    [ProducesResponseType(typeof(ApiResponse<MarkAllNotificationsReadResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var data = await _notificationService.MarkAllAsReadAsync(userId, ct);
        return Ok(ApiResponse<MarkAllNotificationsReadResultDto>.Ok(
            data,
            message: $"Đã đánh dấu {data.MarkedCount} thông báo là đã đọc.",
            messageCode: "MSG-NTF-READ-ALL-OK"));
    }

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? User.FindFirstValue("userId");

        if (!Guid.TryParse(raw, out var userId))
        {
            throw new UnauthorizedAccessException("Không xác định được danh tính tài khoản từ phiên đăng nhập.");
        }

        return userId;
    }
}
