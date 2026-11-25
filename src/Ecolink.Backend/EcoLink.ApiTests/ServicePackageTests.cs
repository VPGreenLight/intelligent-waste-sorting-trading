using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>
/// Bộ kiểm thử tích hợp HTTP toàn diện cho Module M-12 / FE-14 (Module 10 trong Report-5.2):
/// In-System Service Packages &amp; VietQR Integration (15 REST APIs: STT 115 - STT 129).
/// Đối chiếu trực tiếp 16 kịch bản ITC-PAY-001 -> ITC-PAY-016 trong
/// documents/test document/integration test/10_integration_test_in_system_service_packages_vietqr.md.
/// </summary>
[Trait("Category", "ServicePackages")]
public class ServicePackageTests
{
    private static readonly Guid CanonicalBasicPackageId = Guid.Parse("51000001-0000-0000-0000-000000000001");

    private static string UniqueUpperCode(string prefix) =>
        $"{prefix}_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static StringContent JsonBody(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static async Task<(Guid UserId, string Username, string Email, string Token)> RegisterCitizenAsync(
        string? customEmail = null,
        string? phoneNumber = null)
    {
        var suffix = TestData.Unique(8);
        var username = $"paycit_{suffix}";
        var email = customEmail ?? $"{username}@ecolink.test";
        var password = $"EcoLink@{suffix}1";

        var regRes = (await Api.PostJsonAsync(Routes.Register, new
        {
            username,
            email,
            password,
            fullName = $"Pay Citizen {suffix}",
            phoneNumber = phoneNumber ?? "0912345678"
        })).ShouldBeIn(200, 201);

        var token = Api.ExtractToken(regRes.Body)
            ?? throw new InvalidOperationException($"Register returned no token: {regRes.Raw}");
        var meRes = (await Api.GetAsync(Routes.Me, token)).ShouldBe(200);
        var userId = Api.Unwrap(meRes.Body).GetProperty("id").GetGuid();

        return (userId, username, email, token);
    }

    // ITC-PAY-001
    [Fact]
    public async Task PAY01_ServicePackageAndReconciliationEndpoints_EnforceAuthAndAdminRbac_401And403()
    {
        (await Api.GetAsync("/api/v1/service-packages")).ShouldBe(401);
        (await Api.GetAsync("/api/v1/service-packages/subscriptions/me")).ShouldBe(401);
        (await Api.GetAsync("/api/v1/service-payments/orders")).ShouldBe(401);

        var citizenToken = await Tokens.GetAsync("citizen");
        (await Api.GetAsync("/api/v1/admin/service-packages", citizenToken)).ShouldBe(403);
        (await Api.GetAsync("/api/v1/admin/reconciliations/ledger", citizenToken)).ShouldBe(403);

        var staffToken = await Tokens.GetAsync("staff");
        (await Api.GetAsync("/api/v1/admin/service-packages", staffToken)).ShouldBe(403);
        (await Api.GetAsync("/api/v1/admin/reconciliations/ledger", staffToken)).ShouldBe(403);
    }

    // ITC-PAY-002
    [Fact]
    public async Task PAY02_FreshCitizen_CatalogReturnsThreeCanonicalTiersAndDefaultBasicSubscription()
    {
        var (_, _, _, citizenToken) = await RegisterCitizenAsync();

        var catRes = (await Api.GetAsync("/api/v1/service-packages", citizenToken)).ShouldBe(200);
        var items = catRes.Data().EnumerateArray().ToList();
        Assert.Equal(2, items.Count);

        var codes = items.Select(i => i.GetProperty("packageCode").GetString()).ToHashSet();
        Assert.Contains("BASIC", codes);
        Assert.Contains("PREMIUM", codes);
        Assert.DoesNotContain("PRO", codes);

        var recyclerToken = await Tokens.GetAsync("recycler");
        var recCatRes = (await Api.GetAsync("/api/v1/service-packages", recyclerToken)).ShouldBe(200);
        var recItems = recCatRes.Data().EnumerateArray().ToList();
        Assert.True(recItems.Count >= 3);
        var recCodes = recItems.Select(i => i.GetProperty("packageCode").GetString()).ToHashSet();
        Assert.Contains("BASIC", recCodes);
        Assert.Contains("PREMIUM", recCodes);
        Assert.Contains("PRO", recCodes);

        var pro = recItems.First(i => i.GetProperty("packageCode").GetString() == "PRO");
        Assert.False(pro.GetProperty("isEligible").GetBoolean());

        var subRes = (await Api.GetAsync("/api/v1/service-packages/subscriptions/me", citizenToken)).ShouldBe(200);
        var subData = Api.Unwrap(subRes.Body);
        Assert.Equal("Basic", subData.GetProperty("currentTier").GetString());
        Assert.False(subData.GetProperty("entitlements").GetProperty("isAdFree").GetBoolean());
    }

    // ITC-PAY-003
    [Fact]
    public async Task PAY03_CreateOrder_RejectsFreeBasicTamperedAmountZeroAmountAndCitizenBuyingPro()
    {
        var (_, _, _, citizenToken) = await RegisterCitizenAsync();

        (await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "BASIC" }, citizenToken)).ShouldBe(400);

        (await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM", amount = 1000m }, citizenToken)).ShouldBe(400);

        var zeroRes = (await Api.PostJsonAsync("/api/v1/service-payments/create", new { amount = 0m }, citizenToken)).ShouldBe(400);
        Assert.Equal("MSG-PAY-03", zeroRes.Body.GetProperty("messageCode").GetString());

        (await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PRO" }, citizenToken)).ShouldBe(403);
    }

    // ITC-PAY-004
    [Fact]
    public async Task PAY04_CitizenCreatePremiumOrder_PollStatusAndList_BlocksOtherUserIdorAccess()
    {
        var (_, _, _, citizenA) = await RegisterCitizenAsync();
        var (_, _, _, citizenB) = await RegisterCitizenAsync();

        var createRes = (await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenA)).ShouldBe(200);
        Assert.Equal("MSG-PAY-01", createRes.Body.GetProperty("messageCode").GetString());

        var created = Api.Unwrap(createRes.Body);
        var orderCode = created.GetProperty("orderCode").GetString()!;
        Assert.Equal(59000m, created.GetProperty("amount").GetDecimal());
        Assert.Equal("PENDING", created.GetProperty("status").GetString());

        var pollRes = (await Api.GetAsync($"/api/v1/service-payments/orders/{orderCode}", citizenA)).ShouldBe(200);
        Assert.Equal("PENDING", Api.Unwrap(pollRes.Body).GetProperty("status").GetString());

        var listRes = (await Api.GetAsync("/api/v1/service-payments/orders", citizenA)).ShouldBe(200);
        Assert.True(Api.Unwrap(listRes.Body).GetProperty("totalRecords").GetInt32() >= 1);

        // Chống IDOR từ người dùng khác (MSG-ADM-04)
        var idorGet = (await Api.GetAsync($"/api/v1/service-payments/orders/{orderCode}", citizenB)).ShouldBe(403);
        Assert.Equal("MSG-ADM-04", idorGet.Body.GetProperty("messageCode").GetString());

        (await Api.PostJsonAsync($"/api/v1/service-payments/orders/{orderCode}/cancel", new { reason = "Chặn IDOR" }, citizenB)).ShouldBe(403);
    }

    // ITC-PAY-005
    [Fact]
    public async Task PAY05_CitizenCancelPendingOrder_MarksStatusCancelledAndReturnsMsgPay03()
    {
        var (_, _, _, citizenToken) = await RegisterCitizenAsync();

        var createRes = (await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200);
        var orderCode = Api.Unwrap(createRes.Body).GetProperty("orderCode").GetString()!;

        var cancelRes = (await Api.PostJsonAsync(
            $"/api/v1/service-payments/orders/{orderCode}/cancel",
            new { reason = "Khách đổi ý chọn gói dịch vụ khác" },
            citizenToken)).ShouldBe(200);

        Assert.Equal("MSG-PAY-03", cancelRes.Body.GetProperty("messageCode").GetString());
        Assert.Equal("CANCELLED", Api.Unwrap(cancelRes.Body).GetProperty("status").GetString());
    }

    // ITC-PAY-006
    [Fact]
    public async Task PAY06_SimulateExactTransfer_Activates30DayPremiumAndBlocksSubsequentCancel_409Conflict()
    {
        var (_, _, _, citizenToken) = await RegisterCitizenAsync();

        var createRes = (await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200);
        var orderCode = Api.Unwrap(createRes.Body).GetProperty("orderCode").GetString()!;

        var simRes = (await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{orderCode}", new { scenario = "Exact" })).ShouldBe(200);
        Assert.Equal("MSG-PAY-02", simRes.Body.GetProperty("messageCode").GetString());
        var simData = Api.Unwrap(simRes.Body);
        Assert.Equal("PAID", simData.GetProperty("status").GetString());
        Assert.Equal("Matched", simData.GetProperty("reconciliationStatus").GetString());

        var subsRes = (await Api.GetAsync("/api/v1/service-packages/subscriptions/me", citizenToken)).ShouldBe(200);
        var subsData = Api.Unwrap(subsRes.Body);
        Assert.Equal("Premium", subsData.GetProperty("currentTier").GetString());
        Assert.True(subsData.GetProperty("entitlements").GetProperty("isAdFree").GetBoolean());
        var activeSubs = subsData.GetProperty("activeSubscriptions");
        Assert.True(activeSubs.GetArrayLength() == 1);
        Assert.True(activeSubs[0].GetProperty("remainingDays").GetInt32() >= 29);

        // Đơn đã Paid không thể hủy -> 409 Conflict
        (await Api.PostJsonAsync($"/api/v1/service-payments/orders/{orderCode}/cancel", new { reason = "Thử hủy đơn đã Paid" }, citizenToken)).ShouldBe(409);
    }

    // ITC-PAY-007
    [Fact]
    public async Task PAY07_SamePackageEarlyRenewal_StacksExpiryDaysFromPreviousExpiry_BrPay04()
    {
        var (_, _, _, citizenToken) = await RegisterCitizenAsync();

        var firstOrder = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200).Body)
            .GetProperty("orderCode").GetString()!;
        (await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{firstOrder}", new { scenario = "Exact" })).ShouldBe(200);

        var secondOrder = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200).Body)
            .GetProperty("orderCode").GetString()!;
        (await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{secondOrder}", new { scenario = "Exact" })).ShouldBe(200);

        var subsData = Api.Unwrap((await Api.GetAsync("/api/v1/service-packages/subscriptions/me", citizenToken)).ShouldBe(200).Body);
        var activeSubs = subsData.GetProperty("activeSubscriptions").EnumerateArray().ToList();
        Assert.Equal(2, activeSubs.Count);
        Assert.True(activeSubs.Max(s => s.GetProperty("remainingDays").GetInt32()) >= 59);
    }

    // ITC-PAY-008
    [Fact]
    public async Task PAY08_WebhookWithForgedSignature_Returns400MsgPay05AndKeepsOrderPending()
    {
        var (_, _, _, citizenToken) = await RegisterCitizenAsync();

        var orderCode = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200).Body)
            .GetProperty("orderCode").GetString()!;

        var whRes = (await Api.PostJsonAsync("/api/v1/service-payments/webhook", new
        {
            orderCode,
            amount = 59000m,
            description = $"ECOLINK {orderCode}",
            reference = $"FT_FORGED_{TestData.Unique(6)}",
            signature = "FORGED_INVALID_SIGNATURE"
        })).ShouldBe(400);

        Assert.Equal("MSG-PAY-05", whRes.Body.GetProperty("messageCode").GetString());

        var orderCheck = Api.Unwrap((await Api.GetAsync($"/api/v1/service-payments/orders/{orderCode}", citizenToken)).ShouldBe(200).Body);
        Assert.Equal("PENDING", orderCheck.GetProperty("status").GetString());
    }

    // ITC-PAY-009
    [Fact]
    public async Task PAY09_DuplicateWebhookPayload_EnforcesIdempotencyAndActivatesExactlyOnce_BrPay02()
    {
        var (_, _, _, citizenToken) = await RegisterCitizenAsync();

        var orderCode = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200).Body)
            .GetProperty("orderCode").GetString()!;

        var uniqueRef = $"FT_IDEM_{TestData.Unique(8)}";
        var payload = new
        {
            orderCode,
            amount = 59000m,
            description = $"ECOLINK {orderCode}",
            reference = uniqueRef
        };

        var wh1 = (await Api.PostJsonAsync("/api/v1/service-payments/webhook", payload)).ShouldBe(200);
        Assert.Equal("PAID", Api.Unwrap(wh1.Body).GetProperty("status").GetString());

        var wh2 = (await Api.PostJsonAsync("/api/v1/service-payments/webhook", payload)).ShouldBe(200);
        Assert.Equal("ALREADY_PROCESSED", Api.Unwrap(wh2.Body).GetProperty("status").GetString());

        var subsData = Api.Unwrap((await Api.GetAsync("/api/v1/service-packages/subscriptions/me", citizenToken)).ShouldBe(200).Body);
        Assert.Equal(1, subsData.GetProperty("activeSubscriptions").GetArrayLength());
    }

    // ITC-PAY-010
    [Fact]
    public async Task PAY10_SimulateTransferAnomalyMatrix_HandlesUnderpaidOverpaidAndLateTransfer()
    {
        var (_, _, _, citizenToken) = await RegisterCitizenAsync();

        // 1. Underpaid -> giữ Pending, KHÔNG kích hoạt gói
        var underCode = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200).Body)
            .GetProperty("orderCode").GetString()!;
        var underRes = Api.Unwrap((await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{underCode}", new
        {
            scenario = "Underpaid",
            simulatedAmount = 45000m
        })).ShouldBe(200).Body);
        Assert.Equal("PENDING", underRes.GetProperty("status").GetString());
        Assert.Equal("Underpaid", underRes.GetProperty("reconciliationStatus").GetString());

        var tierAfterUnder = Api.Unwrap((await Api.GetAsync("/api/v1/service-packages/subscriptions/me", citizenToken)).ShouldBe(200).Body)
            .GetProperty("currentTier").GetString();
        Assert.Equal("Basic", tierAfterUnder);

        // 2. Overpaid -> Paid ngay & gắn cờ Overpaid
        var overCode = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200).Body)
            .GetProperty("orderCode").GetString()!;
        var overRes = Api.Unwrap((await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{overCode}", new
        {
            scenario = "Overpaid",
            simulatedAmount = 69000m
        })).ShouldBe(200).Body);
        Assert.Equal("PAID", overRes.GetProperty("status").GetString());
        Assert.Equal("Overpaid", overRes.GetProperty("reconciliationStatus").GetString());

        var tierAfterOver = Api.Unwrap((await Api.GetAsync("/api/v1/service-packages/subscriptions/me", citizenToken)).ShouldBe(200).Body)
            .GetProperty("currentTier").GetString();
        Assert.Equal("Premium", tierAfterOver);

        // 3. LateTransfer -> Expired & gắn cờ LateTransfer
        var lateCode = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200).Body)
            .GetProperty("orderCode").GetString()!;
        var lateRes = Api.Unwrap((await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{lateCode}", new
        {
            scenario = "LateTransfer"
        })).ShouldBe(200).Body);
        Assert.Equal("EXPIRED", lateRes.GetProperty("status").GetString());
        Assert.Equal("LateTransfer", lateRes.GetProperty("reconciliationStatus").GetString());
    }

    // ITC-PAY-011
    [Fact]
    public async Task PAY11_PendingKybRecycler_IneligibleForProAndRejectedWithMsgPay06()
    {
        var pendingRecyclerToken = await RecyclerHelper.PendingRecyclerToken();

        var catRes = (await Api.GetAsync("/api/v1/service-packages", pendingRecyclerToken)).ShouldBe(200);
        var proPkg = catRes.Data().EnumerateArray()
            .First(p => p.GetProperty("packageCode").GetString() == "PRO");
        Assert.False(proPkg.GetProperty("isEligible").GetBoolean());

        var createRes = (await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PRO" }, pendingRecyclerToken))
            .ShouldBe(400);
        Assert.Equal("MSG-PAY-06", createRes.Body.GetProperty("messageCode").GetString());
    }

    // ITC-PAY-012
    [Fact]
    public async Task PAY12_VerifiedRecyclerUpgradesPremiumToPro_UnlocksVerifiedBadgeAndFeaturedMapPin()
    {
        var (recUserId, username, password, citToken) = await OnboardingHelper.RegisterFreshCitizenAsync();
        var adminToken = await Tokens.GetAsync("admin");

        // 1. Nộp hồ sơ nâng cấp Recycler (tọa độ Hòa Lạc 21.0135, 105.5265) & Admin phê duyệt KYB
        var appPayload = OnboardingHelper.ValidApplicationPayload($"ProMap_{TestData.Unique(6)}");
        (await ApiJson.PostAsync(OnboardingRoutes.Base, appPayload, citToken)).ShouldBe(201);

        using var approveBody = JsonBody(new { isApproved = true, reviewNote = "Đạt chuẩn KYB cho kiểm thử gói Pro" });
        (await Api.SendAsync(HttpMethod.Put, OnboardingRoutes.Verification(recUserId), approveBody, adminToken)).ShouldBe(200);

        // 2. Đăng nhập lại lấy JWT mang Role = Recycler & bảo đảm có bảng giá + bật Online
        var loginRes = (await Api.PostJsonAsync(Routes.Login, new { usernameOrEmail = username, password })).ShouldBe(200);
        var recyclerToken = Api.ExtractToken(loginRes.Body)!;

        var existingPrices = (await Api.GetAsync(RecyclerRoutes.Prices, recyclerToken)).ShouldBe(200).Data();
        if (existingPrices.GetArrayLength() == 0)
        {
            var plastic = await RecyclerHelper.GetCategoryAsync("plastic");
            using var bulkPrices = JsonBody(new
            {
                items = new[]
                {
                    RecyclerHelper.BulkItem(plastic.Id, 7500m, 1m, true, "Sạch")
                }
            });
            (await Api.SendAsync(HttpMethod.Put, RecyclerRoutes.Prices, bulkPrices, recyclerToken)).ShouldBe(200);
        }

        using var onlineBody = JsonBody(new { isOnline = true });
        (await Api.SendAsync(HttpMethod.Patch, RecyclerRoutes.Availability, onlineBody, recyclerToken)).ShouldBe(200);

        // 3. Cơ sở mới duyệt KYB (0 đánh giá < 50): thẻ PRO vẫn hiển thị trên danh mục nhưng bị khóa đăng ký (MSG-PAY-06)
        var catRes = (await Api.GetAsync("/api/v1/service-packages", recyclerToken)).ShouldBe(200);
        var catalogItems = catRes.Data().EnumerateArray().ToList();
        var codes = catalogItems.Select(i => i.GetProperty("packageCode").GetString()).ToHashSet();
        Assert.Contains("BASIC", codes);
        Assert.Contains("PREMIUM", codes);
        Assert.Contains("PRO", codes);

        var proPkg = catalogItems.First(i => i.GetProperty("packageCode").GetString() == "PRO");
        Assert.False(proPkg.GetProperty("isEligible").GetBoolean());
        var proEval = proPkg.GetProperty("proEligibility");
        Assert.False(proEval.GetProperty("isPositiveFeedbackMet").GetBoolean());
        Assert.Equal(50, proEval.GetProperty("minRequiredReviews").GetInt32());

        var blockedProRes = (await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PRO" }, recyclerToken))
            .ShouldBe(400);
        Assert.Equal("MSG-PAY-06", blockedProRes.Body.GetProperty("messageCode").GetString());

        // 4. Mua gói PREMIUM (59,000đ) -> xác minh quyền lợi Ad-Free
        var premCode = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, recyclerToken)).ShouldBe(200).Body)
            .GetProperty("orderCode").GetString()!;
        (await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{premCode}", new { scenario = "Exact" })).ShouldBe(200);

        var premSubsData = Api.Unwrap((await Api.GetAsync("/api/v1/service-packages/subscriptions/me", recyclerToken)).ShouldBe(200).Body);
        Assert.Equal("Premium", premSubsData.GetProperty("currentTier").GetString());
        Assert.True(premSubsData.GetProperty("entitlements").GetProperty("isAdFree").GetBoolean());

        // 5. Admin tạo gói thương mại đẩy ghim không yêu cầu kiểm duyệt >= 50 đánh giá để xác minh luồng Tích xanh & GeoMatching
        var pinCode = UniqueUpperCode("QA_PIN");
        var createPinPkgRes = (await Api.PostJsonAsync("/api/v1/admin/service-packages", new
        {
            packageCode = pinCode,
            packageName = "Gói QA Ghim Nổi Bật",
            targetRole = "Recycler",
            description = "Gói phục vụ kiểm thử Tích xanh & Ghim nổi bật",
            requiresVerifiedKyb = false,
            priceVnd = 50_000m,
            durationDays = 30,
            displayOrder = 98,
            isActive = true,
            features = new
            {
                tierName = "Pro",
                adFree = true,
                ecoPlusBadge = true,
                verifiedBlueBadge = true,
                featuredMapMarker = true,
                mapPriorityBoost = 100,
                dailyPointMultiplier = 1.2m
            }
        }, adminToken)).ShouldBe(201);
        var pinPkgId = Api.Unwrap(createPinPkgRes.Body).GetProperty("id").GetGuid();

        try
        {
            var pinOrderCode = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = pinCode }, recyclerToken)).ShouldBe(200).Body)
                .GetProperty("orderCode").GetString()!;
            (await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{pinOrderCode}", new { scenario = "Exact" })).ShouldBe(200);

            var subsData = Api.Unwrap((await Api.GetAsync("/api/v1/service-packages/subscriptions/me", recyclerToken)).ShouldBe(200).Body);
            Assert.True(subsData.GetProperty("entitlements").GetProperty("hasVerifiedBlueBadge").GetBoolean());
            Assert.True(subsData.GetProperty("entitlements").GetProperty("isFeaturedMapMarker").GetBoolean());

            var mapRes = (await Api.GetAsync("/api/v1/recyclers/nearby", query: new Dictionary<string, string>
            {
                ["lat"] = "21.0135",
                ["lng"] = "105.5265",
                ["radiusKm"] = "10"
            })).ShouldBe(200);

            var targetPin = Api.Unwrap(mapRes.Body).GetProperty("items").EnumerateArray()
                .First(x => x.GetProperty("id").GetGuid() == recUserId);
            Assert.True(targetPin.GetProperty("isFeatured").GetBoolean());
            Assert.True(targetPin.GetProperty("hasVerifiedBadge").GetBoolean());
        }
        finally
        {
            using var offlineBody = JsonBody(new { isOnline = false });
            await Api.SendAsync(HttpMethod.Patch, RecyclerRoutes.Availability, offlineBody, recyclerToken);
            using var cleanupBody = JsonBody(new { isActive = false, reason = "Dọn gói kiểm thử PAY12" });
            await Api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/service-packages/{pinPkgId}/status", cleanupBody, adminToken);
        }
    }

    // ITC-PAY-013
    [Fact]
    public async Task PAY13_AdminServicePackages_CreateValidateToggleAndProtectDefaultBasicPackage()
    {
        var adminToken = await Tokens.GetAsync("admin");

        var listRes = (await Api.GetAsync("/api/v1/admin/service-packages", adminToken)).ShouldBe(200);
        Assert.True(listRes.Data().GetArrayLength() >= 3);

        // Tạo gói thương mại dùng một lần (từ 2,000đ trở lên)
        var customCode = UniqueUpperCode("QA_BOOST");
        var createRes = (await Api.PostJsonAsync("/api/v1/admin/service-packages", new
        {
            packageCode = customCode,
            packageName = "Gói QA Đẩy Tin",
            targetRole = "Recycler",
            description = "Gói phục vụ kiểm thử A-10",
            requiresVerifiedKyb = true,
            priceVnd = 50_000m,
            durationDays = 30,
            displayOrder = 99,
            isActive = true,
            features = new
            {
                adFree = true,
                ecoPlusBadge = false,
                verifiedBlueBadge = true,
                featuredMapMarker = true,
                mapPriorityBoost = 50,
                dailyPointMultiplier = 1.2m
            }
        }, adminToken)).ShouldBe(201);
        Assert.Equal("MSG-PAY-07", createRes.Body.GetProperty("messageCode").GetString());
        var createdPkgId = Api.Unwrap(createRes.Body).GetProperty("id").GetGuid();

        // Chặn tạo gói dưới ngưỡng 2,000đ PayOS -> 400
        (await Api.PostJsonAsync("/api/v1/admin/service-packages", new
        {
            packageCode = UniqueUpperCode("QA_LOW"),
            packageName = "Gói giá quá thấp",
            targetRole = "All",
            priceVnd = 1_500m,
            durationDays = 30
        }, adminToken)).ShouldBe(400);

        // Tắt gói tạm vừa tạo (dọn dữ liệu test) -> 200 OK
        using var disableCustom = JsonBody(new { isActive = false, reason = "Đóng gói kiểm thử A-10" });
        (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/service-packages/{createdPkgId}/status", disableCustom, adminToken)).ShouldBe(200);

        // Chặn tắt gói mặc định BASIC -> 400 (BR-PAY-05)
        using var disableBasic = JsonBody(new { isActive = false, reason = "Kiểm thử bảo vệ gói BASIC" });
        (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/service-packages/{CanonicalBasicPackageId}/status", disableBasic, adminToken)).ShouldBe(400);
    }

    // ITC-PAY-014
    [Fact]
    public async Task PAY14_AdminUpdatePackagePricing_GrandfathersExistingPaidOrders_BrPay05()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, _, _, citizenToken) = await RegisterCitizenAsync();

        // Tạo gói disposable không ảnh hưởng bảng giá chính thức BASIC/PREMIUM/PRO
        var pkgCode = UniqueUpperCode("QA_GF");
        var createPkg = (await Api.PostJsonAsync("/api/v1/admin/service-packages", new
        {
            packageCode = pkgCode,
            packageName = "Gói Kiểm Thử Grandfathering",
            targetRole = "All",
            requiresVerifiedKyb = false,
            priceVnd = 50_000m,
            durationDays = 30,
            displayOrder = 95,
            isActive = true,
            features = new { adFree = true, ecoPlusBadge = true, dailyPointMultiplier = 1.2m }
        }, adminToken)).ShouldBe(201);
        var pkgId = Api.Unwrap(createPkg.Body).GetProperty("id").GetGuid();

        try
        {
            // Citizen mua ở giá cũ 50,000đ / 30 ngày
            var oldOrderCode = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = pkgCode }, citizenToken)).ShouldBe(200).Body)
                .GetProperty("orderCode").GetString()!;
            (await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{oldOrderCode}", new { scenario = "Exact" })).ShouldBe(200);

            // Admin nâng giá lên 80,000đ / 45 ngày
            using var updateBody = JsonBody(new
            {
                packageCode = pkgCode,
                packageName = "Gói Grandfathering (Giá mới)",
                targetRole = "All",
                requiresVerifiedKyb = false,
                priceVnd = 80_000m,
                durationDays = 45,
                displayOrder = 95,
                isActive = true,
                features = new { adFree = true, ecoPlusBadge = true, dailyPointMultiplier = 1.3m }
            });
            var putRes = (await Api.SendAsync(HttpMethod.Put, $"/api/v1/admin/service-packages/{pkgId}", updateBody, adminToken)).ShouldBe(200);
            Assert.Equal("MSG-PAY-07", putRes.Body.GetProperty("messageCode").GetString());

            // Đơn cũ giữ nguyên snapshot 50,000đ / 30 ngày; đơn mới lấy giá 80,000đ / 45 ngày
            var oldOrderReload = Api.Unwrap((await Api.GetAsync($"/api/v1/service-payments/orders/{oldOrderCode}", citizenToken)).ShouldBe(200).Body);
            Assert.Equal(50_000m, oldOrderReload.GetProperty("amount").GetDecimal());
            Assert.Equal(30, oldOrderReload.GetProperty("durationDays").GetInt32());

            var newOrder = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = pkgCode }, citizenToken)).ShouldBe(200).Body);
            Assert.Equal(80_000m, newOrder.GetProperty("amount").GetDecimal());
            Assert.Equal(45, newOrder.GetProperty("durationDays").GetInt32());
        }
        finally
        {
            using var cleanup = JsonBody(new { isActive = false, reason = "Dọn gói kiểm thử Grandfathering" });
            await Api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/service-packages/{pkgId}/status", cleanup, adminToken);
        }
    }

    // ITC-PAY-015
    [Fact]
    public async Task PAY15_AdminReconciliation_LedgerApproveUnderpaidManualMatchOrphanAndBlockDoubleResolve()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, _, _, citizenToken) = await RegisterCitizenAsync();

        // 1. Tạo đơn Underpaid -> kiểm tra sổ cái & ApproveAndActivate -> chặn giải quyết lặp 409 Conflict
        var underCode = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200).Body)
            .GetProperty("orderCode").GetString()!;
        (await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{underCode}", new
        {
            scenario = "Underpaid",
            simulatedAmount = 45_000m
        })).ShouldBe(200);

        var ledgerRes = (await Api.GetAsync("/api/v1/admin/reconciliations/ledger", adminToken, new Dictionary<string, string>
        {
            ["onlyAnomalies"] = "true",
            ["search"] = underCode
        })).ShouldBe(200);
        var ledgerItems = Api.Unwrap(ledgerRes.Body).GetProperty("items").GetProperty("items");
        Assert.Contains(ledgerItems.EnumerateArray(), x =>
            x.GetProperty("referenceCode").GetString() == underCode
            && x.GetProperty("reconciliationStatus").GetString() == "Underpaid");

        using var resolveUnderBody = JsonBody(new
        {
            action = "ApproveAndActivate",
            resolutionBankRef = $"FT_COMP_{TestData.Unique(6)}",
            resolutionNote = "Khách đã chuyển bù đủ 14.000đ qua ngân hàng."
        });
        var resUnder = (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/reconciliations/orders/{underCode}/resolve", resolveUnderBody, adminToken))
            .ShouldBe(200);
        Assert.Equal("MSG-PAY-08", resUnder.Body.GetProperty("messageCode").GetString());
        Assert.Equal("Resolved", Api.Unwrap(resUnder.Body).GetProperty("reconciliationStatus").GetString());

        using var retryBody = JsonBody(new
        {
            action = "CloseAnomaly",
            resolutionNote = "Thử đối soát lại lần 2 trên đơn đã đóng."
        });
        (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/reconciliations/orders/{underCode}/resolve", retryBody, adminToken))
            .ShouldBe(409);

        // 2. Bắn webhook mồ côi (UnmatchedReference) -> khớp thủ công ManualMatchOrder vào đơn Pending mới
        var pendingOrderCode = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200).Body)
            .GetProperty("orderCode").GetString()!;

        var orphanSuffix = TestData.Unique(6);
        var orphanWhRes = (await Api.PostJsonAsync("/api/v1/service-payments/webhook", new
        {
            amount = 59_000m,
            description = $"CHUYEN KHOAN QUEN MA DON {orphanSuffix}",
            reference = $"REF_ORPHAN_{orphanSuffix}"
        })).ShouldBe(200);
        var webhookCode = Api.Unwrap(orphanWhRes.Body).GetProperty("webhookCode").GetString()!;
        Assert.StartsWith("WH-", webhookCode);

        using var matchBody = JsonBody(new
        {
            action = "ManualMatchOrder",
            targetOrderCode = pendingOrderCode,
            resolutionNote = "Khớp thủ công sao kê mồ côi vào đơn hàng Pending."
        });
        var matchedRes = (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/reconciliations/orders/{webhookCode}/resolve", matchBody, adminToken))
            .ShouldBe(200);
        var matchedData = Api.Unwrap(matchedRes.Body);
        Assert.Equal(pendingOrderCode, matchedData.GetProperty("referenceCode").GetString());
        Assert.Equal("Paid", matchedData.GetProperty("orderStatus").GetString());
        Assert.Equal("Resolved", matchedData.GetProperty("reconciliationStatus").GetString());
    }

    // ITC-PAY-016
    [Fact]
    public async Task PAY16_AdminExportReconciliation_CsvAndXlsx_MasksPiiSetsMsgAdm02HeaderAndLogsAudit()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var suffix = TestData.Unique(8);
        var rawEmail = $"secretpii_{suffix}@gmail.com";
        var rawPhone = "0987654321";

        var (_, _, _, citizenToken) = await RegisterCitizenAsync(rawEmail, rawPhone);
        var orderCode = Api.Unwrap((await Api.PostJsonAsync("/api/v1/service-payments/create", new { packageCode = "PREMIUM" }, citizenToken)).ShouldBe(200).Body)
            .GetProperty("orderCode").GetString()!;
        (await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{orderCode}", new { scenario = "Exact" })).ShouldBe(200);

        // Kiểm tra CSV kèm Header X-EcoLink-Message-Code = MSG-ADM-02 & che PII Email/SĐT (BR-ADM-06)
        var baseUrl = (Environment.GetEnvironmentVariable("ECOLINK_BASE_URL") ?? "http://localhost:5000").TrimEnd('/');
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var csvResp = await http.GetAsync("/api/v1/admin/reconciliations/export?format=csv");
        Assert.Equal(System.Net.HttpStatusCode.OK, csvResp.StatusCode);
        Assert.True(csvResp.Headers.TryGetValues("X-EcoLink-Message-Code", out var msgCodes));
        Assert.Contains("MSG-ADM-02", msgCodes!);

        var csvText = await csvResp.Content.ReadAsStringAsync();
        Assert.Contains(orderCode, csvText);
        Assert.Contains("***@gmail.com", csvText);
        Assert.DoesNotContain(rawEmail, csvText);
        Assert.DoesNotContain(rawPhone, csvText);

        // Kiểm tra XLSX trả về file OpenXML hợp lệ ('PK')
        var xlsxRes = (await Api.GetAsync("/api/v1/admin/reconciliations/export", adminToken, new Dictionary<string, string>
        {
            ["format"] = "xlsx"
        })).ShouldBe(200);
        Assert.StartsWith("PK", xlsxRes.Raw);
        Assert.Contains("sheet1.xml", xlsxRes.Raw);

        // Kiểm tra Audit Trail bất biến đã ghi nhận hành vi xuất bảng kê (BR-ADM-05)
        var auditRes = (await Api.GetAsync("/api/v1/admin/audit-logs", adminToken, new Dictionary<string, string>
        {
            ["action"] = "EXPORT_RECONCILIATION"
        })).ShouldBe(200);
        Assert.True(Api.Unwrap(auditRes.Body).GetProperty("items").GetArrayLength() >= 1);
    }
}
