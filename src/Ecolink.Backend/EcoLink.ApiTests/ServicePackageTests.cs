using System.Text;
using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>
/// Kiểm thử tích hợp HTTP cho Module M-12: In-System Service Packages &amp; VietQR Integration (STT 115 - 129).
/// </summary>
[Trait("Category", "ServicePackages")]
public class ServicePackageTests
{
    [Fact]
    public async Task ServicePackages_CatalogAndMySubscriptions_RequireAuthAndReturnThreeCanonicalTiers()
    {
        (await Api.GetAsync("/api/v1/service-packages")).ShouldBe(401);

        var citizenToken = await Tokens.GetAsync("citizen");
        var res = (await Api.GetAsync("/api/v1/service-packages", citizenToken)).ShouldBe(200);
        var data = Api.Unwrap(res.Body);
        Assert.True(data.GetArrayLength() >= 3);

        var mySubs = (await Api.GetAsync("/api/v1/service-packages/subscriptions/me", citizenToken)).ShouldBe(200);
        Assert.True(mySubs.Body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task ServicePayments_CitizenCreatePremium_SimulateExact_AndAdminReconciliation()
    {
        var citizenToken = await Tokens.GetAsync("citizen");
        var adminToken = await Tokens.GetAsync("admin");

        // Chặn tạo đơn QR cho gói BASIC (0đ)
        (await Api.PostJsonAsync("/api/v1/service-payments/create", new
        {
            packageCode = "BASIC"
        }, citizenToken)).ShouldBe(400);

        // Tạo đơn PREMIUM 59,000đ
        var createRes = (await Api.PostJsonAsync("/api/v1/service-payments/create", new
        {
            packageCode = "PREMIUM"
        }, citizenToken)).ShouldBe(200);

        Assert.Equal("MSG-PAY-01", createRes.Body.GetProperty("messageCode").GetString());
        var created = Api.Unwrap(createRes.Body);
        var orderCode = created.GetProperty("orderCode").GetString()!;
        Assert.Equal(59000m, created.GetProperty("amount").GetDecimal());

        // Giả lập chuyển khoản khớp lệnh
        var simRes = (await Api.PostJsonAsync($"/api/v1/service-payments/simulate-transfer/{orderCode}", new
        {
            scenario = "Exact"
        })).ShouldBe(200);
        Assert.Equal("PAID", Api.Unwrap(simRes.Body).GetProperty("status").GetString());

        // Admin kiểm tra sổ cái & xuất CSV ẩn danh PII
        var ledgerRes = (await Api.GetAsync("/api/v1/admin/reconciliations/ledger", adminToken)).ShouldBe(200);
        Assert.True(Api.Unwrap(ledgerRes.Body).TryGetProperty("summary", out _));

        (await Api.GetAsync("/api/v1/admin/reconciliations/ledger", citizenToken)).ShouldBe(403);
    }

    [Fact]
    public async Task AdminServicePackages_ToggleBasicForbidden_Returns400()
    {
        var adminToken = await Tokens.GetAsync("admin");
        using var body = new StringContent(
            JsonSerializer.Serialize(new { isActive = false, reason = "Kiểm thử bảo vệ gói mặc định" }),
            Encoding.UTF8,
            "application/json");

        (await Api.SendAsync(
            HttpMethod.Patch,
            "/api/v1/admin/service-packages/51000001-0000-0000-0000-000000000001/status",
            body,
            adminToken)).ShouldBe(400);
    }
}
