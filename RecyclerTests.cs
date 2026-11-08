using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>
/// Test cho module Recycler Profile, Pricing &amp; Availability (UC R-01, R-02).
/// Dùng tài khoản demo "collector" (đã Verified, đang Online) cho các luồng thành công và
/// một Recycler mới đăng ký (Pending) cho các luồng bị chặn theo BR-MTH-01.
/// Các test thay đổi dữ liệu của collector đều tự khôi phục về trạng thái nền trong khối finally.
/// </summary>
[Trait("Category", "Recycler")]
public class RecyclerTests
{
    private static readonly Guid SomeId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Task<string> Collector() => RecyclerHelper.CollectorToken();
    private static Task<string> Pending() => RecyclerHelper.PendingRecyclerToken();

    // ========================================================================
    // 1. Phân quyền: chỉ role Recycler mới dùng được các endpoint /recycler
    // ========================================================================
    private static readonly (string Method, string Path)[] RecyclerEndpoints =
    {
        ("GET", RecyclerRoutes.Profile),
        ("PUT", RecyclerRoutes.Profile),
        ("GET", RecyclerRoutes.Prices),
        ("POST", RecyclerRoutes.Prices),
        ("PUT", RecyclerRoutes.Prices),
        ("PUT", $"{RecyclerRoutes.Prices}/00000000-0000-0000-0000-000000000001"),
        ("GET", RecyclerRoutes.Availability),
        ("PATCH", RecyclerRoutes.Availability),
        ("PUT", RecyclerRoutes.Schedule),
    };

