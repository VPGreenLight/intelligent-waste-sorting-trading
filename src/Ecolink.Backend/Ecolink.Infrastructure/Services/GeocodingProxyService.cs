using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Dịch vụ Backend Geocoding Proxy tuân thủ Nominatim Usage Policy:
/// - Không gọi từ phía trình duyệt khi đang gõ phím (chỉ phục vụ truy vấn Submit / Enter).
/// - Luôn định danh ứng dụng qua header User-Agent hợp lệ.
/// - Lưu đệm kết quả trong IMemoryCache (12 giờ) để không truy vấn lặp lại cùng địa danh.
/// - Khoá đồng bộ SemaphoreSlim đảm bảo tốc độ gọi Nominatim không vượt quá 1 request / giây.
/// - Hỗ trợ tự động chuyển sang LocationIQ nếu cấu hình Geocoding:LocationIqApiKey trên máy chủ.
/// </summary>
public class GeocodingProxyService : IGeocodingProxyService
{
    private static readonly SemaphoreSlim RateLimitLock = new(1, 1);
    private static DateTimeOffset _lastUpstreamRequestAt = DateTimeOffset.MinValue;
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeocodingProxyService> _logger;

    public GeocodingProxyService(
        HttpClient httpClient,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<GeocodingProxyService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GeocodingSearchResponseDto> SearchPlacesAsync(
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);
        var clampedLimit = Math.Clamp(limit, 1, 8);

        if (normalizedQuery.Length < 2)
        {
            return new GeocodingSearchResponseDto
            {
                Query = normalizedQuery,
                Provider = "none",
                Cached = false,
                Items = new List<GeocodingSuggestionDto>()
            };
        }

        var locationIqKey = _configuration["Geocoding:LocationIqApiKey"]?.Trim();
        var useLocationIq = !string.IsNullOrWhiteSpace(locationIqKey);
        var providerName = useLocationIq ? "locationiq" : "nominatim";
        var attribution = useLocationIq
            ? "Search by LocationIQ.com • © OpenStreetMap contributors"
            : "© OpenStreetMap contributors";

        var cacheKey = $"ecolink:geocoding:v1:{providerName}:{clampedLimit}:{normalizedQuery.ToLowerInvariant()}";
        if (_cache.TryGetValue<List<GeocodingSuggestionDto>>(cacheKey, out var cachedItems) && cachedItems != null)
        {
            return new GeocodingSearchResponseDto
            {
                Query = normalizedQuery,
                Provider = providerName,
                Cached = true,
                Attribution = attribution,
                Items = cachedItems
            };
        }

        await RateLimitLock.WaitAsync(cancellationToken);
        try
        {
            // Kiểm tra lại cache lần 2 phòng trường hợp request song song vừa nạp xong
            if (_cache.TryGetValue<List<GeocodingSuggestionDto>>(cacheKey, out cachedItems) && cachedItems != null)
            {
                return new GeocodingSearchResponseDto
                {
                    Query = normalizedQuery,
                    Provider = providerName,
                    Cached = true,
                    Attribution = attribution,
                    Items = cachedItems
                };
            }

            var minIntervalMs = useLocationIq ? 550 : 1100;
            var elapsedMs = (DateTimeOffset.UtcNow - _lastUpstreamRequestAt).TotalMilliseconds;
            if (elapsedMs < minIntervalMs)
            {
                var waitMs = (int)Math.Ceiling(minIntervalMs - elapsedMs);
                await Task.Delay(waitMs, cancellationToken);
            }

            var encodedQuery = Uri.EscapeDataString(normalizedQuery);
            var url = useLocationIq
                ? $"https://us1.locationiq.com/v1/search?key={Uri.EscapeDataString(locationIqKey!)}&q={encodedQuery}&format=json&countrycodes=vn&accept-language=vi&addressdetails=1&limit={clampedLimit}"
                : $"https://nominatim.openstreetmap.org/search?format=jsonv2&q={encodedQuery}&countrycodes=vn&accept-language=vi&addressdetails=1&limit={clampedLimit}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.Clear();
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("EcoLinkWastePlatform", "1.0"));
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("(FPT-University-Capstone-Project)"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.AcceptLanguage.ParseAdd("vi,en;q=0.8");

            _lastUpstreamRequestAt = DateTimeOffset.UtcNow;

            using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Geocoding upstream ({Provider}) trả mã HTTP {StatusCode} cho từ khoá '{Query}'",
                    providerName, (int)res.StatusCode, normalizedQuery);

                return new GeocodingSearchResponseDto
                {
                    Query = normalizedQuery,
                    Provider = providerName,
                    Cached = false,
                    Attribution = attribution,
                    Items = new List<GeocodingSuggestionDto>()
                };
            }

            await using var stream = await res.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var items = ParseSuggestions(doc.RootElement);

            // Lưu cache 12 giờ cho kết quả hợp lệ (20 phút nếu rỗng để tránh gọi lặp lại từ khoá rác)
            var ttl = items.Count > 0 ? TimeSpan.FromHours(12) : TimeSpan.FromMinutes(20);
            _cache.Set(cacheKey, items, ttl);

            return new GeocodingSearchResponseDto
            {
                Query = normalizedQuery,
                Provider = providerName,
                Cached = false,
                Attribution = attribution,
                Items = items
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể truy vấn dịch vụ Geocoding cho từ khoá '{Query}'", normalizedQuery);
            return new GeocodingSearchResponseDto
            {
                Query = normalizedQuery,
                Provider = providerName,
                Cached = false,
                Attribution = attribution,
                Items = new List<GeocodingSuggestionDto>()
            };
        }
        finally
        {
            RateLimitLock.Release();
        }
    }

    private static string NormalizeQuery(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var collapsed = MultiSpaceRegex.Replace(raw.Trim(), " ");
        return collapsed.Length > 120 ? collapsed[..120] : collapsed;
    }

    private static List<GeocodingSuggestionDto> ParseSuggestions(JsonElement root)
    {
        var list = new List<GeocodingSuggestionDto>();
        if (root.ValueKind != JsonValueKind.Array) return list;

        foreach (var item in root.EnumerateArray())
        {
            var latStr = TryGetString(item, "lat");
            var lonStr = TryGetString(item, "lon");
            if (!double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            {
                continue;
            }

            if (lat is < -90 or > 90 || lon is < -180 or > 180)
            {
                continue;
            }

            var displayName = TryGetString(item, "display_name") ?? $"{lat:F4}, {lon:F4}";
            var rawName = TryGetString(item, "name");
            var placeId = TryGetRawId(item, "place_id") ?? $"{lat:F5},{lon:F5}";
            var categoryType = TryGetString(item, "type") ?? TryGetString(item, "category");

            list.Add(new GeocodingSuggestionDto
            {
                PlaceId = placeId,
                ShortLabel = BuildShortLabel(rawName, displayName),
                DisplayName = displayName,
                Latitude = Math.Round(lat, 6),
                Longitude = Math.Round(lon, 6),
                CategoryType = categoryType
            });
        }

        return list;
    }

    private static string BuildShortLabel(string? name, string displayName)
    {
        var parts = displayName
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !Regex.IsMatch(p, @"^\d{5,6}$") && !p.Equals("Việt Nam", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (parts.Count == 0)
        {
            return !string.IsNullOrWhiteSpace(name) ? name.Trim() : displayName;
        }

        return string.Join(", ", parts.Take(3));
    }

    private static string? TryGetString(JsonElement element, string propName)
    {
        if (element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static string? TryGetRawId(JsonElement element, string propName)
    {
        if (!element.TryGetProperty(propName, out var prop)) return null;
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null
        };
    }
}

