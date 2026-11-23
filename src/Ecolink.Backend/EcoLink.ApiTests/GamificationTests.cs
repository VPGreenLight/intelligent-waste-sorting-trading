using System.Text.Json;
using Xunit.Sdk;

namespace EcoLink.ApiTests;

public static class GamRoutes
{
    public const string Wallet = "/api/v1/points/wallet";
    public const string Transactions = "/api/v1/points/transactions";
    public const string Gifts = "/api/v1/gifts";
    public const string Redemptions = "/api/v1/gift-redemptions";
    public const string CheckIn = "/api/v1/check-in";
    public const string Tree = "/api/v1/virtual-tree";
    public const string Care = "/api/v1/virtual-tree/care";
    public const string Water = "/api/v1/virtual-tree/water";
    public const string Harvest = "/api/v1/virtual-tree/harvest";
    public const string EcoFacts = "/api/v1/eco-facts";
    public const string StaffGifts = "/api/v1/staff/gifts";
    public const string StaffFacts = "/api/v1/staff/eco-facts";
    public const string Config = "/api/v1/admin/gamification/config";
    public const string ConfigReset = "/api/v1/admin/gamification/config/reset";

    public static string Gift(Guid id) => $"{Gifts}/{id}";
    public static string Redeem(Guid id) => $"{Gifts}/{id}/redeem";
    public static string Redemption(Guid id) => $"{Redemptions}/{id}";
    public static string EcoFact(Guid id) => $"{EcoFacts}/{id}";
    public static string StaffGift(Guid id) => $"{StaffGifts}/{id}";
    public static string Restock(Guid id) => $"{StaffGifts}/{id}/restock";
    public static string GiftStatus(Guid id) => $"{StaffGifts}/{id}/status";
    public static string GiftRedemptions(Guid id) => $"{StaffGifts}/{id}/redemptions";
    public static string Fulfill(Guid id) => $"/api/v1/staff/gift-redemptions/{id}/fulfill";
    public static string StaffFact(Guid id) => $"{StaffFacts}/{id}";
    public static string FactStatus(Guid id) => $"{StaffFacts}/{id}/status";
}

/// <summary>
/// Hàm tiện ích cho test Gamification &amp; Rewards. Cần: tài khoản Staff (xem seed_staff.sql của module Blog; mặc định
/// blogstaff / citizen123, đổi bằng ECOLINK_STAFF_USER / ECOLINK_STAFF_PASSWORD) và đã nối cộng điểm vào biên nhận (GAMIFICATION_README.md).
/// Test dùng người dùng đăng ký mới cho mỗi kịch bản cần ví điểm "sạch".
/// </summary>
public static class GamHelper
{
    private static readonly Lazy<Task<string>> StaffTokenLazy = new(async () =>
    {
        var user = Environment.GetEnvironmentVariable("ECOLINK_STAFF_USER") ?? "staff";
        var password = Environment.GetEnvironmentVariable("ECOLINK_STAFF_PASSWORD") ?? "staff123";
        var r = await Api.PostJsonAsync(Routes.Login, new { usernameOrEmail = user, password });
        if (r.Status != 200)
            throw new XunitException($"Không đăng nhập được tài khoản Staff '{user}' ({r.Status}). {r.Raw}");
        return Api.ExtractToken(r.Body) ?? throw new XunitException("Đăng nhập Staff không trả về token.");
    });

    public static Task<string> Staff() => StaffTokenLazy.Value;
    public static Task<string> Admin() => Tokens.GetAsync("admin");
    public static Task<string> Citizen() => Tokens.GetAsync("citizen");
    public static Task<string> Collector() => Tokens.GetAsync("recycler");

    /// <summary>Đăng ký một người dùng mới và đăng nhập: ví điểm / cây / điểm danh đều bắt đầu từ trạng thái sạch.</summary>
    public static async Task<string> FreshUserAsync()
    {
        // KHÔNG dùng TestData.RegisteredUserAsync(): nó là tài khoản dùng chung cả lần chạy, ví điểm sẽ cộng dồn giữa các test.
        var payload = TestData.ValidRegister();
        var reg = await Api.PostJsonAsync(Routes.Register, payload);
        if (reg.Status is not (200 or 201))
            throw new XunitException($"Không đăng ký được người dùng mới: {reg.Status} {reg.Raw}");
        var login = (await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = payload.Str("username"), password = payload.Str("password") })).ShouldBe(200);
        return Api.ExtractToken(login.Body) ?? throw new XunitException("Đăng nhập người dùng mới không trả về token.");
    }

    public static async Task ResetConfigAsync() =>
        (await ApiJson.PostAsync(GamRoutes.ConfigReset, null, await Admin())).ShouldBe(200);

    public static async Task<JsonElement> GetConfigAsync() =>
        (await Api.GetAsync(GamRoutes.Config, await Admin())).ShouldBe(200).Data();

    /// <summary>Lấy cấu hình hiện tại, chỉnh bằng <paramref name="mutate"/>, rồi PUT lại.</summary>
    public static async Task<ApiResult> PutConfigAsync(Action<Dictionary<string, object?>> mutate)
    {
        var current = await GetConfigAsync();
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(current.GetRawText())!;
        dict.Remove("updatedAt");
        mutate(dict);
        return await ApiJson.PutAsync(GamRoutes.Config, dict, await Admin());
    }

    public static object Levels(params (int Level, string Title, long Points)[] levels) =>
        levels.Select(l => new { level = l.Level, title = l.Title, requiredPoints = l.Points }).ToList();

    public static async Task<long> BalanceAsync(string token) =>
        (await Api.GetAsync(GamRoutes.Wallet, token)).ShouldBe(200).Data().GetProperty("balance").GetInt64();

    /// <summary>Hoàn tất một bàn giao do <paramref name="userToken"/> gửi, đủ để nhận khoảng <paramref name="points"/> điểm (10 điểm/kg).</summary>
    public static async Task<long> EarnAsync(string userToken, long points)
    {
        var before = await BalanceAsync(userToken);
        var kg = (decimal)Math.Ceiling(points / 10.0);
        await HandoverHelper.EnsureRecyclerReadyAsync();

        var created = (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(weight: kg), userToken)).ShouldBe(201);
        var id = created.Data().GetProperty("id").GetGuid();
        (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, await Collector())).ShouldBe(200);
        var items = await ReceiptHelper.ItemsAsync(id);
        (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), ReceiptHelper.Body(items, new[] { kg }), await Collector())).ShouldBe(201);
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, userToken)).ShouldBe(200);

        var gained = await BalanceAsync(userToken) - before;
        if (gained <= 0)
            throw new XunitException("Hoàn tất bàn giao nhưng ví điểm không tăng. Hãy nối AwardHandoverPointsAsync vào HandoverReceiptService (xem GAMIFICATION_README.md).");
        return gained;
    }

    public static async Task<Guid> CreateGiftAsync(string type = "Voucher", long points = 10, int stock = 5, string? name = null,
        string? image = null)
    {
        var r = (await ApiJson.PostAsync(GamRoutes.StaffGifts, new
        {
            name = name ?? $"Quà test {TestData.Unique(10)}",
            type,
            description = "Quà dùng cho test tự động.",
            imageUrl = image,
            pointsRequired = points,
            stockQuantity = stock
        }, await Staff())).ShouldBe(201);
        return r.Data().GetProperty("id").GetGuid();
    }

    public static async Task<JsonElement> StaffGiftAsync(Guid id) =>
        (await Api.GetAsync(GamRoutes.StaffGift(id), await Staff())).ShouldBe(200).Data();

    public static async Task<Guid> CreateFactAsync(int level = 1, string? title = null, string? content = null)
    {
        var r = (await ApiJson.PostAsync(GamRoutes.StaffFacts, new
        {
            title = title ?? $"Sự thật xanh {TestData.Unique(10)}",
            content = content ?? "Một sự thật môi trường ngắn gọn.",
            unlockTreeLevel = level
        }, await Staff())).ShouldBe(201);
        return r.Data().GetProperty("id").GetGuid();
    }

    public static ApiResult GamShouldBeIn(this ApiResult r, params int[] statuses)
    {
        if (!statuses.Contains(r.Status))
            throw new XunitException($"Mong đợi một trong [{string.Join(", ", statuses)}] nhưng nhận {r.Status}: {r.Raw}");
        return r;
    }

    public static string GS(this JsonElement e, string name) => e.GetProperty(name).GetString()!;
    public static long GL(this JsonElement e, string name) => e.GetProperty(name).GetInt64();

    public static async Task<List<JsonElement>> AllTransactionsAsync(string token)
    {
        var all = new List<JsonElement>();
        for (var page = 1; page < 50; page++)
        {
            var d = (await Api.GetAsync(GamRoutes.Transactions, token,
                new Dictionary<string, string> { ["page"] = page.ToString(), ["pageSize"] = "50" })).ShouldBe(200).Data();
            var items = d.GetProperty("items").EnumerateArray().ToList();
            all.AddRange(items);
            if (all.Count >= d.GetProperty("totalCount").GetInt32() || items.Count == 0) break;
        }
        return all;
    }
}

// =====================================================================================
public class GamAccessTests
{
    [Fact] public async Task Wallet_WithoutToken_401() => (await Api.GetAsync(GamRoutes.Wallet)).ShouldBe(401);
    [Fact] public async Task Wallet_AsStaff_403() => (await Api.GetAsync(GamRoutes.Wallet, await GamHelper.Staff())).ShouldBe(403);
    [Fact] public async Task Wallet_AsAdmin_403() => (await Api.GetAsync(GamRoutes.Wallet, await GamHelper.Admin())).ShouldBe(403);
    [Fact] public async Task Wallet_AsCitizen_200() => (await Api.GetAsync(GamRoutes.Wallet, await GamHelper.Citizen())).ShouldBe(200);
    [Fact] public async Task Wallet_AsRecycler_200() => (await Api.GetAsync(GamRoutes.Wallet, await GamHelper.Collector())).ShouldBe(200);

    [Fact] public async Task GiftStore_WithoutToken_401() => (await Api.GetAsync(GamRoutes.Gifts)).ShouldBe(401);
    [Fact] public async Task GiftStore_AsStaff_403() => (await Api.GetAsync(GamRoutes.Gifts, await GamHelper.Staff())).ShouldBe(403);
    [Fact] public async Task GiftStore_AsCitizen_200() => (await Api.GetAsync(GamRoutes.Gifts, await GamHelper.Citizen())).ShouldBe(200);
    [Fact] public async Task GiftStore_AsRecycler_200() => (await Api.GetAsync(GamRoutes.Gifts, await GamHelper.Collector())).ShouldBe(200);

