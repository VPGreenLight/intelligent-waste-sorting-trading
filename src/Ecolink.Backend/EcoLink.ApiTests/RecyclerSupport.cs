using System.Text;
using System.Text.Json;
using Xunit.Sdk;

namespace EcoLink.ApiTests;

/// <summary>Đường dẫn endpoint của module Recycler Profile, Pricing &amp; Availability.</summary>
public static class RecyclerRoutes
{
    public const string RegisterRecycler = "/api/v1/auth/register-recycler";
    public const string WasteCategories = "/api/v1/waste-categories";
    public const string Profile = "/api/v1/recycler/profile";
    public const string Prices = "/api/v1/recycler/prices";
    public const string Availability = "/api/v1/recycler/availability";
    public const string Schedule = "/api/v1/recycler/availability/schedule";

    public static string Price(Guid id) => $"{Prices}/{id}";
}

/// <summary>Gọi API với body JSON cho các method PUT / PATCH / POST (Support.cs chỉ có sẵn GET và POST).</summary>
public static class ApiJson
{
    public static Task<ApiResult> SendAsync(HttpMethod method, string path, object? body = null, string? token = null) =>
        Api.SendAsync(method, path,
            body == null ? null : new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            token);

    public static Task<ApiResult> PostAsync(string path, object? body, string? token = null) =>
        SendAsync(HttpMethod.Post, path, body, token);

    public static Task<ApiResult> PutAsync(string path, object? body, string? token = null) =>
        SendAsync(HttpMethod.Put, path, body, token);

    public static Task<ApiResult> PatchAsync(string path, object? body, string? token = null) =>
        SendAsync(HttpMethod.Patch, path, body, token);

    public static Task<ApiResult> RawAsync(HttpMethod method, string path, string rawJson, string? token = null) =>
        Api.SendAsync(method, path, new StringContent(rawJson, Encoding.UTF8, "application/json"), token);
}

public sealed record Category(Guid Id, string Code, string Name);

public sealed record PriceRow(
    Guid Id, Guid CategoryId, string Code, decimal PricePerKg, decimal MinQuantityKg,
    string? Conditions, bool IsActive, DateTime LastUpdatedAt, bool IsOutdated);

/// <summary>Hàm tiện ích + dữ liệu nền (baseline) cho test module Recycler.</summary>
public static class RecyclerHelper
{
    // Loại phế liệu được dành riêng cho test POST /recycler/prices (baseline KHÔNG tạo dòng giá cho nó)
    public const string ReservedCategoryCode = "cardboard";

    // Bảng giá nền dùng để đưa tài khoản collector về trạng thái đã biết trước mỗi test
    private static readonly Dictionary<string, (decimal Price, decimal MinQty, string? Conditions)> Baseline = new()
    {
        ["plastic"] = (8000m, 2m, "Đã rửa sạch"),
        ["paper"] = (3000m, 5m, null),
        ["glass"] = (1000m, 5m, null),
        ["metal"] = (15000m, 1m, null),
    };

    public static Task<string> CollectorToken() => Tokens.GetAsync("collector");

    // ---------- Đọc response ----------
    public static JsonElement Data(this ApiResult r) => r.Body.GetProperty("data");

