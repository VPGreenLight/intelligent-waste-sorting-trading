using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit.Sdk;

namespace EcoLink.ApiTests;

/// <summary>Đường dẫn endpoint.</summary>
public static class Routes
{
    public const string Health = "/health";
    public const string Register = "/api/v1/auth/register";
    public const string Login = "/api/v1/auth/login";
    public const string Me = "/api/v1/auth/me";
    public const string DemoUsers = "/api/v1/auth/demo-users";
    public const string VietQrToken = "/api/v1/auth/vietqr-token";
    public const string ScanClassify = "/api/v1/waste-scan/classify";
    public const string ScanModels = "/api/v1/waste-scan/models";
    public const string ScanRules = "/api/v1/waste-scan/rules";
    public const string ScanFeedback = "/api/v1/waste-scan/feedback";
}

/// <summary>Kết quả một lần gọi API.</summary>
public sealed record ApiResult(int Status, string Raw, JsonElement? Json)
{
    public JsonElement Body =>
        Json ?? throw new XunitException($"Response is not JSON (status {Status}): {Truncate(Raw)}");

    public ApiResult ShouldBe(int expected)
    {
        if (Status != expected)
            throw new XunitException($"Expected status {expected}, got {Status}: {Truncate(Raw)}");
        return this;
    }

    public ApiResult ShouldBeIn(params int[] expected)
    {
        if (!expected.Contains(Status))
            throw new XunitException(
                $"Expected status in [{string.Join(", ", expected)}], got {Status}: {Truncate(Raw)}");
        return this;
    }

    public ApiResult ShouldBeClientError()
    {
        if (Status < 400 || Status >= 500)
            throw new XunitException($"Expected a 4xx status, got {Status}: {Truncate(Raw)}");
        return this;
    }

    public ApiResult ShouldNotBeServerError()
    {
        if (Status >= 500)
            throw new XunitException($"Unexpected server error ({Status}): {Truncate(Raw)}");
        return this;
    }

    private static string Truncate(string s) => s.Length <= 400 ? s : s[..400] + "...";
}

/// <summary>HttpClient dùng chung + cấu hình qua biến môi trường.</summary>
public static class Api
{
    // ECOLINK_BASE_URL, ECOLINK_VERIFY_SSL (false để bỏ qua chứng chỉ tự ký),
    // ECOLINK_TIMEOUT (giây), ECOLINK_REGISTER_ROLE (giá trị enum UserRole khi đăng ký)
    public static readonly string BaseUrl =
        (Environment.GetEnvironmentVariable("ECOLINK_BASE_URL") ?? "http://localhost:5000").TrimEnd('/');

    public static readonly int RegisterRole = ParseInt("ECOLINK_REGISTER_ROLE", 1);

    private static readonly HttpClient Http = CreateClient();

    private static int ParseInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler();
        var verify = Environment.GetEnvironmentVariable("ECOLINK_VERIFY_SSL");
        if (string.Equals(verify, "false", StringComparison.OrdinalIgnoreCase))
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(ParseInt("ECOLINK_TIMEOUT", 30)) };
        // Bỏ qua trang cảnh báo của ngrok (bản miễn phí)
        client.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
        return client;
    }

    public static async Task<ApiResult> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content = null,
        string? token = null,
        IDictionary<string, string>? query = null)
    {
        var url = BaseUrl + path;
        if (query is { Count: > 0 })
            url += "?" + string.Join("&",
                query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await Http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        JsonElement? json = null;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                json = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                // không phải JSON -> để json = null
            }
        }

        return new ApiResult((int)response.StatusCode, raw, json);
    }

    public static Task<ApiResult> GetAsync(string path, string? token = null,
        IDictionary<string, string>? query = null) =>
        SendAsync(HttpMethod.Get, path, null, token, query);

    public static Task<ApiResult> PostJsonAsync(string path, object body, string? token = null) =>
        PostRawJsonAsync(path, JsonSerializer.Serialize(body), token);

    public static Task<ApiResult> PostRawJsonAsync(string path, string rawJson, string? token = null) =>
        SendAsync(HttpMethod.Post, path,
            new StringContent(rawJson, Encoding.UTF8, "application/json"), token);

    public static Task<ApiResult> PostEmptyAsync(string path, string? token = null) =>
        SendAsync(HttpMethod.Post, path, null, token);

    /// <summary>
    /// Response hiện tại bọc trong envelope { success, messageCode, message, data: {...} }.
    /// Trả về phần "data" nếu có, ngược lại trả về chính body (tương thích cả hai kiểu).
    /// </summary>
    public static JsonElement Unwrap(JsonElement body) =>
        body.ValueKind == JsonValueKind.Object &&
        body.TryGetProperty("data", out var data) &&
        data.ValueKind == JsonValueKind.Object
            ? data
            : body;

    public static string? ExtractToken(JsonElement body)
    {
        foreach (var candidate in new[] { body, Unwrap(body) })
        {
            foreach (var name in new[] { "accessToken", "token" })
            {
                if (candidate.ValueKind == JsonValueKind.Object &&
                    candidate.TryGetProperty(name, out var p) &&
                    p.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(p.GetString()))
                    return p.GetString();
            }
        }
        return null;
    }
}