    // Cây ảo, Eco Fact: chỉ role User (BR-GAM-08); Daily Check-in: Citizen & Recycler (STT 078-079)
    public static TheoryData<string> UserOnlyPaths => new() { GamRoutes.Tree, GamRoutes.EcoFacts };

    [Theory, MemberData(nameof(UserOnlyPaths))]
    public async Task UserOnly_AsRecycler_403(string path) => (await Api.GetAsync(path, await GamHelper.Collector())).ShouldBe(403);

    [Theory, MemberData(nameof(UserOnlyPaths))]
    public async Task UserOnly_AsStaff_403(string path) => (await Api.GetAsync(path, await GamHelper.Staff())).ShouldBe(403);

    [Theory, MemberData(nameof(UserOnlyPaths))]
    public async Task UserOnly_WithoutToken_401(string path) => (await Api.GetAsync(path)).ShouldBe(401);

    [Fact] public async Task Checkin_Status_AsRecycler_200() => (await Api.GetAsync(GamRoutes.CheckIn, await GamHelper.Collector())).ShouldBe(200);
    [Fact] public async Task Checkin_AsStaff_403() => (await Api.GetAsync(GamRoutes.CheckIn, await GamHelper.Staff())).ShouldBe(403);
    [Fact] public async Task Checkin_WithoutToken_401() => (await Api.GetAsync(GamRoutes.CheckIn)).ShouldBe(401);
    [Fact] public async Task Water_AsRecycler_403() => (await ApiJson.PostAsync(GamRoutes.Water, null, await GamHelper.Collector())).ShouldBe(403);

    // Staff / Admin (BR-ADM-02)
    public static TheoryData<string> StaffPaths => new() { GamRoutes.StaffGifts, GamRoutes.StaffFacts };

    [Theory, MemberData(nameof(StaffPaths))]
    public async Task StaffEndpoints_AsCitizen_403(string path) => (await Api.GetAsync(path, await GamHelper.Citizen())).ShouldBe(403);

    [Theory, MemberData(nameof(StaffPaths))]
    public async Task StaffEndpoints_AsAdmin_200(string path) => (await Api.GetAsync(path, await GamHelper.Admin())).ShouldBe(200);

    [Theory, MemberData(nameof(StaffPaths))]
    public async Task StaffEndpoints_WithoutToken_401(string path) => (await Api.GetAsync(path)).ShouldBe(401);

    [Fact] public async Task StaffEndpoints_AsStaff_200() => (await Api.GetAsync(GamRoutes.StaffGifts, await GamHelper.Staff())).ShouldBe(200);

    // Admin cấu hình
    [Fact] public async Task Config_WithoutToken_401() => (await Api.GetAsync(GamRoutes.Config)).ShouldBe(401);
    [Fact] public async Task Config_AsStaff_403() => (await Api.GetAsync(GamRoutes.Config, await GamHelper.Staff())).ShouldBe(403);
    [Fact] public async Task Config_AsCitizen_403() => (await Api.GetAsync(GamRoutes.Config, await GamHelper.Citizen())).ShouldBe(403);
    [Fact] public async Task Config_Put_AsStaff_403() => (await ApiJson.PutAsync(GamRoutes.Config, new { }, await GamHelper.Staff())).ShouldBe(403);
    [Fact] public async Task Config_Reset_AsStaff_403() => (await ApiJson.PostAsync(GamRoutes.ConfigReset, null, await GamHelper.Staff())).ShouldBe(403);
    [Fact] public async Task Config_AsAdmin_200() => (await Api.GetAsync(GamRoutes.Config, await GamHelper.Admin())).ShouldBe(200);
}

// =====================================================================================
public class GamWalletTests
{
    [Fact]
    public async Task NewUser_Wallet_StartsAtZero()
    {
        var token = await GamHelper.FreshUserAsync();
        var d = (await Api.GetAsync(GamRoutes.Wallet, token)).ShouldBe(200).Data();
        Assert.Equal(0, d.GL("balance"));
        Assert.Equal(0, d.GL("totalEarned"));
        Assert.Equal(0, d.GL("totalSpent"));
    }

    [Fact]
    public async Task Transactions_NewUser_Empty()
    {
        var token = await GamHelper.FreshUserAsync();
        var d = (await Api.GetAsync(GamRoutes.Transactions, token)).ShouldBe(200).Data();
        Assert.Equal(0, d.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Ledger_SumEqualsBalance_AfterEarnAndRedeem()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 200);
        var gift = await GamHelper.CreateGiftAsync("Voucher", points: 30, stock: 3);
        (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, token)).ShouldBe(201);

        var all = await GamHelper.AllTransactionsAsync(token);
        var wallet = (await Api.GetAsync(GamRoutes.Wallet, token)).ShouldBe(200).Data();
        Assert.Equal(wallet.GL("balance"), all.Sum(t => t.GL("points")));
        Assert.Equal(wallet.GL("totalEarned") - wallet.GL("totalSpent"), wallet.GL("balance"));
        Assert.Equal(30, wallet.GL("totalSpent"));
    }

    [Fact]
    public async Task Transactions_FilterByActionType()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 100);
        (await ApiJson.PostAsync(GamRoutes.CheckIn, null, token)).ShouldBe(200);

        var d = (await Api.GetAsync(GamRoutes.Transactions, token,
            new Dictionary<string, string> { ["actionType"] = "Daily_Checkin" })).ShouldBe(200).Data();
        Assert.Equal(1, d.GetProperty("totalCount").GetInt32());
        Assert.Equal("Daily_Checkin", d.GetProperty("items")[0].GS("actionType"));

        var d2 = (await Api.GetAsync(GamRoutes.Transactions, token,
            new Dictionary<string, string> { ["actionType"] = "HandoverReward" })).ShouldBe(200).Data();
        Assert.Equal(1, d2.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Transactions_InvalidActionType_400() =>
        (await Api.GetAsync(GamRoutes.Transactions, await GamHelper.Citizen(),
            new Dictionary<string, string> { ["actionType"] = "Nonsense" })).ShouldBe(400);

    [Fact]
    public async Task Transactions_DateRangeInFuture_ReturnsNothing()
    {
        var token = await GamHelper.FreshUserAsync();
        await ApiJson.PostAsync(GamRoutes.CheckIn, null, token);
        var d = (await Api.GetAsync(GamRoutes.Transactions, token,
            new Dictionary<string, string> { ["from"] = DateTime.UtcNow.AddDays(2).ToString("o") })).ShouldBe(200).Data();
        Assert.Equal(0, d.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Transactions_PageSizeIsClamped()
    {
        var d = (await Api.GetAsync(GamRoutes.Transactions, await GamHelper.Citizen(),
            new Dictionary<string, string> { ["pageSize"] = "9999", ["page"] = "0" })).ShouldBe(200).Data();
        Assert.True(d.GetProperty("pageSize").GetInt32() <= 50);
        Assert.Equal(1, d.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task Transactions_AreOrderedNewestFirst()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 50);
        await ApiJson.PostAsync(GamRoutes.CheckIn, null, token);
        var items = (await Api.GetAsync(GamRoutes.Transactions, token)).ShouldBe(200).Data().GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("createdAt").GetDateTime()).ToList();
        Assert.Equal(items.OrderByDescending(x => x).ToList(), items);
    }
}

// =====================================================================================
public class GamHandoverPointsTests
{
    [Fact]
    public async Task CompletedHandover_CreditsBothParties_OncePerRole()
    {
        await GamHelper.ResetConfigAsync();
        var user = await GamHelper.FreshUserAsync();
        var collector = await GamHelper.Collector();
        var collectorBefore = await GamHelper.BalanceAsync(collector);

        var gained = await GamHelper.EarnAsync(user, 100);       // 10 kg x 10 điểm/kg
        Assert.Equal(100, gained);
        Assert.Equal(collectorBefore + 100, await GamHelper.BalanceAsync(collector));

        var tx = await GamHelper.AllTransactionsAsync(user);
        var rewards = tx.Where(t => t.GS("actionType") == "Handover_Reward").ToList();
        Assert.Single(rewards);
        Assert.Equal("HandoverReceipt", rewards[0].GS("sourceType"));
        Assert.Equal(100, rewards[0].GL("points"));
    }

    [Fact]
    public async Task ReceiptPointsAwarded_MatchesCreditedPoints()
    {
        await GamHelper.ResetConfigAsync();
        var user = await GamHelper.FreshUserAsync();
        await HandoverHelper.EnsureRecyclerReadyAsync();
        var id = (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(weight: 12m), user)).ShouldBe(201)
            .Data().GetProperty("id").GetGuid();
        (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, await GamHelper.Collector())).ShouldBe(200);
        var items = await ReceiptHelper.ItemsAsync(id);
        (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), ReceiptHelper.Body(items, new[] { 12m }), await GamHelper.Collector())).ShouldBe(201);
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, user)).ShouldBe(200);

        var receipt = (await Api.GetAsync(HandoverRoutes.Receipt(id), user)).ShouldBe(200).Data();
        Assert.Equal(120, receipt.GL("points"));
        Assert.Equal(120, await GamHelper.BalanceAsync(user));
    }

    [Fact]
    public async Task ConfirmTwice_DoesNotCreditTwice()
    {
        await GamHelper.ResetConfigAsync();
        var user = await GamHelper.FreshUserAsync();
        await HandoverHelper.EnsureRecyclerReadyAsync();
        var id = (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(weight: 5m), user)).ShouldBe(201)
            .Data().GetProperty("id").GetGuid();
        (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, await GamHelper.Collector())).ShouldBe(200);
        var items = await ReceiptHelper.ItemsAsync(id);
        (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), ReceiptHelper.Body(items, new[] { 5m }), await GamHelper.Collector())).ShouldBe(201);

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, user)));
        Assert.Contains(results, r => r.Status == 200);
        Assert.Equal(50, await GamHelper.BalanceAsync(user));
    }

    [Fact]
    public async Task RejectedHandover_AwardsNoPoints()
    {
        var user = await GamHelper.FreshUserAsync();
        await HandoverHelper.EnsureRecyclerReadyAsync();
        var id = (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(weight: 5m), user)).ShouldBe(201)
            .Data().GetProperty("id").GetGuid();
        (await ApiJson.PostAsync(HandoverRoutes.Reject(id), new { reason = "Không đủ nhân lực" }, await GamHelper.Collector()))
            .GamShouldBeIn(200, 400);
        Assert.Equal(0, await GamHelper.BalanceAsync(user));
    }

    [Fact]
    public async Task CustomPointRate_ChangesPointsPerKg_ThenResets()
    {
        await GamHelper.ResetConfigAsync();
        try
        {
            (await GamHelper.PutConfigAsync(c => c["pointRates"] = new[] { new { categoryCode = "plastic", pointsPerKg = 25L } })).ShouldBe(200);
            var user = await GamHelper.FreshUserAsync();
            await HandoverHelper.EnsureRecyclerReadyAsync();
            var id = (await ApiJson.PostAsync(HandoverRoutes.Base, await HandoverHelper.PayloadAsync(weight: 4m), user)).ShouldBe(201)
                .Data().GetProperty("id").GetGuid();
            (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, await GamHelper.Collector())).ShouldBe(200);
            var items = await ReceiptHelper.ItemsAsync(id);
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), ReceiptHelper.Body(items, new[] { 4m }), await GamHelper.Collector())).ShouldBe(201);
            (await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, user)).ShouldBe(200);
            Assert.Equal(100, await GamHelper.BalanceAsync(user));     // 4 kg nhựa x 25
        }
        finally { await GamHelper.ResetConfigAsync(); }
    }
}

