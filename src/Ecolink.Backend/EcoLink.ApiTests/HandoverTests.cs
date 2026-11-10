using System.Text.Json;

namespace EcoLink.ApiTests;

public static class HandoverRoutes
{
    public const string Base = "/api/v1/handover-requests";
    public const string RecyclerBase = "/api/v1/recycler/handover-requests";

    public static string One(Guid id) => $"{Base}/{id}";
    public static string Cancel(Guid id) => $"{Base}/{id}/cancel";
    public static string RcOne(Guid id) => $"{RecyclerBase}/{id}";
    public static string Accept(Guid id) => $"{RecyclerBase}/{id}/accept";
    public static string Reject(Guid id) => $"{RecyclerBase}/{id}/reject";
    public static string RcCancel(Guid id) => $"{RecyclerBase}/{id}/cancel";

    // Handover Completion & Transaction Receipt
    public static string Receipt(Guid id) => $"{Base}/{id}/receipt";
    public static string Confirm(Guid id) => $"{Base}/{id}/receipt/confirm";
    public static string RcReceipt(Guid id) => $"{RecyclerBase}/{id}/receipt";
    public static string RcPreview(Guid id) => $"{RecyclerBase}/{id}/receipt/preview";
    public const string Transactions = "/api/v1/transactions";
    public const string RecyclerTransactions = "/api/v1/recycler/transactions";
}

/// <summary>Hàm tiện ích + dữ liệu nền cho test module Handover Request Management.</summary>
public static class HandoverHelper
{
    public const string PhotoUrl = "https://example.com/photos/scrap-1.jpg";
    public static Guid? RecyclerIdCache;

    public static Task<string> Citizen() => Tokens.GetAsync("citizen");
    public static Task<string> Collector() => Tokens.GetAsync("collector");

    /// <summary>Id của Recycler demo = userId trong claim JWT (lấy từ /auth/me hoặc hồ sơ).</summary>
    public static async Task<Guid> RecyclerIdAsync()
    {
        if (RecyclerIdCache.HasValue) return RecyclerIdCache.Value;
        var r = (await Api.GetAsync(RecyclerRoutes.Profile, await Collector())).ShouldBe(200);
        var d = r.Data();
        foreach (var name in new[] { "recyclerId", "userId", "id" })
            if (d.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && Guid.TryParse(p.GetString(), out var g))
                return (RecyclerIdCache = g).Value;
        throw new Xunit.Sdk.XunitException("Không tìm thấy id của recycler trong hồ sơ: " + r.Raw);
    }

    /// <summary>Đưa Recycler về trạng thái nền: bảng giá chuẩn, Online, mở cửa cả tuần 00:00–23:59.</summary>
    public static async Task EnsureRecyclerReadyAsync()
    {
        await RecyclerHelper.EnsureBaselineAsync();
        var days = Enumerable.Range(0, 7)
            .Select(d => new { dayOfWeek = d, isClosed = false, openTime = "00:00", closeTime = "23:59" }).ToList();
        (await ApiJson.PutAsync(RecyclerRoutes.Schedule, new { days }, await Collector())).ShouldBe(200);
    }

