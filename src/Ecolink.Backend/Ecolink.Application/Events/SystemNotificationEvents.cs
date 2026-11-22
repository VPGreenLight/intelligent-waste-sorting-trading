using Ecolink.Application.Common;

namespace Ecolink.Application.Events;

/// <summary>
/// Integration Event phát ra khi các luồng nghiệp vụ (KYB, Thu gom, Khiếu nại, Thanh toán, AI Feedback...)
/// hoàn tất và cần gửi thông báo bền vững vào DB + đẩy thời gian thực qua SignalR (/hubs/notifications).
/// </summary>
public sealed record SystemNotificationDispatchedEvent(
    Guid RecipientUserId,
    string Title,
    string Message,
    string NotificationType,
    string? ReferenceType = null,
    Guid? ReferenceId = null) : BaseIntegrationEvent;
