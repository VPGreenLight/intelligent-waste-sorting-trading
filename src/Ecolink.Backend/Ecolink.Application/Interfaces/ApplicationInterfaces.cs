using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

public interface IAiVisionClient
{
    Task<AiScanResultDto> ClassifyWasteImageAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default);
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
}

public interface ITradeTransactionService
{
    Task<TradeOrderDto> CreateTradeOrderAsync(CreateTradeOrderRequest request, CancellationToken cancellationToken = default);
    Task<TransactionReceiptDto> ConfirmReceiptByQrAsync(ConfirmQrReceiptRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<TradeOrderDto>> GetUserOrdersAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IGamificationService
{
    Task<EcoProfileDto> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<EcoProfileDto> AwardPointsForRecyclingAsync(Guid userId, decimal weightKg, CancellationToken cancellationToken = default);
    Task<IEnumerable<LeaderboardEntryDto>> GetLeaderboardAsync(int top = 10, CancellationToken cancellationToken = default);
}