    /// <summary>Hủy mọi yêu cầu đang xử lý của citizen để không chạm giới hạn 3 yêu cầu.</summary>
    public static async Task ClearCitizenActiveAsync()
    {
        var token = await Citizen();
        foreach (var status in new[] { "Pending", "Negotiating", "Accepted", "Scheduled", "AwaitingConfirmation" })
        {
            var r = await Api.GetAsync(HandoverRoutes.Base, token,
                new Dictionary<string, string> { ["status"] = status, ["pageSize"] = "50" });
            if (r.Status != 200) continue;
            foreach (var item in r.Data().GetProperty("items").EnumerateArray())
            {
                var id = item.GetProperty("id").GetGuid();
                if (status == "AwaitingConfirmation")   // đã có biên nhận: không hủy được, hoàn tất để dọn
                    await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, token);
                else
                    await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "dọn dẹp test" }, token);
            }
        }
    }

    public static string Iso(DateTimeOffset t) => t.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

    public static async Task<object> PayloadAsync(
        Guid? recyclerId = null, string[]? categoryCodes = null, decimal weight = 10m,
        DateTimeOffset? slot = null, string[]? photos = null, string address = "12 Đường Test, Quận 1, TP.HCM",
        string? note = "Để trước cổng")
    {
        var codes = categoryCodes ?? new[] { "plastic" };
        var items = new List<object>();
        foreach (var code in codes)
            items.Add(new { wasteCategoryId = (await RecyclerHelper.GetCategoryAsync(code)).Id, estimatedWeightKg = weight });

        return new
        {
            recyclerId = recyclerId ?? await RecyclerIdAsync(),
            items,
            photoUrls = photos ?? new[] { PhotoUrl },
            proposedTimeSlot = Iso(slot ?? DateTimeOffset.UtcNow.AddHours(26)),
            pickupAddress = address,
            userNote = note,
        };
    }

    /// <summary>Tạo yêu cầu thành công (kiểm tra 201) và trả về id.</summary>
    public static async Task<Guid> CreateAsync(DateTimeOffset? slot = null, string? token = null)
    {
        token ??= await Citizen();
        var r = (await ApiJson.PostAsync(HandoverRoutes.Base, await PayloadAsync(slot: slot), token)).ShouldBe(201);
        return r.Data().GetProperty("id").GetGuid();
    }

    public static async Task<ApiResult> PostWithAsync(Func<Dictionary<string, object?>, Task> mutate, string? token = null)
    {
        token ??= await Citizen();
        var json = JsonSerializer.Serialize(await PayloadAsync());
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        await mutate(dict);
        return await ApiJson.PostAsync(HandoverRoutes.Base, dict, token);
    }

    public static async Task<string> SecondUserTokenAsync()
    {
        var payload = await TestData.RegisteredUserAsync();
        var login = (await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = payload.Str("username"), password = payload.Str("password") })).ShouldBe(200);
        return Api.ExtractToken(login.Body)!;
    }
}

/// <summary>
/// Test cho module Handover Request Management (UC U-06, U-07, R-03, R-04; BR-HND-01..08).
/// Dùng tài khoản demo citizen (người dân) và collector (Recycler Verified + Online).
/// </summary>
[Trait("Category", "Handover")]
public class HandoverTests
{
    private static Task<string> Citizen() => HandoverHelper.Citizen();
    private static Task<string> Collector() => HandoverHelper.Collector();

    private static async Task Prepare()
    {
        await HandoverHelper.EnsureRecyclerReadyAsync();
        await HandoverHelper.ClearCitizenActiveAsync();
    }

    // ---------------- 1. Phân quyền ----------------
    private static readonly (string Method, string Path)[] UserEndpoints =
    {
        ("GET", HandoverRoutes.Base), ("POST", HandoverRoutes.Base),
        ("GET", $"{HandoverRoutes.Base}/00000000-0000-0000-0000-000000000001"),
        ("PUT", $"{HandoverRoutes.Base}/00000000-0000-0000-0000-000000000001"),
        ("POST", $"{HandoverRoutes.Base}/00000000-0000-0000-0000-000000000001/cancel"),
    };

    private static readonly (string Method, string Path)[] RecyclerEndpoints =
    {
        ("GET", HandoverRoutes.RecyclerBase),
        ("GET", $"{HandoverRoutes.RecyclerBase}/00000000-0000-0000-0000-000000000001"),
        ("POST", $"{HandoverRoutes.RecyclerBase}/00000000-0000-0000-0000-000000000001/accept"),
        ("POST", $"{HandoverRoutes.RecyclerBase}/00000000-0000-0000-0000-000000000001/reject"),
        ("POST", $"{HandoverRoutes.RecyclerBase}/00000000-0000-0000-0000-000000000001/cancel"),
    };

    public static TheoryData<string, string> AllEndpoints
    {
        get
        {
            var d = new TheoryData<string, string>();
            foreach (var (m, p) in UserEndpoints.Concat(RecyclerEndpoints)) d.Add(m, p);
            return d;
        }
    }

    public static TheoryData<string, string> UserEndpointData
    {
        get { var d = new TheoryData<string, string>(); foreach (var (m, p) in UserEndpoints) d.Add(m, p); return d; }
    }

