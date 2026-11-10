using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>Hàm tiện ích cho test Handover Completion &amp; Transaction Receipt.</summary>
public static class ReceiptHelper
{
    public sealed record ItemInfo(Guid ItemId, Guid CategoryId, string Code, decimal Estimated);

    /// <summary>Tạo yêu cầu (citizen) rồi Recycler chấp nhận. Trả về id.</summary>
    public static async Task<Guid> AcceptedRequestAsync(string[]? categories = null, decimal weight = 10m)
    {
        var token = await HandoverHelper.Citizen();
        var r = (await ApiJson.PostAsync(HandoverRoutes.Base,
            await HandoverHelper.PayloadAsync(categoryCodes: categories, weight: weight), token)).ShouldBe(201);
        var id = r.Data().GetProperty("id").GetGuid();
        (await ApiJson.PostAsync(HandoverRoutes.Accept(id), new { }, await HandoverHelper.Collector())).ShouldBe(200);
        return id;
    }

    public static async Task<List<ItemInfo>> ItemsAsync(Guid requestId)
    {
        var r = (await Api.GetAsync(HandoverRoutes.RcOne(requestId), await HandoverHelper.Collector())).ShouldBe(200);
        return r.Data().GetProperty("items").EnumerateArray().Select(i => new ItemInfo(
            i.GetProperty("id").GetGuid(),
            i.GetProperty("wasteCategoryId").GetGuid(),
            i.GetProperty("wasteCategoryCode").GetString()!,
            i.GetProperty("estimatedWeightKg").GetDecimal())).ToList();
    }

    /// <summary>Body biên nhận: mỗi dòng có sẵn nhận một khối lượng, kèm ảnh cân hợp lệ.</summary>
    public static object Body(IEnumerable<ItemInfo> items, decimal[] weights, string[]? photos = null,
        IEnumerable<object>? extras = null)
    {
        var list = new List<object>();
        var i = 0;
        foreach (var it in items) list.Add(new { itemId = it.ItemId, actualWeightKg = weights[Math.Min(i++, weights.Length - 1)] });
        if (extras != null) list.AddRange(extras);
        return new
        {
            items = list,
            scaleProofPhotoUrls = photos ?? new[] { "https://example.com/scale/1.jpg" },
        };
    }

    /// <summary>Tạo yêu cầu đã chấp nhận và lập biên nhận. Trả về (requestId, items).</summary>
    public static async Task<(Guid Id, List<ItemInfo> Items)> AwaitingRequestAsync(
        string[]? categories = null, decimal estimated = 10m, decimal? actual = null)
    {
        var id = await AcceptedRequestAsync(categories, estimated);
        var items = await ItemsAsync(id);
        var weights = new[] { actual ?? estimated };
        (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), Body(items, weights), await HandoverHelper.Collector())).ShouldBe(201);
        return (id, items);
    }

    public static async Task CompleteQuietlyAsync(Guid id) =>
        await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, await HandoverHelper.Citizen());

    public static async Task<JsonElement> UserRequestAsync(Guid id) =>
        (await Api.GetAsync(HandoverRoutes.One(id), await HandoverHelper.Citizen())).ShouldBe(200).Data();
}

/// <summary>
/// Test cho module Handover Completion &amp; Transaction Receipt (UC R-06, U-09, U-12; BR-HND-04/05/07, BR-GAM-01).
/// Dùng citizen + collector (Recycler Verified/Online); bảng giá nền: plastic 8.000đ/kg (min 2kg), paper 3.000đ (min 5kg),
/// glass 1.000đ, metal 15.000đ/kg (min 1kg), cardboard không thu mua.
/// </summary>
[Trait("Category", "Receipt")]
public class ReceiptTests
{
    private static Task<string> Citizen() => HandoverHelper.Citizen();
    private static Task<string> Collector() => HandoverHelper.Collector();

    private static async Task Prepare()
    {
        await HandoverHelper.EnsureRecyclerReadyAsync();
        await HandoverHelper.ClearCitizenActiveAsync();
    }