// =====================================================================================
public class GamCheckinTests
{
    [Fact]
    public async Task Status_NewUser_Defaults()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        var d = (await Api.GetAsync(GamRoutes.CheckIn, token)).ShouldBe(200).Data();
        Assert.False(d.GetProperty("checkedInToday").GetBoolean());
        Assert.Equal(0, d.GetProperty("currentStreak").GetInt32());
        Assert.Equal(1, d.GetProperty("nextStreak").GetInt32());
        Assert.False(d.GetProperty("streakWillReset").GetBoolean());
        Assert.Equal(5, d.GL("todayRewardPreview"));
        Assert.Equal(50, d.GL("dailyCapRemaining"));
        Assert.Equal(0, d.GL("walletBalance"));
    }

    [Fact]
    public async Task Status_Calendar_HasSevenDaysEndingToday()
    {
        var token = await GamHelper.FreshUserAsync();
        var cal = (await Api.GetAsync(GamRoutes.CheckIn, token)).ShouldBe(200).Data().GetProperty("calendar").EnumerateArray().ToList();
        Assert.Equal(7, cal.Count);
        var dates = cal.Select(c => DateOnly.Parse(c.GS("date"))).ToList();
        Assert.Equal(dates.OrderBy(x => x).ToList(), dates);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)), dates[^1]);
    }

    [Fact]
    public async Task CheckIn_FirstTime_AwardsBasePoints()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        var r = (await ApiJson.PostAsync(GamRoutes.CheckIn, null, token)).ShouldBe(200);
        Assert.Equal("MSG-GAM-08", r.MessageCode());
        var d = r.Data();
        Assert.Equal(5, d.GL("pointsAwarded"));
        Assert.Equal(1, d.GetProperty("streakCount").GetInt32());
        Assert.Equal(5, d.GL("walletBalance"));
        Assert.False(d.GetProperty("limitedByDailyCap").GetBoolean());
    }

    [Fact]
    public async Task CheckIn_Twice_SecondReturns400_AndBalanceUnchanged()
    {
        var token = await GamHelper.FreshUserAsync();
        (await ApiJson.PostAsync(GamRoutes.CheckIn, null, token)).ShouldBe(200);
        (await ApiJson.PostAsync(GamRoutes.CheckIn, null, token)).ShouldBe(400);
        Assert.Equal(5, await GamHelper.BalanceAsync(token));
    }

    [Fact]
    public async Task Status_AfterCheckIn_ShowsCheckedInToday()
    {
        var token = await GamHelper.FreshUserAsync();
        await ApiJson.PostAsync(GamRoutes.CheckIn, null, token);
        var d = (await Api.GetAsync(GamRoutes.CheckIn, token)).ShouldBe(200).Data();
        Assert.True(d.GetProperty("checkedInToday").GetBoolean());
        Assert.Equal(1, d.GetProperty("currentStreak").GetInt32());
        Assert.Equal(1, d.GetProperty("nextStreak").GetInt32());
        Assert.Equal(0, d.GL("todayRewardPreview"));
        Assert.Equal(45, d.GL("dailyCapRemaining"));
        var last = d.GetProperty("calendar").EnumerateArray().Last();
        Assert.True(last.GetProperty("checkedIn").GetBoolean());
        Assert.Equal(5, last.GL("pointsAwarded"));
    }

    [Fact]
    public async Task CheckIn_CreatesDailyCheckinLedgerEntry()
    {
        var token = await GamHelper.FreshUserAsync();
        await ApiJson.PostAsync(GamRoutes.CheckIn, null, token);
        var tx = (await GamHelper.AllTransactionsAsync(token)).Single();
        Assert.Equal("Daily_Checkin", tx.GS("actionType"));
        Assert.Equal(5, tx.GL("points"));
    }

    [Fact]
    public async Task CheckIn_ConcurrentRequests_OnlyOneWins()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => ApiJson.PostAsync(GamRoutes.CheckIn, null, token)));
        Assert.Equal(1, results.Count(r => r.Status == 200));
        Assert.Equal(5, results.Count(r => r.Status == 400));
        Assert.Equal(5, await GamHelper.BalanceAsync(token));
        Assert.Single(await GamHelper.AllTransactionsAsync(token));
    }

    [Fact]
    public async Task CheckIn_UsesConfiguredBasePoints()
    {
        try
        {
            (await GamHelper.PutConfigAsync(c => c["checkinBasePoints"] = 8L)).ShouldBe(200);
            var token = await GamHelper.FreshUserAsync();
            Assert.Equal(8, (await ApiJson.PostAsync(GamRoutes.CheckIn, null, token)).ShouldBe(200).Data().GL("pointsAwarded"));
        }
        finally { await GamHelper.ResetConfigAsync(); }
    }

    [Fact]
    public async Task CheckIn_DailyCapSmallerThanReward_TruncatesPoints()
    {
        try
        {
            (await GamHelper.PutConfigAsync(c => c["dailyNonTransactionCap"] = 3L)).ShouldBe(200);
            var token = await GamHelper.FreshUserAsync();
            var d = (await ApiJson.PostAsync(GamRoutes.CheckIn, null, token)).ShouldBe(200).Data();
            Assert.Equal(3, d.GL("pointsAwarded"));
            Assert.True(d.GetProperty("limitedByDailyCap").GetBoolean());
        }
        finally { await GamHelper.ResetConfigAsync(); }
    }

    [Fact]
    public async Task CheckIn_DailyCapZero_Returns400WithMsgGam05()
    {
        try
        {
            (await GamHelper.PutConfigAsync(c => c["dailyNonTransactionCap"] = 0L)).ShouldBe(200);
            var token = await GamHelper.FreshUserAsync();
            (await ApiJson.PostAsync(GamRoutes.CheckIn, null, token)).ShouldFailWith(400, "MSG-GAM-05");
            Assert.Equal(0, await GamHelper.BalanceAsync(token));
            Assert.False((await Api.GetAsync(GamRoutes.CheckIn, token)).ShouldBe(200).Data().GetProperty("checkedInToday").GetBoolean());
        }
        finally { await GamHelper.ResetConfigAsync(); }
    }
}

// =====================================================================================
public class GamConfigTests
{
    [Fact]
    public async Task Get_AfterReset_ReturnsDefaults()
    {
        await GamHelper.ResetConfigAsync();
        var d = await GamHelper.GetConfigAsync();
        Assert.Equal(50, d.GL("dailyNonTransactionCap"));
        Assert.Equal(5, d.GL("checkinBasePoints"));
        Assert.Equal(1, d.GL("streakBonusIncrement"));
        Assert.Equal(20, d.GL("streakBonusCap"));
        Assert.Equal(3, d.GetProperty("dailyTreeCareLimit").GetInt32());
        Assert.Equal(30, d.GetProperty("voucherValidityDays").GetInt32());
        Assert.Equal(10, d.GL("defaultPointsPerKg"));
        Assert.Equal(0, d.GetProperty("pointRates").GetArrayLength());
        var levels = d.GetProperty("treeLevels").EnumerateArray().ToList();
        Assert.Equal(4, levels.Count);
        Assert.Equal(new long[] { 0, 100, 300, 700 }, levels.Select(l => l.GL("requiredPoints")).ToArray());
    }

    [Fact]
    public async Task Put_ValidChange_IsPersisted_ThenResetRestores()
    {
        try
        {
            (await GamHelper.PutConfigAsync(c =>
            {
                c["dailyNonTransactionCap"] = 80L;
                c["dailyTreeCareLimit"] = 5;
                c["pointRates"] = new[] { new { categoryCode = "plastic", pointsPerKg = 12L }, new { categoryCode = "metal", pointsPerKg = 30L } };
            })).ShouldBe(200);
            var d = await GamHelper.GetConfigAsync();
            Assert.Equal(80, d.GL("dailyNonTransactionCap"));
            Assert.Equal(5, d.GetProperty("dailyTreeCareLimit").GetInt32());
            Assert.Equal(2, d.GetProperty("pointRates").GetArrayLength());

            (await ApiJson.PostAsync(GamRoutes.ConfigReset, null, await GamHelper.Admin())).ShouldBe(200);
            var r = await GamHelper.GetConfigAsync();
            Assert.Equal(50, r.GL("dailyNonTransactionCap"));
            Assert.Equal(0, r.GetProperty("pointRates").GetArrayLength());
        }
        finally { await GamHelper.ResetConfigAsync(); }
    }

    [Fact]
    public async Task Put_CustomTreeLevels_AreApplied()
    {
        try
        {
            (await GamHelper.PutConfigAsync(c => c["treeLevels"] =
                GamHelper.Levels((1, "Hạt", 0), (2, "Mầm", 20), (3, "Cây", 60), (4, "Cổ thụ", 150), (5, "Rừng", 400)))).ShouldBe(200);
            var d = await GamHelper.GetConfigAsync();
            Assert.Equal(5, d.GetProperty("treeLevels").GetArrayLength());
            var token = await GamHelper.FreshUserAsync();
            Assert.Equal(5, (await Api.GetAsync(GamRoutes.Tree, token)).ShouldBe(200).Data().GetProperty("maxLevel").GetInt32());
        }
        finally { await GamHelper.ResetConfigAsync(); }
    }

