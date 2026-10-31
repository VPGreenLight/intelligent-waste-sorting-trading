using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

/// <summary>
/// Nghiệp vụ Recycler Profile, Pricing &amp; Availability (UC R-01, R-02).
/// recyclerId luôn là UserId của Recycler đang đăng nhập (lấy từ JWT).
/// </summary>
public interface IRecyclerManagementService
{
    // Danh mục chuẩn
    Task<IReadOnlyList<WasteCategoryDto>> GetWasteCategoriesAsync(CancellationToken ct = default);

    // Profile
    Task<RecyclerProfileDto> GetProfileAsync(Guid recyclerId, CancellationToken ct = default);
    Task<RecyclerProfileDto> UpdateProfileAsync(Guid recyclerId, UpdateRecyclerProfileRequest request, CancellationToken ct = default);

    // Pricing (R-01)
    Task<IReadOnlyList<RecyclerPriceDto>> GetPricesAsync(Guid recyclerId, string? keyword, bool? isActive, CancellationToken ct = default);
    Task<RecyclerPriceDto> AddPriceAsync(Guid recyclerId, CreateRecyclerPriceRequest request, CancellationToken ct = default);
    Task<RecyclerPriceDto> UpdatePriceAsync(Guid recyclerId, Guid priceId, UpdateRecyclerPriceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<RecyclerPriceDto>> BulkSavePricesAsync(Guid recyclerId, BulkSavePricesRequest request, CancellationToken ct = default);

    // Availability (R-02)
    Task<AvailabilityDto> GetAvailabilityAsync(Guid recyclerId, CancellationToken ct = default);
    Task<AvailabilityDto> UpdateAvailabilityAsync(Guid recyclerId, UpdateAvailabilityRequest request, CancellationToken ct = default);
    Task<AvailabilityDto> UpdateScheduleAsync(Guid recyclerId, UpdateScheduleRequest request, CancellationToken ct = default);
}
