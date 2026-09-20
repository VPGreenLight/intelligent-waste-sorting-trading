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

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime ScheduledAt { get; set; }
    public decimal EstimatedWeightKg { get; set; }
    public string? Note { get; set; }

    // Navigation
    public TransactionReceipt? Receipt { get; set; }
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