    public static TheoryData<string> InvalidChanges => new()
    {
        "negativeCap", "negativeBase", "limitZero", "voucherDaysZero", "negativeRate", "duplicateRate", "unknownCategory",
        "levelsNotIncreasing", "levelsEqualThreshold", "levelOneNotZero", "levelsGap", "levelsEmpty", "tooManyLevels", "emptyTitle",
    };

    [Theory, MemberData(nameof(InvalidChanges))]
    public async Task Put_InvalidValues_Return400_AndKeepConfig(string scenario)
    {
        await GamHelper.ResetConfigAsync();
        var res = await GamHelper.PutConfigAsync(c =>
        {
            switch (scenario)
            {
                case "negativeCap": c["dailyNonTransactionCap"] = -1L; break;
                case "negativeBase": c["checkinBasePoints"] = -5L; break;
                case "limitZero": c["dailyTreeCareLimit"] = 0; break;
                case "voucherDaysZero": c["voucherValidityDays"] = 0; break;
                case "negativeRate": c["pointRates"] = new[] { new { categoryCode = "plastic", pointsPerKg = -1L } }; break;
                case "duplicateRate":
                    c["pointRates"] = new[] { new { categoryCode = "plastic", pointsPerKg = 1L }, new { categoryCode = "PLASTIC", pointsPerKg = 2L } }; break;
                case "unknownCategory": c["pointRates"] = new[] { new { categoryCode = "khong-ton-tai", pointsPerKg = 1L } }; break;
                case "levelsNotIncreasing": c["treeLevels"] = GamHelper.Levels((1, "A", 0), (2, "B", 100), (3, "C", 50)); break;
                case "levelsEqualThreshold": c["treeLevels"] = GamHelper.Levels((1, "A", 0), (2, "B", 100), (3, "C", 100)); break;
                case "levelOneNotZero": c["treeLevels"] = GamHelper.Levels((1, "A", 10), (2, "B", 100)); break;
                case "levelsGap": c["treeLevels"] = GamHelper.Levels((1, "A", 0), (3, "C", 100)); break;
                case "levelsEmpty": c["treeLevels"] = new List<object>(); break;
                case "tooManyLevels":
                    c["treeLevels"] = Enumerable.Range(1, 11).Select(i => new { level = i, title = $"L{i}", requiredPoints = (long)(i - 1) * 10 }).ToList(); break;
                case "emptyTitle": c["treeLevels"] = GamHelper.Levels((1, "A", 0), (2, " ", 100)); break;
            }
        });
        res.ShouldBe(400);
        var d = await GamHelper.GetConfigAsync();
        Assert.Equal(50, d.GL("dailyNonTransactionCap"));
        Assert.Equal(4, d.GetProperty("treeLevels").GetArrayLength());
    }

    [Fact]
    public async Task Put_ThresholdsNotIncreasing_UsesMsgAuth02()
    {
        await GamHelper.ResetConfigAsync();
        (await GamHelper.PutConfigAsync(c => c["treeLevels"] = GamHelper.Levels((1, "A", 0), (2, "B", 100), (3, "C", 50))))
            .ShouldFailWith(400, "MSG-AUTH-02");
    }

    [Fact]
    public async Task Put_RemovingLevelStillUsedByActiveFact_Returns400()
    {
        await GamHelper.ResetConfigAsync();
        var fact = await GamHelper.CreateFactAsync(level: 4);
        try
        {
            (await GamHelper.PutConfigAsync(c => c["treeLevels"] = GamHelper.Levels((1, "A", 0), (2, "B", 100), (3, "C", 300))))
                .ShouldBe(400);
        }
        finally
        {
            await ApiJson.PostAsync(GamRoutes.FactStatus(fact), new { status = "INACTIVE" }, await GamHelper.Staff());
        }
    }

    [Fact]
    public async Task Reset_IsIdempotent()
    {
        await GamHelper.ResetConfigAsync();
        await GamHelper.ResetConfigAsync();
        Assert.Equal(4, (await GamHelper.GetConfigAsync()).GetProperty("treeLevels").GetArrayLength());
    }
}

// =====================================================================================
public class GamEcoFactTests
{
    [Fact]
    public async Task Create_ReturnsActiveFactWithCode()
    {
        var title = $"Sự thật xanh {TestData.Unique(10)}";
        var r = (await ApiJson.PostAsync(GamRoutes.StaffFacts,
            new { title, content = "Nội dung ngắn.", sourceUrl = "https://example.com/nguon", imageUrl = "https://example.com/a.jpg", unlockTreeLevel = 2 },
            await GamHelper.Staff())).ShouldBe(201);
        var d = r.Data();
        Assert.Equal("ACTIVE", d.GS("status"));
        Assert.Equal(2, d.GetProperty("unlockTreeLevel").GetInt32());
        Assert.StartsWith("EF-", d.GS("code"));
        Assert.Equal(title, d.GS("title"));
    }

    [Theory]
    [InlineData("", "Nội dung")]
    [InlineData("Tiêu đề", "")]
    [InlineData("   ", "   ")]
    public async Task Create_MissingField_Returns400WithMsgAuth02(string title, string content) =>
        (await ApiJson.PostAsync(GamRoutes.StaffFacts, new { title, content, unlockTreeLevel = 1 }, await GamHelper.Staff()))
            .ShouldFailWith(400, "MSG-AUTH-02");

    [Fact]
    public async Task Create_TitleTooLong_Returns400WithMsgNtf03() =>
        (await ApiJson.PostAsync(GamRoutes.StaffFacts, new { title = new string('t', 201), content = "ok", unlockTreeLevel = 1 }, await GamHelper.Staff()))
            .ShouldFailWith(400, "MSG-NTF-03");

    [Fact]
    public async Task Create_ContentTooLong_Returns400WithMsgNtf03() =>
        (await ApiJson.PostAsync(GamRoutes.StaffFacts, new { title = "Tiêu đề", content = new string('c', 301), unlockTreeLevel = 1 }, await GamHelper.Staff()))
            .ShouldFailWith(400, "MSG-NTF-03");

