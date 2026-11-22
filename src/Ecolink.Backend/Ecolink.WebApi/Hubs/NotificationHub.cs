using System.Security.Claims;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ecolink.WebApi.Hubs;

/// <summary>
/// [STT 172] Kênh WebSocket SignalR (/hubs/notifications) đẩy thông báo thời gian thực tới người dùng (SU-04, EI-07).
/// Hỗ trợ JWT qua query ?access_token=... khi kết nối WebSocket / Server-Sent Events.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public static string GetUserGroup(Guid userId) => $"user:{userId:D}";

    public override async Task OnConnectedAsync()
    {
        var userId = ExtractUserId(Context.User);
        if (userId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    public Task<string> Ping() => Task.FromResult("pong");

    private static Guid ExtractUserId(ClaimsPrincipal? user)
    {
        if (user == null) return Guid.Empty;

        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue("sub")
                  ?? user.FindFirstValue("userId");

        return Guid.TryParse(raw, out var userId) ? userId : Guid.Empty;
    }
}

/// <summary>
/// Adapter phát thông báo realtime qua NotificationHub tới group riêng của từng người dùng.
/// </summary>
public sealed class SignalRNotificationNotifier : INotificationRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PushNotificationAsync(
        Guid userId,
        NotificationDto notification,
        int unreadCount,
        CancellationToken ct = default)
    {
        var group = NotificationHub.GetUserGroup(userId);
        await _hubContext.Clients.Group(group).SendAsync("ReceiveNotification", notification, ct);
        await _hubContext.Clients.Group(group).SendAsync("UnreadCountUpdated", new { unreadCount }, ct);
    }

    public Task PushUnreadCountAsync(Guid userId, int unreadCount, CancellationToken ct = default)
    {
        var group = NotificationHub.GetUserGroup(userId);
        return _hubContext.Clients.Group(group).SendAsync("UnreadCountUpdated", new { unreadCount }, ct);
    }
}
