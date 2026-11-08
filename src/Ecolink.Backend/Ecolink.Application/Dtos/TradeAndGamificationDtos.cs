namespace Ecolink.Application.Dtos;

public class CollectionPartnerDto
{
    public Guid Id { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string? RepresentativeName { get; set; }
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceKm { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BusinessPhotoUrl { get; set; }
    public List<AcceptedMaterialDto> Materials { get; set; } = new();
}

public class AcceptedMaterialDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
    public decimal MinimumQuantityKg { get; set; }
    public string? CollectionConditions { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}

/// <summary>
/// Query Request cho Use Case U-05: Tìm kiếm cơ sở thu gom gần vị trí GPS người dùng (BR-MTH-04, BR-MTH-05)
/// </summary>
public class NearbyRecyclerFilterRequest
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Category { get; set; }            // Mã loại rác từ bước quét AI: plastic, paper, cardboard, metal, glass
    public double RadiusKm { get; set; } = 10.0;     // Bán kính mặc định 10km (cho phép từ 0.5km đến 50km)
    public string? Keyword { get; set; }             // Tìm theo tên cơ sở hoặc địa chỉ
    public string SortBy { get; set; } = "distance"; // "distance" (mặc định theo BR-MTH-04) hoặc "price_desc"
}

/// <summary>
/// Response tổng hợp kết quả tìm kiếm bản đồ (U-05) kèm phương án dự phòng khi không có người mua (MSG-MTH-01)
/// </summary>
public class NearbyRecyclerSearchResponseDto
{
    public int TotalFound { get; set; }
    public double SearchRadiusKm { get; set; }
    public string? FilteredCategory { get; set; }
    public IReadOnlyList<CollectionPartnerDto> Items { get; set; } = Array.Empty<CollectionPartnerDto>();
    public DiscoveryFallbackSuggestionDto? FallbackSuggestion { get; set; }
}

/// <summary>
/// Gợi ý xử lý rác khi xung quanh không có cơ sở thu mua nào hoạt động (MSG-MTH-01)
/// </summary>
public class DiscoveryFallbackSuggestionDto
{
    public double SuggestedExpandedRadiusKm { get; set; } = 25.0;
    public string GuidanceMessage { get; set; } = string.Empty;
    public WasteSortingGuideDto? PublicBinSortingGuide { get; set; }
    public bool AllowCommunityPickupPost { get; set; } = true;
}

public class TradeOrderLineDto
{
    public string WasteId { get; set; } = string.Empty;
    public decimal Kg { get; set; }
}

public class CreateTradeOrderRequest
{
    public Guid UserId { get; set; }
    public Guid PartnerId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public decimal EstimatedWeightKg { get; set; }
    public string? Note { get; set; }
    public string? CategoryCode { get; set; }
    public decimal? InitialPricePerKg { get; set; }
    public string? ProposedTimeSlot { get; set; }
    public string? PickupAddress { get; set; }
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    public List<TradeOrderLineDto>? Lines { get; set; }
}

public class UpdateTradeOrderStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class UpdateTradeOrderTimeSlotRequest
{
    public string ProposedTimeSlot { get; set; } = string.Empty;
}

public class TradeOrderDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? CitizenName { get; set; }
    public Guid PartnerId { get; set; }
    public Guid? RecyclerUserId { get; set; }
    public string? PartnerName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public decimal EstimatedWeightKg { get; set; }
    public string? CategoryCode { get; set; }
    public decimal? InitialPricePerKg { get; set; }
    public decimal? AgreedPricePerKg { get; set; }
    public string? ProposedTimeSlot { get; set; }
    public string? ConfirmedTimeSlot { get; set; }
    public string? PickupAddress { get; set; }
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    public string? Note { get; set; }
    public string? CancelReason { get; set; }
    public List<TradeOrderLineDto>? Lines { get; set; }
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
