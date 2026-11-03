using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ Thương lượng &amp; Khung giờ thu gom thời gian thực qua Chatbox (Module NEG: U-08 &amp; R-05).
/// </summary>
public interface INegotiationChatService
{
    /// <summary>
    /// Lấy thông tin tổng quan phòng chat (trạng thái đơn, điều khoản hiện tại, đề xuất đang hiệu lực)
    /// và lịch sử tin nhắn bất biến phân trang theo thời gian (U-08, R-05, BR-NEG-01, BR-DSP-06).
    /// </summary>
    Task<OrderChatThreadDto> GetOrderChatThreadAsync(
        Guid orderId,
        Guid callerUserId,
        string callerRole,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Gửi tin nhắn văn bản (TEXT) hoặc hình ảnh (IMAGE) trong phạm vi đơn bàn giao.
    /// Kênh chat TEXT/IMAGE vẫn được giữ mở sau khi đơn đã Accepted hoặc Completed để hai bên hỏi đường (BR-NEG-02).
    /// </summary>
    Task<(ChatMessageDto Message, string RecipientNotification)> SendMessageAsync(
        Guid orderId,
        Guid senderUserId,
        string senderRole,
        SendChatMessageRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gửi đề xuất / phản đề xuất (PROPOSAL) về đơn giá, khối lượng hoặc khung giờ thu gom (U-08, R-05, BR-NEG-04).
    /// Tự động ghi đè (Superseded) các đề xuất Pending trước đó (BR-NEG-03) và chuyển đơn sang Negotiating.
    /// Bị khóa nếu đơn đã ở trạng thái Accepted/Scheduled/Completed/Rejected/Cancelled (BR-NEG-02).
    /// </summary>
    Task<(ChatMessageDto ProposalMessage, IReadOnlyList<Guid> SupersededMessageIds, string ToastMessage, string RecipientNotification)> SendProposalAsync(
        Guid orderId,
        Guid senderUserId,
        string senderRole,
        SendProposalRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Đối phương chấp nhận hoặc từ chối đề xuất mới nhất đang hiệu lực (BR-NEG-03, BR-NEG-05).
    /// Khi chấp nhận: lưu cứng AgreedPricePerKg, EstimatedWeightKg, ConfirmedTimeSlot vào TradeOrder và chuyển trạng thái sang Accepted.
    /// </summary>
    Task<(ProposalAcceptanceResultDto Result, string ToastMessage)> RespondToProposalAsync(
        Guid orderId,
        Guid proposalMessageId,
        Guid responderUserId,
        string responderRole,
        RespondProposalRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Cơ sở thu gom (Recycler - R-05) bấm "Chấp nhận" trực tiếp điều khoản và khung giờ đề xuất ban đầu của đơn bàn giao
    /// để khóa khung giờ thu gom chính thức (BR-NEG-05).
    /// </summary>
    Task<(ProposalAcceptanceResultDto Result, string ToastMessage)> AcceptOrderInitialTermsAsync(
        Guid orderId,
        Guid recyclerUserId,
        string recyclerRole,
        CancellationToken ct = default);

    /// <summary>
    /// Đánh dấu toàn bộ tin nhắn chưa đọc của đối phương trong đơn bàn giao là đã đọc.
    /// </summary>
    Task<int> MarkMessagesAsReadAsync(
        Guid orderId,
        Guid readerUserId,
        string readerRole,
        CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra quyền tham gia phòng chat SignalR của người dùng hiện tại đối với orderId (BR-NEG-01, BR-DSP-06).
    /// </summary>
    Task<bool> VerifyChatRoomAccessAsync(
        Guid orderId,
        Guid callerUserId,
        string callerRole,
        CancellationToken ct = default);
}

/// <summary>
/// Outbound Port phát sự kiện thời gian thực qua SignalR Hub tới các thành viên trong phòng chat của đơn bàn giao.
/// </summary>
public interface INegotiationChatNotifier
{
    Task NotifyMessageReceivedAsync(
        Guid orderId,
        ChatMessageDto message,
        string notificationMessage,
        CancellationToken ct = default);

    Task NotifyProposalCreatedAsync(
        Guid orderId,
        ChatMessageDto proposalMessage,
        IReadOnlyList<Guid> supersededMessageIds,
        string notificationMessage,
        CancellationToken ct = default);

    Task NotifyProposalRespondedAsync(
        Guid orderId,
        ProposalAcceptanceResultDto result,
        string notificationMessage,
        CancellationToken ct = default);

    Task NotifyMessagesReadAsync(
        Guid orderId,
        Guid readerUserId,
        int markedCount,
        DateTime readAtUtc,
        CancellationToken ct = default);
}

