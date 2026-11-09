using Ecolink.Domain.Common;
using Ecolink.Domain.Enums;

namespace Ecolink.Domain.Entities;

public class CollectionPartner : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string FacilityName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? OperatingHours { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public bool IsVerified { get; set; } = false;

    // Navigation
    public ICollection<AcceptedMaterial> AcceptedMaterials { get; set; } = new List<AcceptedMaterial>();
    public ICollection<TradeOrder> ReceivedOrders { get; set; } = new List<TradeOrder>();
}

public class AcceptedMaterial : BaseEntity
{
    public Guid PartnerId { get; set; }
    public CollectionPartner? Partner { get; set; }

    public Guid WasteCategoryId { get; set; }
    public WasteCategory? WasteCategory { get; set; }

    public decimal PricePerKg { get; set; }
    public decimal MinimumQuantityKg { get; set; } = 1.0m;
}

public class TradeOrder : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid PartnerId { get; set; }
    public CollectionPartner? Partner { get; set; }

    /// <summary>
    /// Định danh UserId của Cơ sở thu gom (RecyclerProfile.UserId) phục vụ kết nối trực tiếp U-05, U-08, R-05.
    /// </summary>
    public Guid? RecyclerUserId { get; set; }
    public RecyclerProfile? RecyclerProfile { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime ScheduledAt { get; set; }
    public decimal EstimatedWeightKg { get; set; }
    public string? Note { get; set; }

    // Các thuộc tính phục vụ Thương lượng & Khung giờ thu gom (Module NEG: U-08 & R-05)
    public string? CategoryCode { get; set; }
    public decimal? InitialPricePerKg { get; set; }
    public decimal? AgreedPricePerKg { get; set; }
    public string? ProposedTimeSlot { get; set; }
    public string? ConfirmedTimeSlot { get; set; }
    public string? PickupAddress { get; set; }
    public DateTime? AcceptedAt { get; set; }

    // Navigation
    public TransactionReceipt? Receipt { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// <summary>
/// Tin nhắn thương lượng thời gian thực trong phạm vi một yêu cầu bàn giao (U-08 &amp; R-05).
/// Toàn bộ lịch sử tin nhắn được lưu trữ bất biến (Immutable Audit Trail) phục vụ đối soát tranh chấp (BR-NEG-01, BR-DSP-06).
/// </summary>
public class ChatMessage : BaseEntity
{
    public Guid TradeOrderId { get; set; }
    public TradeOrder? TradeOrder { get; set; }

    public Guid SenderUserId { get; set; }
    public User? SenderUser { get; set; }

    public string SenderRole { get; set; } = string.Empty;
    public ChatMessageType MessageType { get; set; } = ChatMessageType.Text;
    public string? MessageContent { get; set; }
    public string? AttachmentUrl { get; set; }

    // Các thuộc tính dành riêng cho bản tin Đề xuất thương lượng (MessageType == ChatMessageType.Proposal)
    public ProposalStatus? ProposalStatus { get; set; }
    public decimal? ProposedPricePerKg { get; set; }
    public decimal? ProposedWeightKg { get; set; }
    public string? ProposedTimeSlot { get; set; }
    public string? ProposalDataJson { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

public class TransactionReceipt : BaseEntity
{
    public Guid TradeOrderId { get; set; }
    public TradeOrder? TradeOrder { get; set; }

    public decimal ActualWeightKg { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmountCash { get; set; }
    public int PointsAwarded { get; set; }
    public string QrSecurityCode { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
