using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

/// <summary>
/// Dịch vụ Proxy tra cứu toạ độ địa danh (Forward Geocoding) phục vụ tìm kiếm khu vực trên bản đồ (UC U-05).
/// Thực hiện ẩn API Key (nếu dùng LocationIQ), gắn User-Agent định danh, lưu đệm IMemoryCache và giới hạn tốc độ gọi Nominatim.
/// </summary>
public interface IGeocodingProxyService
{
    Task<GeocodingSearchResponseDto> SearchPlacesAsync(
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default);
}

