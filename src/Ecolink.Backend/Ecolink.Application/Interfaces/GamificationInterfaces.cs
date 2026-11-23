using Ecolink.Application.Dtos;
using Ecolink.Domain.Enums;

namespace Ecolink.Application.Interfaces;

/// <summary>Ví điểm và sổ cái điểm (BR-GAM-01, BR-GAM-03, BR-GAM-08).</summary>
public interface IPointsService
{
    Task<WalletDto> GetWalletAsync(Guid userId, CancellationToken ct = default);
    Task<GamPagedResult<PointTransactionDto>> GetTransactionsAsync(Guid userId, DateTime? from, DateTime? to,
        string? actionType, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Cộng điểm một lần theo <paramref name="idempotencyKey"/>. Trả số điểm thực cộng; 0 nếu key đã xử lý trước đó.
    /// </summary>
    Task<long> CreditAsync(Guid userId, long points, PointActionType action, string? sourceType, string? sourceId,
        string idempotencyKey, string? description, CancellationToken ct = default);

    /// <summary>
    /// Trừ điểm nguyên tử (không bao giờ âm). Trả false nếu không đủ điểm. KHÔNG tự mở giao dịch: người gọi bọc trong
    /// transaction của mình để trừ điểm cùng các thay đổi khác.
    /// </summary>
    Task<bool> TryDebitAsync(Guid userId, long points, PointActionType action, string? sourceType, string? sourceId,
        string idempotencyKey, string? description, CancellationToken ct = default);

    /// <summary>
    /// BR-GAM-01: cộng điểm cho cả User và Recycler của biên nhận đã hoàn tất, mỗi bên đúng một lần, theo bảng
    /// điểm/kg đã cấu hình (A-07). Cập nhật points_awarded của biên nhận. Trả số điểm mỗi bên nhận.
    /// </summary>
    Task<long> AwardHandoverPointsAsync(Guid receiptId, CancellationToken ct = default);
}

public interface IGamificationConfigService
{
    Task<GamificationConfigDto> GetAsync(CancellationToken ct = default);
    Task<GamSettings> GetSettingsAsync(CancellationToken ct = default);
    Task<GamificationConfigDto> UpdateAsync(Guid adminId, GamificationConfigDto request, CancellationToken ct = default);
    Task<GamificationConfigDto> ResetToDefaultsAsync(Guid adminId, CancellationToken ct = default);
}

public interface ICheckinService
{
    Task<CheckinStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default);
    Task<CheckinResultDto> CheckInAsync(Guid userId, CancellationToken ct = default);
}

public interface IGiftService
{
    // Cửa hàng (User, Recycler)
    Task<List<GiftDto>> ListStoreAsync(string? type, string? sort, string? search, CancellationToken ct = default);
    Task<GiftDto> GetStoreGiftAsync(Guid giftId, CancellationToken ct = default);
    Task<RedeemResultDto> RedeemAsync(Guid userId, Guid giftId, RedeemGiftRequest request, CancellationToken ct = default);
    Task<GamPagedResult<RedemptionDto>> ListMyRedemptionsAsync(Guid userId, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<RedemptionDto> GetMyRedemptionAsync(Guid userId, Guid redemptionId, CancellationToken ct = default);

    // Staff (S-10)
    Task<GamPagedResult<GiftDto>> ListInventoryAsync(string? type, string? status, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<GiftDto> GetInventoryGiftAsync(Guid giftId, CancellationToken ct = default);
    Task<GiftDto> CreateAsync(SaveGiftRequest request, CancellationToken ct = default);
    Task<GiftDto> UpdateAsync(Guid giftId, SaveGiftRequest request, CancellationToken ct = default);
    Task<GiftDto> RestockAsync(Guid giftId, int quantity, CancellationToken ct = default);
    Task<ImportVouchersResultDto> ImportVouchersAsync(Guid giftId, ImportVouchersRequest request, CancellationToken ct = default);
    Task<GiftDto> SetStatusAsync(Guid giftId, string status, CancellationToken ct = default);
    Task<GamPagedResult<RedemptionDto>> ListGiftRedemptionsAsync(Guid giftId, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<GamPagedResult<RedemptionDto>> ListAllRedemptionsAsync(string? status, string? type, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<RedemptionDto> FulfillAsync(Guid redemptionId, CancellationToken ct = default);
    Task<RedemptionDto> FulfillAsync(Guid redemptionId, FulfillRedemptionRequest? request, CancellationToken ct = default);

    /// <summary>JOB-12: đánh dấu Expired các voucher đã quá hạn. Trả số dòng cập nhật.</summary>
    Task<int> ExpireVouchersAsync(CancellationToken ct = default);
}

public interface IVirtualTreeService
{
    Task<VirtualTreeDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task<VirtualTreeDto> CareAsync(Guid userId, string? actionType, CancellationToken ct = default);
    Task<WaterTreeResultDto> WaterAndUpgradeAsync(Guid userId, CancellationToken ct = default);
    Task<HarvestTreeResultDto> HarvestAsync(Guid userId, CancellationToken ct = default);
    Task<List<EcoFactDto>> ListUnlockedFactsAsync(Guid userId, CancellationToken ct = default);
    Task<List<EcoFactDto>> ListUnlockedFactsAsync(Guid userId, bool includeLocked, CancellationToken ct = default) =>
        ListUnlockedFactsAsync(userId, ct);
    Task<EcoFactDto> GetUnlockedFactAsync(Guid userId, Guid factId, CancellationToken ct = default);
}

public interface IEcoFactService
{
    Task<GamPagedResult<EcoFactDto>> ListAsync(int? treeLevel, string? status, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<EcoFactDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<EcoFactDto> CreateAsync(Guid staffId, SaveEcoFactRequest request, CancellationToken ct = default);
    Task<EcoFactDto> UpdateAsync(Guid id, SaveEcoFactRequest request, CancellationToken ct = default);
    Task<EcoFactDto> SetStatusAsync(Guid id, string status, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ILeaderboardService
{
    Task<List<LeaderboardCycleDto>> GetCyclesAsync(CancellationToken ct = default);
    Task<LeaderboardCycleDto> CreateCycleAsync(Guid adminId, SaveLeaderboardCycleRequest request, CancellationToken ct = default);
    Task<LeaderboardCycleDto> UpdateCycleAsync(Guid adminId, Guid cycleId, SaveLeaderboardCycleRequest request, CancellationToken ct = default);
    Task<LeaderboardCycleDto> ActivateCycleAsync(Guid adminId, Guid cycleId, CancellationToken ct = default);
    Task<LeaderboardCycleDto> FinalizeCycleAsync(Guid adminId, Guid cycleId, CancellationToken ct = default);
    Task<LeaderboardViewDto> GetLeaderboardViewAsync(int top = 10, string? periodType = null, Guid? cycleId = null, Guid? viewerUserId = null, CancellationToken ct = default) =>
        GetLeaderboardViewAsync(top, periodType, cycleId, viewerUserId, null, ct);
    Task<LeaderboardViewDto> GetLeaderboardViewAsync(int top, string? periodType, Guid? cycleId, Guid? viewerUserId, string? role, CancellationToken ct = default);
}
