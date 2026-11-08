=using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

public interface IAiVisionClient
{
    Task<AiScanResultDto> ClassifyWasteImageAsync(Stream imageStream, string fileName, string? model = "yolov8_detect", CancellationToken cancellationToken = default);
    Task<object?> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
    Task<bool> SubmitFeedbackAsync(SubmitFeedbackDto feedback, CancellationToken cancellationToken = default);
}

public interface IWasteRuleEngineService
{
    Task<WasteSortingGuideDto?> GetSortingGuideAsync(string categoryCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<WasteSortingGuideDto>> GetAllRulesAsync(CancellationToken cancellationToken = default);
    Task InvalidateRuleCacheAsync(string? categoryCode = null, CancellationToken cancellationToken = default);
}

public interface IGeoMatchingService
{
    Task<IEnumerable<CollectionPartnerDto>> FindNearestPartnersAsync(
        double latitude,
        double longitude,
        string? categoryCode = null,
        double radiusKm = 10.0,
        CancellationToken cancellationToken = default);

    Task<NearbyRecyclerSearchResponseDto> SearchNearbyRecyclersAsync(
        NearbyRecyclerFilterRequest filter,
        CancellationToken cancellationToken = default);

    Task<CollectionPartnerDto> GetRecyclerPublicDetailAsync(
        Guid recyclerId,
        double? userLat = null,
        double? userLng = null,
        CancellationToken cancellationToken = default);
}

public interface ITradeTransactionService
{
    Task<TradeOrderDto> CreateTradeOrderAsync(CreateTradeOrderRequest request, CancellationToken cancellationToken = default);
    Task<TransactionReceiptDto> ConfirmReceiptByQrAsync(ConfirmQrReceiptRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<TradeOrderDto>> GetUserOrdersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TradeOrderDto> UpdateOrderStatusAsync(Guid orderId, Guid callerUserId, UpdateTradeOrderStatusRequest request, CancellationToken cancellationToken = default);
    Task<TradeOrderDto> UpdateOrderTimeSlotAsync(Guid orderId, Guid callerUserId, string proposedTimeSlot, CancellationToken cancellationToken = default);
}

public interface IGamificationService
{
    Task<EcoProfileDto> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<EcoProfileDto> AwardPointsForRecyclingAsync(Guid userId, decimal weightKg, CancellationToken cancellationToken = default);
    Task<IEnumerable<LeaderboardEntryDto>> GetLeaderboardAsync(int top = 10, CancellationToken cancellationToken = default);
}