/// <summary>Tài khoản demo (theo mô tả trong swagger) + cache JWT.</summary>
public static class Tokens
{
    public static readonly Dictionary<string, (string User, string Password)> Accounts = new()
    {
        ["citizen"] = ("citizen", "citizen123"),
        ["collector"] = ("collector", "collector123"),
        ["staff"] = ("staff", "staff123"),
        ["admin"] = ("admin", "admin123"),
    };

    public static TheoryData<string> Roles => new() { "citizen", "collector", "admin" };

    private static readonly ConcurrentDictionary<string, Lazy<Task<string>>> Cache = new();

    public static Task<string> GetAsync(string role) =>
        Cache.GetOrAdd(role, r => new Lazy<Task<string>>(() => LoginAsync(r))).Value;

    private static async Task<string> LoginAsync(string role)
    {
        var (user, password) = Accounts[role];
        var result = await Api.PostJsonAsync(Routes.Login, new { usernameOrEmail = user, password });
        if (result.Status != 200)
            throw new XunitException($"Could not log in demo account '{role}': {result.Status} {result.Raw}");

        return Api.ExtractToken(result.Body)
               ?? throw new XunitException($"Login for '{role}' succeeded but returned no token: {result.Raw}");
    }
}

/// <summary>Dữ liệu test dùng chung.</summary>
public static class TestData
{
    public static string Unique(int n = 10) => Guid.NewGuid().ToString("N")[..n];

    public static Dictionary<string, object?> ValidRegister()
    {
        var suffix = Unique();
        return new Dictionary<string, object?>
        {
            ["username"] = $"qa_{suffix}",
            ["email"] = $"qa_{suffix}@example.com",
            ["password"] = "Passw0rd!23",
            ["fullName"] = "QA Tester",
            ["phoneNumber"] = "0912345678",
            ["role"] = Api.RegisterRole,
        };
    }

    public static string Str(this Dictionary<string, object?> payload, string key) => (string)payload[key]!;

    private static readonly Lazy<Task<Dictionary<string, object?>>> Registered = new(async () =>
    {
        var payload = ValidRegister();
        var result = await Api.PostJsonAsync(Routes.Register, payload);
        if (result.Status is not (200 or 201))
            throw new XunitException($"Failed to register the shared test account: {result.Status} {result.Raw}");
        return payload;
    });

    /// <summary>Một tài khoản mới đăng ký, dùng chung cho cả lần chạy.</summary>
    public static Task<Dictionary<string, object?>> RegisteredUserAsync() => Registered.Value;

    public static bool IsNonEmpty(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Array => e.GetArrayLength() > 0,
        JsonValueKind.Object => e.EnumerateObject().Any(),
        JsonValueKind.String => !string.IsNullOrEmpty(e.GetString()),
        JsonValueKind.Null or JsonValueKind.Undefined => false,
        _ => true,
    };
}

/// <summary>Tạo ảnh PNG hợp lệ ngay trong code (không cần thư viện ảnh).</summary>
public static class TestImages
{
    public static byte[] Png(int width = 64, int height = 64)
    {
        var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0); // filter: none
            for (var x = 0; x < width; x++)
            {
                var inSquare = x > width / 4 && x < width * 3 / 4 && y > height / 4 && y < height * 3 / 4;
                raw.WriteByte((byte)(inSquare ? 200 : 120));
                raw.WriteByte((byte)(inSquare ? 60 : 180));
                raw.WriteByte((byte)(inSquare ? 60 : 90));
            }
        }

        var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw.ToArray());
        }

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 2; // color type RGB

        var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        WriteChunk(png, "IHDR", ihdr);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var buf = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)data.Length);
        s.Write(buf);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);

        BinaryPrimitives.WriteUInt32BigEndian(buf, Crc32(typeBytes, data));
        s.Write(buf);
    }

    private static uint Crc32(byte[] a, byte[] b)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var arr in new[] { a, b })
        {
            foreach (var by in arr)
            {
                crc ^= by;
                for (var k = 0; k < 8; k++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return crc ^ 0xFFFFFFFFu;
    }
}
