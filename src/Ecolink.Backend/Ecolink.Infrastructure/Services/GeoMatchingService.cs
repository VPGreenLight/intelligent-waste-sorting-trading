using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Dịch vụ tìm kiếm cơ sở thu gom theo bán kính địa lý GPS (UC U-05).
/// Kết nối trực tiếp với bảng recycler_profiles (cột không gian PostGIS geom SRID 4326 + GiST index)
/// và recycler_price_list từ Module R-01 &amp; R-02
/// (BR-MTH-01, BR-MTH-03, BR-MTH-04, BR-MTH-05, BR-MTH-06, BR-MTH-07).
/// </summary>
public class GeoMatchingService : IGeoMatchingService
{
    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static bool _demoSeedChecked;

    private readonly ILogger<GeoMatchingService> _logger;
    private readonly ApplicationDbContext? _db;
    private readonly IWasteRuleEngineService? _ruleEngine;

    // Danh sách dự phòng khi chạy ở chế độ không kết nối CSDL (ưu tiên khu vực Hòa Lạc & Hà Nội)
    private static readonly List<CollectionPartnerDto> _mockPartners = new()
    {
        new()
        {
            Id = Guid.Parse("99999991-9999-9999-9999-999999999991"),
            FacilityName = "Trạm Thu Gom Tái Chế Xanh Hòa Lạc",
            RepresentativeName = "Đỗ Văn Hùng",
            Address = "Thôn 3, Xã Tân Xã (Gần Khu CNC Hòa Lạc & ĐH FPT), Thạch Thất, Hà Nội",
            Latitude = 21.0182,
            Longitude = 105.5345,
            ContactPhone = "0968112233",
            OperatingHours = "07:30 - 19:00",
            Materials = new List<AcceptedMaterialDto>
            {
                new() { CategoryCode = "plastic", CategoryName = "Nhựa tái chế (Plastic)", PricePerKg = 7200m, MinimumQuantityKg = 1m },
                new() { CategoryCode = "paper", CategoryName = "Giấy báo & Tài liệu (Paper)", PricePerKg = 4800m, MinimumQuantityKg = 1.5m },
                new() { CategoryCode = "cardboard", CategoryName = "Bìa Carton (Cardboard)", PricePerKg = 4100m, MinimumQuantityKg = 2m },
                new() { CategoryCode = "metal", CategoryName = "Kim loại & Vỏ lon (Metal)", PricePerKg = 26000m, MinimumQuantityKg = 0.5m }
            }
        },
        new()
        {
            Id = Guid.Parse("99999992-9999-9999-9999-999999999992"),
            FacilityName = "Cơ Sở Phế Liệu Minh Tâm — Ngã Tư Hòa Lạc",
            RepresentativeName = "Nguyễn Thị Tâm",
            Address = "Km28 Quốc lộ 21A, Xã Thạch Hòa, Huyện Thạch Thất, Hà Nội",
            Latitude = 20.9986,
            Longitude = 105.5184,
            ContactPhone = "0973445566",
            OperatingHours = "07:00 - 18:30",
            Materials = new List<AcceptedMaterialDto>
            {
                new() { CategoryCode = "plastic", CategoryName = "Nhựa tái chế (Plastic)", PricePerKg = 7000m, MinimumQuantityKg = 1m },
                new() { CategoryCode = "glass", CategoryName = "Thủy tinh (Glass)", PricePerKg = 2400m, MinimumQuantityKg = 4m },
                new() { CategoryCode = "metal", CategoryName = "Kim loại & Vỏ lon (Metal)", PricePerKg = 25500m, MinimumQuantityKg = 1m },
                new() { CategoryCode = "cardboard", CategoryName = "Bìa Carton (Cardboard)", PricePerKg = 3900m, MinimumQuantityKg = 2.5m }
            }
        },
        new()
        {
            Id = Guid.Parse("99999993-9999-9999-9999-999999999993"),
            FacilityName = "Vựa Tái Chế Bình Yên — Hòa Lạc",
            RepresentativeName = "Chu Văn Khánh",
            Address = "Đường Tỉnh lộ 420, Xã Bình Yên, Huyện Thạch Thất, Hà Nội",
            Latitude = 21.0345,
            Longitude = 105.5468,
            ContactPhone = "0915889900",
            OperatingHours = "07:30 - 18:00",
            Materials = new List<AcceptedMaterialDto>
            {
                new() { CategoryCode = "metal", CategoryName = "Kim loại & Vỏ lon (Metal)", PricePerKg = 27000m, MinimumQuantityKg = 1m },
                new() { CategoryCode = "paper", CategoryName = "Giấy báo & Tài liệu (Paper)", PricePerKg = 4900m, MinimumQuantityKg = 2m },
                new() { CategoryCode = "cardboard", CategoryName = "Bìa Carton (Cardboard)", PricePerKg = 4200m, MinimumQuantityKg = 3m },
                new() { CategoryCode = "plastic", CategoryName = "Nhựa tái chế (Plastic)", PricePerKg = 6800m, MinimumQuantityKg = 1.5m }
            }
        },
        new()
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            FacilityName = "Trạm Tái Chế Xanh Cầu Giấy",
            RepresentativeName = "Hoàng Minh Tuấn",
            Address = "68 Xuân Thủy, Dịch Vọng Hậu, Cầu Giấy, Hà Nội",
            Latitude = 21.0368,
            Longitude = 105.7825,
            ContactPhone = "0944556677",
            OperatingHours = "08:00 - 18:30",
            Materials = new List<AcceptedMaterialDto>
            {
                new() { CategoryCode = "plastic", CategoryName = "Nhựa tái chế (Plastic)", PricePerKg = 6900m, MinimumQuantityKg = 1m },
                new() { CategoryCode = "paper", CategoryName = "Giấy báo & Tài liệu (Paper)", PricePerKg = 4700m, MinimumQuantityKg = 2m },
                new() { CategoryCode = "metal", CategoryName = "Kim loại & Vỏ lon (Metal)", PricePerKg = 25500m, MinimumQuantityKg = 0.5m },
                new() { CategoryCode = "cardboard", CategoryName = "Bìa Carton (Cardboard)", PricePerKg = 3900m, MinimumQuantityKg = 2m }
            }
        },
        new()
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            FacilityName = "Vựa Phế Liệu Xanh Quận 1",
            RepresentativeName = "Nguyễn Văn Minh",
            Address = "123 Nguyễn Thị Minh Khai, Phường Bến Thành, Quận 1, TP.HCM",
            Latitude = 10.7725,
            Longitude = 106.6920,
            ContactPhone = "0901234567",
            OperatingHours = "08:00 - 18:00",
            Materials = new List<AcceptedMaterialDto>
            {
                new() { CategoryCode = "plastic", CategoryName = "Nhựa tái chế (Plastic)", PricePerKg = 6800m, MinimumQuantityKg = 1.5m },
                new() { CategoryCode = "paper", CategoryName = "Giấy báo & Tài liệu (Paper)", PricePerKg = 4500m, MinimumQuantityKg = 2m },
                new() { CategoryCode = "glass", CategoryName = "Thủy tinh (Glass)", PricePerKg = 2200m, MinimumQuantityKg = 5m },
                new() { CategoryCode = "metal", CategoryName = "Kim loại & Vỏ lon (Metal)", PricePerKg = 24500m, MinimumQuantityKg = 0.5m }
            }
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            FacilityName = "Vựa phế liệu EcoGreen",
            RepresentativeName = "Trần Thu Gom",
            Address = "123 Đường Nguyễn Hữu Cảnh, P. 22, Bình Thạnh, TP.HCM",
            Latitude = 10.7938,
            Longitude = 106.7182,
            ContactPhone = "0988776655",
            OperatingHours = "07:30 - 18:30",
            Materials = new List<AcceptedMaterialDto>
            {
                new() { CategoryCode = "plastic", CategoryName = "Nhựa tái chế (Plastic)", PricePerKg = 7000m, MinimumQuantityKg = 1m },
                new() { CategoryCode = "paper", CategoryName = "Giấy báo & Tài liệu (Paper)", PricePerKg = 4500m, MinimumQuantityKg = 2m },
                new() { CategoryCode = "metal", CategoryName = "Kim loại & Vỏ lon (Metal)", PricePerKg = 25000m, MinimumQuantityKg = 0.5m },
                new() { CategoryCode = "cardboard", CategoryName = "Bìa Carton (Cardboard)", PricePerKg = 3800m, MinimumQuantityKg = 3m }
            }
        },
        new()
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            FacilityName = "Trạm Thu Gom Tái Chế Tân Bình",
            RepresentativeName = "Lê Hoàng Phúc",
            Address = "88 Cộng Hòa, Phường 4, Quận Tân Bình, TP.HCM",
            Latitude = 10.7995,
            Longitude = 106.6570,
            ContactPhone = "0934567890",
            OperatingHours = "08:00 - 17:30",
            Materials = new List<AcceptedMaterialDto>
            {
                new() { CategoryCode = "metal", CategoryName = "Kim loại & Vỏ lon (Metal)", PricePerKg = 26500m, MinimumQuantityKg = 1m },
                new() { CategoryCode = "paper", CategoryName = "Giấy báo & Tài liệu (Paper)", PricePerKg = 4800m, MinimumQuantityKg = 2m },
                new() { CategoryCode = "cardboard", CategoryName = "Bìa Carton (Cardboard)", PricePerKg = 4000m, MinimumQuantityKg = 2.5m }
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

        var safeRadiusKm = Math.Clamp(radiusKm, 0.5, 50.0);

        _logger.LogInformation(
            "Tìm cơ sở thu mua đã xác minh (Verified + Online) qua PostGIS ST_DWithin trong bán kính {Radius}km, loại rác: {Cat}",
            safeRadiusKm, normalizedCategory ?? "Tất cả");

        var results = new List<CollectionPartnerDto>();

        if (_db != null)
        {
            await EnsureDemoCollectorPricesSeededAsync(cancellationToken);

            // Cột geom trong recycler_profiles có kiểu geometry(Point, 4326) kèm GiST index (gix_recycler_profiles_geom).
            // Quy đổi bán kính km sang ngưỡng độ (degrees) bao phủ an toàn theo vĩ độ hiện tại để PostgreSQL lọc trước
            // bằng chỉ mục không gian ST_DWithin(geom, searchPoint, maxDegreeRadius), sau đó tinh chỉnh chính xác bằng Haversine.
            var searchPoint = new Point(longitude, latitude) { SRID = 4326 };
            var cosLat = Math.Max(0.1, Math.Cos(DegreesToRadians(latitude)));
            var latDeltaDeg = safeRadiusKm / 110.574;
            var lngDeltaDeg = safeRadiusKm / (111.320 * cosLat);
            var maxDegreeRadius = Math.Max(latDeltaDeg, lngDeltaDeg) * 1.05;

            var minLat = (decimal)(latitude - latDeltaDeg * 1.05);
            var maxLat = (decimal)(latitude + latDeltaDeg * 1.05);
            var minLng = (decimal)(longitude - lngDeltaDeg * 1.05);
            var maxLng = (decimal)(longitude + lngDeltaDeg * 1.05);

            // Truy vấn không gian trực tiếp trên PostgreSQL + PostGIS (ST_DWithin trên gix_recycler_profiles_geom)
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
                            && ((r.Location != null && r.Location.IsWithinDistance(searchPoint, maxDegreeRadius))
                                || (r.Location == null
                                    && r.Latitude >= minLat && r.Latitude <= maxLat
                                    && r.Longitude >= minLng && r.Longitude <= maxLng))
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

                if (distance <= safeRadiusKm)
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
            if (distance <= safeRadiusKm)
            {
                results.Add(new CollectionPartnerDto
                {
                    Id = partner.Id,
                    FacilityName = partner.FacilityName,
                    RepresentativeName = partner.RepresentativeName,
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
            await EnsureDemoCollectorPricesSeededAsync(cancellationToken);

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
        if (_db == null || _demoSeedChecked) return;

        await SeedLock.WaitAsync(ct);
        try
        {
            if (_demoSeedChecked) return;

            // 1. Đảm bảo 6 danh mục phế liệu chuẩn luôn tồn tại
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

            // 2. Đảm bảo các cơ sở thu gom mẫu đã xác minh (Verified + Online) sẵn sàng ngay lần đầu mở /map
            var demoFacilities = new (
                Guid UserId,
                string Username,
                string Email,
                string FacilityName,
                string RepresentativeName,
                string Address,
                decimal Lat,
                decimal Lng,
                string Phone,
                string Hours,
                string Description,
                (string Code, decimal Price, decimal MinKg, string Cond)[] Prices
            )[]
            {
                (
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    "collector",
                    "collector@ecolink.vn",
                    "Vựa phế liệu EcoGreen",
                    "Trần Thu Gom",
                    "123 Đường Nguyễn Hữu Cảnh, P. 22, Bình Thạnh, TP.HCM",
                    10.7938m,
                    106.7182m,
                    "0988776655",
                    "07:30 - 18:30",
                    "Chuyên thu mua nhựa sạch, bìa carton và vỏ lon nhôm tận nơi khu vực Bình Thạnh - Quận 1.",
                    new[]
                    {
                        ("plastic", 7000m, 1.0m, "Chai nhựa PET/HDPE đã tráng sạch, bẹp chai"),
                        ("paper", 4500m, 2.0m, "Giấy báo, tài liệu khô ráo, không dính dầu mỡ"),
                        ("metal", 25000m, 0.5m, "Vỏ lon nhôm, sắt vụn đã dốc sạch nước"),
                        ("cardboard", 3800m, 3.0m, "Thùng carton gập phẳng, khô ráo")
                    }
                ),
                (
                    Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    "collector_q1",
                    "collector_q1@ecolink.vn",
                    "Vựa Phế Liệu Xanh Quận 1",
                    "Nguyễn Văn Minh",
                    "123 Nguyễn Thị Minh Khai, P. Bến Thành, Quận 1, TP.HCM",
                    10.7725m,
                    106.6920m,
                    "0901234567",
                    "08:00 - 18:00",
                    "Điểm tiếp nhận phế liệu tái chế trung tâm Quận 1, cân điện tử minh bạch tại chỗ.",
                    new[]
                    {
                        ("plastic", 6800m, 1.5m, "Bóc nhãn rời, súc sạch cặn đồ uống"),
                        ("paper", 4600m, 2.0m, "Giấy văn phòng, sách báo cũ cột gọn"),
                        ("glass", 2200m, 5.0m, "Chai lọ nguyên vẹn, không lẫn kính cường lực"),
                        ("metal", 24500m, 0.5m, "Lon nước ngọt nhôm, hộp thiếc sạch")
                    }
                ),
                (
                    Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    "collector_tb",
                    "collector_tb@ecolink.vn",
                    "Trạm Thu Gom Tái Chế Tân Bình",
                    "Lê Hoàng Phúc",
                    "88 Cộng Hòa, Phường 4, Quận Tân Bình, TP.HCM",
                    10.7995m,
                    106.6570m,
                    "0934567890",
                    "08:00 - 17:30",
                    "Thu mua kim loại màu, lon nhôm, bìa carton số lượng từ hộ gia đình và văn phòng.",
                    new[]
                    {
                        ("metal", 26500m, 1.0m, "Nhôm lon, đồng, sắt vụn phân loại riêng"),
                        ("paper", 4800m, 2.0m, "Hồ sơ giấy, tạp chí khô sạch"),
                        ("cardboard", 4000m, 2.5m, "Thùng giấy gập gọn, tháo băng keo dày"),
                        ("plastic", 6600m, 2.0m, "Nhựa cứng, can nhựa HDPE/PET sạch")
                    }
                ),
                (
                    Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    "collector_q7",
                    "collector_q7@ecolink.vn",
                    "Cơ Sở Phế Liệu Nam Sài Gòn",
                    "Phạm Thị Lan",
                    "45 Nguyễn Thị Thập, P. Tân Phú, Quận 7, TP.HCM",
                    10.7324m,
                    106.7215m,
                    "0977112233",
                    "07:00 - 18:00",
                    "Hỗ trợ thu gom tận sảnh chung cư khu vực Quận 7, Nhà Bè và Quận 4.",
                    new[]
                    {
                        ("plastic", 7200m, 1.0m, "Chai PET trong suốt đã tráng sạch"),
                        ("cardboard", 3900m, 3.0m, "Bìa carton đóng kiện phẳng"),
                        ("glass", 2100m, 5.0m, "Chai thuỷ tinh trắng và nâu không vỡ")
                    }
                ),
                (
                    Guid.Parse("77777777-7777-7777-7777-777777777777"),
                    "collector_hn",
                    "collector_hn@ecolink.vn",
                    "Trạm Tái Chế Xanh Cầu Giấy",
                    "Hoàng Minh Tuấn",
                    "68 Xuân Thủy, Dịch Vọng Hậu, Cầu Giấy, Hà Nội",
                    21.0368m,
                    105.7825m,
                    "0944556677",
                    "08:00 - 18:30",
                    "Trạm thu mua phế liệu sinh hoạt khu vực Cầu Giấy - Mỹ Đình - Đống Đa.",
                    new[]
                    {
                        ("plastic", 6900m, 1.0m, "Chai nhựa sạch, đã tháo nắp"),
                        ("paper", 4700m, 2.0m, "Giấy báo, vở học sinh cũ khô ráo"),
                        ("metal", 25500m, 0.5m, "Vỏ lon bia, nước ngọt nhôm"),
                        ("cardboard", 3900m, 2.0m, "Bìa carton khô, không rách mục")
                    }
                ),
                (
                    Guid.Parse("99999991-9999-9999-9999-999999999991"),
                    "collector_hl1",
                    "collector_hl1@ecolink.vn",
                    "Trạm Thu Gom Tái Chế Xanh Hòa Lạc",
                    "Đỗ Văn Hùng",
                    "Thôn 3, Xã Tân Xã (Gần Khu CNC Hòa Lạc & ĐH FPT), Huyện Thạch Thất, Hà Nội",
                    21.0182m,
                    105.5345m,
                    "0968112233",
                    "07:30 - 19:00",
                    "Chuyên thu mua chai nhựa, vỏ lon, bìa carton và giáo trình cũ tại khu KTX sinh viên, nhà trọ Tân Xã - Khu CNC Hòa Lạc.",
                    new[]
                    {
                        ("plastic", 7200m, 1.0m, "Chai nhựa PET/HDPE đã tráng sạch, bẹp chai"),
                        ("paper", 4800m, 1.5m, "Giấy vở, tài liệu in ấn, sách báo khô ráo"),
                        ("cardboard", 4100m, 2.0m, "Thùng carton chuyển trọ gập phẳng"),
                        ("metal", 26000m, 0.5m, "Vỏ lon nước ngọt, lon nhôm sinh hoạt đã dốc sạch")
                    }
                ),
                (
                    Guid.Parse("99999992-9999-9999-9999-999999999992"),
                    "collector_hl2",
                    "collector_hl2@ecolink.vn",
                    "Cơ Sở Phế Liệu Minh Tâm — Ngã Tư Hòa Lạc",
                    "Nguyễn Thị Tâm",
                    "Km28 Quốc lộ 21A, Xã Thạch Hòa, Huyện Thạch Thất, Hà Nội",
                    20.9986m,
                    105.5184m,
                    "0973445566",
                    "07:00 - 18:30",
                    "Điểm cân điện tử minh bạch ngay trục Quốc lộ 21A - Ngã tư Hòa Lạc, hỗ trợ thu gom tận sảnh tòa nhà & khu trọ sinh viên.",
                    new[]
                    {
                        ("plastic", 7000m, 1.0m, "Bóc nhãn rời nếu có, không lẫn rác thực phẩm"),
                        ("glass", 2400m, 4.0m, "Chai lọ thủy tinh nguyên vẹn, không vỡ vụn"),
                        ("metal", 25500m, 1.0m, "Nhôm lon, sắt vụn, thiết bị gia dụng kim loại"),
                        ("cardboard", 3900m, 2.5m, "Bìa carton khô, buộc gọn")
                    }
                ),
                (
                    Guid.Parse("99999993-9999-9999-9999-999999999993"),
                    "collector_hl3",
                    "collector_hl3@ecolink.vn",
                    "Vựa Tái Chế Bình Yên — Hòa Lạc",
                    "Chu Văn Khánh",
                    "Đường Tỉnh lộ 420, Xã Bình Yên, Huyện Thạch Thất, Hà Nội",
                    21.0345m,
                    105.5468m,
                    "0915889900",
                    "07:30 - 18:00",
                    "Cơ sở thu mua kim loại màu, sắt vụn, giấy báo và thùng carton khu vực Bình Yên - Khu CNC Hòa Lạc - Liên Quan.",
                    new[]
                    {
                        ("metal", 27000m, 1.0m, "Đồng, nhôm lon, sắt thép phân loại riêng"),
                        ("paper", 4900m, 2.0m, "Hồ sơ văn phòng, báo chí, sách vở khô sạch"),
                        ("cardboard", 4200m, 3.0m, "Thùng carton số lượng từ hộ gia đình & cửa hàng"),
                        ("plastic", 6800m, 1.5m, "Can nhựa HDPE, vỏ chai PET sạch"),
                        ("glass", 2200m, 5.0m, "Chai thủy tinh trắng và nâu không lẫn sành sứ")
                    }
                ),
                (
                    Guid.Parse("99999994-9999-9999-9999-999999999994"),
                    "collector_hl4",
                    "collector_hl4@ecolink.vn",
                    "Điểm Thu Mua Phế Liệu Phú Cát — Hòa Lạc",
                    "Bùi Văn Thành",
                    "Khu Công nghiệp Phú Cát - Nam Hòa Lạc, Huyện Quốc Oai, Hà Nội",
                    20.9765m,
                    105.5192m,
                    "0984667788",
                    "07:30 - 18:00",
                    "Nhận thu mua phế liệu tái chế định kỳ cho văn phòng công nghệ, xưởng sản xuất và khu dân cư Nam Hòa Lạc - Phú Cát.",
                    new[]
                    {
                        ("cardboard", 4300m, 3.0m, "Bìa carton kiện phẳng, khô ráo"),
                        ("plastic", 7100m, 1.5m, "Nhựa PET, màng bọc PE sạch"),
                        ("metal", 26500m, 1.0m, "Nhôm định hình, vỏ lon, sắt thép"),
                        ("paper", 4700m, 2.0m, "Giấy in văn phòng, tài liệu hủy")
                    }
                ),
                (
                    Guid.Parse("99999995-9999-9999-9999-999999999995"),
                    "collector_hndd",
                    "collector_hndd@ecolink.vn",
                    "Vựa Phế Liệu Sạch Đống Đa",
                    "Vũ Quang Huy",
                    "142 Tây Sơn, Phường Quang Trung, Quận Đống Đa, Hà Nội",
                    21.0085m,
                    105.8248m,
                    "0982334455",
                    "08:00 - 18:30",
                    "Thu gom tận ngõ khu vực Đống Đa - Thanh Xuân, cân điện tử chính xác và thanh toán ngay.",
                    new[]
                    {
                        ("plastic", 7100m, 1.0m, "Chai PET trong suốt đã tráng sạch"),
                        ("paper", 4700m, 2.0m, "Sách báo, giấy văn phòng buộc gọn"),
                        ("cardboard", 4000m, 2.0m, "Bìa carton gập phẳng"),
                        ("glass", 2300m, 5.0m, "Chai lọ thủy tinh sạch")
                    }
                ),
                (
                    Guid.Parse("99999996-9999-9999-9999-999999999996"),
                    "collector_hnhbt",
                    "collector_hnhbt@ecolink.vn",
                    "Điểm Thu Mua Tái Chế Bách Khoa",
                    "Phạm Thu Hương",
                    "55 Trần Đại Nghĩa, Phường Bách Khoa, Quận Hai Bà Trưng, Hà Nội",
                    21.0028m,
                    105.8442m,
                    "0936778899",
                    "07:30 - 18:00",
                    "Chuyên thu mua giấy tài liệu, vỏ lon và nhựa tái chế khu vực Bách Khoa - Kinh Tế - Xây Dựng.",
                    new[]
                    {
                        ("paper", 5000m, 1.5m, "Giấy photo, giáo trình, báo cũ khô sạch"),
                        ("cardboard", 4100m, 2.0m, "Thùng giấy gập gọn"),
                        ("metal", 26000m, 0.5m, "Lon nhôm, hộp thiếc sạch"),
                        ("plastic", 6900m, 1.0m, "Chai nhựa sạch không dính dầu mỡ")
                    }
                )
            };

            string? sharedCollectorPasswordHash = null;

            foreach (var f in demoFacilities)
            {
                var existingUser = await _db.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == f.UserId || u.Username == f.Username, ct);

                Guid targetUserId;
                if (existingUser == null)
                {
                    sharedCollectorPasswordHash ??= BCrypt.Net.BCrypt.HashPassword("collector123");
                    targetUserId = f.UserId;

                    var newUser = new User
                    {
                        Id = targetUserId,
                        Username = f.Username,
                        Email = f.Email,
                        PasswordHash = sharedCollectorPasswordHash,
                        FullName = $"{f.RepresentativeName} ({f.FacilityName})",
                        PhoneNumber = f.Phone,
                        Role = UserRole.Recycler,
                        Status = UserStatus.Active,
                        RecyclerProfile = new RecyclerProfile
                        {
                            UserId = targetUserId,
                            FacilityName = f.FacilityName,
                            RepresentativeName = f.RepresentativeName,
                            BusinessAddress = f.Address,
                            Latitude = f.Lat,
                            Longitude = f.Lng,
                            Location = new Point((double)f.Lng, (double)f.Lat) { SRID = 4326 },
                            OperatingHours = f.Hours,
                            ContactPhone = f.Phone,
                            Description = f.Description,
                            VerificationStatus = VerificationStatus.Verified,
                            ActiveStatus = RecyclerActiveStatus.Online
                        }
                    };

                    _db.Users.Add(newUser);
                    await _db.SaveChangesAsync(ct);
                }
                else
                {
                    targetUserId = existingUser.Id;
                    var existingProfile = await _db.RecyclerProfiles
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(r => r.UserId == targetUserId, ct);

                    if (existingProfile == null && existingUser.Role == UserRole.Recycler)
                    {
                        _db.RecyclerProfiles.Add(new RecyclerProfile
                        {
                            UserId = targetUserId,
                            FacilityName = f.FacilityName,
                            RepresentativeName = f.RepresentativeName,
                            BusinessAddress = f.Address,
                            Latitude = f.Lat,
                            Longitude = f.Lng,
                            Location = new Point((double)f.Lng, (double)f.Lat) { SRID = 4326 },
                            OperatingHours = f.Hours,
                            ContactPhone = f.Phone,
                            Description = f.Description,
                            VerificationStatus = VerificationStatus.Verified,
                            ActiveStatus = RecyclerActiveStatus.Online
                        });
                        await _db.SaveChangesAsync(ct);
                    }
                    else if (existingProfile != null && existingProfile.Location == null)
                    {
                        existingProfile.Location = new Point((double)existingProfile.Longitude, (double)existingProfile.Latitude)
                        {
                            SRID = 4326
                        };
                        await _db.SaveChangesAsync(ct);
                    }
                }
            }

            // 3. Seed bảng giá Active cho các cơ sở mẫu nếu chưa có
            var categoriesByCode = await _db.WasteCategories
                .Where(c => c.IsRecyclable)
                .ToDictionaryAsync(c => c.Code, ct);

            var addedAnyPrice = false;
            foreach (var f in demoFacilities)
            {
                var profileExists = await _db.RecyclerProfiles.AnyAsync(r => r.UserId == f.UserId, ct);
                if (!profileExists) continue;

                var hasPrices = await _db.RecyclerPrices.AnyAsync(p => p.RecyclerId == f.UserId, ct);
                if (hasPrices) continue;

                foreach (var (code, price, minKg, cond) in f.Prices)
                {
                    if (categoriesByCode.TryGetValue(code, out var cat))
                    {
                        _db.RecyclerPrices.Add(new RecyclerPrice
                        {
                            RecyclerId = f.UserId,
                            WasteCategoryId = cat.Id,
                            PricePerKg = price,
                            MinQuantityKg = minKg,
                            CollectionConditions = cond,
                            Status = PriceStatus.Active
                        });
                        addedAnyPrice = true;
                    }
                }
            }

            if (addedAnyPrice)
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Đã khởi tạo dữ liệu cơ sở thu mua mẫu đã xác minh (Verified + Online) trên bản đồ.");
            }

            _demoSeedChecked = true;
        }
        finally
        {
            SeedLock.Release();
        }
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
