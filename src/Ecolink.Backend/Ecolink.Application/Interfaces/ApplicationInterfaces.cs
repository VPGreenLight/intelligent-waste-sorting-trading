using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

public interface IAiVisionClient
{
    Task<AiScanResultDto> ClassifyWasteImageAsync(Stream imageStream, string fileName, string? model = "yolo26_detect", CancellationToken cancellationToken = default);
    Task<object?> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
    Task<bool> SubmitFeedbackAsync(SubmitFeedbackDto feedback, CancellationToken cancellationToken = default);

    Task<CurateDatasetSyncResponseDto> CurateDatasetAsync(CurateDatasetSyncRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new CurateDatasetSyncResponseDto
        {
            Success = false,
            VersionCode = request.VersionCode,
            ErrorMessage = "CurateDatasetAsync chưa được triển khai trên client này."
        });

    Task<AiModelReloadResponseDto> ReloadModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AiModelReloadResponseDto
        {
            Success = false,
            ErrorMessage = "ReloadModelsAsync chưa được triển khai trên client này."
        });

    Task<AiRuleSyncResponseDto> SyncSortingRuleAsync(
        string categoryCode,
        string binColorName,
        string instructions,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new AiRuleSyncResponseDto
        {
            Success = false,
            Message = "SyncSortingRuleAsync chưa được triển khai trên client này."
        });

    Task<AiRuleSyncResponseDto> SyncCategoryRuleStatusAsync(
        string categoryCode,
        bool isActive,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new AiRuleSyncResponseDto
        {
            Success = false,
            Message = "SyncCategoryRuleStatusAsync chưa được triển khai trên client này."
        });
}

public interface IWasteRuleEngineService
{
    Task<WasteSortingGuideDto?> GetSortingGuideAsync(string categoryCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<WasteSortingGuideDto>> GetAllRulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SortingRuleDetailDto>> GetAllRuleDetailsAsync(bool includeDraft = true, CancellationToken cancellationToken = default);
    Task<SortingRuleDetailDto> UpsertSortingRuleAsync(Guid adminUserId, Guid categoryId, UpsertSortingRuleRequest request, CancellationToken cancellationToken = default);
    Task<PublishSortingRuleResultDto> PublishSortingRuleAsync(Guid adminUserId, Guid ruleId, CancellationToken cancellationToken = default);
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
