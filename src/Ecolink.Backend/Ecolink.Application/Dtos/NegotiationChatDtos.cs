namespace Ecolink.Application.Dtos;

/// <summary>
/// Thông tin tổng quan phiên chat thương lượng gắn với một đơn bàn giao (U-08 &amp; R-05, BR-NEG-01, BR-NEG-02).
/// </summary>
public class OrderChatThreadDto
{
    public Guid OrderId { get; set; }
    public Guid CitizenUserId { get; set; }
    public string CitizenName { get; set; } = string.Empty;
    public Guid RecyclerUserId { get; set; }
    public string FacilityName { get; set; } = string.Empty;

    public string OrderStatus { get; set; } = string.Empty;
    public string? CategoryCode { get; set; }
    public decimal EstimatedWeightKg { get; set; }
    public decimal? InitialPricePerKg { get; set; }
    public decimal? AgreedPricePerKg { get; set; }
    public string? ProposedTimeSlot { get; set; }
    public string? ConfirmedTimeSlot { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    /// <summary>
    /// true khi đơn đã chuyển sang Accepted, Scheduled, Completed, Rejected, Cancelled, Expired, Disputed (BR-NEG-02).
    /// Khi IsProposalLocked = true, hai bên không thể tạo hoặc sửa đổi đề xuất giá/khung giờ mới.
    /// </summary>
    public bool IsProposalLocked { get; set; }

    /// <summary>
    /// true ngay cả khi đơn đã Accepted, Scheduled hoặc Completed để hai bên vẫn có thể nhắn tin TEXT/IMAGE hỏi đường (BR-NEG-02).
    /// </summary>
    public bool IsChatOpen { get; set; }

    public int UnreadCount { get; set; }

    /// <summary>
    /// Bản đề xuất mới nhất đang ở trạng thái Pending chờ đối phương phản hồi (BR-NEG-03).
    /// </summary>
    public ChatMessageDto? ActiveProposal { get; set; }

    public PagedResult<ChatMessageDto> Messages { get; set; } = new();
}

/// <summary>
/// Chi tiết một tin nhắn bất biến trong phòng chat thương lượng (TEXT, IMAGE, PROPOSAL, SYSTEM).
/// </summary>
public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid SenderUserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string? MessageContent { get; set; }
    public string? AttachmentUrl { get; set; }
    public NegotiationProposalPayloadDto? Proposal { get; set; }
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

/// <summary>
/// Dữ liệu chi tiết của bản đề xuất / phản đề xuất Giá, Khối lượng và Khung giờ thu gom (BR-NEG-03, BR-NEG-04).
/// </summary>
public class NegotiationProposalPayloadDto
{
    public decimal ProposedPricePerKg { get; set; }
    public decimal ProposedWeightKg { get; set; }
    public string ProposedTimeSlot { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string ProposalStatus { get; set; } = "Pending"; // Pending, Accepted, Superseded, Rejected
    public Guid? RespondedByUserId { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? ResponseNote { get; set; }
}

/// <summary>
/// Request gửi tin nhắn văn bản (TEXT) hoặc hình ảnh (IMAGE) trong phòng chat (U-08 &amp; R-05).
/// </summary>
public class SendChatMessageRequest
{
    public string MessageType { get; set; } = "Text"; // "Text" hoặc "Image"
    public string? MessageContent { get; set; }
    public string? AttachmentUrl { get; set; }
}

/// <summary>
/// Request gửi đề xuất / phản đề xuất (PROPOSAL) về đơn giá, khối lượng ước tính và khung giờ thu gom (U-08 &amp; R-05, BR-NEG-02, BR-NEG-03, BR-NEG-04).
/// </summary>
public class SendProposalRequest
{
    public decimal ProposedPricePerKg { get; set; }
    public decimal ProposedWeightKg { get; set; }
    public string ProposedTimeSlot { get; set; } = string.Empty;
    public string? Note { get; set; }
}

/// <summary>
/// Request phản hồi (Chấp nhận hoặc Từ chối) bản đề xuất mới nhất đang hiệu lực (BR-NEG-03, BR-NEG-05).
/// </summary>
public class RespondProposalRequest
{
    public bool IsAccepted { get; set; } = true;
    public string? ResponseNote { get; set; }
}

/// <summary>
/// Kết quả trả về khi phản hồi đề xuất hoặc chốt điều khoản đơn bàn giao (BR-NEG-03, BR-NEG-05, MSG-NEG-03).
/// </summary>
public class ProposalAcceptanceResultDto
{
    public Guid OrderId { get; set; }
    public Guid? ProposalMessageId { get; set; }
    public string ProposalStatus { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public decimal? AgreedPricePerKg { get; set; }
    public decimal ConfirmedWeightKg { get; set; }
    public string? ConfirmedTimeSlot { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public bool IsProposalLocked { get; set; }
    public ChatMessageDto SystemMessage { get; set; } = null!;
}

/// <summary>
/// Kết quả tải ảnh đính kèm trong phòng chat lên Cloudflare R2 Object Storage.
/// </summary>
public class ChatAttachmentUploadResponseDto
{
    public string AttachmentUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

