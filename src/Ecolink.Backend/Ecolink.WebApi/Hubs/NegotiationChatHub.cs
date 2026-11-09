using System.Security.Claims;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ecolink.WebApi.Hubs;

/// <summary>
/// SignalR WebSocket Hub phục vụ thương lượng thời gian thực trong phạm vi đơn bàn giao (Module NEG: U-08 &amp; R-05).
/// Endpoint: /hubs/negotiation-chat
/// </summary>
[Authorize]
public class NegotiationChatHub : Hub
{
    private readonly INegotiationChatService _chatService;
    private readonly ILogger<NegotiationChatHub> _logger;

    public NegotiationChatHub(
        INegotiationChatService chatService,
        ILogger<NegotiationChatHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public static string GetOrderGroupName(Guid orderId) => $"order:{orderId:D}";

    /// <summary>
    /// Người dân (U-08) hoặc Cơ sở thu gom (R-05) tham gia vào phòng chat thời gian thực của đơn bàn giao (BR-NEG-01).
    /// </summary>
    public async Task JoinOrderChatRoom(Guid orderId)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        var hasAccess = await _chatService.VerifyChatRoomAccessAsync(orderId, userId, role, Context.ConnectionAborted);
        if (!hasAccess)
        {
            throw new HubException("Bạn không có quyền tham gia phòng thương lượng của đơn bàn giao này (BR-NEG-01).");
        }

        var groupName = GetOrderGroupName(orderId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName, Context.ConnectionAborted);

        _logger.LogInformation(
            "[SignalR NEG] User {UserId} ({Role}) đã kết nối vào phòng chat {GroupName}.",
            userId, role, groupName);

        await Clients.Caller.SendAsync("JoinedOrderChatRoom", new
        {
            orderId,
            connectionId = Context.ConnectionId,
            joinedAt = DateTime.UtcNow
        }, Context.ConnectionAborted);
    }

    /// <summary>
    /// Rời khỏi phòng chat của đơn bàn giao.
    /// </summary>
    public async Task LeaveOrderChatRoom(Guid orderId)
    {
        var groupName = GetOrderGroupName(orderId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName, Context.ConnectionAborted);
    }

    /// <summary>
    /// Gửi trạng thái đang nhập tin nhắn (Typing indicator) tới đối phương trong phòng chat.
    /// </summary>
    public async Task SendTypingIndicator(Guid orderId, bool isTyping)
    {
        var userId = GetCurrentUserId();
        var groupName = GetOrderGroupName(orderId);

        await Clients.OthersInGroup(groupName).SendAsync("UserTypingChanged", new
        {
            orderId,
            userId,
            isTyping,
            timestamp = DateTime.UtcNow
        }, Context.ConnectionAborted);
    }

    private Guid GetCurrentUserId()
    {
        var claim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? Context.User?.FindFirstValue("sub");

        if (Guid.TryParse(claim, out var userId))
        {
            return userId;
        }

        throw new HubException("Phiên xác thực WebSocket không hợp lệ.");
    }

    private string GetCurrentUserRole() =>
        Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}

/// <summary>
/// Adapter phát sự kiện thời gian thực qua SignalR Hub tới các thành viên trong phòng chat đơn bàn giao.
/// </summary>
public class SignalRNegotiationChatNotifier : INegotiationChatNotifier
{
    private readonly IHubContext<NegotiationChatHub> _hubContext;

    public SignalRNegotiationChatNotifier(IHubContext<NegotiationChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyMessageReceivedAsync(
        Guid orderId,
        ChatMessageDto message,
        string notificationMessage,
        CancellationToken ct = default)
    {
        var groupName = NegotiationChatHub.GetOrderGroupName(orderId);
        return _hubContext.Clients.Group(groupName).SendAsync("ReceiveChatMessage", new
        {
            orderId,
            message,
            notification = notificationMessage // MSG-NEG-02
        }, ct);
    }

    public Task NotifyProposalCreatedAsync(
        Guid orderId,
        ChatMessageDto proposalMessage,
        IReadOnlyList<Guid> supersededMessageIds,
        string notificationMessage,
        CancellationToken ct = default)
    {
        var groupName = NegotiationChatHub.GetOrderGroupName(orderId);
        return _hubContext.Clients.Group(groupName).SendAsync("ProposalCreated", new
        {
            orderId,
            proposalMessage,
            supersededMessageIds,
            notification = notificationMessage // MSG-NEG-02
        }, ct);
    }

    public Task NotifyProposalRespondedAsync(
        Guid orderId,
        ProposalAcceptanceResultDto result,
        string notificationMessage,
        CancellationToken ct = default)
    {
        var groupName = NegotiationChatHub.GetOrderGroupName(orderId);
        return _hubContext.Clients.Group(groupName).SendAsync("ProposalResponded", new
        {
            orderId,
            result,
            notification = notificationMessage // MSG-NEG-03
        }, ct);
    }

    public Task NotifyMessagesReadAsync(
        Guid orderId,
        Guid readerUserId,
        int markedCount,
        DateTime readAtUtc,
        CancellationToken ct = default)
    {
        var groupName = NegotiationChatHub.GetOrderGroupName(orderId);
        return _hubContext.Clients.Group(groupName).SendAsync("MessagesMarkedAsRead", new
        {
            orderId,
            readerUserId,
            markedCount,
            readAtUtc
        }, ct);
    }
}

