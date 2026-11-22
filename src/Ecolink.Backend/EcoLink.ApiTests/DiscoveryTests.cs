using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>
/// Đường dẫn dùng cho kiểm thử tích hợp Module 5:
/// Khám phá &amp; Tìm kiếm Cơ sở Thu gom theo Vị trí Địa lý (Location-Based Recycler Discovery: U-05)
/// tích hợp liên module với Module 4 (Recycler Profile, Pricing &amp; Availability: R-01, R-02).
/// </summary>
public static class DiscoveryRoutes
{
    public const string NearbyRecyclers = "/api/v1/recyclers/nearby";
    public const string NearbyTradePartners = "/api/v1/trade/partners/nearby";
    public const string GeocodingSearch = "/api/v1/geocoding/search";

    public static string RecyclerPublicDetail(Guid recyclerId) => $"/api/v1/recyclers/{recyclerId}";
}

/// <summary>
/// Bộ kiểm thử tích hợp (Integration Tests) cho Module 5: Location-Based Recycler Discovery (U-05).
/// Kiểm chứng luồng tích hợp: PostGIS/Haversine tìm kiếm theo tọa độ GPS, bộ lọc loại phế liệu &amp; từ khóa,
/// sắp xếp theo khoảng cách / đơn giá thu mua, gợi ý mở rộng bán kính khi 0 kết quả (MSG-MTH-01),
/// và đồng bộ tức thời khi cơ sở bật/tắt trạng thái Online hoặc cập nhật bảng giá ở Module 4.
/// </summary>
[Trait("Category", "Discovery")]
public class DiscoveryTests
{
    private const string HoaLacLat = "21.0124";
    private const string HoaLacLng = "105.5253";

    // ======================= DSC-01: Tìm kiếm GPS chuẩn (BR-MTH-04) =======================

    [Fact]
    public async Task DSC01_SearchNearby_ValidHoaLacCoordinates_ReturnsVerifiedOnlineRecyclersSortedByDistanceAsc()
    {
        await RecyclerHelper.EnsureBaselineAsync();

        var res = (await Api.GetAsync(DiscoveryRoutes.NearbyRecyclers, query: new Dictionary<string, string>
        {
            ["lat"] = HoaLacLat,
            ["lng"] = HoaLacLng,
            ["radiusKm"] = "15"
        })).ShouldBe(200);

        Assert.Equal("MSG-GEN-01", res.MessageCode());
        var data = res.Data();
        var totalFound = data.GetProperty("totalFound").GetInt32();
        Assert.True(totalFound >= 1, "Expected at least 1 verified online recycler near Hoa Lac.");

        var distances = data.GetProperty("items")
            .EnumerateArray()
            .Select(i => i.GetProperty("distanceKm").GetDouble())
            .ToList();

        Assert.Equal(distances.OrderBy(d => d).ToList(), distances);
        Assert.All(distances, d => Assert.InRange(d, 0.0, 15.0));
    }

    // ======================= DSC-02: Validation tọa độ & bán kính (MSG-MTH-02) =======================

    [Theory]
    [InlineData("0", "0", "10", "plastic")]
    [InlineData("95.5", "105.5253", "10", "plastic")]
    [InlineData("21.0124", "-185.0", "10", "plastic")]
    [InlineData("21.0124", "105.5253", "0.2", "plastic")]
    [InlineData("21.0124", "105.5253", "65.0", "plastic")]
    [InlineData("21.0124", "105.5253", "10", "trash")]
    public async Task DSC02_SearchNearby_InvalidCoordinatesRadiusOrCategory_Returns400WithMsgMth02(
        string lat, string lng, string radiusKm, string category)
    {
        (await Api.GetAsync(DiscoveryRoutes.NearbyRecyclers, query: new Dictionary<string, string>
        {
            ["lat"] = lat,
            ["lng"] = lng,
            ["radiusKm"] = radiusKm,
            ["category"] = category
        })).ShouldFailWith(400, "MSG-MTH-02");
    }

    // ======================= DSC-03: Lọc theo loại phế liệu & sắp xếp giá cao nhất (BR-MTH-07) =======================