    [Fact]
    public async Task Create_ContentAtLimit_Accepted() =>
        (await ApiJson.PostAsync(GamRoutes.StaffFacts, new { title = $"Giới hạn {TestData.Unique(8)}", content = new string('c', 300), unlockTreeLevel = 1 },
            await GamHelper.Staff())).ShouldBe(201);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(99)]
    public async Task Create_LevelOutsideConfiguredRange_Returns400(int level)
    {
        await GamHelper.ResetConfigAsync();
        (await ApiJson.PostAsync(GamRoutes.StaffFacts, new { title = "Cấp không hợp lệ", content = "ok", unlockTreeLevel = level }, await GamHelper.Staff()))
            .ShouldFailWith(400, "MSG-AUTH-02");
    }

    [Fact]
    public async Task Create_UnsupportedImage_Returns400WithMsgCls01() =>
        (await ApiJson.PostAsync(GamRoutes.StaffFacts,
            new { title = "Ảnh sai", content = "ok", imageUrl = "https://example.com/a.gif", unlockTreeLevel = 1 }, await GamHelper.Staff()))
            .ShouldFailWith(400, "MSG-CLS-01");

    [Fact]
    public async Task Create_InvalidSourceUrl_Returns400() =>
        (await ApiJson.PostAsync(GamRoutes.StaffFacts,
            new { title = "Nguồn sai", content = "ok", sourceUrl = "not-a-url", unlockTreeLevel = 1 }, await GamHelper.Staff()))
            .ShouldFailWith(400, "MSG-AUTH-02");

    [Fact]
    public async Task Update_ChangesFields()
    {
        var id = await GamHelper.CreateFactAsync(1);
        var title = $"Đã sửa {TestData.Unique(8)}";
        var d = (await ApiJson.PutAsync(GamRoutes.StaffFact(id), new { title, content = "Nội dung mới", unlockTreeLevel = 3 }, await GamHelper.Staff()))
            .ShouldBe(200).Data();
        Assert.Equal(title, d.GS("title"));
        Assert.Equal(3, d.GetProperty("unlockTreeLevel").GetInt32());
    }

    [Fact]
    public async Task Update_Unknown_Returns404() =>
        (await ApiJson.PutAsync(GamRoutes.StaffFact(Guid.NewGuid()), new { title = "x", content = "y", unlockTreeLevel = 1 }, await GamHelper.Staff())).ShouldBe(404);

    [Fact]
    public async Task Get_Unknown_Returns404() =>
        (await Api.GetAsync(GamRoutes.StaffFact(Guid.NewGuid()), await GamHelper.Staff())).ShouldBe(404);

    [Fact]
    public async Task Status_Toggle_AndInvalidValue()
    {
        var id = await GamHelper.CreateFactAsync();
        var staff = await GamHelper.Staff();
        Assert.Equal("INACTIVE", (await ApiJson.PostAsync(GamRoutes.FactStatus(id), new { status = "inactive" }, staff)).ShouldBe(200).Data().GS("status"));
        Assert.Equal("ACTIVE", (await ApiJson.PostAsync(GamRoutes.FactStatus(id), new { status = "ACTIVE" }, staff)).ShouldBe(200).Data().GS("status"));
        (await ApiJson.PostAsync(GamRoutes.FactStatus(id), new { status = "DELETED" }, staff)).ShouldBe(400);
        (await ApiJson.PostAsync(GamRoutes.FactStatus(Guid.NewGuid()), new { status = "ACTIVE" }, staff)).ShouldBe(404);
    }

    [Fact]
    public async Task List_FilterByLevelStatusAndSearch()
    {
        var key = TestData.Unique(10);
        var a = await GamHelper.CreateFactAsync(2, $"Lọc {key} A");
        var b = await GamHelper.CreateFactAsync(3, $"Lọc {key} B");
        var staff = await GamHelper.Staff();
        await ApiJson.PostAsync(GamRoutes.FactStatus(b), new { status = "INACTIVE" }, staff);

        var bySearch = (await Api.GetAsync(GamRoutes.StaffFacts, staff, new Dictionary<string, string> { ["search"] = key })).ShouldBe(200).Data();
        Assert.Equal(2, bySearch.GetProperty("totalCount").GetInt32());

        var byLevel = (await Api.GetAsync(GamRoutes.StaffFacts, staff,
            new Dictionary<string, string> { ["search"] = key, ["treeLevel"] = "2" })).ShouldBe(200).Data();
        Assert.Equal(a, byLevel.GetProperty("items")[0].GetProperty("id").GetGuid());

        var byStatus = (await Api.GetAsync(GamRoutes.StaffFacts, staff,
            new Dictionary<string, string> { ["search"] = key, ["status"] = "INACTIVE" })).ShouldBe(200).Data();
        Assert.Equal(1, byStatus.GetProperty("totalCount").GetInt32());
        Assert.Equal(b, byStatus.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task List_InvalidStatus_Returns400() =>
        (await Api.GetAsync(GamRoutes.StaffFacts, await GamHelper.Staff(), new Dictionary<string, string> { ["status"] = "x" })).ShouldBe(400);

    [Fact]
    public async Task List_PageSizeIsClamped()
    {
        var d = (await Api.GetAsync(GamRoutes.StaffFacts, await GamHelper.Staff(),
            new Dictionary<string, string> { ["pageSize"] = "1000" })).ShouldBe(200).Data();
        Assert.True(d.GetProperty("pageSize").GetInt32() <= 50);
    }
}

// =====================================================================================
public class GamTreeTests
{
    [Fact]
    public async Task Tree_NewUser_StartsAtLevelOne()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        var d = (await Api.GetAsync(GamRoutes.Tree, token)).ShouldBe(200).Data();
        Assert.Equal(1, d.GetProperty("level").GetInt32());
        Assert.Equal("Seedling", d.GS("levelTitle"));
        Assert.Equal(4, d.GetProperty("maxLevel").GetInt32());
        Assert.False(d.GetProperty("isMaxLevel").GetBoolean());
        Assert.Equal(2, d.GetProperty("nextLevel").GetInt32());
        Assert.Equal(100, d.GL("upgradeCost"));
        Assert.Equal(0, d.GetProperty("progressPercent").GetInt32());
        Assert.False(d.GetProperty("canUpgrade").GetBoolean());
        Assert.Equal(4, d.GetProperty("levels").GetArrayLength());
        Assert.Equal(3, d.GetProperty("dailyLimit").GetInt32());
    }

    [Fact]
    public async Task Water_WithoutEnoughPoints_Returns400WithMsgGam03()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        (await ApiJson.PostAsync(GamRoutes.Water, null, token)).ShouldFailWith(400, "MSG-GAM-03");
        var d = (await Api.GetAsync(GamRoutes.Tree, token)).ShouldBe(200).Data();
        Assert.Equal(1, d.GetProperty("level").GetInt32());
        Assert.Equal(0, d.GetProperty("totalWatered").GetInt32());
        Assert.Equal(0, await GamHelper.BalanceAsync(token));
    }

    [Fact]
    public async Task Progress_ReflectsBalanceAgainstCost()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 50);
        var d = (await Api.GetAsync(GamRoutes.Tree, token)).ShouldBe(200).Data();
        Assert.Equal(50, d.GetProperty("progressPercent").GetInt32());
        Assert.False(d.GetProperty("canUpgrade").GetBoolean());
    }

    [Fact]
    public async Task Water_WithEnoughPoints_UpgradesOneLevel_AndSpendsPoints()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 1000);
        var r = (await ApiJson.PostAsync(GamRoutes.Water, null, token)).ShouldBe(200);
        Assert.Equal("MSG-GAM-06", r.MessageCode());
        var d = r.Data();
        Assert.Equal(100, d.GL("pointsSpent"));
        var tree = d.GetProperty("tree");
        Assert.Equal(2, tree.GetProperty("level").GetInt32());
        Assert.Equal("Sprout", tree.GS("levelTitle"));
        Assert.Equal(1, tree.GetProperty("totalWatered").GetInt32());
        Assert.Equal(0, tree.GetProperty("dailyWaterCount").GetInt32());
        Assert.Equal(900, tree.GL("walletBalance"));
        Assert.Equal(900, await GamHelper.BalanceAsync(token));
        Assert.Contains((await GamHelper.AllTransactionsAsync(token)), t => t.GS("actionType") == "Tree_Care" && t.GL("points") == -100);
    }

    [Fact]
    public async Task Water_UnlocksEcoFactOfNewLevel()
    {
        await GamHelper.ResetConfigAsync();
        var fact = await GamHelper.CreateFactAsync(2, $"Mở khóa {TestData.Unique(8)}");
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 1000);

        // Chưa lên cấp: Eco Fact bị khóa
        (await Api.GetAsync(GamRoutes.EcoFact(fact), token)).ShouldBe(404);
        Assert.DoesNotContain((await Api.GetAsync(GamRoutes.EcoFacts, token)).ShouldBe(200).Data().EnumerateArray(),
            f => f.GetProperty("id").GetGuid() == fact);

        var water = (await ApiJson.PostAsync(GamRoutes.Water, null, token)).ShouldBe(200).Data();
        Assert.Contains(water.GetProperty("unlockedFacts").EnumerateArray(), f => f.GetProperty("id").GetGuid() == fact);

        (await Api.GetAsync(GamRoutes.EcoFact(fact), token)).ShouldBe(200);
        Assert.Contains((await Api.GetAsync(GamRoutes.EcoFacts, token)).ShouldBe(200).Data().EnumerateArray(),
            f => f.GetProperty("id").GetGuid() == fact);
        Assert.True((await Api.GetAsync(GamRoutes.Tree, token)).ShouldBe(200).Data().GetProperty("unlockedFactCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task InactiveFact_IsHiddenFromUser()
    {
        await GamHelper.ResetConfigAsync();
        var fact = await GamHelper.CreateFactAsync(1);
        await ApiJson.PostAsync(GamRoutes.FactStatus(fact), new { status = "INACTIVE" }, await GamHelper.Staff());
        var token = await GamHelper.FreshUserAsync();
        (await Api.GetAsync(GamRoutes.EcoFact(fact), token)).ShouldBe(404);
    }

    [Fact]
    public async Task EcoFacts_NewUser_SeesOnlyLevelOneFacts()
    {
        await GamHelper.ResetConfigAsync();
        var level1 = await GamHelper.CreateFactAsync(1);
        var level3 = await GamHelper.CreateFactAsync(3);
        var token = await GamHelper.FreshUserAsync();
        var ids = (await Api.GetAsync(GamRoutes.EcoFacts, token)).ShouldBe(200).Data().EnumerateArray()
            .Select(f => f.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(level1, ids);
        Assert.DoesNotContain(level3, ids);
    }

    [Fact]
    public async Task EcoFact_Unknown_Returns404() =>
        (await Api.GetAsync(GamRoutes.EcoFact(Guid.NewGuid()), await GamHelper.Citizen())).ShouldBe(404);

    [Fact]
    public async Task Water_ReachingMaxLevel_ThenFurtherWater_Returns400()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 1200);                 // 1100 điểm để lên cấp 4 (100 + 300 + 700)
        for (var i = 0; i < 3; i++)
            (await ApiJson.PostAsync(GamRoutes.Water, null, token)).ShouldBe(200);

        var tree = (await Api.GetAsync(GamRoutes.Tree, token)).ShouldBe(200).Data();
        Assert.Equal(4, tree.GetProperty("level").GetInt32());
        Assert.True(tree.GetProperty("isMaxLevel").GetBoolean());
        Assert.Equal(JsonValueKind.Null, tree.GetProperty("upgradeCost").ValueKind);
        Assert.False(tree.GetProperty("canUpgrade").GetBoolean());
        Assert.Equal(100, tree.GetProperty("progressPercent").GetInt32());
        Assert.Equal(100, await GamHelper.BalanceAsync(token));
        (await ApiJson.PostAsync(GamRoutes.Water, null, token)).ShouldBe(400);
        Assert.Equal(100, await GamHelper.BalanceAsync(token));
    }

    [Fact]
    public async Task Water_DailyLimit_BlocksFurtherCare_WhileAllowingPaidUpgrade()
    {
        try
        {
            await GamHelper.ResetConfigAsync();
            (await GamHelper.PutConfigAsync(c => c["dailyTreeCareLimit"] = 1)).ShouldBe(200);
            var token = await GamHelper.FreshUserAsync();
            await GamHelper.EarnAsync(token, 600);

            // Tưới lần 1 thành công (75% -> 100%, dailyWater = 1/1); tưới lần 2 bị chặn
            (await ApiJson.PostAsync(GamRoutes.Care, new { actionType = "water" }, token)).ShouldBe(200);
            (await ApiJson.PostAsync(GamRoutes.Care, new { actionType = "water" }, token)).ShouldBe(400);

            // Bón phân có giới hạn riêng (tối đa 1 lượt miễn phí/ngày khi dailyTreeCareLimit=1), ngoài ra EarnAsync đã tặng +2 túi phân hữu cơ
            var treeBeforeUpgrade = (await Api.GetAsync(GamRoutes.Tree, token)).ShouldBe(200).Data();
            Assert.Equal(1, treeBeforeUpgrade.GetProperty("dailyWaterCount").GetInt32());
            Assert.Equal(0, treeBeforeUpgrade.GetProperty("dailyFertilizeCount").GetInt32());
            Assert.Equal(100, treeBeforeUpgrade.GetProperty("hydrationPercent").GetInt32());
            Assert.Equal(2, treeBeforeUpgrade.GetProperty("fertilizerPacks").GetInt32());
            Assert.True(treeBeforeUpgrade.GetProperty("canUpgrade").GetBoolean());

            (await ApiJson.PostAsync(GamRoutes.Care, new { actionType = "fertilize" }, token)).ShouldBe(200);
            var afterFert = (await Api.GetAsync(GamRoutes.Tree, token)).ShouldBe(200).Data();
            Assert.Equal(1, afterFert.GetProperty("dailyFertilizeCount").GetInt32());
            Assert.Equal(90, afterFert.GetProperty("hydrationPercent").GetInt32());

            (await ApiJson.PostAsync(GamRoutes.Water, null, token)).ShouldBe(200);
            var tree = (await Api.GetAsync(GamRoutes.Tree, token)).ShouldBe(200).Data();
            Assert.Equal(2, tree.GetProperty("level").GetInt32());
            Assert.Equal(1, tree.GetProperty("dailyWaterCount").GetInt32());
            Assert.Equal(500, await GamHelper.BalanceAsync(token));
        }
        finally { await GamHelper.ResetConfigAsync(); }
    }

    [Fact]
    public async Task Water_ConcurrentRequests_OnlyOneUpgrades()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 1000);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ApiJson.PostAsync(GamRoutes.Water, null, token)));
        // Mỗi request hợp lệ lên đúng một cấp; không bao giờ lên nhiều hơn số lần thành công và điểm trừ khớp số lần đó.
        var ok = results.Count(r => r.Status == 200);
        Assert.InRange(ok, 1, 3);
        var tree = (await Api.GetAsync(GamRoutes.Tree, token)).ShouldBe(200).Data();
        Assert.Equal(1 + ok, tree.GetProperty("level").GetInt32());
        var spent = new long[] { 0, 100, 400, 1100 }[ok];
        Assert.Equal(1000 - spent, await GamHelper.BalanceAsync(token));
        Assert.All(results.Where(r => r.Status != 200), r => r.ShouldBe(400));
    }

    [Fact]
    public async Task Water_CostFollowsConfiguredThreshold()
    {
        try
        {
            (await GamHelper.PutConfigAsync(c => c["treeLevels"] = GamHelper.Levels((1, "A", 0), (2, "B", 30), (3, "C", 90)))).ShouldBe(200);
            var token = await GamHelper.FreshUserAsync();
            await GamHelper.EarnAsync(token, 100);
            var d = (await ApiJson.PostAsync(GamRoutes.Water, null, token)).ShouldBe(200).Data();
            Assert.Equal(30, d.GL("pointsSpent"));
            Assert.Equal(70, await GamHelper.BalanceAsync(token));
        }
        finally { await GamHelper.ResetConfigAsync(); }
    }
}

