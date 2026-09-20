using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

public class GeoMatchingService : IGeoMatchingService
{
    private readonly ILogger<GeoMatchingService> _logger;

    // Danh sách điểm thu mua mẫu tại TP.HCM phục vụ demo và matching
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
            Materials = new List<AcceptedMaterialDto>
            {
                new() { CategoryCode = "metal", CategoryName = "Kim loại & Đồng, Nhôm", PricePerKg = 28000m, MinimumQuantityKg = 1m },
                new() { CategoryCode = "paper", CategoryName = "Giấy vụn, Hồ sơ", PricePerKg = 5000m, MinimumQuantityKg = 2m }
            }
        }
    };

    public GeoMatchingService(ILogger<GeoMatchingService> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<CollectionPartnerDto>> FindNearestPartnersAsync(
        double latitude,
        double longitude,
        string? categoryCode = null,
        double radiusKm = 10.0,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Tìm điểm thu mua gần tọa độ ({Lat}, {Lng}) trong bán kính {Radius}km, loại rác: {Cat}",
            latitude, longitude, radiusKm, categoryCode ?? "Tất cả");

        var results = new List<CollectionPartnerDto>();

        foreach (var partner in _mockPartners)
        {
            // Lọc theo loại vật liệu chấp nhận
            if (!string.IsNullOrEmpty(categoryCode) && 
                !partner.Materials.Any(m => m.CategoryCode.Equals(categoryCode, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var distance = CalculateDistanceKm(latitude, longitude, partner.Latitude, partner.Longitude);
            if (distance <= radiusKm)
            {
                var clone = new CollectionPartnerDto
                {
                    Id = partner.Id,
                    FacilityName = partner.FacilityName,
                    Address = partner.Address,
                    Latitude = partner.Latitude,
                    Longitude = partner.Longitude,
                    ContactPhone = partner.ContactPhone,
                    DistanceKm = Math.Round(distance, 2),
                    Materials = partner.Materials
                };
                results.Add(clone);
            }
        }

        // Sắp xếp theo khoảng cách gần nhất
        var sorted = results.OrderBy(p => p.DistanceKm).ToList();
        return Task.FromResult<IEnumerable<CollectionPartnerDto>>(sorted);
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