    public static string? MessageCode(this ApiResult r) =>
        r.Json is { ValueKind: JsonValueKind.Object } j &&
        j.TryGetProperty("messageCode", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString()
            : null;

    /// <summary>Kiểm tra response lỗi chuẩn: đúng status, success = false và (tùy chọn) đúng messageCode.</summary>
    public static ApiResult ShouldFailWith(this ApiResult r, int status, string? messageCode = null)
    {
        r.ShouldBe(status);
        Assert.False(r.Body.GetProperty("success").GetBoolean());
        if (messageCode != null)
            Assert.Equal(messageCode, r.MessageCode());
        return r;
    }

    public static string? Str(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static PriceRow ParsePrice(JsonElement e) => new(
        e.GetProperty("id").GetGuid(),
        e.GetProperty("wasteCategoryId").GetGuid(),
        e.GetProperty("wasteCategoryCode").GetString()!,
        e.GetProperty("pricePerKg").GetDecimal(),
        e.GetProperty("minQuantityKg").GetDecimal(),
        e.Str("collectionConditions"),
        e.GetProperty("isActive").GetBoolean(),
        e.GetProperty("lastUpdatedAt").GetDateTime(),
        e.GetProperty("isOutdated").GetBoolean());

    public static PriceRow ToPriceRow(this JsonElement e) => ParsePrice(e);

    // ---------- Gọi API ----------
    public static async Task<List<Category>> GetCategoriesAsync()
    {
        var token = await CollectorToken();
        var r = (await Api.GetAsync(RecyclerRoutes.WasteCategories, token)).ShouldBe(200);
        return r.Data().EnumerateArray()
            .Select(e => new Category(e.GetProperty("id").GetGuid(), e.GetProperty("code").GetString()!,
                e.GetProperty("name").GetString()!))
            .ToList();
    }

    public static async Task<Category> GetCategoryAsync(string code) =>
        (await GetCategoriesAsync()).First(c => c.Code == code);

    public static async Task<List<PriceRow>> GetPricesAsync(string? token = null, IDictionary<string, string>? query = null)
    {
        token ??= await CollectorToken();
        var r = (await Api.GetAsync(RecyclerRoutes.Prices, token, query)).ShouldBe(200);
        return r.Data().EnumerateArray().Select(ParsePrice).ToList();
    }

    public static object BulkItem(Guid categoryId, decimal price, decimal minQty = 1m, bool active = true, string? conditions = null) =>
        new { wasteCategoryId = categoryId, isActive = active, pricePerKg = price, minQuantityKg = minQty, collectionConditions = conditions };

    /// <summary>Đưa collector về trạng thái nền: 4 loại phế liệu đang thu mua với giá cố định và cơ sở đang Online.</summary>
    public static async Task EnsureBaselineAsync()
    {
        var token = await CollectorToken();
        var categories = await GetCategoriesAsync();

        var items = new List<object>();
        foreach (var c in categories)
        {
            if (!Baseline.TryGetValue(c.Code, out var b)) continue;
            items.Add(BulkItem(c.Id, b.Price, b.MinQty, true, b.Conditions));
        }

        (await ApiJson.PutAsync(RecyclerRoutes.Prices, new { items }, token)).ShouldBe(200);
        (await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = true }, token)).ShouldBe(200);
    }

    /// <summary>Chuyển toàn bộ dòng giá hiện có của collector sang trạng thái tạm ngưng (giữ nguyên giá).</summary>
    public static async Task<ApiResult> DeactivateAllAsync(IEnumerable<string>? exceptCodes = null)
    {
        var token = await CollectorToken();
        var except = exceptCodes?.ToHashSet() ?? new HashSet<string>();
        var rows = await GetPricesAsync(token);
        var items = rows
            .Select(p => BulkItem(p.CategoryId, p.PricePerKg, p.MinQuantityKg, except.Contains(p.Code), p.Conditions))
            .ToList();
        return await ApiJson.PutAsync(RecyclerRoutes.Prices, new { items }, token);
    }

    // ---------- Recycler mới đăng ký (luôn ở trạng thái Pending) ----------
    private static readonly Lazy<Task<string>> PendingToken = new(async () =>
    {
        var suffix = TestData.Unique();
        var payload = new Dictionary<string, object?>
        {
            ["username"] = $"qa_rc_{suffix}",
            ["email"] = $"qa_rc_{suffix}@example.com",
            ["password"] = "Passw0rd!23",
            ["fullName"] = "QA Recycler",
            ["phoneNumber"] = "0912345678",
            ["facilityName"] = $"Vựa QA {suffix}",
            ["representativeName"] = "QA Representative",
            ["businessAddress"] = "1 Đường Test, Quận 1, TP.HCM",
            ["latitude"] = 10.7769,
            ["longitude"] = 106.7009,
            ["contactPhone"] = "0912345678",
        };

        var register = await Api.PostJsonAsync(RecyclerRoutes.RegisterRecycler, payload);
        if (register.Status is not (200 or 201))
            throw new XunitException($"Failed to register the pending recycler: {register.Status} {register.Raw}");

        var token = Api.ExtractToken(register.Body);
        if (token != null) return token;

        var login = await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = (string)payload["username"]!, password = (string)payload["password"]! });
        if (login.Status != 200)
            throw new XunitException($"Could not log in as the pending recycler: {login.Status} {login.Raw}");
        return Api.ExtractToken(login.Body)
               ?? throw new XunitException("Login of the pending recycler returned no token.");
    });

    /// <summary>JWT của một Recycler vừa đăng ký, hồ sơ đang chờ Staff duyệt (Pending) – dùng chung cho cả lần chạy.</summary>
    public static Task<string> PendingRecyclerToken() => PendingToken.Value;
}
