namespace Ecolink.Application.Dtos;

public class CollectionPartnerDto
{
    public Guid Id { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceKm { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public List<AcceptedMaterialDto> Materials { get; set; } = new();
}

public class AcceptedMaterialDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
    public decimal MinimumQuantityKg { get; set; }
}

public class CreateTradeOrderRequest
{
    public Guid UserId { get; set; }
    public Guid PartnerId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public decimal EstimatedWeightKg { get; set; }
    public string? Note { get; set; }
}

public class TradeOrderDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PartnerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public decimal EstimatedWeightKg { get; set; }
    public string QrCodeValue { get; set; } = string.Empty;
}

public class ConfirmQrReceiptRequest
{
    public Guid TradeOrderId { get; set; }
    public string QrSecurityCode { get; set; } = string.Empty;
    public decimal ActualWeightKg { get; set; }
    public decimal UnitPrice { get; set; }
}

public class TransactionReceiptDto
{
    public Guid ReceiptId { get; set; }
    public Guid TradeOrderId { get; set; }
    public decimal ActualWeightKg { get; set; }
    public decimal TotalAmountCash { get; set; }
    public int PointsAwarded { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class EcoProfileDto
{
    public Guid UserId { get; set; }
    public int CurrentGreenPoints { get; set; }
    public int TotalLifetimePoints { get; set; }
    public int TreeLevel { get; set; }
    public int TreeExperience { get; set; }
    public int ExperienceToNextLevel { get; set; }
    public int TotalWasteScanned { get; set; }
    public decimal TotalRecycledKg { get; set; }
}

public class LeaderboardEntryDto
{
    public int Rank { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int TreeLevel { get; set; }
}