    [Fact]
    public async Task DSC03_SearchNearby_FilterByCategoryAndSortByPriceDesc_ReturnsMatchingFacilitiesOrderedByPrice()
    {
        await RecyclerHelper.EnsureBaselineAsync();

        var res = (await Api.GetAsync(DiscoveryRoutes.NearbyRecyclers, query: new Dictionary<string, string>
        {
            ["lat"] = HoaLacLat,
            ["lng"] = HoaLacLng,
            ["radiusKm"] = "20",
            ["category"] = "metal",
            ["sortBy"] = "price_desc"
        })).ShouldBe(200);

        var items = res.Data().GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);

        var metalPrices = new List<decimal>();
        foreach (var partner in items)
        {
            var materials = partner.GetProperty("materials").EnumerateArray().ToList();
            var metalRow = materials.FirstOrDefault(m =>
                string.Equals(m.Str("categoryCode"), "metal", StringComparison.OrdinalIgnoreCase));
            Assert.NotEqual(JsonValueKind.Undefined, metalRow.ValueKind);
            metalPrices.Add(metalRow.GetProperty("pricePerKg").GetDecimal());
        }

        Assert.Equal(metalPrices.OrderByDescending(p => p).ToList(), metalPrices);
    }

    // ======================= DSC-04: Không có kết quả -> MSG-MTH-01 & FallbackSuggestion =======================

    [Fact]
    public async Task DSC04_SearchNearby_ZeroResultsInRadius_ReturnsMsgMth01AndFallbackSuggestion()
    {
        // Tọa độ ngoài khơi biển Đông không có cơ sở thu gom trong bán kính 4km
        var res = (await Api.GetAsync(DiscoveryRoutes.NearbyRecyclers, query: new Dictionary<string, string>
        {
            ["lat"] = "15.0000",
            ["lng"] = "112.0000",
            ["radiusKm"] = "4",
            ["category"] = "paper"
        })).ShouldBe(200);

        Assert.Equal("MSG-MTH-01", res.MessageCode());
        var data = res.Data();
        Assert.Equal(0, data.GetProperty("totalFound").GetInt32());
        Assert.Empty(data.GetProperty("items").EnumerateArray());

        var fallback = data.GetProperty("fallbackSuggestion");
        Assert.Equal(JsonValueKind.Object, fallback.ValueKind);
        Assert.Equal(10.0, fallback.GetProperty("suggestedExpandedRadiusKm").GetDouble());
        Assert.True(fallback.GetProperty("allowCommunityPickupPost").GetBoolean());
        Assert.Equal("paper", fallback.GetProperty("publicBinSortingGuide").Str("categoryCode"));
    }

    // ======================= DSC-05: Tích hợp liên Module 4 -> Module 5 (Online/Offline & Giá) =======================

    [Fact]
    public async Task DSC05_CrossModule_WhenRecyclerTogglesAvailability_DiscoveryReflectsOnlineStatusImmediately()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var collectorToken = await RecyclerHelper.CollectorToken();

        // Lấy thông tin tọa độ và ID của collector hiện tại từ Module 4
        var profile = (await Api.GetAsync(RecyclerRoutes.Profile, collectorToken)).ShouldBe(200).Data();
        var collectorUserId = profile.GetProperty("recyclerId").GetGuid();
        var lat = profile.GetProperty("latitude").GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lng = profile.GetProperty("longitude").GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            // 1. Khi collector chuyển sang Offline (Module 4) -> Không xuất hiện trên bản đồ tìm kiếm (Module 5)
            (await ApiJson.PatchAsync(RecyclerRoutes.Availability, new
            {
                isOnline = false,
                unavailableReason = "Đầy kho tạm thời"
            }, collectorToken)).ShouldBe(200);

            var whileOffline = (await Api.GetAsync(DiscoveryRoutes.NearbyRecyclers, query: new Dictionary<string, string>
            {
                ["lat"] = lat,
                ["lng"] = lng,
                ["radiusKm"] = "5"
            })).ShouldBe(200);

            var offlineIds = whileOffline.Data().GetProperty("items")
                .EnumerateArray()
                .Select(i => i.GetProperty("id").GetGuid())
                .ToList();
            Assert.DoesNotContain(collectorUserId, offlineIds);

            // 2. Khi collector bật lại Online (Module 4) -> Xuất hiện ngay trên bản đồ tìm kiếm (Module 5)
            (await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = true }, collectorToken))
                .ShouldBe(200);

            var whileOnline = (await Api.GetAsync(DiscoveryRoutes.NearbyRecyclers, query: new Dictionary<string, string>
            {
                ["lat"] = lat,
                ["lng"] = lng,
                ["radiusKm"] = "5"
            })).ShouldBe(200);

            var onlineIds = whileOnline.Data().GetProperty("items")
                .EnumerateArray()
                .Select(i => i.GetProperty("id").GetGuid())
                .ToList();
            Assert.Contains(collectorUserId, onlineIds);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    // ======================= DSC-06: Xem chi tiết công khai cơ sở trên bản đồ =======================

    [Fact]
    public async Task DSC06_GetRecyclerPublicDetail_ValidVerifiedRecycler_ReturnsProfilePricesAndDistanceKm()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var collectorToken = await RecyclerHelper.CollectorToken();
        var collectorUserId = (await Api.GetAsync(RecyclerRoutes.Profile, collectorToken))
            .ShouldBe(200).Data().GetProperty("recyclerId").GetGuid();

        var detailRes = (await Api.GetAsync(DiscoveryRoutes.RecyclerPublicDetail(collectorUserId), query: new Dictionary<string, string>
        {
            ["lat"] = HoaLacLat,
            ["lng"] = HoaLacLng
        })).ShouldBe(200);

        Assert.Equal("MSG-GEN-01", detailRes.MessageCode());
        var detail = detailRes.Data();
        Assert.Equal(collectorUserId, detail.GetProperty("id").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(detail.Str("facilityName")));
        Assert.NotEmpty(detail.GetProperty("materials").EnumerateArray());
    }

    // ======================= DSC-07: Xem chi tiết cơ sở không tồn tại -> 404 NotFound =======================

    [Fact]
    public async Task DSC07_GetRecyclerPublicDetail_NonExistentId_Returns404NotFound()
    {
        (await Api.GetAsync(DiscoveryRoutes.RecyclerPublicDetail(Guid.NewGuid())))
            .ShouldFailWith(404);
    }

    // ======================= DSC-08: Trade Partners Nearby & Geocoding Proxy =======================

    [Fact]
    public async Task DSC08_TradePartnersNearby_AndGeocodingProxy_ValidateAndReturnExpectedEnvelopes()
    {
        await RecyclerHelper.EnsureBaselineAsync();

        // 1. GET /api/v1/trade/partners/nearby
        var tradePartners = (await Api.GetAsync(DiscoveryRoutes.NearbyTradePartners, query: new Dictionary<string, string>
        {
            ["lat"] = HoaLacLat,
            ["lng"] = HoaLacLng,
            ["category"] = "plastic",
            ["radiusKm"] = "15"
        })).ShouldBe(200);

        Assert.Equal(JsonValueKind.Array, tradePartners.Body.ValueKind);
        Assert.NotEmpty(tradePartners.Body.EnumerateArray());

        // 2. GET /api/v1/geocoding/search với từ khóa quá ngắn (< 2 ký tự) -> 400 Bad Request
        (await Api.GetAsync(DiscoveryRoutes.GeocodingSearch, query: new Dictionary<string, string>
        {
            ["q"] = "a"
        })).ShouldFailWith(400, "MSG-GEN-01");

        // 3. GET /api/v1/geocoding/search với địa danh hợp lệ -> 200 OK
        var geoRes = (await Api.GetAsync(DiscoveryRoutes.GeocodingSearch, query: new Dictionary<string, string>
        {
            ["q"] = "Hoa Lac",
            ["limit"] = "3"
        })).ShouldBe(200);

        Assert.True(geoRes.Body.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Array, geoRes.Data().GetProperty("items").ValueKind);
    }
}

