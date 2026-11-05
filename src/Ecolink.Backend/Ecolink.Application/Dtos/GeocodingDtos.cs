namespace Ecolink.Application.Dtos;

/// <summary>
/// Một gợi ý địa điểm chuẩn hoá trả về từ dịch vụ Geocoding Proxy (Nominatim / LocationIQ).
/// </summary>
public class GeocodingSuggestionDto
{
    public string PlaceId { get; set; } = string.Empty;
    public string ShortLabel { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? CategoryType { get; set; }
}

/// <summary>
/// Kết quả tìm kiếm địa danh qua Backend Geocoding Proxy (tuân thủ Usage Policy và lưu đệm).
/// </summary>
public class GeocodingSearchResponseDto
{
    public string Query { get; set; } = string.Empty;
    public string Provider { get; set; } = "nominatim";
    public bool Cached { get; set; }
    public string Attribution { get; set; } = "© OpenStreetMap contributors";
    public List<GeocodingSuggestionDto> Items { get; set; } = new();
}