    public static TheoryData<string, string> EndpointData
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var (method, path) in RecyclerEndpoints) data.Add(method, path);
            return data;
        }
    }

    public static TheoryData<string, string, string> NonRecyclerData
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var role in new[] { "citizen", "admin" })
                foreach (var (method, path) in RecyclerEndpoints)
                    data.Add(role, method, path);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EndpointData))]
    public async Task RecyclerEndpoint_WithoutToken_Returns401(string method, string path) =>
        (await ApiJson.SendAsync(new HttpMethod(method), path, new { })).ShouldBe(401);

    [Theory]
    [MemberData(nameof(EndpointData))]
    public async Task RecyclerEndpoint_WithInvalidToken_Returns401(string method, string path) =>
        (await ApiJson.SendAsync(new HttpMethod(method), path, new { }, "aaa.bbb.ccc")).ShouldBe(401);

    [Theory]
    [MemberData(nameof(NonRecyclerData))]
    public async Task RecyclerEndpoint_WithNonRecyclerRole_Returns403(string role, string method, string path)
    {
        var token = await Tokens.GetAsync(role);
        (await ApiJson.SendAsync(new HttpMethod(method), path, new { }, token)).ShouldBe(403);
    }

    // ========================================================================
    // 2. Danh mục phế liệu chuẩn  GET /api/v1/waste-categories
    // ========================================================================
    [Fact]
    public async Task Categories_WithoutToken_Returns401() =>
        (await Api.GetAsync(RecyclerRoutes.WasteCategories)).ShouldBe(401);

    [Theory]
    [InlineData("citizen")]
    [InlineData("collector")]
    [InlineData("admin")]
    public async Task Categories_WithAnyAuthenticatedRole_Returns200(string role)
    {
        var token = await Tokens.GetAsync(role);
        var r = (await Api.GetAsync(RecyclerRoutes.WasteCategories, token)).ShouldBe(200);
        Assert.True(r.Body.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Array, r.Data().ValueKind);
    }

    [Fact]
    public async Task Categories_ContainsStandardRecyclableTypes()
    {
        var categories = await RecyclerHelper.GetCategoriesAsync();
        var codes = categories.Select(c => c.Code).ToList();

        foreach (var expected in new[] { "plastic", "paper", "glass", "metal", "cardboard" })
            Assert.Contains(expected, codes);

        Assert.All(categories, c =>
        {
            Assert.NotEqual(Guid.Empty, c.Id);
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
        });
    }

    [Fact]
    public async Task Categories_ExcludesNonRecyclableTrash()
    {
        var categories = await RecyclerHelper.GetCategoriesAsync();
        Assert.DoesNotContain(categories, c => c.Code == "trash");
    }

    [Fact]
    public async Task Categories_CalledTwice_DoesNotCreateDuplicates()
    {
        var first = await RecyclerHelper.GetCategoriesAsync();
        var second = await RecyclerHelper.GetCategoriesAsync();

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first.Select(c => c.Id).Order(), second.Select(c => c.Id).Order());
        Assert.Equal(first.Count, first.Select(c => c.Code).Distinct().Count());
    }

    // ========================================================================
    // 3. Recycler Profile  GET/PUT /api/v1/recycler/profile
    // ========================================================================
    [Fact]
    public async Task Profile_Get_ReturnsExpectedShape()
    {
        var token = await Collector();
        var r = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200);
        Assert.True(r.Body.GetProperty("success").GetBoolean());

        var p = r.Data();
        Assert.True(Guid.TryParse(p.GetProperty("recyclerId").GetString(), out _));
        Assert.False(string.IsNullOrWhiteSpace(p.GetProperty("facilityName").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(p.GetProperty("businessAddress").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(p.GetProperty("contactPhone").GetString()));
        Assert.Equal(JsonValueKind.Number, p.GetProperty("latitude").ValueKind);
        Assert.Equal(JsonValueKind.Number, p.GetProperty("longitude").ValueKind);
        Assert.Equal(JsonValueKind.Number, p.GetProperty("activePriceCount").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(p.GetProperty("operatingHours").GetString()));
        Assert.Equal("Verified", p.GetProperty("verificationStatus").GetString());
    }

    [Fact]
    public async Task Profile_Get_RecyclerIdMatchesLoggedInUser()
    {
        var token = await Collector();
        var me = (await Api.GetAsync(Routes.Me, query: new Dictionary<string, string> { ["token"] = token })).ShouldBe(200);
        var profile = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200);

        Assert.Equal(Api.Unwrap(me.Body).GetProperty("id").GetString(), profile.Data().GetProperty("recyclerId").GetString());
    }

    [Fact]
    public async Task Profile_Get_ActivePriceCountMatchesPriceList()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();

        var prices = await RecyclerHelper.GetPricesAsync(token);
        var profile = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200);

        Assert.Equal(prices.Count(p => p.IsActive), profile.Data().GetProperty("activePriceCount").GetInt32());
    }

    [Fact]
    public async Task Profile_Get_DoesNotLeakSensitiveFields()
    {
        var token = await Collector();
        var raw = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200).Raw.ToLowerInvariant();
        Assert.DoesNotContain("passwordhash", raw);
        Assert.DoesNotContain("password_hash", raw);
        Assert.DoesNotContain("geom", raw);
    }

    private static Dictionary<string, object?> ProfileBody(JsonElement p) => new()
    {
        ["facilityName"] = p.Str("facilityName"),
        ["representativeName"] = p.Str("representativeName"),
        ["businessAddress"] = p.Str("businessAddress"),
        ["latitude"] = p.GetProperty("latitude").GetDouble(),
        ["longitude"] = p.GetProperty("longitude").GetDouble(),
        ["contactPhone"] = p.Str("contactPhone"),
        ["description"] = p.Str("description"),
        ["businessPhotoUrl"] = p.Str("businessPhotoUrl"),
    };

    [Fact]
    public async Task Profile_Update_PersistsChangesAndKeepsVerification()
    {
        var token = await Collector();
        var original = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200).Data();
        var body = ProfileBody(original);
        var marker = $"qa-{TestData.Unique()}";
        body["description"] = marker;
        body["businessPhotoUrl"] = "https://example.com/qa-photo.png";

        try
        {
            var put = (await ApiJson.PutAsync(RecyclerRoutes.Profile, body, token)).ShouldBe(200);
            Assert.True(put.Body.GetProperty("success").GetBoolean());
            Assert.Equal(marker, put.Data().Str("description"));

            var get = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200).Data();
            Assert.Equal(marker, get.Str("description"));
            Assert.Equal("https://example.com/qa-photo.png", get.Str("businessPhotoUrl"));
            Assert.Equal("Verified", get.Str("verificationStatus")); // sửa hồ sơ không làm mất trạng thái đã duyệt
            Assert.Equal(original.Str("facilityName"), get.Str("facilityName"));
        }
        finally
        {
            await ApiJson.PutAsync(RecyclerRoutes.Profile, ProfileBody(original), token);
        }
    }

    [Fact]
    public async Task Profile_Update_ChangesCoordinates()
    {
        var token = await Collector();
        var original = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200).Data();
        var body = ProfileBody(original);
        body["latitude"] = 10.8231;
        body["longitude"] = 106.6297;

        try
        {
            (await ApiJson.PutAsync(RecyclerRoutes.Profile, body, token)).ShouldBe(200);

            var get = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200).Data();
            Assert.Equal(10.8231, get.GetProperty("latitude").GetDouble(), 4);
            Assert.Equal(106.6297, get.GetProperty("longitude").GetDouble(), 4);
        }
        finally
        {
            await ApiJson.PutAsync(RecyclerRoutes.Profile, ProfileBody(original), token);
        }
    }

    [Fact]
    public async Task Profile_Update_WithMaxLengthFields_Succeeds()
    {
        var token = await Collector();
        var original = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200).Data();
        var body = ProfileBody(original);
        body["facilityName"] = new string('F', 150);
        body["representativeName"] = new string('R', 100);
        body["contactPhone"] = new string('9', 20);

        try
        {
            (await ApiJson.PutAsync(RecyclerRoutes.Profile, body, token)).ShouldBe(200);
        }
        finally
        {
            await ApiJson.PutAsync(RecyclerRoutes.Profile, ProfileBody(original), token);
        }
    }

    [Theory]
    [InlineData("facility-missing")]
    [InlineData("facility-empty")]
    [InlineData("facility-too-long-151")]
    [InlineData("representative-too-long-101")]
    [InlineData("address-missing")]
    [InlineData("address-empty")]
    [InlineData("phone-missing")]
    [InlineData("phone-too-long-21")]
    [InlineData("latitude-91")]
    [InlineData("latitude-minus-91")]
    [InlineData("longitude-181")]
    [InlineData("longitude-minus-181")]
    [InlineData("coordinates-zero-zero")]
    public async Task Profile_Update_WithInvalidData_Returns400(string scenario)
    {
        var token = await Collector();
        var original = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200).Data();
        var body = ProfileBody(original);

        switch (scenario)
        {
            case "facility-missing": body.Remove("facilityName"); break;
            case "facility-empty": body["facilityName"] = ""; break;
            case "facility-too-long-151": body["facilityName"] = new string('F', 151); break;
            case "representative-too-long-101": body["representativeName"] = new string('R', 101); break;
            case "address-missing": body.Remove("businessAddress"); break;
            case "address-empty": body["businessAddress"] = ""; break;
            case "phone-missing": body.Remove("contactPhone"); break;
            case "phone-too-long-21": body["contactPhone"] = new string('9', 21); break;
            case "latitude-91": body["latitude"] = 91.0; break;
            case "latitude-minus-91": body["latitude"] = -91.0; break;
            case "longitude-181": body["longitude"] = 181.0; break;
            case "longitude-minus-181": body["longitude"] = -181.0; break;
            case "coordinates-zero-zero": body["latitude"] = 0.0; body["longitude"] = 0.0; break;
            default: throw new ArgumentException(scenario);
        }

        var r = (await ApiJson.PutAsync(RecyclerRoutes.Profile, body, token)).ShouldFailWith(400);
        if (scenario == "coordinates-zero-zero")
            Assert.Equal("MSG-MTH-06", r.MessageCode());

        // Dữ liệu không bị thay đổi sau khi bị từ chối
        var after = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200).Data();
        Assert.Equal(original.Str("facilityName"), after.Str("facilityName"));
        Assert.Equal(original.GetProperty("latitude").GetDouble(), after.GetProperty("latitude").GetDouble(), 4);
    }

    [Fact]
    public async Task Profile_Update_WithEmptyJsonObject_Returns400() =>
        (await ApiJson.RawAsync(HttpMethod.Put, RecyclerRoutes.Profile, "{}", await Collector())).ShouldFailWith(400);

    [Fact]
    public async Task PendingRecycler_Profile_IsPendingAndOffline()
    {
        var token = await Pending();
        var r = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200);

        Assert.Equal("Pending", r.Data().GetProperty("verificationStatus").GetString());
        Assert.Equal("Offline", r.Data().GetProperty("activeStatus").GetString());
        Assert.Equal(0, r.Data().GetProperty("activePriceCount").GetInt32());
    }

    [Fact]
    public async Task PendingRecycler_CanUpdateProfile_StatusStaysPending()
    {
        var token = await Pending();
        var original = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200).Data();
        var body = ProfileBody(original);
        body["description"] = $"qa-{TestData.Unique()}";

        (await ApiJson.PutAsync(RecyclerRoutes.Profile, body, token)).ShouldBe(200);

        var after = (await Api.GetAsync(RecyclerRoutes.Profile, token)).ShouldBe(200).Data();
        Assert.Equal(body["description"], after.Str("description"));
        Assert.Equal("Pending", after.GetProperty("verificationStatus").GetString());
    }

    // ========================================================================
    // 4. Pricing – xem danh sách  GET /api/v1/recycler/prices
    // ========================================================================
    [Fact]
    public async Task Prices_Get_ReturnsExpectedShape()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var r = (await Api.GetAsync(RecyclerRoutes.Prices, token)).ShouldBe(200);

        Assert.True(r.Body.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Array, r.Data().ValueKind);
        Assert.True(r.Data().GetArrayLength() >= 4);

        foreach (var e in r.Data().EnumerateArray())
        {
            Assert.True(Guid.TryParse(e.GetProperty("id").GetString(), out _));
            Assert.True(Guid.TryParse(e.GetProperty("wasteCategoryId").GetString(), out _));
            Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("wasteCategoryCode").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("wasteCategoryName").GetString()));
            Assert.True(e.GetProperty("pricePerKg").GetDecimal() > 0);
            Assert.True(e.GetProperty("minQuantityKg").GetDecimal() > 0);
            Assert.True(e.GetProperty("isActive").ValueKind is JsonValueKind.True or JsonValueKind.False);
            Assert.True(e.GetProperty("isOutdated").ValueKind is JsonValueKind.True or JsonValueKind.False);
            Assert.True(e.TryGetProperty("lastUpdatedAt", out _));
        }
    }

    [Fact]
    public async Task Prices_Get_HasNoDuplicateCategory()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var prices = await RecyclerHelper.GetPricesAsync();
        Assert.Equal(prices.Count, prices.Select(p => p.CategoryId).Distinct().Count());
    }

    [Fact]
    public async Task Prices_Get_RecentlySavedPrices_AreNotOutdated()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var prices = await RecyclerHelper.GetPricesAsync();
        Assert.All(prices.Where(p => p.IsActive), p => Assert.False(p.IsOutdated));
    }

    [Fact]
    public async Task Prices_Get_FilterByIsActive()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var plastic = await RecyclerHelper.GetCategoryAsync("plastic");

        try
        {
            (await ApiJson.PutAsync(RecyclerRoutes.Prices,
                new { items = new[] { RecyclerHelper.BulkItem(plastic.Id, 8000m, 2m, active: false) } }, token)).ShouldBe(200);

            var inactive = await RecyclerHelper.GetPricesAsync(token, new Dictionary<string, string> { ["isActive"] = "false" });
            Assert.NotEmpty(inactive);
            Assert.All(inactive, p => Assert.False(p.IsActive));
            Assert.Contains(inactive, p => p.Code == "plastic");

            var active = await RecyclerHelper.GetPricesAsync(token, new Dictionary<string, string> { ["isActive"] = "true" });
            Assert.NotEmpty(active);
            Assert.All(active, p => Assert.True(p.IsActive));
            Assert.DoesNotContain(active, p => p.Code == "plastic");
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Theory]
    [InlineData("plastic")]
    [InlineData("PLASTIC")] // không phân biệt hoa thường
    [InlineData("Nhựa")]
    public async Task Prices_Get_SearchByKeyword_ReturnsOnlyMatching(string keyword)
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var rows = await RecyclerHelper.GetPricesAsync(query: new Dictionary<string, string> { ["keyword"] = keyword });

        Assert.NotEmpty(rows);
        Assert.All(rows, p => Assert.Equal("plastic", p.Code));
    }

    [Fact]
    public async Task Prices_Get_SearchByKeyword_NoMatch_ReturnsEmptyList()
    {
        var rows = await RecyclerHelper.GetPricesAsync(query: new Dictionary<string, string> { ["keyword"] = $"zzz{TestData.Unique()}" });
        Assert.Empty(rows);
    }

    [Fact]
    public async Task PendingRecycler_Prices_Get_ReturnsEmptyList()
    {
        var rows = await RecyclerHelper.GetPricesAsync(await Pending());
        Assert.Empty(rows);
    }

    // ========================================================================
    // 5. Pricing – thêm loại phế liệu  POST /api/v1/recycler/prices
    // ========================================================================
    [Fact]
    public async Task Prices_Add_NewCategory_Returns201()
    {
        var token = await Collector();
        var category = await RecyclerHelper.GetCategoryAsync(RecyclerHelper.ReservedCategoryCode);
        var existing = await RecyclerHelper.GetPricesAsync(token);

        // Không có API xóa dòng giá, nên chỉ thêm mới được ở lần chạy đầu trên DB mới -> các lần sau bỏ qua
        if (existing.Any(p => p.CategoryId == category.Id)) return;

        var r = (await ApiJson.PostAsync(RecyclerRoutes.Prices, new
        {
            wasteCategoryId = category.Id,
            pricePerKg = 4200,
            minQuantityKg = 2.5,
            collectionConditions = "Chỉ nhận loại đã ép phẳng"
        }, token)).ShouldBe(201);

        Assert.True(r.Body.GetProperty("success").GetBoolean());
        var row = r.Data().ToPriceRow();
        Assert.Equal(category.Id, row.CategoryId);
        Assert.Equal(4200m, row.PricePerKg);
        Assert.Equal(2.5m, row.MinQuantityKg);
        Assert.Equal("Chỉ nhận loại đã ép phẳng", row.Conditions);
        Assert.True(row.IsActive);
        Assert.NotEqual(Guid.Empty, row.Id);

        var listed = await RecyclerHelper.GetPricesAsync(token);
        Assert.Contains(listed, p => p.Id == row.Id);
    }

    [Fact]
    public async Task Prices_Add_DuplicateCategory_Returns400()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var plastic = await RecyclerHelper.GetCategoryAsync("plastic");

        (await ApiJson.PostAsync(RecyclerRoutes.Prices,
            new { wasteCategoryId = plastic.Id, pricePerKg = 9000, minQuantityKg = 1 }, token)).ShouldFailWith(400);

        // Giá cũ không bị ghi đè
        var row = (await RecyclerHelper.GetPricesAsync(token)).First(p => p.Code == "plastic");
        Assert.Equal(8000m, row.PricePerKg);
    }

    [Fact]
    public async Task Prices_BulkSave_WithBoundaryValues_Succeeds()
    {
        // Giá trị biên hợp lệ (giá 1 và 1 tỷ, khối lượng 0.001 và 100000 kg, điều kiện đúng 200 ký tự) phải được chấp nhận.
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var plastic = await RecyclerHelper.GetCategoryAsync("plastic");

        try
        {
            (await ApiJson.PutAsync(RecyclerRoutes.Prices, new
            {
                items = new[] { RecyclerHelper.BulkItem(plastic.Id, 1m, 0.001m, true, new string('c', 200)) }
            }, token)).ShouldBe(200);

            (await ApiJson.PutAsync(RecyclerRoutes.Prices, new
            {
                items = new[] { RecyclerHelper.BulkItem(plastic.Id, 1_000_000_000m, 100000m, true) }
            }, token)).ShouldBe(200);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Theory]
    [InlineData("price-missing")]
    [InlineData("price-zero")]
    [InlineData("price-negative")]
    [InlineData("price-fractional")]
    [InlineData("price-too-large")]
    [InlineData("minqty-zero")]
    [InlineData("minqty-negative")]
    [InlineData("minqty-too-large")]
    [InlineData("conditions-too-long-201")]
    [InlineData("category-missing")]
    public async Task Prices_Add_WithInvalidData_Returns400(string scenario)
    {
        var token = await Collector();
        var category = await RecyclerHelper.GetCategoryAsync(RecyclerHelper.ReservedCategoryCode);
        var body = new Dictionary<string, object?>
        {
            ["wasteCategoryId"] = category.Id,
            ["pricePerKg"] = 5000,
            ["minQuantityKg"] = 1,
        };

        switch (scenario)
        {
            case "price-missing": body.Remove("pricePerKg"); break;
            case "price-zero": body["pricePerKg"] = 0; break;
            case "price-negative": body["pricePerKg"] = -1000; break;
            case "price-fractional": body["pricePerKg"] = 1500.5; break;
            case "price-too-large": body["pricePerKg"] = 1_000_000_001; break;
            case "minqty-zero": body["minQuantityKg"] = 0; break;
            case "minqty-negative": body["minQuantityKg"] = -1; break;
            case "minqty-too-large": body["minQuantityKg"] = 100001; break;
            case "conditions-too-long-201": body["collectionConditions"] = new string('c', 201); break;
            case "category-missing": body.Remove("wasteCategoryId"); break;
            default: throw new ArgumentException(scenario);
        }

        (await ApiJson.PostAsync(RecyclerRoutes.Prices, body, token)).ShouldFailWith(400);
    }

    [Fact]
    public async Task Prices_Add_WithUnknownCategory_Returns404()
    {
        var token = await Collector();
        var r = (await ApiJson.PostAsync(RecyclerRoutes.Prices,
            new { wasteCategoryId = Guid.NewGuid(), pricePerKg = 5000, minQuantityKg = 1 }, token)).ShouldBe(404);
        Assert.False(r.Body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Prices_Add_WithEmptyBody_Returns400() =>
        (await ApiJson.RawAsync(HttpMethod.Post, RecyclerRoutes.Prices, "{}", await Collector())).ShouldFailWith(400);

    [Fact]
    public async Task PendingRecycler_Prices_Add_IsBlockedByVerification()
    {
        var category = await RecyclerHelper.GetCategoryAsync("plastic");
        (await ApiJson.PostAsync(RecyclerRoutes.Prices,
                new { wasteCategoryId = category.Id, pricePerKg = 5000, minQuantityKg = 1 }, await Pending()))
            .ShouldFailWith(400, "MSG-MTH-03");
    }

    // ========================================================================
    // 6. Pricing – sửa một dòng giá  PUT /api/v1/recycler/prices/{id}
    // ========================================================================
    private static async Task<PriceRow> PlasticRow()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        return (await RecyclerHelper.GetPricesAsync()).First(p => p.Code == "plastic");
    }

    [Fact]
    public async Task Prices_Update_ChangesAllFields()
    {
        var row = await PlasticRow();
        var token = await Collector();

        try
        {
            var r = (await ApiJson.PutAsync(RecyclerRoutes.Price(row.Id), new
            {
                pricePerKg = 9500,
                minQuantityKg = 3.5,
                collectionConditions = "Không nhận chai dầu nhớt",
                isActive = true
            }, token)).ShouldBe(200);

            var updated = r.Data().ToPriceRow();
            Assert.Equal(row.Id, updated.Id);
            Assert.Equal(9500m, updated.PricePerKg);
            Assert.Equal(3.5m, updated.MinQuantityKg);
            Assert.Equal("Không nhận chai dầu nhớt", updated.Conditions);

            var listed = (await RecyclerHelper.GetPricesAsync(token)).First(p => p.Id == row.Id);
            Assert.Equal(9500m, listed.PricePerKg);
            Assert.Equal(3.5m, listed.MinQuantityKg);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Prices_Update_PartialBody_KeepsOtherFields()
    {
        var row = await PlasticRow();
        var token = await Collector();

        try
        {
            var r = (await ApiJson.PutAsync(RecyclerRoutes.Price(row.Id), new { pricePerKg = 8800 }, token)).ShouldBe(200);

            var updated = r.Data().ToPriceRow();
            Assert.Equal(8800m, updated.PricePerKg);
            Assert.Equal(row.MinQuantityKg, updated.MinQuantityKg);
            Assert.Equal(row.Conditions, updated.Conditions);
            Assert.True(updated.IsActive);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Prices_Update_SuspendAndResume()
    {
        var row = await PlasticRow();
        var token = await Collector();

        try
        {
            var off = (await ApiJson.PutAsync(RecyclerRoutes.Price(row.Id), new { isActive = false }, token)).ShouldBe(200);
            Assert.False(off.Data().ToPriceRow().IsActive);
            Assert.Equal(row.PricePerKg, off.Data().ToPriceRow().PricePerKg); // giá giữ nguyên khi tạm ngưng

            var on = (await ApiJson.PutAsync(RecyclerRoutes.Price(row.Id), new { isActive = true }, token)).ShouldBe(200);
            Assert.True(on.Data().ToPriceRow().IsActive);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Prices_Update_RefreshesLastUpdatedTimestamp()
    {
        var row = await PlasticRow();
        var token = await Collector();

        try
        {
            await Task.Delay(1200);
            var r = (await ApiJson.PutAsync(RecyclerRoutes.Price(row.Id), new { pricePerKg = 8100 }, token)).ShouldBe(200);
            Assert.True(r.Data().ToPriceRow().LastUpdatedAt > row.LastUpdatedAt);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Prices_Update_DoesNotAffectOtherRows()
    {
        var row = await PlasticRow();
        var token = await Collector();
        var before = (await RecyclerHelper.GetPricesAsync(token)).Where(p => p.Id != row.Id).ToList();

        try
        {
            (await ApiJson.PutAsync(RecyclerRoutes.Price(row.Id), new { pricePerKg = 8300 }, token)).ShouldBe(200);

            var after = (await RecyclerHelper.GetPricesAsync(token)).Where(p => p.Id != row.Id).ToList();
            Assert.Equal(before.Select(p => (p.Id, p.PricePerKg, p.IsActive)).Order(),
                after.Select(p => (p.Id, p.PricePerKg, p.IsActive)).Order());
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Prices_Update_WithBoundaryValues_Succeeds()
    {
        var row = await PlasticRow();
        var token = await Collector();

        try
        {
            (await ApiJson.PutAsync(RecyclerRoutes.Price(row.Id), new
            {
                pricePerKg = 1,
                minQuantityKg = 0.001,
                collectionConditions = new string('c', 200)
            }, token)).ShouldBe(200);

            (await ApiJson.PutAsync(RecyclerRoutes.Price(row.Id), new { pricePerKg = 1_000_000_000, minQuantityKg = 100000 }, token))
                .ShouldBe(200);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Theory]
    [InlineData("price-zero")]
    [InlineData("price-negative")]
    [InlineData("price-fractional")]
    [InlineData("price-too-large")]
    [InlineData("minqty-zero")]
    [InlineData("minqty-negative")]
    [InlineData("conditions-too-long-201")]
    public async Task Prices_Update_WithInvalidData_Returns400_AndKeepsOldValues(string scenario)
    {
        var row = await PlasticRow();
        var token = await Collector();

        object body = scenario switch
        {
            "price-zero" => new { pricePerKg = 0 },
            "price-negative" => new { pricePerKg = -5 },
            "price-fractional" => new { pricePerKg = 8000.5 },
            "price-too-large" => new { pricePerKg = 1_000_000_001 },
            "minqty-zero" => new { minQuantityKg = 0 },
            "minqty-negative" => new { minQuantityKg = -1 },
            "conditions-too-long-201" => new { collectionConditions = new string('c', 201) },
            _ => throw new ArgumentException(scenario)
        };

        (await ApiJson.PutAsync(RecyclerRoutes.Price(row.Id), body, token)).ShouldFailWith(400);

        var after = (await RecyclerHelper.GetPricesAsync(token)).First(p => p.Id == row.Id);
        Assert.Equal(row.PricePerKg, after.PricePerKg);
        Assert.Equal(row.MinQuantityKg, after.MinQuantityKg);
        Assert.Equal(row.Conditions, after.Conditions);
    }

    [Fact]
    public async Task Prices_Update_WithUnknownId_Returns404()
    {
        var r = (await ApiJson.PutAsync(RecyclerRoutes.Price(Guid.NewGuid()), new { pricePerKg = 5000 }, await Collector())).ShouldBe(404);
        Assert.False(r.Body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Prices_Update_WithMalformedId_Returns404Or400() =>
        (await ApiJson.SendAsync(HttpMethod.Put, $"{RecyclerRoutes.Prices}/not-a-guid", new { pricePerKg = 5000 }, await Collector()))
            .ShouldBeIn(400, 404);

    [Fact]
    public async Task Prices_Update_CannotTouchAnotherRecyclersRow()
    {
        // Dòng giá của collector không được sửa bằng token của Recycler khác (phải trả về 400 do chưa duyệt hoặc 404 do không sở hữu)
        var row = await PlasticRow();
        var r = await ApiJson.PutAsync(RecyclerRoutes.Price(row.Id), new { pricePerKg = 1 }, await Pending());
        r.ShouldBeIn(400, 404);

        var after = (await RecyclerHelper.GetPricesAsync()).First(p => p.Id == row.Id);
        Assert.Equal(row.PricePerKg, after.PricePerKg);
    }

    [Fact]
    public async Task PendingRecycler_Prices_Update_IsBlockedByVerification() =>
        (await ApiJson.PutAsync(RecyclerRoutes.Price(SomeId), new { pricePerKg = 5000 }, await Pending()))
            .ShouldFailWith(400, "MSG-MTH-03");

    [Fact]
    public async Task Prices_Update_DeactivatingLastActiveRow_WhileOnline_IsBlocked()
    {
        var row = await PlasticRow();
        var token = await Collector();

        try
        {
            // Chỉ giữ lại duy nhất plastic đang thu mua
            (await RecyclerHelper.DeactivateAllAsync(new[] { "plastic" })).ShouldBe(200);

            (await ApiJson.PutAsync(RecyclerRoutes.Price(row.Id), new { isActive = false }, token))
                .ShouldFailWith(400, "MSG-MTH-06");

            var after = (await RecyclerHelper.GetPricesAsync(token)).First(p => p.Id == row.Id);
            Assert.True(after.IsActive);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    // ========================================================================
    // 7. Pricing – lưu cả bảng giá  PUT /api/v1/recycler/prices
    // ========================================================================
    [Fact]
    public async Task Prices_BulkSave_UpdatesRowsAndReturnsFullList()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var plastic = await RecyclerHelper.GetCategoryAsync("plastic");
        var metal = await RecyclerHelper.GetCategoryAsync("metal");

        try
        {
            var r = (await ApiJson.PutAsync(RecyclerRoutes.Prices, new
            {
                items = new[]
                {
                    RecyclerHelper.BulkItem(plastic.Id, 8200m, 2m, true, "Rửa sạch"),
                    RecyclerHelper.BulkItem(metal.Id, 15500m, 1.5m, true),
                }
            }, token)).ShouldBe(200);

            Assert.True(r.Body.GetProperty("success").GetBoolean());
            var rows = r.Data().EnumerateArray().Select(e => e.ToPriceRow()).ToList();
            Assert.True(rows.Count >= 4); // trả về toàn bộ bảng giá, không chỉ các dòng vừa gửi
            Assert.Equal(8200m, rows.First(p => p.Code == "plastic").PricePerKg);
            Assert.Equal("Rửa sạch", rows.First(p => p.Code == "plastic").Conditions);
            Assert.Equal(15500m, rows.First(p => p.Code == "metal").PricePerKg);
            Assert.Equal(1.5m, rows.First(p => p.Code == "metal").MinQuantityKg);

            var listed = await RecyclerHelper.GetPricesAsync(token);
            Assert.Equal(8200m, listed.First(p => p.Code == "plastic").PricePerKg);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Prices_BulkSave_PartialList_KeepsOtherRowsUnchanged()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var plastic = await RecyclerHelper.GetCategoryAsync("plastic");

        try
        {
            (await ApiJson.PutAsync(RecyclerRoutes.Prices,
                new { items = new[] { RecyclerHelper.BulkItem(plastic.Id, 8100m, 2m, true, "Rửa sạch") } }, token)).ShouldBe(200);

            var listed = await RecyclerHelper.GetPricesAsync(token);
            Assert.Equal(15000m, listed.First(p => p.Code == "metal").PricePerKg);
            Assert.Equal(3000m, listed.First(p => p.Code == "paper").PricePerKg);
            Assert.Equal(1000m, listed.First(p => p.Code == "glass").PricePerKg);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Prices_BulkSave_DoesNotCreateDuplicateRows()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var before = await RecyclerHelper.GetPricesAsync(token);

        await RecyclerHelper.EnsureBaselineAsync();
        await RecyclerHelper.EnsureBaselineAsync();

        var after = await RecyclerHelper.GetPricesAsync(token);
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before.Select(p => p.Id).Order(), after.Select(p => p.Id).Order());
    }

    [Fact]
    public async Task Prices_BulkSave_RefreshesLastUpdatedEvenWhenValuesUnchanged()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var before = (await RecyclerHelper.GetPricesAsync(token)).First(p => p.Code == "plastic");

        await Task.Delay(1200);
        await RecyclerHelper.EnsureBaselineAsync(); // gửi lại đúng giá trị cũ

        var after = (await RecyclerHelper.GetPricesAsync(token)).First(p => p.Code == "plastic");
        Assert.Equal(before.PricePerKg, after.PricePerKg);
        Assert.True(after.LastUpdatedAt > before.LastUpdatedAt);
    }

    [Fact]
    public async Task Prices_BulkSave_CanSuspendOneCategory_WhileOthersStayActive()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var glass = await RecyclerHelper.GetCategoryAsync("glass");

        try
        {
            (await ApiJson.PutAsync(RecyclerRoutes.Prices,
                new { items = new[] { RecyclerHelper.BulkItem(glass.Id, 1000m, 5m, active: false) } }, token)).ShouldBe(200);

            var rows = await RecyclerHelper.GetPricesAsync(token);
            Assert.False(rows.First(p => p.Code == "glass").IsActive);
            Assert.True(rows.First(p => p.Code == "plastic").IsActive);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Prices_BulkSave_AllInactive_WhileOnline_IsBlockedAndNothingChanges()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var before = await RecyclerHelper.GetPricesAsync(token);

        try
        {
            (await RecyclerHelper.DeactivateAllAsync()).ShouldFailWith(400, "MSG-MTH-06");

            var after = await RecyclerHelper.GetPricesAsync(token);
            Assert.Equal(before.Count(p => p.IsActive), after.Count(p => p.IsActive));
            Assert.Equal(before.Select(p => (p.Id, p.PricePerKg, p.IsActive)).Order(),
                after.Select(p => (p.Id, p.PricePerKg, p.IsActive)).Order());
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Prices_BulkSave_AllInactive_WhileOffline_IsAllowed_ButCannotGoOnlineAfterwards()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();

        try
        {
            (await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = false }, token)).ShouldBe(200);
            (await RecyclerHelper.DeactivateAllAsync()).ShouldBe(200);

            (await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = true }, token))
                .ShouldFailWith(400, "MSG-MTH-06");

            var availability = (await Api.GetAsync(RecyclerRoutes.Availability, token)).ShouldBe(200).Data();
            Assert.False(availability.GetProperty("isOnline").GetBoolean());
            Assert.Equal(0, availability.GetProperty("activePriceCount").GetInt32());
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Prices_BulkSave_DuplicateCategoryInRequest_Returns400()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var plastic = await RecyclerHelper.GetCategoryAsync("plastic");

        (await ApiJson.PutAsync(RecyclerRoutes.Prices, new
        {
            items = new[]
            {
                RecyclerHelper.BulkItem(plastic.Id, 8000m),
                RecyclerHelper.BulkItem(plastic.Id, 9000m),
            }
        }, await Collector())).ShouldFailWith(400);

        var row = (await RecyclerHelper.GetPricesAsync()).First(p => p.Code == "plastic");
        Assert.Equal(8000m, row.PricePerKg);
    }

    [Fact]
    public async Task Prices_BulkSave_UnknownCategory_Returns404_AndNothingChanges()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var plastic = await RecyclerHelper.GetCategoryAsync("plastic");

        (await ApiJson.PutAsync(RecyclerRoutes.Prices, new
        {
            items = new[]
            {
                RecyclerHelper.BulkItem(plastic.Id, 7777m), // dòng hợp lệ đứng trước
                RecyclerHelper.BulkItem(Guid.NewGuid(), 5000m),
            }
        }, token)).ShouldBe(404);

        var row = (await RecyclerHelper.GetPricesAsync(token)).First(p => p.Code == "plastic");
        Assert.Equal(8000m, row.PricePerKg); // không có thay đổi nào được lưu một phần
    }

    [Fact]
    public async Task Prices_BulkSave_OneInvalidItem_RejectsWholeRequest()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var plastic = await RecyclerHelper.GetCategoryAsync("plastic");
        var metal = await RecyclerHelper.GetCategoryAsync("metal");

        (await ApiJson.PutAsync(RecyclerRoutes.Prices, new
        {
            items = new[]
            {
                RecyclerHelper.BulkItem(plastic.Id, 7777m),
                RecyclerHelper.BulkItem(metal.Id, 0m), // giá không hợp lệ
            }
        }, token)).ShouldFailWith(400);

        var rows = await RecyclerHelper.GetPricesAsync(token);
        Assert.Equal(8000m, rows.First(p => p.Code == "plastic").PricePerKg);
        Assert.Equal(15000m, rows.First(p => p.Code == "metal").PricePerKg);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"items\":[]}")]
    [InlineData("{\"items\":null}")]
    [InlineData("{\"items\":[{}]}")]
    [InlineData("{\"items\":[{\"isActive\":true,\"pricePerKg\":5000}]}")]
    public async Task Prices_BulkSave_WithInvalidBody_Returns400(string rawJson) =>
        (await ApiJson.RawAsync(HttpMethod.Put, RecyclerRoutes.Prices, rawJson, await Collector())).ShouldFailWith(400);

    [Fact]
    public async Task PendingRecycler_Prices_BulkSave_IsBlockedByVerification()
    {
        var plastic = await RecyclerHelper.GetCategoryAsync("plastic");
        (await ApiJson.PutAsync(RecyclerRoutes.Prices,
                new { items = new[] { RecyclerHelper.BulkItem(plastic.Id, 5000m) } }, await Pending()))
            .ShouldFailWith(400, "MSG-MTH-03");
    }

    // ========================================================================
    // 8. Availability – xem  GET /api/v1/recycler/availability
    // ========================================================================
    [Fact]
    public async Task Availability_Get_ReturnsExpectedShape()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var r = (await Api.GetAsync(RecyclerRoutes.Availability, token)).ShouldBe(200);

        Assert.True(r.Body.GetProperty("success").GetBoolean());
        var a = r.Data();
        Assert.True(a.GetProperty("isOnline").GetBoolean());
        Assert.Equal("Online", a.GetProperty("activeStatus").GetString());
        Assert.Equal("Verified", a.GetProperty("verificationStatus").GetString());
        Assert.False(string.IsNullOrWhiteSpace(a.GetProperty("operatingHours").GetString()));
        Assert.Equal(JsonValueKind.Array, a.GetProperty("weeklySchedule").ValueKind);
        Assert.True(a.GetProperty("activePriceCount").GetInt32() >= 4);
    }

    [Fact]
    public async Task PendingRecycler_Availability_Get_IsOfflineAndPending()
    {
        var a = (await Api.GetAsync(RecyclerRoutes.Availability, await Pending())).ShouldBe(200).Data();

        Assert.False(a.GetProperty("isOnline").GetBoolean());
        Assert.Equal("Offline", a.GetProperty("activeStatus").GetString());
        Assert.Equal("Pending", a.GetProperty("verificationStatus").GetString());
        Assert.Equal(0, a.GetProperty("activePriceCount").GetInt32());
    }

    // ========================================================================
    // 9. Availability – bật/tắt  PATCH /api/v1/recycler/availability
    // ========================================================================
    [Fact]
    public async Task Availability_GoOffline_WithBusyReason_ThenOnline_ClearsReason()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();

        try
        {
            var off = (await ApiJson.PatchAsync(RecyclerRoutes.Availability,
                new { isOnline = false, busyReason = "Kho đã đầy, nghỉ đến mai" }, token)).ShouldBe(200);
            Assert.False(off.Data().GetProperty("isOnline").GetBoolean());
            Assert.Equal("Offline", off.Data().GetProperty("activeStatus").GetString());
            Assert.Equal("Kho đã đầy, nghỉ đến mai", off.Data().Str("busyReason"));

            var get = (await Api.GetAsync(RecyclerRoutes.Availability, token)).ShouldBe(200).Data();
            Assert.False(get.GetProperty("isOnline").GetBoolean());
            Assert.Equal("Kho đã đầy, nghỉ đến mai", get.Str("busyReason"));

            var on = (await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = true }, token)).ShouldBe(200);
            Assert.True(on.Data().GetProperty("isOnline").GetBoolean());
            Assert.Null(on.Data().Str("busyReason")); // Online thì xóa ghi chú bận
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Availability_TogglingIsIdempotent()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();

        try
        {
            for (var i = 0; i < 2; i++)
                Assert.False((await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = false }, token))
                    .ShouldBe(200).Data().GetProperty("isOnline").GetBoolean());

            for (var i = 0; i < 2; i++)
                Assert.True((await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = true }, token))
                    .ShouldBe(200).Data().GetProperty("isOnline").GetBoolean());
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Availability_Offline_DoesNotChangePriceList()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();
        var before = await RecyclerHelper.GetPricesAsync(token);

        try
        {
            (await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = false }, token)).ShouldBe(200);

            var after = await RecyclerHelper.GetPricesAsync(token);
            Assert.Equal(before.Select(p => (p.Id, p.PricePerKg, p.IsActive)).Order(),
                after.Select(p => (p.Id, p.PricePerKg, p.IsActive)).Order());
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Fact]
    public async Task Availability_BusyReason_AtMaxLength_Succeeds()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();

        try
        {
            (await ApiJson.PatchAsync(RecyclerRoutes.Availability,
                new { isOnline = false, busyReason = new string('b', 100) }, token)).ShouldBe(200);
        }
        finally
        {
            await RecyclerHelper.EnsureBaselineAsync();
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"isOnline\":null}")]
    [InlineData("{\"busyReason\":\"abc\"}")]
    [InlineData("{\"isOnline\":\"maybe\"}")]
    public async Task Availability_Update_WithInvalidBody_Returns400(string rawJson) =>
        (await ApiJson.RawAsync(HttpMethod.Patch, RecyclerRoutes.Availability, rawJson, await Collector())).ShouldFailWith(400);

    [Fact]
    public async Task Availability_Update_BusyReasonTooLong_Returns400_AndStatusUnchanged()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var token = await Collector();

        (await ApiJson.PatchAsync(RecyclerRoutes.Availability,
            new { isOnline = false, busyReason = new string('b', 101) }, token)).ShouldFailWith(400);

        var a = (await Api.GetAsync(RecyclerRoutes.Availability, token)).ShouldBe(200).Data();
        Assert.True(a.GetProperty("isOnline").GetBoolean());
    }

    [Fact]
    public async Task PendingRecycler_Availability_GoOnline_IsBlockedByVerification()
    {
        var token = await Pending();
        (await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = true }, token))
            .ShouldFailWith(400, "MSG-MTH-03");

        var a = (await Api.GetAsync(RecyclerRoutes.Availability, token)).ShouldBe(200).Data();
        Assert.False(a.GetProperty("isOnline").GetBoolean());
    }

    [Fact]
    public async Task PendingRecycler_Availability_GoOffline_IsAllowed() =>
        (await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = false }, await Pending())).ShouldBe(200);

    // ========================================================================
    // 10. Availability – lịch tuần  PUT /api/v1/recycler/availability/schedule
    // ========================================================================
    private static object Day(int dayOfWeek, string? open = null, string? close = null, bool closed = false) =>
        new { dayOfWeek, isClosed = closed, openTime = open, closeTime = close };

    private static List<object> WorkWeek(string open = "08:00", string close = "17:30") =>
        Enumerable.Range(1, 6).Select(d => Day(d, open, close)).Append(Day(0, closed: true)).ToList();

    private static async Task RestoreScheduleAsync(string token)
    {
        var allDays = Enumerable.Range(0, 7).Select(d => Day(d, "07:30", "18:30")).ToList();
        await ApiJson.PutAsync(RecyclerRoutes.Schedule, new { days = allDays }, token);
    }

    [Fact]
    public async Task Schedule_Update_WithValidWeek_PersistsAndBuildsSummary()
    {
        var token = await Collector();

        try
        {
            var r = (await ApiJson.PutAsync(RecyclerRoutes.Schedule, new { days = WorkWeek() }, token)).ShouldBe(200);
            Assert.True(r.Body.GetProperty("success").GetBoolean());

            var a = r.Data();
            var schedule = a.GetProperty("weeklySchedule").EnumerateArray().ToList();
            Assert.Equal(7, schedule.Count);
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 0 }, schedule.Select(d => d.GetProperty("dayOfWeek").GetInt32()).ToArray()); // Thứ hai đứng đầu, Chủ nhật cuối
            Assert.True(schedule[^1].GetProperty("isClosed").GetBoolean());
            Assert.Equal("08:00", schedule[0].Str("openTime"));
            Assert.Equal("17:30", schedule[0].Str("closeTime"));

            var hours = a.GetProperty("operatingHours").GetString()!;
            Assert.Contains("T2", hours);
            Assert.Contains("T7", hours);
            Assert.Contains("08:00 - 17:30", hours);
            Assert.DoesNotContain("CN", hours); // Chủ nhật nghỉ nên không xuất hiện

            var get = (await Api.GetAsync(RecyclerRoutes.Availability, token)).ShouldBe(200).Data();
            Assert.Equal(hours, get.GetProperty("operatingHours").GetString());
            Assert.Equal(7, get.GetProperty("weeklySchedule").GetArrayLength());
        }
        finally
        {
            await RestoreScheduleAsync(token);
        }
    }

    [Fact]
    public async Task Schedule_Update_GroupsDaysWithDifferentHours()
    {
        var token = await Collector();

        try
        {
            var days = new List<object>
            {
                Day(1, "08:00", "17:00"), Day(2, "08:00", "17:00"), Day(3, "08:00", "17:00"),
                Day(4, "08:00", "17:00"), Day(5, "08:00", "17:00"),
                Day(6, "09:00", "12:00"),
                Day(0, closed: true),
            };
            var hours = (await ApiJson.PutAsync(RecyclerRoutes.Schedule, new { days }, token)).ShouldBe(200)
                .Data().GetProperty("operatingHours").GetString()!;

            Assert.Contains("08:00 - 17:00", hours);
            Assert.Contains("T7: 09:00 - 12:00", hours);
        }
        finally
        {
            await RestoreScheduleAsync(token);
        }
    }

    [Fact]
    public async Task Schedule_Update_AllDaysClosed_IsAccepted()
    {
        var token = await Collector();

        try
        {
            var days = Enumerable.Range(0, 7).Select(d => Day(d, closed: true)).ToList();
            var a = (await ApiJson.PutAsync(RecyclerRoutes.Schedule, new { days }, token)).ShouldBe(200).Data();
            Assert.All(a.GetProperty("weeklySchedule").EnumerateArray(), d => Assert.True(d.GetProperty("isClosed").GetBoolean()));
        }
        finally
        {
            await RestoreScheduleAsync(token);
        }
    }

    [Fact]
    public async Task Schedule_Update_PartialWeek_IsAccepted()
    {
        var token = await Collector();

        try
        {
            var a = (await ApiJson.PutAsync(RecyclerRoutes.Schedule,
                new { days = new[] { Day(1, "08:00", "12:00"), Day(3, "13:00", "17:00") } }, token)).ShouldBe(200).Data();
            Assert.Equal(2, a.GetProperty("weeklySchedule").GetArrayLength());
        }
        finally
        {
            await RestoreScheduleAsync(token);
        }
    }

    [Fact]
    public async Task Schedule_Update_ClosedDayIgnoresTimes()
    {
        var token = await Collector();

        try
        {
            var a = (await ApiJson.PutAsync(RecyclerRoutes.Schedule,
                new { days = new[] { Day(0, "25:99", "bad", closed: true), Day(1, "08:00", "17:00") } }, token)).ShouldBe(200).Data();

            var sunday = a.GetProperty("weeklySchedule").EnumerateArray().First(d => d.GetProperty("dayOfWeek").GetInt32() == 0);
            Assert.True(sunday.GetProperty("isClosed").GetBoolean());
            Assert.Null(sunday.Str("openTime"));
        }
        finally
        {
            await RestoreScheduleAsync(token);
        }
    }

    [Theory]
    [InlineData("time-without-leading-zero")]
    [InlineData("time-not-a-time")]
    [InlineData("time-hour-24")]
    [InlineData("close-before-open")]
    [InlineData("close-equals-open")]
    [InlineData("missing-open")]
    [InlineData("missing-close")]
    [InlineData("day-7")]
    [InlineData("day-negative")]
    [InlineData("duplicate-day")]
    [InlineData("more-than-7-days")]
    [InlineData("empty-days")]
    public async Task Schedule_Update_WithInvalidData_Returns400_AndKeepsOldSchedule(string scenario)
    {
        var token = await Collector();
        var before = (await Api.GetAsync(RecyclerRoutes.Availability, token)).ShouldBe(200).Data().GetProperty("operatingHours").GetString();

        var days = scenario switch
        {
            "time-without-leading-zero" => new List<object> { Day(1, "8:00", "17:00") },
            "time-not-a-time" => new List<object> { Day(1, "abc", "17:00") },
            "time-hour-24" => new List<object> { Day(1, "08:00", "24:00") },
            "close-before-open" => new List<object> { Day(1, "17:30", "08:00") },
            "close-equals-open" => new List<object> { Day(1, "08:00", "08:00") },
            "missing-open" => new List<object> { Day(1, null, "17:00") },
            "missing-close" => new List<object> { Day(1, "08:00", null) },
            "day-7" => new List<object> { Day(7, "08:00", "17:00") },
            "day-negative" => new List<object> { Day(-1, "08:00", "17:00") },
            "duplicate-day" => new List<object> { Day(1, "08:00", "17:00"), Day(1, "09:00", "18:00") },
            "more-than-7-days" => Enumerable.Range(0, 8).Select(d => Day(d % 7, "08:00", "17:00")).ToList(),
            "empty-days" => new List<object>(),
            _ => throw new ArgumentException(scenario)
        };

        (await ApiJson.PutAsync(RecyclerRoutes.Schedule, new { days }, token)).ShouldFailWith(400);

        var after = (await Api.GetAsync(RecyclerRoutes.Availability, token)).ShouldBe(200).Data().GetProperty("operatingHours").GetString();
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"days\":null}")]
    public async Task Schedule_Update_WithMissingDays_Returns400(string rawJson) =>
        (await ApiJson.RawAsync(HttpMethod.Put, RecyclerRoutes.Schedule, rawJson, await Collector())).ShouldFailWith(400);

    [Fact]
    public async Task PendingRecycler_Schedule_Update_IsBlockedByVerification() =>
        (await ApiJson.PutAsync(RecyclerRoutes.Schedule, new { days = WorkWeek() }, await Pending()))
            .ShouldFailWith(400, "MSG-MTH-03");

    // ========================================================================
    // 11. Response envelope chung
    // ========================================================================
    [Theory]
    [InlineData("profile")]
    [InlineData("prices")]
    [InlineData("availability")]
    [InlineData("categories")]
    public async Task GetEndpoints_ReturnStandardEnvelope(string endpoint)
    {
        var path = endpoint switch
        {
            "profile" => RecyclerRoutes.Profile,
            "prices" => RecyclerRoutes.Prices,
            "availability" => RecyclerRoutes.Availability,
            _ => RecyclerRoutes.WasteCategories
        };

        var r = (await Api.GetAsync(path, await Collector())).ShouldBe(200);
        Assert.True(r.Body.GetProperty("success").GetBoolean());
        Assert.True(r.Body.TryGetProperty("timestamp", out _), "Missing 'timestamp' field in response envelope");
        Assert.True(r.Body.TryGetProperty("data", out _), "Missing 'data' field in response envelope");
    }
}
