using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Dịch vụ tìm kiếm cơ sở thu gom theo bán kính địa lý GPS (UC U-05).
/// Kết nối trực tiếp với dữ liệu bảng recycler_profiles và recycler_price_list từ Module R-01 &amp; R-02
/// (BR-MTH-01, BR-MTH-03, BR-MTH-04, BR-MTH-05, BR-MTH-06, BR-MTH-07).
/// </summary>
public class GeoMatchingService : IGeoMatchingService
{
    private readonly ILogger<GeoMatchingService> _logger;
    private readonly ApplicationDbContext? _db;
    private readonly IWasteRuleEngineService? _ruleEngine;

    // Danh sách dự phòng khi chạy ở chế độ không kết nối CSDL
    private static readonly List<CollectionPartnerDto> _mockPartners = new()
    {
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FacilityName = "Vựa Phế Liệu Xanh Quận 1",
            Address = "123 Nguyễn Thị Minh Khai, Phường Bến Thành, Quận 1, TP.HCM",
            Latitude = 10.7725,
            Longitude = 106.6920,
            ContactPhone = "0901234567",
            OperatingHours = "08:00 - 18:00",
            Materials = new List<AcceptedMaterialDto>
            {
                new() { CategoryCode = "plastic", CategoryName = "Nhựa tái chế", PricePerKg = 7000m, MinimumQuantityKg = 1m },
                new() { CategoryCode = "paper", CategoryName = "Giấy báo & Carton", PricePerKg = 4500m, MinimumQuantityKg = 2m },
                new() { CategoryCode = "metal", CategoryName = "Kim loại / Vỏ lon", PricePerKg = 25000m, MinimumQuantityKg = 0.5m }
            }
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            FacilityName = "Cơ Sở Thu Mua Tái Chế Bình Thạnh",
            Address = "45 Điện Biên Phủ, Phường 15, Quận Bình Thạnh, TP.HCM",
            Latitude = 10.7960,
            Longitude = 106.7080,
            ContactPhone = "0912345678",
            OperatingHours = "07:30 - 18:30",
            Materials = new List<AcceptedMaterialDto>
            {
                new() { CategoryCode = "plastic", CategoryName = "Nhựa tái chế", PricePerKg = 7500m, MinimumQuantityKg = 1.5m },
                new() { CategoryCode = "glass", CategoryName = "Chai lọ thủy tinh", PricePerKg = 2000m, MinimumQuantityKg = 5m },
                new() { CategoryCode = "cardboard", CategoryName = "Bìa Carton", PricePerKg = 3800m, MinimumQuantityKg = 3m }
            }
        },
        new()
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            FacilityName = "Trạm Thu Gom Phế Liệu Tân Bình",
            Address = "88 Cộng Hòa, Phường 4, Quận Tân Bình, TP.HCM",
            Latitude = 10.7995,
            Longitude = 106.6570,
            ContactPhone = "0934567890",
            OperatingHours = "08:00 - 17:30",
            Materials = new List<AcceptedMaterialDto>
            {
                new() { CategoryCode = "metal", CategoryName = "Kim loại & Đồng, Nhôm", PricePerKg = 28000m, MinimumQuantityKg = 1m },
                new() { CategoryCode = "paper", CategoryName = "Giấy vụn, Hồ sơ", PricePerKg = 5000m, MinimumQuantityKg = 2m }
            }
        }
    };

    public GeoMatchingService(
        ILogger<GeoMatchingService> logger,
        ApplicationDbContext? db = null,
        IWasteRuleEngineService? ruleEngine = null)
    {
        _logger = logger;
        _db = db;
        _ruleEngine = ruleEngine;
    }

    public async Task<IEnumerable<CollectionPartnerDto>> FindNearestPartnersAsync(
        double latitude,
        double longitude,
        string? categoryCode = null,
        double radiusKm = 10.0,
        CancellationToken cancellationToken = default)
    {
        var normalizedCategory = string.IsNullOrWhiteSpace(categoryCode)
            ? null
            : categoryCode.Trim().ToLowerInvariant();

        _logger.LogInformation(
            "Tìm cơ sở thu mua đã xác minh (Verified + Online) gần tọa độ trong bán kính {Radius}km, loại rác: {Cat}",
            radiusKm, normalizedCategory ?? "Tất cả");

        var results = new List<CollectionPartnerDto>();

        if (_db != null)
        {
            await EnsureDemoCollectorPricesSeededAsync(cancellationToken);

            // Truy vấn trực tiếp bảng recycler_profiles + recycler_price_list (R-01, R-02)
            // Tuân thủ BR-MTH-01 (Verified), BR-MTH-03 & BR-MTH-07 (Prices Active), BR-MTH-06 (Online & User Active)
            var dbProfiles = await _db.RecyclerProfiles
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.Prices.Where(p => p.Status == PriceStatus.Active))
                    .ThenInclude(p => p.WasteCategory)
                .Where(r => r.VerificationStatus == VerificationStatus.Verified
                            && r.ActiveStatus == RecyclerActiveStatus.Online
                            && r.User != null
                            && r.User.Status == UserStatus.Active
                            && r.Prices.Any(p => p.Status == PriceStatus.Active
                                && p.WasteCategory != null
                                && (normalizedCategory == null || p.WasteCategory.Code.ToLower() == normalizedCategory)))
                .ToListAsync(cancellationToken);

            foreach (var profile in dbProfiles)
            {
                var distance = CalculateDistanceKm(
                    latitude,
                    longitude,
                    (double)profile.Latitude,
                    (double)profile.Longitude);

                if (distance <= radiusKm)
                {
                    results.Add(MapProfileToPartnerDto(profile, Math.Round(distance, 2)));
                }
            }

            return results.OrderBy(p => p.DistanceKm).ToList();
        }

        // Fallback khi chạy không có kết nối PostgreSQL
        foreach (var partner in _mockPartners)
        {
            if (normalizedCategory != null &&
                !partner.Materials.Any(m => m.CategoryCode.Equals(normalizedCategory, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var distance = CalculateDistanceKm(latitude, longitude, partner.Latitude, partner.Longitude);
            if (distance <= radiusKm)
            {
                results.Add(new CollectionPartnerDto
                {
                    Id = partner.Id,
                    FacilityName = partner.FacilityName,
                    Address = partner.Address,
                    Latitude = partner.Latitude,
                    Longitude = partner.Longitude,
                    ContactPhone = partner.ContactPhone,
                    OperatingHours = partner.OperatingHours,
                    DistanceKm = Math.Round(distance, 2),
                    Materials = partner.Materials
                });
            }
        }

        return results.OrderBy(p => p.DistanceKm).ToList();
    }

    public async Task<NearbyRecyclerSearchResponseDto> SearchNearbyRecyclersAsync(
        NearbyRecyclerFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var partners = (await FindNearestPartnersAsync(
            filter.Lat,
            filter.Lng,
            filter.Category,
            filter.RadiusKm,
            cancellationToken)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLowerInvariant();
            partners = partners
                .Where(p => p.FacilityName.ToLowerInvariant().Contains(kw)
                            || p.Address.ToLowerInvariant().Contains(kw))
                .ToList();
        }

        if (string.Equals(filter.SortBy, "price_desc", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(filter.Category))
        {
            var cat = filter.Category.Trim().ToLowerInvariant();
            partners = partners
                .OrderByDescending(p => p.Materials
                    .FirstOrDefault(m => m.CategoryCode.Equals(cat, StringComparison.OrdinalIgnoreCase))?.PricePerKg ?? 0m)
                .ThenBy(p => p.DistanceKm)
                .ToList();
        }

        var response = new NearbyRecyclerSearchResponseDto
        {
            TotalFound = partners.Count,
            SearchRadiusKm = filter.RadiusKm,
            FilteredCategory = filter.Category,
            Items = partners
        };

        if (partners.Count == 0)
        {
            var categoryCode = !string.IsNullOrWhiteSpace(filter.Category)
                ? filter.Category.Trim().ToLowerInvariant()
                : "plastic";

            var sortingGuide = _ruleEngine != null
                ? await _ruleEngine.GetSortingGuideAsync(categoryCode, cancellationToken)
                : null;

            response.FallbackSuggestion = new DiscoveryFallbackSuggestionDto
            {
                SuggestedExpandedRadiusKm = Math.Min(50.0, Math.Round(filter.RadiusKm * 2.5, 1)),
                GuidanceMessage = "Không tìm thấy cơ sở thu mua rác gần bạn trong bán kính hiện tại. Bạn có thể mở rộng bán kính tìm kiếm, hoặc xử lý theo hướng dẫn thùng rác công cộng bên dưới.",
                PublicBinSortingGuide = sortingGuide,
                AllowCommunityPickupPost = true
            };
        }

        return response;
    }

    public async Task<CollectionPartnerDto> GetRecyclerPublicDetailAsync(
        Guid recyclerId,
        double? userLat = null,
        double? userLng = null,
        CancellationToken cancellationToken = default)
    {
        if (_db != null)
        {
            var profile = await _db.RecyclerProfiles
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.Prices.Where(p => p.Status == PriceStatus.Active))
                    .ThenInclude(p => p.WasteCategory)
                .FirstOrDefaultAsync(r => r.UserId == recyclerId
                                          && r.VerificationStatus == VerificationStatus.Verified
                                          && r.User != null
                                          && r.User.Status == UserStatus.Active,
                    cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy cơ sở thu mua hoặc cơ sở chưa được phê duyệt.");

            var distanceKm = (userLat.HasValue && userLng.HasValue && (userLat.Value != 0 || userLng.Value != 0))
                ? Math.Round(CalculateDistanceKm(userLat.Value, userLng.Value, (double)profile.Latitude, (double)profile.Longitude), 2)
                : 0.0;

            return MapProfileToPartnerDto(profile, distanceKm);
        }

        var mock = _mockPartners.FirstOrDefault(p => p.Id == recyclerId)
                   ?? throw new NotFoundException("Không tìm thấy cơ sở thu mua.");

        return mock;
    }

    private static CollectionPartnerDto MapProfileToPartnerDto(RecyclerProfile profile, double distanceKm)
    {
        var activeMaterials = profile.Prices
            .Where(p => p.Status == PriceStatus.Active && p.WasteCategory != null)
            .OrderBy(p => p.WasteCategory!.Name)
            .Select(p => new AcceptedMaterialDto
            {
                CategoryCode = p.WasteCategory!.Code,
                CategoryName = p.WasteCategory.Name,
                PricePerKg = p.PricePerKg,
                MinimumQuantityKg = p.MinQuantityKg,
                CollectionConditions = p.CollectionConditions,
                LastUpdatedAt = p.UpdatedAt
            })
            .ToList();

        return new CollectionPartnerDto
        {
            Id = profile.UserId,
            FacilityName = profile.FacilityName,
            RepresentativeName = profile.RepresentativeName,
            Address = profile.BusinessAddress,
            Latitude = (double)profile.Latitude,
            Longitude = (double)profile.Longitude,
            DistanceKm = distanceKm,
            ContactPhone = profile.ContactPhone,
            OperatingHours = profile.OperatingHours,
            Description = profile.Description,
            BusinessPhotoUrl = profile.BusinessPhotoUrl,
            Materials = activeMaterials
        };
    }

    private async Task EnsureDemoCollectorPricesSeededAsync(CancellationToken ct)
    {
        if (_db == null) return;

        var demoCollectorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        if (!await _db.RecyclerProfiles.AnyAsync(r => r.UserId == demoCollectorId, ct))
        {
            return;
        }

        if (await _db.RecyclerPrices.AnyAsync(p => p.RecyclerId == demoCollectorId, ct))
        {
            return;
        }

        var defaultCategories = new (string Code, string Name, string BinColor, bool IsRecyclable)[]
        {
            ("plastic", "Nhựa tái chế (Plastic)", "Cam", true),
            ("paper", "Giấy báo & Tài liệu (Paper)", "Xanh Dương", true),
            ("glass", "Thủy tinh (Glass)", "Trắng", true),
            ("metal", "Kim loại & Vỏ lon (Metal)", "Xám Kim Loại", true),
            ("cardboard", "Bìa Carton (Cardboard)", "Nâu Vàng", true),
            ("trash", "Rác thải thông thường / Không thể tái chế", "Đen", false)
        };

        var existingCodes = await _db.WasteCategories.Select(c => c.Code).ToListAsync(ct);
        var missingCategories = defaultCategories.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missingCategories.Count > 0)
        {
            foreach (var (code, name, binColor, isRecyclable) in missingCategories)
            {
                _db.WasteCategories.Add(new WasteCategory
                {
                    Code = code,
                    Name = name,
                    StandardBinColor = binColor,
                    IsRecyclable = isRecyclable,
                    Status = "Active"
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        var categoriesByCode = await _db.WasteCategories
            .Where(c => c.IsRecyclable)
            .ToDictionaryAsync(c => c.Code, ct);

        var demoPrices = new (string Code, decimal PricePerKg, decimal MinKg, string Conditions)[]
        {
            ("plastic", 7000m, 1.0m, "Chai nhựa PET/HDPE đã tráng sạch, bẹp chai"),
            ("paper", 4500m, 2.0m, "Giấy báo, tài liệu khô ráo, không dính dầu mỡ"),
            ("metal", 25000m, 0.5m, "Vỏ lon nhôm, sắt vụn đã dốc sạch nước"),
            ("cardboard", 3800m, 3.0m, "Thùng carton gập phẳng, khô ráo")
        };

        foreach (var (code, price, minKg, cond) in demoPrices)
        {
            if (categoriesByCode.TryGetValue(code, out var cat))
            {
                _db.RecyclerPrices.Add(new RecyclerPrice
                {
                    RecyclerId = demoCollectorId,
                    WasteCategoryId = cat.Id,
                    PricePerKg = price,
                    MinQuantityKg = minKg,
                    CollectionConditions = cond,
                    Status = PriceStatus.Active
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