    // ---------------- 1. Phân quyền ----------------
    private static readonly string Zero = "00000000-0000-0000-0000-000000000001";

    private static readonly (string Method, string Path)[] UserEndpoints =
    {
        ("GET", $"/api/v1/handover-requests/{Zero}/receipt"),
        ("POST", $"/api/v1/handover-requests/{Zero}/receipt/confirm"),
        ("GET", HandoverRoutes.Transactions),
    };

    private static readonly (string Method, string Path)[] RecyclerEndpoints =
    {
        ("GET", $"/api/v1/recycler/handover-requests/{Zero}/receipt"),
        ("POST", $"/api/v1/recycler/handover-requests/{Zero}/receipt"),
        ("POST", $"/api/v1/recycler/handover-requests/{Zero}/receipt/preview"),
        ("GET", HandoverRoutes.RecyclerTransactions),
    };

    public static TheoryData<string, string> AllEndpoints
    {
        get { var d = new TheoryData<string, string>(); foreach (var (m, p) in UserEndpoints.Concat(RecyclerEndpoints)) d.Add(m, p); return d; }
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

    // ---------------- 2. Xem trước biên nhận ----------------
    [Fact]
    public async Task Preview_ComputesTotals_AndDoesNotSave()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            var r = (await ApiJson.PostAsync(HandoverRoutes.RcPreview(id), ReceiptHelper.Body(items, new[] { 12.5m }), await Collector())).ShouldBe(200);
            var d = r.Data();
            Assert.Equal(12.5m, d.GetProperty("totalWeightKg").GetDecimal());
            Assert.Equal(100000m, d.GetProperty("totalAmountCash").GetDecimal());   // 12.5 kg x 8.000
            Assert.Equal(125, d.GetProperty("points").GetInt64());                   // 10 điểm / kg
            Assert.False(d.GetProperty("pointsCredited").GetBoolean());
            Assert.Equal(JsonValueKind.Null, d.GetProperty("receiptId").ValueKind);

            // Không lưu gì: chưa có biên nhận, yêu cầu vẫn Accepted
            (await Api.GetAsync(HandoverRoutes.RcReceipt(id), await Collector())).ShouldBe(404);
            Assert.Equal("Accepted", (await ReceiptHelper.UserRequestAsync(id)).Str("status"));
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    // ---------------- 3. Lập biên nhận (R-06) ----------------
    [Fact]
    public async Task Create_Valid_Returns201_AwaitingConfirmation()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            var r = (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), ReceiptHelper.Body(items, new[] { 12.5m }), await Collector())).ShouldBe(201);
            var d = r.Data();
            Assert.Matches(@"^RCP-\d{4}-\d{4}$", d.Str("receiptCode")!);
            Assert.Equal("AwaitingConfirmation", d.Str("requestStatus"));
            Assert.Equal(12.5m, d.GetProperty("totalWeightKg").GetDecimal());
            Assert.Equal(100000m, d.GetProperty("totalAmountCash").GetDecimal());
            Assert.False(d.GetProperty("isLocked").GetBoolean());
            Assert.False(d.GetProperty("canConfirm").GetBoolean());                  // phía Recycler không xác nhận được
            var line = d.GetProperty("lines")[0];
            Assert.Equal(8000m, line.GetProperty("unitPricePerKg").GetDecimal());
            Assert.Equal(100000m, line.GetProperty("subTotal").GetDecimal());
            Assert.False(line.GetProperty("isExtra").GetBoolean());
            Assert.Single(d.GetProperty("scaleProofPhotoUrls").EnumerateArray());

            var req = await ReceiptHelper.UserRequestAsync(id);
            Assert.Equal("AwaitingConfirmation", req.Str("status"));
            Assert.True(req.GetProperty("hasReceipt").GetBoolean());
            Assert.False(req.GetProperty("canCancel").GetBoolean());
        }
        finally { await ReceiptHelper.CompleteQuietlyAsync(id); }
    }

    [Fact]
    public async Task Create_MultipleItems_SumsWeightAndAmount()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync(new[] { "plastic", "metal" }, 10m);
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            var weights = items.Select(i => i.Code == "plastic" ? 10m : 4m).ToArray();
            var r = (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), ReceiptHelper.Body(items, weights), await Collector())).ShouldBe(201);
            Assert.Equal(14m, r.Data().GetProperty("totalWeightKg").GetDecimal());
            Assert.Equal(10m * 8000m + 4m * 15000m, r.Data().GetProperty("totalAmountCash").GetDecimal());
        }
        finally { await ReceiptHelper.CompleteQuietlyAsync(id); }
    }

    [Fact]
    public async Task Create_ExtraItem_FromAcceptedCatalog_IsMarkedExtra()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            var paper = await RecyclerHelper.GetCategoryAsync("paper");
            var extras = new object[] { new { wasteCategoryId = paper.Id, actualWeightKg = 6m } };
            var r = (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id),
                ReceiptHelper.Body(items, new[] { 10m }, extras: extras), await Collector())).ShouldBe(201);

            var lines = r.Data().GetProperty("lines").EnumerateArray().ToList();
            Assert.Equal(2, lines.Count);
            var extra = lines.Single(l => l.GetProperty("isExtra").GetBoolean());
            Assert.Equal("paper", extra.Str("wasteCategoryCode"));
            Assert.Equal(3000m, extra.GetProperty("unitPricePerKg").GetDecimal());
            Assert.Equal(80000m + 18000m, r.Data().GetProperty("totalAmountCash").GetDecimal());
        }
        finally { await ReceiptHelper.CompleteQuietlyAsync(id); }
    }

    [Fact]
    public async Task Create_ExtraItem_NotInCatalog_Returns400()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            var cardboard = await RecyclerHelper.GetCategoryAsync(RecyclerHelper.ReservedCategoryCode);
            var extras = new object[] { new { wasteCategoryId = cardboard.Id, actualWeightKg = 3m } };
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id),
                ReceiptHelper.Body(items, new[] { 10m }, extras: extras), await Collector())).ShouldFailWith(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Create_ExtraItem_OfCategoryAlreadyInRequest_Returns400()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            var extras = new object[] { new { wasteCategoryId = items[0].CategoryId, actualWeightKg = 3m } };
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id),
                ReceiptHelper.Body(items, new[] { 10m }, extras: extras), await Collector())).ShouldFailWith(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Create_MissingWeightOfARequestedItem_Returns400()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync(new[] { "plastic", "metal" });
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id),
                ReceiptHelper.Body(items.Take(1), new[] { 10m }), await Collector())).ShouldFailWith(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("10.555")]
    public async Task Create_InvalidWeight_Returns400(string weight)
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            var raw = $@"{{""items"":[{{""itemId"":""{items[0].ItemId}"",""actualWeightKg"":{weight}}}],
                ""scaleProofPhotoUrls"":[""https://example.com/s.jpg""]}}";
            (await ApiJson.RawAsync(HttpMethod.Post, HandoverRoutes.RcReceipt(id), raw, await Collector())).ShouldBeClientError();
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Create_DuplicateItem_Returns400()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            var body = new
            {
                items = new object[]
                {
                    new { itemId = items[0].ItemId, actualWeightKg = 5m },
                    new { itemId = items[0].ItemId, actualWeightKg = 5m },
                },
                scaleProofPhotoUrls = new[] { "https://example.com/s.jpg" },
            };
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), body, await Collector())).ShouldFailWith(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Create_ItemOfAnotherRequest_Returns400()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var body = new
            {
                items = new object[] { new { itemId = Guid.NewGuid(), actualWeightKg = 5m } },
                scaleProofPhotoUrls = new[] { "https://example.com/s.jpg" },
            };
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), body, await Collector())).ShouldFailWith(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Create_NoScalePhoto_Returns400_BR_HND_04()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id),
                ReceiptHelper.Body(items, new[] { 10m }, photos: new string[0]), await Collector())).ShouldBe(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Create_MoreThan5ScalePhotos_Returns400()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            var photos = Enumerable.Range(1, 6).Select(i => $"https://example.com/s{i}.jpg").ToArray();
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id),
                ReceiptHelper.Body(items, new[] { 10m }, photos: photos), await Collector())).ShouldBe(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Create_InvalidPhotoUrl_Returns400()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id),
                ReceiptHelper.Body(items, new[] { 10m }, photos: new[] { "not-a-url" }), await Collector())).ShouldFailWith(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Create_ForPendingRequest_Returns400()
    {
        await Prepare();
        var id = await HandoverHelper.CreateAsync();   // Pending, chưa Accept
        try
        {
            var body = new
            {
                items = new object[] { new { itemId = Guid.NewGuid(), actualWeightKg = 5m } },
                scaleProofPhotoUrls = new[] { "https://example.com/s.jpg" },
            };
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), body, await Collector())).ShouldFailWith(400);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Create_ForUnknownRequest_Returns404()
    {
        var body = new
        {
            items = new object[] { new { itemId = Guid.NewGuid(), actualWeightKg = 5m } },
            scaleProofPhotoUrls = new[] { "https://example.com/s.jpg" },
        };
        (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(Guid.NewGuid()), body, await Collector())).ShouldBe(404);
    }

    [Fact]
    public async Task Create_ByAnotherRecycler_Returns404_BR_HND_03()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var body = new
            {
                items = new object[] { new { itemId = Guid.NewGuid(), actualWeightKg = 5m } },
                scaleProofPhotoUrls = new[] { "https://example.com/s.jpg" },
            };
            var other = await RecyclerHelper.PendingRecyclerToken();
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), body, other)).ShouldBe(404);
            (await Api.GetAsync(HandoverRoutes.RcReceipt(id), other)).ShouldBe(404);
        }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Create_Twice_Returns400()
    {
        await Prepare();
        var (id, items) = await ReceiptHelper.AwaitingRequestAsync();
        try
        {
            (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), ReceiptHelper.Body(items, new[] { 10m }), await Collector())).ShouldFailWith(400);
        }
        finally { await ReceiptHelper.CompleteQuietlyAsync(id); }
    }

    [Fact]
    public async Task Create_ForCancelledRequest_Returns400()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        var items = await ReceiptHelper.ItemsAsync(id);
        (await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "Đổi ý" }, await Citizen())).ShouldBe(200);
        (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), ReceiptHelper.Body(items, new[] { 10m }), await Collector())).ShouldFailWith(400);
    }

    [Fact]
    public async Task Create_LocksUnitPrice_LaterPriceChangeDoesNotAffectReceipt()
    {
        await Prepare();
        var (id, _) = await ReceiptHelper.AwaitingRequestAsync(actual: 10m);
        try
        {
            var plastic = await RecyclerHelper.GetCategoryAsync("plastic");
            (await ApiJson.PutAsync(RecyclerRoutes.Prices,
                new { items = new[] { RecyclerHelper.BulkItem(plastic.Id, 9999m, 2m) } }, await Collector())).ShouldBe(200);

            var r = (await Api.GetAsync(HandoverRoutes.Receipt(id), await Citizen())).ShouldBe(200).Data();
            Assert.Equal(8000m, r.GetProperty("lines")[0].GetProperty("unitPricePerKg").GetDecimal());
            Assert.Equal(80000m, r.GetProperty("totalAmountCash").GetDecimal());
        }
        finally
        {
            await HandoverHelper.EnsureRecyclerReadyAsync();
            await ReceiptHelper.CompleteQuietlyAsync(id);
        }
    }

    // ---------------- 4. Cảnh báo lệch khối lượng (MSG-HND-04) ----------------
    [Fact]
    public async Task Create_DeviationOverThreshold_ReturnsWarning()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try
        {
            var items = await ReceiptHelper.ItemsAsync(id);
            var r = (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), ReceiptHelper.Body(items, new[] { 20m }), await Collector())).ShouldBe(201);
            Assert.Equal("MSG-HND-04", r.MessageCode());
            Assert.True(r.Data().GetProperty("hasDeviationWarning").GetBoolean());
            Assert.Equal(100m, r.Data().GetProperty("deviationPercent").GetDecimal());   // 10 -> 20 kg

            var mine = (await Api.GetAsync(HandoverRoutes.Receipt(id), await Citizen())).ShouldBe(200);
            Assert.Equal("MSG-HND-04", mine.MessageCode());
        }
        finally { await ReceiptHelper.CompleteQuietlyAsync(id); }
    }

    [Fact]
    public async Task Create_DeviationWithinThreshold_NoWarning()
    {
        await Prepare();
        var (id, _) = await ReceiptHelper.AwaitingRequestAsync(actual: 10.5m);   // lệch 5%
        try
        {
            var r = (await Api.GetAsync(HandoverRoutes.Receipt(id), await Citizen())).ShouldBe(200);
            Assert.False(r.Data().GetProperty("hasDeviationWarning").GetBoolean());
            Assert.Equal(5m, r.Data().GetProperty("deviationPercent").GetDecimal());
            Assert.NotEqual("MSG-HND-04", r.MessageCode());
        }
        finally { await ReceiptHelper.CompleteQuietlyAsync(id); }
    }

    // ---------------- 5. Trạng thái sau khi lập biên nhận ----------------
    [Fact]
    public async Task AfterReceipt_UserAndRecyclerCannotCancel_NorUserEdit()
    {
        await Prepare();
        var (id, _) = await ReceiptHelper.AwaitingRequestAsync();
        try
        {
            (await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "x" }, await Citizen())).ShouldFailWith(400);
            (await ApiJson.PostAsync(HandoverRoutes.RcCancel(id), new { reason = "x" }, await Collector())).ShouldFailWith(400);
            (await ApiJson.PutAsync(HandoverRoutes.One(id), await HandoverHelper.PayloadAsync(), await Citizen())).ShouldFailWith(400);
        }
        finally { await ReceiptHelper.CompleteQuietlyAsync(id); }
    }

    [Fact]
    public async Task AwaitingRequest_IsListedUnderThatStatus()
    {
        await Prepare();
        var (id, _) = await ReceiptHelper.AwaitingRequestAsync();
        try
        {
            var q = new Dictionary<string, string> { ["status"] = "AwaitingConfirmation" };
            var mine = (await Api.GetAsync(HandoverRoutes.Base, await Citizen(), q)).ShouldBe(200).Data();
            Assert.Contains(mine.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == id);
            var rc = (await Api.GetAsync(HandoverRoutes.RecyclerBase, await Collector(), q)).ShouldBe(200).Data();
            Assert.Contains(rc.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == id);
        }
        finally { await ReceiptHelper.CompleteQuietlyAsync(id); }
    }

    // ---------------- 6. Xem biên nhận ----------------
    [Fact]
    public async Task User_GetReceipt_ShowsDetails_AndCanConfirm()
    {
        await Prepare();
        var (id, _) = await ReceiptHelper.AwaitingRequestAsync(actual: 12.5m);
        try
        {
            var d = (await Api.GetAsync(HandoverRoutes.Receipt(id), await Citizen())).ShouldBe(200).Data();
            Assert.True(d.GetProperty("canConfirm").GetBoolean());
            Assert.Equal(100000m, d.GetProperty("totalAmountCash").GetDecimal());
            Assert.Equal(125, d.GetProperty("points").GetInt64());
            Assert.False(d.GetProperty("pointsCredited").GetBoolean());
            Assert.False(string.IsNullOrEmpty(d.GetProperty("recycler").Str("name")));
        }
        finally { await ReceiptHelper.CompleteQuietlyAsync(id); }
    }

    [Fact]
    public async Task User_GetReceipt_WhenNoneOrNotOwner_Returns404()
    {
        await Prepare();
        var accepted = await ReceiptHelper.AcceptedRequestAsync();       // chưa có biên nhận
        var (awaiting, _) = await ReceiptHelper.AwaitingRequestAsync();
        try
        {
            (await Api.GetAsync(HandoverRoutes.Receipt(accepted), await Citizen())).ShouldBe(404);
            (await Api.GetAsync(HandoverRoutes.Receipt(Guid.NewGuid()), await Citizen())).ShouldBe(404);
            var other = await HandoverHelper.SecondUserTokenAsync();
            (await Api.GetAsync(HandoverRoutes.Receipt(awaiting), other)).ShouldBe(404);
            (await ApiJson.PostAsync(HandoverRoutes.Confirm(awaiting), null, other)).ShouldBe(404);
        }
        finally
        {
            await ApiJson.PostAsync(HandoverRoutes.Cancel(accepted), new { reason = "cleanup" }, await Citizen());
            await ReceiptHelper.CompleteQuietlyAsync(awaiting);
        }
    }

    [Fact]
    public async Task Recycler_GetReceipt_Returns200()
    {
        await Prepare();
        var (id, _) = await ReceiptHelper.AwaitingRequestAsync();
        try
        {
            var d = (await Api.GetAsync(HandoverRoutes.RcReceipt(id), await Collector())).ShouldBe(200).Data();
            Assert.Equal(id, d.GetProperty("requestId").GetGuid());
            Assert.False(d.GetProperty("canConfirm").GetBoolean());
        }
        finally { await ReceiptHelper.CompleteQuietlyAsync(id); }
    }

    // ---------------- 7. Xác nhận hoàn tất (U-09) ----------------
    [Fact]
    public async Task Confirm_Returns200_Completed_LockedAndPointsCredited()
    {
        await Prepare();
        var (id, _) = await ReceiptHelper.AwaitingRequestAsync(actual: 12.5m);
        var r = (await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, await Citizen())).ShouldBe(200);
        Assert.Equal("MSG-HND-03", r.MessageCode());
        var d = r.Data();
        Assert.Equal("Completed", d.Str("requestStatus"));
        Assert.True(d.GetProperty("isLocked").GetBoolean());
        Assert.True(d.GetProperty("pointsCredited").GetBoolean());
        Assert.Equal(125, d.GetProperty("points").GetInt64());
        Assert.NotEqual(JsonValueKind.Null, d.GetProperty("confirmedByUserAt").ValueKind);
        Assert.False(d.GetProperty("canConfirm").GetBoolean());

        Assert.Equal("Completed", (await ReceiptHelper.UserRequestAsync(id)).Str("status"));
    }

    [Fact]
    public async Task Confirm_Twice_Returns400_NoDoublePoints()
    {
        await Prepare();
        var (id, _) = await ReceiptHelper.AwaitingRequestAsync();
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, await Citizen())).ShouldBe(200);
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, await Citizen())).ShouldFailWith(400);
    }

    [Fact]
    public async Task Confirm_WithoutReceipt_Returns400()
    {
        await Prepare();
        var id = await ReceiptHelper.AcceptedRequestAsync();
        try { (await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, await Citizen())).ShouldFailWith(400); }
        finally { await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "cleanup" }, await Citizen()); }
    }

    [Fact]
    public async Task Confirm_UnknownRequest_Returns404() =>
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(Guid.NewGuid()), null, await Citizen())).ShouldBe(404);

    [Fact]
    public async Task Completed_IsImmutable_BR_HND_07()
    {
        await Prepare();
        var (id, items) = await ReceiptHelper.AwaitingRequestAsync();
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, await Citizen())).ShouldBe(200);

        (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(id), ReceiptHelper.Body(items, new[] { 99m }), await Collector())).ShouldFailWith(400);
        (await ApiJson.PostAsync(HandoverRoutes.Cancel(id), new { reason = "x" }, await Citizen())).ShouldFailWith(400);
        (await ApiJson.PostAsync(HandoverRoutes.RcCancel(id), new { reason = "x" }, await Collector())).ShouldFailWith(400);
        (await ApiJson.PutAsync(HandoverRoutes.One(id), await HandoverHelper.PayloadAsync(), await Citizen())).ShouldFailWith(400);
    }

    // ---------------- 8. Lịch sử giao dịch (U-12) ----------------
    [Fact]
    public async Task UserHistory_ContainsCompletedOnly_WithTotals()
    {
        await Prepare();
        var (done, _) = await ReceiptHelper.AwaitingRequestAsync(actual: 12.5m);
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(done), null, await Citizen())).ShouldBe(200);
        var (awaiting, _) = await ReceiptHelper.AwaitingRequestAsync();
        try
        {
            var d = (await Api.GetAsync(HandoverRoutes.Transactions, await Citizen())).ShouldBe(200).Data();
            var rows = d.GetProperty("items").EnumerateArray().ToList();

            var row = rows.Single(i => i.GetProperty("requestId").GetGuid() == done);
            Assert.Equal(12.5m, row.GetProperty("totalWeightKg").GetDecimal());
            Assert.Equal(100000m, row.GetProperty("totalAmountCash").GetDecimal());
            Assert.Equal(125, row.GetProperty("pointsAwarded").GetInt64());
            Assert.False(string.IsNullOrEmpty(row.Str("counterpartName")));
            Assert.Contains(row.GetProperty("categories").EnumerateArray(), c => c.GetString()!.Length > 0);

            // Giao dịch đang chờ xác nhận chưa xuất hiện
            Assert.DoesNotContain(rows, i => i.GetProperty("requestId").GetGuid() == awaiting);

            Assert.True(d.GetProperty("totalCount").GetInt32() >= 1);
            Assert.True(d.GetProperty("totalAmountCash").GetDecimal() >= 100000m);
            Assert.True(d.GetProperty("totalPoints").GetInt64() >= 125);
        }
        finally { await ReceiptHelper.CompleteQuietlyAsync(awaiting); }
    }

    [Fact]
    public async Task RecyclerHistory_ContainsCompletedTransaction()
    {
        await Prepare();
        var (id, _) = await ReceiptHelper.AwaitingRequestAsync(actual: 10m);
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, await Citizen())).ShouldBe(200);

        var d = (await Api.GetAsync(HandoverRoutes.RecyclerTransactions, await Collector())).ShouldBe(200).Data();
        Assert.Contains(d.GetProperty("items").EnumerateArray(), i => i.GetProperty("requestId").GetGuid() == id);
        Assert.True(d.GetProperty("totalWeightKg").GetDecimal() >= 10m);
    }

    [Fact]
    public async Task History_FilterByDate_ExcludesOutsideRange()
    {
        await Prepare();
        var (id, _) = await ReceiptHelper.AwaitingRequestAsync();
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(id), null, await Citizen())).ShouldBe(200);

        var tomorrow = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd");
        var future = (await Api.GetAsync(HandoverRoutes.Transactions, await Citizen(),
            new Dictionary<string, string> { ["from"] = tomorrow })).ShouldBe(200).Data();
        Assert.Empty(future.GetProperty("items").EnumerateArray());
        Assert.Equal(0, future.GetProperty("totalCount").GetInt32());

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var current = (await Api.GetAsync(HandoverRoutes.Transactions, await Citizen(),
            new Dictionary<string, string> { ["from"] = yesterday, ["to"] = today })).ShouldBe(200).Data();
        Assert.Contains(current.GetProperty("items").EnumerateArray(), i => i.GetProperty("requestId").GetGuid() == id);
    }

    [Fact]
    public async Task History_PageSize_IsRespected()
    {
        await Prepare();
        var (a, _) = await ReceiptHelper.AwaitingRequestAsync();
        var (b, _) = await ReceiptHelper.AwaitingRequestAsync();
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(a), null, await Citizen())).ShouldBe(200);
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(b), null, await Citizen())).ShouldBe(200);

        var d = (await Api.GetAsync(HandoverRoutes.Transactions, await Citizen(),
            new Dictionary<string, string> { ["pageSize"] = "1", ["page"] = "1" })).ShouldBe(200).Data();
        Assert.Single(d.GetProperty("items").EnumerateArray());
        Assert.True(d.GetProperty("totalCount").GetInt32() >= 2);
    }
}