    public static TheoryData<string, string> RecyclerEndpointData
    {
        get { var d = new TheoryData<string, string>(); foreach (var (m, p) in RecyclerEndpoints) d.Add(m, p); return d; }
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task Endpoint_WithoutToken_Returns401(string method, string path) =>
        (await ApiJson.SendAsync(new HttpMethod(method), path, new { })).ShouldBe(401);

    [Theory]
    [MemberData(nameof(UserEndpointData))]
    public async Task UserEndpoint_AsRecycler_Returns403(string method, string path) =>
        (await ApiJson.SendAsync(new HttpMethod(method), path, new { }, await Collector())).ShouldBe(403);

    [Theory]
    [MemberData(nameof(RecyclerEndpointData))]
    public async Task RecyclerEndpoint_AsCitizen_Returns403(string method, string path) =>
        (await ApiJson.SendAsync(new HttpMethod(method), path, new { }, await Citizen())).ShouldBe(403);

    // ---------------- 2. Tạo yêu cầu (U-06) ----------------
    [Fact]
    public async Task Create_Valid_Returns201_Pending_WithCode()
    {
        await Prepare();
        var token = await Citizen();
        var r = (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(), token)).ShouldBe(201);
        try
        {
            Assert.Equal("MSG-HND-01", r.MessageCode());
            var d = r.Data();
            Assert.Equal("Pending", d.Str("status"));
            Assert.Matches(@"^HND-\d{8}-\d{4}$", d.Str("requestCode")!);
            Assert.Equal(10m, d.GetProperty("totalEstimatedWeightKg").GetDecimal());
            Assert.Single(d.GetProperty("photoUrls").EnumerateArray());
            Assert.True(d.GetProperty("canEdit").GetBoolean());
            Assert.False(d.GetProperty("isContactRevealed").GetBoolean());
            // BR-HND-08: chưa Accepted thì chưa lộ SĐT Recycler
            Assert.Equal(JsonValueKind.Null, d.GetProperty("recycler").GetProperty("phone").ValueKind);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(r.Data().GetProperty("id").GetGuid()), new { reason = "cleanup" }, token); }
    }

    [Fact]
    public async Task Create_MultipleCategories_Returns201()
    {
        await Prepare();
        var token = await Citizen();
        var r = (await ApiJson.PostAsync(HandoverRoutes.Base,
            await HandoverHelper.PayloadAsync(categoryCodes: new[] { "plastic", "metal" }), token)).ShouldBe(201);
        Assert.Equal(2, r.Data().GetProperty("items").GetArrayLength());
        await ApiJson.PostAsync(HandoverRoutes.Cancel(r.Data().GetProperty("id").GetGuid()), new { reason = "cleanup" }, token);
    }

    [Fact]
    public async Task Create_NoItems_Returns400()
    {
        await Prepare();
        (await HandoverHelper.PostWithAsync(d => { d["items"] = new object[0]; return Task.CompletedTask; })).ShouldBe(400);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("10.555")]
    public async Task Create_InvalidWeight_Returns400(string weight)
    {
        await Prepare();
        var cat = await RecyclerHelper.GetCategoryAsync("plastic");
        var raw = $@"{{""recyclerId"":""{await HandoverHelper.RecyclerIdAsync()}"",
            ""items"":[{{""wasteCategoryId"":""{cat.Id}"",""estimatedWeightKg"":{weight}}}],
            ""photoUrls"":[""{HandoverHelper.PhotoUrl}""],
            ""proposedTimeSlot"":""{HandoverHelper.Iso(DateTimeOffset.UtcNow.AddHours(26))}"",
            ""pickupAddress"":""12 Test""}}";
        (await ApiJson.RawAsync(HttpMethod.Post, HandoverRoutes.Base, raw, await Citizen())).ShouldBe(400);
    }

    [Fact]
    public async Task Create_DuplicateCategory_Returns400()
    {
        await Prepare();
        (await ApiJson.PostAsync(HandoverRoutes.Base,
            await HandoverHelper.PayloadAsync(categoryCodes: new[] { "plastic", "plastic" }), await Citizen())).ShouldBe(400);
    }

    [Fact]
    public async Task Create_CategoryRecyclerDoesNotAccept_Returns400_BR_HND_01()
    {
        await Prepare();
        // "cardboard" không có dòng giá trong baseline -> Recycler không nhận
        (await ApiJson.PostAsync(HandoverRoutes.Base,
            await HandoverHelper.PayloadAsync(categoryCodes: new[] { RecyclerHelper.ReservedCategoryCode }), await Citizen()))
            .ShouldFailWith(400);
    }

    [Fact]
    public async Task Create_BelowMinimumQuantity_Returns400()
    {
        await Prepare();
        // plastic min = 2 kg
        (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(weight: 0.5m), await Citizen())).ShouldFailWith(400);
    }

    [Fact]
    public async Task Create_SlotLessThan2HoursAhead_Returns400()
    {
        await Prepare();
        (await ApiJson.PostAsync(HandoverRoutes.Base,
            await HandoverHelper.PayloadAsync(slot: DateTimeOffset.UtcNow.AddMinutes(30)), await Citizen())).ShouldFailWith(400);
    }

    [Fact]
    public async Task Create_SlotInThePast_Returns400()
    {
        await Prepare();
        (await ApiJson.PostAsync(HandoverRoutes.Base,
            await HandoverHelper.PayloadAsync(slot: DateTimeOffset.UtcNow.AddHours(-5)), await Citizen())).ShouldFailWith(400);
    }

    [Fact]
    public async Task Create_SlotMoreThan7DaysAhead_Returns400()
    {
        await Prepare();
        (await ApiJson.PostAsync(HandoverRoutes.Base,
            await HandoverHelper.PayloadAsync(slot: DateTimeOffset.UtcNow.AddDays(9)), await Citizen())).ShouldFailWith(400);
    }

    [Fact]
    public async Task Create_NoPhotos_Returns400()
    {
        await Prepare();
        (await ApiJson.PostAsync(HandoverRoutes.Base,
            await HandoverHelper.PayloadAsync(photos: new string[0]), await Citizen())).ShouldBe(400);
    }

    [Fact]
    public async Task Create_MoreThan5Photos_Returns400()
    {
        await Prepare();
        var photos = Enumerable.Range(1, 6).Select(i => $"https://example.com/p{i}.jpg").ToArray();
        (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(photos: photos), await Citizen())).ShouldBe(400);
    }

    [Fact]
    public async Task Create_InvalidPhotoUrl_Returns400()
    {
        await Prepare();
        (await ApiJson.PostAsync(HandoverRoutes.Base,
            await HandoverHelper.PayloadAsync(photos: new[] { "not-a-url" }), await Citizen())).ShouldFailWith(400);
    }

    [Fact]
    public async Task Create_MissingAddress_Returns400()
    {
        await Prepare();
        (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(address: ""), await Citizen())).ShouldBe(400);
    }

    [Fact]
    public async Task Create_NoteLongerThan200_Returns400()
    {
        await Prepare();
        (await ApiJson.PostAsync(HandoverRoutes.Base,
            await HandoverHelper.PayloadAsync(note: new string('a', 201)), await Citizen())).ShouldBe(400);
    }

    [Fact]
    public async Task Create_MissingRecycler_Returns400()
    {
        await Prepare();
        (await HandoverHelper.PostWithAsync(d => { d.Remove("recyclerId"); return Task.CompletedTask; })).ShouldBe(400);
    }

    [Fact]
    public async Task Create_UnknownRecycler_Returns404()
    {
        await Prepare();
        (await ApiJson.PostAsync(HandoverRoutes.Base,
            await HandoverHelper.PayloadAsync(recyclerId: Guid.NewGuid()), await Citizen())).ShouldBe(404);
    }

    [Fact]
    public async Task Create_RecyclerPendingVerification_Returns400()
    {
        await Prepare();
        // Recycler mới đăng ký chưa được duyệt: lấy id từ hồ sơ của chính họ
        var profile = (await Api.GetAsync(RecyclerRoutes.Profile, await RecyclerHelper.PendingRecyclerToken())).ShouldBe(200);
        var id = Guid.Parse(profile.Data().Str("recyclerId")!);
        (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(recyclerId: id), await Citizen())).ShouldFailWith(400);
    }

    [Fact]
    public async Task Create_RecyclerOffline_Returns400_AndWorksAgainWhenOnline()
    {
        await Prepare();
        var rc = await Collector();
        try
        {
            (await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = false }, rc)).ShouldBe(200);
            (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(), await Citizen())).ShouldFailWith(400);
        }
        finally { await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = true }, rc); }
    }

    [Fact]
    public async Task Create_FourthActiveRequest_Returns400()
    {
        await Prepare();
        var token = await Citizen();
        var ids = new List<Guid>();
        try
        {
            for (var i = 0; i < 3; i++) ids.Add(await HandoverHelper.CreateAsync());
            (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(), token)).ShouldFailWith(400);
        }
        finally
        {
            foreach (var id in ids) await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, token);
        }
    }

    [Fact]
    public async Task Create_ClosedDay_Returns400()
    {
        await Prepare();
        var rc = await Collector();
        var slot = DateTimeOffset.UtcNow.AddHours(30);
        var localDay = (int)(slot.UtcDateTime + TimeSpan.FromHours(7)).DayOfWeek;
        try
        {
            var days = Enumerable.Range(0, 7).Select(d => new
            {
                dayOfWeek = d, isClosed = d == localDay,
                openTime = d == localDay ? null : "00:00", closeTime = d == localDay ? null : "23:59"
            }).ToList();
            (await ApiJson.PutAsync(RecyclerRoutes.Schedule, new { days }, rc)).ShouldBe(200);
            (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(slot: slot), await Citizen())).ShouldFailWith(400);
        }
        finally { await HandoverHelper.EnsureRecyclerReadyAsync(); }
    }

    // ---------------- 3. Xem yêu cầu của tôi ----------------
    [Fact]
    public async Task GetMine_ContainsCreatedRequest_AndFilterByStatus()
    {
        await Prepare();
        var token = await Citizen();
        var id = await HandoverHelper.CreateAsync();
        try
        {
            var all = (await Api.GetAsync(HandoverRoutes.Base, token)).ShouldBe(200).Data();
            Assert.Contains(all.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == id);

            var pending = (await Api.GetAsync(HandoverRoutes.Base, token,
                new Dictionary<string, string> { ["status"] = "Pending" })).ShouldBe(200).Data();
            Assert.All(pending.GetProperty("items").EnumerateArray(), i => Assert.Equal("Pending", i.Str("status")));

            var cancelled = (await Api.GetAsync(HandoverRoutes.Base, token,
                new Dictionary<string, string> { ["status"] = "Cancelled" })).ShouldBe(200).Data();
            Assert.DoesNotContain(cancelled.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == id);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, token); }
    }

    [Fact]
    public async Task GetMine_InvalidStatus_Returns400() =>
        (await Api.GetAsync(HandoverRoutes.Base, await Citizen(),
            new Dictionary<string, string> { ["status"] = "Nope" })).ShouldBe(400);

    [Fact]
    public async Task GetMine_Pagination_RespectsPageSize()
    {
        await Prepare();
        var token = await Citizen();
        var ids = new List<Guid> { await HandoverHelper.CreateAsync(), await HandoverHelper.CreateAsync() };
        try
        {
            var d = (await Api.GetAsync(HandoverRoutes.Base, token,
                new Dictionary<string, string> { ["pageSize"] = "1", ["page"] = "1" })).ShouldBe(200).Data();
            Assert.Single(d.GetProperty("items").EnumerateArray());
            Assert.True(d.GetProperty("totalCount").GetInt32() >= 2);
        }
        finally { foreach (var id in ids) await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, token); }
    }

    [Fact]
    public async Task GetById_Own_Returns200_UnknownReturns404()
    {
        await Prepare();
        var token = await Citizen();
        var id = await HandoverHelper.CreateAsync();
        try
        {
            Assert.Equal(id, (await Api.GetAsync(HandoverRoutes.One(id), token)).ShouldBe(200).Data().GetProperty("id").GetGuid());
            (await Api.GetAsync(HandoverRoutes.One(Guid.NewGuid()), token)).ShouldBe(404);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, token); }
    }

    [Fact]
    public async Task GetById_OtherUser_Returns404()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync();
        try
        {
            var other = await HandoverHelper.SecondUserTokenAsync();
            (await Api.GetAsync(HandoverRoutes.One(id), other)).ShouldBe(404);
            (await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "x" }, other)).ShouldBe(404);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    // ---------------- 4. Chỉnh sửa (U-07) ----------------
    [Fact]
    public async Task Update_Pending_Returns200_AndChangesData()
    {
        await Prepare();
        var token = await Citizen();
        var id = await HandoverHelper.CreateAsync();
        try
        {
            var body = await HandoverHelper.PayloadAsync(categoryCodes: new[] { "metal" }, weight: 7.5m, address: "99 Địa chỉ mới");
            var r = (await ApiJson.PutAsync(HandoverRoutes.One(id), body, token)).ShouldBe(200).Data();
            Assert.Equal(7.5m, r.GetProperty("totalEstimatedWeightKg").GetDecimal());
            Assert.Equal("99 Địa chỉ mới", r.Str("pickupAddress"));
            Assert.Equal("metal", r.GetProperty("items")[0].Str("wasteCategoryCode"));
            Assert.Equal("Pending", r.Str("status"));
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, token); }
    }

    [Fact]
    public async Task Update_InvalidData_Returns400()
    {
        await Prepare();
        var token = await Citizen();
        var id = await HandoverHelper.CreateAsync();
        try
        {
            var body = await HandoverHelper.PayloadAsync(slot: DateTimeOffset.UtcNow.AddMinutes(10));
            (await ApiJson.PutAsync(HandoverRoutes.One(id), body, token)).ShouldFailWith(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, token); }
    }

    [Fact]
    public async Task Update_AfterAccepted_Returns400()
    {
        await Prepare();
        var token = await Citizen();
        var id = await HandoverHelper.CreateAsync();
        try
        {
            (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, await Collector())).ShouldBe(200);
            (await ApiJson.PutAsync(HandoverRoutes.One(id), await HandoverHelper.PayloadAsync(), token)).ShouldFailWith(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, token); }
    }

    // ---------------- 5. Hủy bởi người dân (U-07, BR-HND-06) ----------------
    [Fact]
    public async Task Cancel_WithoutReason_Returns400()
    {
        await Prepare();
        var token = await Citizen();
        var id = await HandoverHelper.CreateAsync();
        try { (await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "" }, token)).ShouldBe(400); }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, token); }
    }

    [Fact]
    public async Task Cancel_Pending_Returns200_Cancelled_NotLate()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync();
        var r = (await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "Đổi kế hoạch" }, await Citizen())).ShouldBe(200);
        Assert.Equal("MSG-HND-05", r.MessageCode());
        Assert.Equal("Cancelled", r.Data().Str("status"));
        Assert.Equal("Đổi kế hoạch", r.Data().Str("cancelReason"));
        Assert.Equal("User", r.Data().Str("cancelledByRole"));
        Assert.False(r.Data().GetProperty("isLateCancellation").GetBoolean());
    }

    [Fact]
    public async Task Cancel_Twice_Returns400_BR_HND_02()
    {
        await Prepare();
        var token = await Citizen();
        var id = await HandoverHelper.CreateAsync();
        (await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "lần 1" }, token)).ShouldBe(200);
        (await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "lần 2" }, token)).ShouldFailWith(400);
    }

    [Fact]
    public async Task Cancel_AcceptedWithinLateWindow_IsRecordedAsLate()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync(slot: DateTimeOffset.UtcNow.AddHours(3));
        (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, await Collector())).ShouldBe(200);
        var r = (await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "Bận đột xuất" }, await Citizen())).ShouldBe(200);
        Assert.Equal("MSG-HND-06", r.MessageCode());
        Assert.True(r.Data().GetProperty("isLateCancellation").GetBoolean());
    }

    [Fact]
    public async Task Cancel_AcceptedFarFromSlot_IsNotLate()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync(slot: DateTimeOffset.UtcNow.AddHours(30));
        (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, await Collector())).ShouldBe(200);
        var r = (await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "Đổi ý" }, await Citizen())).ShouldBe(200);
        Assert.False(r.Data().GetProperty("isLateCancellation").GetBoolean());
    }

    // ---------------- 6. Recycler xem / chấp nhận / từ chối / hủy (R-03, R-04) ----------------
    [Fact]
    public async Task Recycler_List_ContainsIncomingRequest_AndMasksContact()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync();
        try
        {
            var rc = await Collector();
            var list = (await Api.GetAsync(HandoverRoutes.RecyclerBase, rc,
                new Dictionary<string, string> { ["status"] = "Pending" })).ShouldBe(200).Data();
            Assert.Contains(list.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == id);

            var d = (await Api.GetAsync(HandoverRoutes.RcOne(id), rc)).ShouldBe(200).Data();
            // BR-HND-08: chưa Accepted -> không lộ địa chỉ chính xác / SĐT
            Assert.False(d.GetProperty("isContactRevealed").GetBoolean());
            Assert.Equal(JsonValueKind.Null, d.GetProperty("pickupAddress").ValueKind);
            Assert.Equal(JsonValueKind.Null, d.GetProperty("requester").GetProperty("phone").ValueKind);
            Assert.False(d.GetProperty("canEdit").GetBoolean());
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Recycler_Accept_Returns200_ConfirmsSlot_AndRevealsContact()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync();
        try
        {
            var r = (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { note = "Sẽ đến đúng giờ" }, await Collector())).ShouldBe(200);
            Assert.Equal("MSG-HND-02", r.MessageCode());
            var d = r.Data();
            Assert.Equal("Accepted", d.Str("status"));
            Assert.NotEqual(JsonValueKind.Null, d.GetProperty("confirmedTimeSlot").ValueKind);
            Assert.True(d.GetProperty("isContactRevealed").GetBoolean());
            Assert.Equal("12 Đường Test, Quận 1, TP.HCM", d.Str("pickupAddress"));
            Assert.Equal("Sẽ đến đúng giờ", d.Str("recyclerNote"));

            // Phía người dân cũng thấy thông tin liên hệ Recycler
            var mine = (await Api.GetAsync(HandoverRoutes.One(id), await Citizen())).ShouldBe(200).Data();
            Assert.True(mine.GetProperty("isContactRevealed").GetBoolean());
            Assert.True(mine.GetProperty("canCancel").GetBoolean());
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Recycler_AcceptTwice_Returns400()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync();
        try
        {
            var rc = await Collector();
            (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, rc)).ShouldBe(200);
            (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, rc)).ShouldFailWith(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Recycler_Reject_WithoutReason_Returns400()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync();
        try { (await ApiJson.PostAsync(HandoverRoutes.Reject(id), new { reason = " " }, await Collector())).ShouldBe(400); }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Recycler_Reject_Returns200_Rejected_ThenCannotAccept()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync();
        var rc = await Collector();
        var r = (await ApiJson.PostAsync(HandoverRoutes.Reject(id), new { reason = "Hết công suất" }, rc)).ShouldBe(200);
        Assert.Equal("Rejected", r.Data().Str("status"));
        Assert.Equal("Hết công suất", r.Data().Str("cancelReason"));
        Assert.False(r.Data().GetProperty("isContactRevealed").GetBoolean());

        (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, rc)).ShouldFailWith(400);  // BR-HND-02: không hoàn trạng thái
        (await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "x" }, await Citizen())).ShouldFailWith(400);

        var mine = (await Api.GetAsync(HandoverRoutes.One(id), await Citizen())).ShouldBe(200).Data();
        Assert.Equal("Rejected", mine.Str("status"));
    }

    [Fact]
    public async Task Recycler_CancelPending_Returns400_OnlyAcceptedCanBeCancelled()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync();
        try { (await ApiJson.PostAsync(HandoverRoutes.RcCancel(id), new { reason = "x" }, await Collector())).ShouldFailWith(400); }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Recycler_CancelAccepted_Returns200_WithReason()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync();
        var rc = await Collector();
        (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, rc)).ShouldBe(200);

        (await ApiJson.PostAsync(HandoverRoutes.RcCancel(id), new { reason = "" }, rc)).ShouldBe(400);

        var r = (await ApiJson.PostAsync(HandoverRoutes.RcCancel(id), new { reason = "Xe hỏng" }, rc)).ShouldBe(200);
        Assert.Equal("Cancelled", r.Data().Str("status"));
        Assert.Equal("Recycler", r.Data().Str("cancelledByRole"));
        Assert.Equal("Xe hỏng", r.Data().Str("cancelReason"));
    }

    [Fact]
    public async Task Recycler_CannotSeeRequestsOfOtherRecyclers_BR_HND_03()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync();
        try
        {
            var other = await RecyclerHelper.PendingRecyclerToken();
            (await Api.GetAsync(HandoverRoutes.RcOne(id), other)).ShouldBe(404);
            (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, other)).ShouldBe(404);
            (await ApiJson.PostAsync(HandoverRoutes.Reject(id), new { reason = "x" }, other)).ShouldBe(404);
            var list = (await Api.GetAsync(HandoverRoutes.RecyclerBase, other)).ShouldBe(200).Data();
            Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == id);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Recycler_UnknownRequest_Returns404()
    {
        var rc = await Collector();
        (await Api.GetAsync(HandoverRoutes.RcOne(Guid.NewGuid()), rc)).ShouldBe(404);
        (await ApiJson.PostAsync(HandoverRoutes.Accept(Guid.NewGuid()), new { }, rc)).ShouldBe(404);
    }

    [Fact]
    public async Task Recycler_List_InvalidStatus_Returns400() =>
        (await Api.GetAsync(HandoverRoutes.RecyclerBase, await Collector(),
            new Dictionary<string, string> { ["status"] = "Nope" })).ShouldBe(400);
}