// =====================================================================================
public class GamGiftInventoryTests
{
    [Fact]
    public async Task Create_ReturnsActiveGift()
    {
        var name = $"Quà tạo {TestData.Unique(10)}";
        var r = (await ApiJson.PostAsync(GamRoutes.StaffGifts, new
        {
            name,
            type = "Physical",
            description = "Mô tả",
            imageUrl = "https://example.com/g.png",
            pointsRequired = 120,
            stockQuantity = 7
        }, await GamHelper.Staff())).ShouldBe(201);
        var d = r.Data();
        Assert.Equal("ACTIVE", d.GS("status"));
        Assert.Equal("Physical", d.GS("type"));
        Assert.Equal(120, d.GL("pointsRequired"));
        Assert.Equal(7, d.GetProperty("stockQuantity").GetInt32());
        Assert.True(d.GetProperty("inStock").GetBoolean());
    }

    [Fact]
    public async Task Create_MissingName_Returns400WithMsgAuth02() =>
        (await ApiJson.PostAsync(GamRoutes.StaffGifts, new { name = "  ", type = "Voucher", pointsRequired = 10, stockQuantity = 1 }, await GamHelper.Staff()))
            .ShouldFailWith(400, "MSG-AUTH-02");

    [Fact]
    public async Task Create_MissingType_Returns400WithMsgAuth02() =>
        (await ApiJson.PostAsync(GamRoutes.StaffGifts, new { name = "Quà không loại", pointsRequired = 10, stockQuantity = 1 }, await GamHelper.Staff()))
            .ShouldFailWith(400, "MSG-AUTH-02");

    [Fact]
    public async Task Create_NameTooLong_Returns400WithMsgNtf03() =>
        (await ApiJson.PostAsync(GamRoutes.StaffGifts, new { name = new string('q', 151), type = "Voucher", pointsRequired = 10, stockQuantity = 1 }, await GamHelper.Staff()))
            .ShouldFailWith(400, "MSG-NTF-03");

    [Fact]
    public async Task Create_InvalidType_Returns400() =>
        (await ApiJson.PostAsync(GamRoutes.StaffGifts, new { name = "Quà loại sai", type = "Digital", pointsRequired = 10, stockQuantity = 1 }, await GamHelper.Staff()))
            .ShouldBe(400);

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(10, -1)]
    public async Task Create_NegativeNumbers_Return400(long points, int stock) =>
        (await ApiJson.PostAsync(GamRoutes.StaffGifts, new { name = "Quà số âm", type = "Voucher", pointsRequired = points, stockQuantity = stock }, await GamHelper.Staff()))
            .ShouldBe(400);

    [Fact]
    public async Task Create_UnsupportedImage_Returns400WithMsgCls01() =>
        (await ApiJson.PostAsync(GamRoutes.StaffGifts,
            new { name = "Quà ảnh sai", type = "Voucher", imageUrl = "https://example.com/a.bmp", pointsRequired = 1, stockQuantity = 1 }, await GamHelper.Staff()))
            .ShouldFailWith(400, "MSG-CLS-01");

    [Fact]
    public async Task Update_ChangesPoints_ButNotStock()
    {
        var id = await GamHelper.CreateGiftAsync("Voucher", points: 10, stock: 4);
        var d = (await ApiJson.PutAsync(GamRoutes.StaffGift(id),
            new { name = $"Quà sửa {TestData.Unique(8)}", type = "Physical", pointsRequired = 55, stockQuantity = 999 }, await GamHelper.Staff())).ShouldBe(200).Data();
        Assert.Equal(55, d.GL("pointsRequired"));
        Assert.Equal("Physical", d.GS("type"));
        Assert.Equal(4, d.GetProperty("stockQuantity").GetInt32());
    }

    [Fact]
    public async Task Update_Unknown_Returns404() =>
        (await ApiJson.PutAsync(GamRoutes.StaffGift(Guid.NewGuid()), new { name = "x quà", type = "Voucher", pointsRequired = 1 }, await GamHelper.Staff())).ShouldBe(404);

    [Fact]
    public async Task Restock_AddsToStock()
    {
        var id = await GamHelper.CreateGiftAsync(stock: 2);
        var d = (await ApiJson.PostAsync(GamRoutes.Restock(id), new { quantity = 5 }, await GamHelper.Staff())).ShouldBe(200).Data();
        Assert.Equal(7, d.GetProperty("stockQuantity").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Restock_NonPositive_Returns400(int qty)
    {
        var id = await GamHelper.CreateGiftAsync(stock: 2);
        (await ApiJson.PostAsync(GamRoutes.Restock(id), new { quantity = qty }, await GamHelper.Staff())).ShouldBe(400);
        Assert.Equal(2, (await GamHelper.StaffGiftAsync(id)).GetProperty("stockQuantity").GetInt32());
    }

    [Fact]
    public async Task Restock_Unknown_Returns404() =>
        (await ApiJson.PostAsync(GamRoutes.Restock(Guid.NewGuid()), new { quantity = 1 }, await GamHelper.Staff())).ShouldBe(404);

    [Fact]
    public async Task Status_Toggle_ControlsStoreVisibility()
    {
        var id = await GamHelper.CreateGiftAsync();
        var staff = await GamHelper.Staff();
        var citizen = await GamHelper.Citizen();
        (await Api.GetAsync(GamRoutes.Gift(id), citizen)).ShouldBe(200);

        Assert.Equal("INACTIVE", (await ApiJson.PostAsync(GamRoutes.GiftStatus(id), new { status = "INACTIVE" }, staff)).ShouldBe(200).Data().GS("status"));
        (await Api.GetAsync(GamRoutes.Gift(id), citizen)).ShouldBe(404);
        Assert.DoesNotContain((await Api.GetAsync(GamRoutes.Gifts, citizen)).ShouldBe(200).Data().EnumerateArray(),
            g => g.GetProperty("id").GetGuid() == id);

        (await ApiJson.PostAsync(GamRoutes.GiftStatus(id), new { status = "ACTIVE" }, staff)).ShouldBe(200);
        (await Api.GetAsync(GamRoutes.Gift(id), citizen)).ShouldBe(200);
    }

    [Fact]
    public async Task Status_Invalid_Returns400()
    {
        var id = await GamHelper.CreateGiftAsync();
        (await ApiJson.PostAsync(GamRoutes.GiftStatus(id), new { status = "PAUSED" }, await GamHelper.Staff())).ShouldBe(400);
    }

    [Fact]
    public async Task Inventory_List_IncludesInactive_AndFilters()
    {
        var key = TestData.Unique(10);
        var v = await GamHelper.CreateGiftAsync("Voucher", name: $"Lọc {key} V");
        var p = await GamHelper.CreateGiftAsync("Physical", name: $"Lọc {key} P");
        var staff = await GamHelper.Staff();
        await ApiJson.PostAsync(GamRoutes.GiftStatus(p), new { status = "INACTIVE" }, staff);

        var all = (await Api.GetAsync(GamRoutes.StaffGifts, staff, new Dictionary<string, string> { ["search"] = key })).ShouldBe(200).Data();
        Assert.Equal(2, all.GetProperty("totalCount").GetInt32());

        var inactive = (await Api.GetAsync(GamRoutes.StaffGifts, staff,
            new Dictionary<string, string> { ["search"] = key, ["status"] = "INACTIVE" })).ShouldBe(200).Data();
        Assert.Equal(p, inactive.GetProperty("items")[0].GetProperty("id").GetGuid());

        var vouchers = (await Api.GetAsync(GamRoutes.StaffGifts, staff,
            new Dictionary<string, string> { ["search"] = key, ["type"] = "Voucher" })).ShouldBe(200).Data();
        Assert.Equal(v, vouchers.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Inventory_List_InvalidFilters_Return400()
    {
        var staff = await GamHelper.Staff();
        (await Api.GetAsync(GamRoutes.StaffGifts, staff, new Dictionary<string, string> { ["type"] = "x" })).ShouldBe(400);
        (await Api.GetAsync(GamRoutes.StaffGifts, staff, new Dictionary<string, string> { ["status"] = "x" })).ShouldBe(400);
    }

    [Fact]
    public async Task Inventory_Get_Unknown_Returns404() =>
        (await Api.GetAsync(GamRoutes.StaffGift(Guid.NewGuid()), await GamHelper.Staff())).ShouldBe(404);
}

// =====================================================================================
public class GamRedeemTests
{
    [Fact]
    public async Task Store_SortByPoints_AndTypeFilter()
    {
        var key = TestData.Unique(10);
        await GamHelper.CreateGiftAsync("Voucher", points: 30, name: $"Xếp {key} A");
        await GamHelper.CreateGiftAsync("Physical", points: 10, name: $"Xếp {key} B");
        await GamHelper.CreateGiftAsync("Voucher", points: 20, name: $"Xếp {key} C");
        var citizen = await GamHelper.Citizen();

        List<long> Points(JsonElement d) => d.EnumerateArray().Select(g => g.GL("pointsRequired")).ToList();
        Assert.Equal(new long[] { 10, 20, 30 }, Points((await Api.GetAsync(GamRoutes.Gifts, citizen,
            new Dictionary<string, string> { ["search"] = key, ["sort"] = "points_asc" })).ShouldBe(200).Data()));
        Assert.Equal(new long[] { 30, 20, 10 }, Points((await Api.GetAsync(GamRoutes.Gifts, citizen,
            new Dictionary<string, string> { ["search"] = key, ["sort"] = "points_desc" })).ShouldBe(200).Data()));
        Assert.Equal(new long[] { 20, 30 }, Points((await Api.GetAsync(GamRoutes.Gifts, citizen,
            new Dictionary<string, string> { ["search"] = key, ["type"] = "Voucher", ["sort"] = "points_asc" })).ShouldBe(200).Data()));
    }

    [Fact]
    public async Task Store_InvalidType_Returns400() =>
        (await Api.GetAsync(GamRoutes.Gifts, await GamHelper.Citizen(), new Dictionary<string, string> { ["type"] = "x" })).ShouldBe(400);

    [Fact]
    public async Task Store_Get_Unknown_Returns404() =>
        (await Api.GetAsync(GamRoutes.Gift(Guid.NewGuid()), await GamHelper.Citizen())).ShouldBe(404);

    [Fact]
    public async Task Redeem_Voucher_DeductsPointsAndStock_AndIssuesCode()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 200);
        var gift = await GamHelper.CreateGiftAsync("Voucher", points: 60, stock: 3);

        var r = (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, token)).ShouldBe(201);
        Assert.Equal("MSG-GAM-02", r.MessageCode());
        var d = r.Data();
        Assert.Equal(140, d.GL("walletBalance"));
        var red = d.GetProperty("redemption");
        Assert.Equal("Redeemed", red.GS("status"));
        Assert.Equal(60, red.GL("pointsSpent"));
        Assert.Matches("^ECO-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$", red.GS("voucherCode"));
        Assert.Matches("^RDM-\\d{8}-[A-Z0-9]{6}$", red.GS("redemptionCode"));
        var days = (red.GetProperty("expiresAt").GetDateTime() - DateTime.UtcNow).TotalDays;
        Assert.InRange(days, 29, 31);
        Assert.Equal(2, (await GamHelper.StaffGiftAsync(gift)).GetProperty("stockQuantity").GetInt32());
        Assert.Contains(await GamHelper.AllTransactionsAsync(token), t => t.GS("actionType") == "Redeem_Gift" && t.GL("points") == -60);
    }

    [Fact]
    public async Task Redeem_InsufficientPoints_Returns400_AndNothingChanges()
    {
        var token = await GamHelper.FreshUserAsync();
        var gift = await GamHelper.CreateGiftAsync("Voucher", points: 5000, stock: 3);
        (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, token)).ShouldFailWith(400, "MSG-GAM-03");
        Assert.Equal(3, (await GamHelper.StaffGiftAsync(gift)).GetProperty("stockQuantity").GetInt32());
        Assert.Equal(0, await GamHelper.BalanceAsync(token));
        Assert.Equal(0, (await Api.GetAsync(GamRoutes.Redemptions, token)).ShouldBe(200).Data().GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Redeem_OutOfStock_Returns400WithMsgGam04()
    {
        var token = await GamHelper.FreshUserAsync();
        await ApiJson.PostAsync(GamRoutes.CheckIn, null, token);
        var gift = await GamHelper.CreateGiftAsync("Voucher", points: 1, stock: 0);
        (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, token)).ShouldFailWith(400, "MSG-GAM-04");
        Assert.Equal(5, await GamHelper.BalanceAsync(token));
    }

    [Fact]
    public async Task Redeem_InactiveGift_Returns400WithMsgGam04()
    {
        var token = await GamHelper.FreshUserAsync();
        await ApiJson.PostAsync(GamRoutes.CheckIn, null, token);
        var gift = await GamHelper.CreateGiftAsync("Voucher", points: 1, stock: 3);
        await ApiJson.PostAsync(GamRoutes.GiftStatus(gift), new { status = "INACTIVE" }, await GamHelper.Staff());
        (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, token)).ShouldFailWith(400, "MSG-GAM-04");
        Assert.Equal(5, await GamHelper.BalanceAsync(token));
    }

    [Fact]
    public async Task Redeem_Unknown_Returns404() =>
        (await ApiJson.PostAsync(GamRoutes.Redeem(Guid.NewGuid()), new { }, await GamHelper.Citizen())).ShouldBe(404);

    [Fact]
    public async Task Redeem_VoucherQuantityTwo_Returns400()
    {
        var token = await GamHelper.FreshUserAsync();
        await ApiJson.PostAsync(GamRoutes.CheckIn, null, token);
        var gift = await GamHelper.CreateGiftAsync("Voucher", points: 1, stock: 5);
        (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { quantity = 2 }, token)).ShouldBe(400);
        Assert.Equal(5, (await GamHelper.StaffGiftAsync(gift)).GetProperty("stockQuantity").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public async Task Redeem_QuantityOutOfRange_Returns400(int qty)
    {
        var token = await GamHelper.FreshUserAsync();
        var gift = await GamHelper.CreateGiftAsync("Physical", points: 1, stock: 20);
        (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { quantity = qty, deliveryAddress = "1 Test" }, token)).ShouldBe(400);
    }

    [Fact]
    public async Task Redeem_Physical_WithoutAddress_Returns400WithMsgAuth02()
    {
        var token = await GamHelper.FreshUserAsync();
        await ApiJson.PostAsync(GamRoutes.CheckIn, null, token);
        var gift = await GamHelper.CreateGiftAsync("Physical", points: 1, stock: 5);
        (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { deliveryAddress = "   " }, token)).ShouldFailWith(400, "MSG-AUTH-02");
        Assert.Equal(5, await GamHelper.BalanceAsync(token));
        Assert.Equal(5, (await GamHelper.StaffGiftAsync(gift)).GetProperty("stockQuantity").GetInt32());
    }

    [Fact]
    public async Task Redeem_Physical_QuantityMultipliesCost_AndStartsPending()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 100);
        var gift = await GamHelper.CreateGiftAsync("Physical", points: 20, stock: 5);
        var r = (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { quantity = 3, deliveryAddress = "12 Đường Test, Quận 1" }, token)).ShouldBe(201).Data();
        var red = r.GetProperty("redemption");
        Assert.Equal("Pending", red.GS("status"));
        Assert.Equal(60, red.GL("pointsSpent"));
        Assert.Equal(3, red.GetProperty("quantity").GetInt32());
        Assert.Equal("12 Đường Test, Quận 1", red.GS("deliveryAddress"));
        Assert.Equal(JsonValueKind.Null, red.GetProperty("voucherCode").ValueKind);
        Assert.Equal(40, r.GL("walletBalance"));
        Assert.Equal(2, (await GamHelper.StaffGiftAsync(gift)).GetProperty("stockQuantity").GetInt32());
    }

    [Fact]
    public async Task Redeem_QuantityMoreThanStock_Returns400WithMsgGam04()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 100);
        var gift = await GamHelper.CreateGiftAsync("Physical", points: 1, stock: 2);
        (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { quantity = 3, deliveryAddress = "1 Test" }, token)).ShouldFailWith(400, "MSG-GAM-04");
        Assert.Equal(2, (await GamHelper.StaffGiftAsync(gift)).GetProperty("stockQuantity").GetInt32());
        Assert.Equal(100, await GamHelper.BalanceAsync(token));
    }

    [Fact]
    public async Task Redeem_FreeGift_DoesNotChangeBalance()
    {
        var token = await GamHelper.FreshUserAsync();
        var gift = await GamHelper.CreateGiftAsync("Voucher", points: 0, stock: 2);
        (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, token)).ShouldBe(201);
        Assert.Equal(0, await GamHelper.BalanceAsync(token));
    }

    [Fact]
    public async Task Redeem_ByRecycler_IsAllowed()
    {
        var collector = await GamHelper.Collector();
        var before = await GamHelper.BalanceAsync(collector);
        if (before < 10) await GamHelper.EarnAsync(await GamHelper.FreshUserAsync(), 100);   // Recycler cũng nhận điểm khi bàn giao hoàn tất
        var gift = await GamHelper.CreateGiftAsync("Voucher", points: 10, stock: 2);
        var balance = await GamHelper.BalanceAsync(collector);
        (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, collector)).ShouldBe(201);
        Assert.Equal(balance - 10, await GamHelper.BalanceAsync(collector));
    }

    [Fact]
    public async Task Redeem_LastItem_TwoBuyers_OnlyOneSucceeds()
    {
        await GamHelper.ResetConfigAsync();
        var a = await GamHelper.FreshUserAsync();
        var b = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(a, 100);
        await GamHelper.EarnAsync(b, 100);
        var gift = await GamHelper.CreateGiftAsync("Voucher", points: 40, stock: 1);

        var results = await Task.WhenAll(
            ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, a),
            ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, b));
        Assert.Equal(1, results.Count(r => r.Status == 201));
        Assert.Equal(1, results.Count(r => r.Status == 400));
        Assert.Equal(0, (await GamHelper.StaffGiftAsync(gift)).GetProperty("stockQuantity").GetInt32());
        Assert.Equal(160, await GamHelper.BalanceAsync(a) + await GamHelper.BalanceAsync(b));   // đúng một người bị trừ 40
    }

    [Fact]
    public async Task Redeem_SameUserParallel_NeverOverspends()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 100);
        var gift = await GamHelper.CreateGiftAsync("Voucher", points: 60, stock: 10);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, token)));
        Assert.Equal(1, results.Count(r => r.Status == 201));
        Assert.Equal(40, await GamHelper.BalanceAsync(token));
        Assert.Equal(9, (await GamHelper.StaffGiftAsync(gift)).GetProperty("stockQuantity").GetInt32());
    }

    [Fact]
    public async Task MyRedemptions_ListsOwnOnly_WithFilterAndDetail()
    {
        var token = await GamHelper.FreshUserAsync();
        var other = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 100);
        var gift = await GamHelper.CreateGiftAsync("Voucher", points: 10, stock: 5);
        var id = (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, token)).ShouldBe(201)
            .Data().GetProperty("redemption").GetProperty("id").GetGuid();

        var list = (await Api.GetAsync(GamRoutes.Redemptions, token)).ShouldBe(200).Data();
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await Api.GetAsync(GamRoutes.Redemptions, other)).ShouldBe(200).Data().GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await Api.GetAsync(GamRoutes.Redemptions, token, new Dictionary<string, string> { ["status"] = "Redeemed" }))
            .ShouldBe(200).Data().GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await Api.GetAsync(GamRoutes.Redemptions, token, new Dictionary<string, string> { ["status"] = "Expired" }))
            .ShouldBe(200).Data().GetProperty("totalCount").GetInt32());
        (await Api.GetAsync(GamRoutes.Redemptions, token, new Dictionary<string, string> { ["status"] = "Nonsense" })).ShouldBe(400);

        var detail = (await Api.GetAsync(GamRoutes.Redemption(id), token)).ShouldBe(200).Data();
        Assert.Equal(gift, detail.GetProperty("gift").GetProperty("id").GetGuid());
        (await Api.GetAsync(GamRoutes.Redemption(id), other)).ShouldBe(404);       // voucher gắn với tài khoản đã đổi
        (await Api.GetAsync(GamRoutes.Redemption(Guid.NewGuid()), token)).ShouldBe(404);
    }

    [Fact]
    public async Task Staff_Fulfill_PhysicalRedemption_ThenSecondFulfillFails()
    {
        await GamHelper.ResetConfigAsync();
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 100);
        var gift = await GamHelper.CreateGiftAsync("Physical", points: 10, stock: 3);
        var id = (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { deliveryAddress = "9 Test" }, token)).ShouldBe(201)
            .Data().GetProperty("redemption").GetProperty("id").GetGuid();
        var staff = await GamHelper.Staff();

        var list = (await Api.GetAsync(GamRoutes.GiftRedemptions(gift), staff)).ShouldBe(200).Data();
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(list.GetProperty("items")[0].GS("userName")));

        var done = (await ApiJson.PostAsync(GamRoutes.Fulfill(id), null, staff)).ShouldBe(200).Data();
        Assert.Equal("Delivered", done.GS("status"));
        Assert.NotEqual(JsonValueKind.Null, done.GetProperty("fulfilledAt").ValueKind);
        (await ApiJson.PostAsync(GamRoutes.Fulfill(id), null, staff)).ShouldBe(400);
        Assert.Equal("Delivered", (await Api.GetAsync(GamRoutes.Redemption(id), token)).ShouldBe(200).Data().GS("status"));
    }

    [Fact]
    public async Task Staff_Fulfill_Unknown_Returns404() =>
        (await ApiJson.PostAsync(GamRoutes.Fulfill(Guid.NewGuid()), null, await GamHelper.Staff())).ShouldBe(404);

    [Fact]
    public async Task Staff_Fulfill_AsCitizen_Returns403() =>
        (await ApiJson.PostAsync(GamRoutes.Fulfill(Guid.NewGuid()), null, await GamHelper.Citizen())).ShouldBe(403);

    [Fact]
    public async Task Staff_GiftRedemptions_UnknownGift_Returns404() =>
        (await Api.GetAsync(GamRoutes.GiftRedemptions(Guid.NewGuid()), await GamHelper.Staff())).ShouldBe(404);

    [Fact]
    public async Task Redeem_VoucherExpiryFollowsConfiguredDays()
    {
        try
        {
            await GamHelper.ResetConfigAsync();
            (await GamHelper.PutConfigAsync(c => c["voucherValidityDays"] = 7)).ShouldBe(200);
            var token = await GamHelper.FreshUserAsync();
            await ApiJson.PostAsync(GamRoutes.CheckIn, null, token);
            var gift = await GamHelper.CreateGiftAsync("Voucher", points: 1, stock: 2);
            var red = (await ApiJson.PostAsync(GamRoutes.Redeem(gift), new { }, token)).ShouldBe(201).Data().GetProperty("redemption");
            Assert.InRange((red.GetProperty("expiresAt").GetDateTime() - DateTime.UtcNow).TotalDays, 6, 8);
        }
        finally { await GamHelper.ResetConfigAsync(); }
    }
}

/// <summary>
/// Kiểm thử tích hợp cho Canonical Routes (STT 076–086, 094–097, 100–102) và Chu kỳ Bảng xếp hạng (A-08).
/// </summary>
public class GamificationCanonicalAndLeaderboardCyclesTests
{
    [Fact]
    public async Task CanonicalRoutes_CheckInWalletLedgerGiftsRedemptionsAndTree_WorkEndToEnd()
    {
        var token = await GamHelper.FreshUserAsync();
        await GamHelper.EarnAsync(token, 150);

        // STT 076: EcoProfile thật từ UserPointWallet & VirtualTreeProgress
        var meRes = (await Api.GetAsync("/api/v1/gamification/profile/me", token)).ShouldBe(200).Data();
        Assert.True(meRes.GetProperty("currentGreenPoints").GetInt32() >= 150);

        // STT 078–079: Check-in canonical route
        var chkStatus = (await Api.GetAsync("/api/v1/gamification/check-in/status", token)).ShouldBe(200).Data();
        Assert.False(chkStatus.GetProperty("checkedInToday").GetBoolean());
        var chkDone = (await ApiJson.PostAsync("/api/v1/gamification/check-in", null, token)).ShouldBe(200).Data();
        Assert.True(chkDone.GetProperty("pointsAwarded").GetInt64() > 0);

        // STT 080: Sổ cái canonical route
        var ledger = (await Api.GetAsync("/api/v1/gamification/points/ledger", token)).ShouldBe(200).Data();
        Assert.True(ledger.GetProperty("totalCount").GetInt32() >= 2);

        // STT 081–083 & 095–097: Kho quà Staff import voucher, toggle status, đổi bằng canonical route & hủy hoàn điểm
        var staff = await GamHelper.Staff();
        var voucherGift = await GamHelper.CreateGiftAsync("Voucher", points: 10, stock: 1);
        var imported = (await ApiJson.PostAsync($"/api/v1/staff/gifts/{voucherGift}/vouchers/import",
            new { codes = new[] { $"ECO-IMP-{TestData.Unique(4)}", $"ECO-IMP-{TestData.Unique(4)}" } }, staff)).ShouldBe(200).Data();
        Assert.Equal(2, imported.GetProperty("importedCount").GetInt32());

        var giftList = (await Api.GetAsync("/api/v1/gamification/gifts", token)).ShouldBe(200).Data();
        Assert.Contains(giftList.EnumerateArray(), g => g.GetProperty("id").GetGuid() == voucherGift);

        var physicalGift = await GamHelper.CreateGiftAsync("Physical", points: 25, stock: 2);
        var redeemRes = (await ApiJson.PostAsync("/api/v1/gamification/redemptions",
            new { giftId = physicalGift, quantity = 1, deliveryAddress = "Số 1 Xuân Thủy, Hà Nội" }, token)).ShouldBe(201).Data();
        var redemptionId = redeemRes.GetProperty("redemption").GetProperty("id").GetGuid();

        var myRedemptions = (await Api.GetAsync("/api/v1/gamification/redemptions/me", token)).ShouldBe(200).Data();
        Assert.True(myRedemptions.GetProperty("totalCount").GetInt32() >= 1);

        var allRedemptions = (await Api.GetAsync("/api/v1/staff/redemptions", staff)).ShouldBe(200).Data();
        Assert.True(allRedemptions.GetProperty("totalCount").GetInt32() >= 1);

        var cancelled = (await ApiJson.PatchAsync($"/api/v1/staff/redemptions/{redemptionId}/fulfill",
            new { action = "Cancel", reason = "ApiTest hủy hoàn điểm" }, staff)).ShouldBe(200).Data();
        Assert.Equal("Cancelled", cancelled.GS("status"));

        // STT 084–086: Cây ảo & EcoFact canonical route
        var treeBefore = (await Api.GetAsync("/api/v1/gamification/tree/me", token)).ShouldBe(200).Data();
        Assert.Equal(1, treeBefore.GetProperty("level").GetInt32());
        var upgraded = (await ApiJson.PostAsync("/api/v1/gamification/tree/upgrade", null, token)).ShouldBe(200).Data();
        Assert.Equal(2, upgraded.GetProperty("tree").GetProperty("level").GetInt32());
        (await Api.GetAsync("/api/v1/gamification/eco-facts/me", token)).ShouldBe(200);
    }

    [Fact]
    public async Task LeaderboardCycles_AdminCreateActivateFinalize_AndPublicView_WorkEndToEnd()
    {
        var admin = await GamHelper.Admin();
        var citizen = await GamHelper.Citizen();

        // Chặn quyền Citizen & Anonymous trên routes A-08
        (await Api.GetAsync("/api/v1/admin/gamification/leaderboard-cycles")).ShouldBe(401);
        (await Api.GetAsync("/api/v1/admin/gamification/leaderboard-cycles", citizen)).ShouldBe(403);

        var cycleName = $"ApiTest Cycle {TestData.Unique(6)}";
        var created = (await ApiJson.PostAsync("/api/v1/admin/gamification/leaderboard-cycles", new
        {
            name = cycleName,
            periodType = "Weekly",
            startsAt = DateTime.UtcNow.AddDays(-5),
            endsAt = DateTime.UtcNow.AddDays(2),
            isActive = false,
            top1BonusPoints = 100,
            top2BonusPoints = 60,
            top3BonusPoints = 30
        }, admin)).ShouldBe(201).Data();
        var cycleId = created.GetProperty("id").GetGuid();

        var cycles = (await Api.GetAsync("/api/v1/admin/gamification/leaderboard-cycles", admin)).ShouldBe(200).Data();
        Assert.Contains(cycles.EnumerateArray(), c => c.GetProperty("id").GetGuid() == cycleId);

        // Bảng xếp hạng công khai theo cycleId
        var board = (await Api.GetAsync("/api/v1/gamification/leaderboard",
            query: new Dictionary<string, string> { ["top"] = "10", ["cycleId"] = cycleId.ToString() })).ShouldBe(200).Data();
        Assert.Equal(cycleId, board.GetProperty("activeCycle").GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Array, board.GetProperty("items").ValueKind);

        var finalized = (await ApiJson.PostAsync($"/api/v1/admin/gamification/leaderboard-cycles/{cycleId}/finalize", null, admin))
            .ShouldBe(200).Data();
        Assert.Equal("Finalized", finalized.GS("status"));
    }
}