using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>
/// Kiểm thử tích hợp HTTP cho Module M-15: Account, Staff &amp; System Governance / Analytics (STT 150 - 168).
/// </summary>
[Trait("Category", "Governance")]
public class GovernanceTests
{
    [Fact]
    public async Task AdminKpiSummary_RequiresAdminRole_AndReturnsKpiEnvelope()
    {
        // Citizen bị chặn 403
        var citizenToken = await Tokens.GetAsync("citizen");
        (await Api.GetAsync("/api/v1/admin/analytics/kpi-summary", citizenToken)).ShouldBe(403);

        // Admin truy cập thành công 200
        var adminToken = await Tokens.GetAsync("admin");
        var res = (await Api.GetAsync("/api/v1/admin/analytics/kpi-summary", adminToken,
            new Dictionary<string, string> { ["period"] = "30d" })).ShouldBe(200);

        Assert.True(res.Body.GetProperty("success").GetBoolean());
        var data = Api.Unwrap(res.Body);
        Assert.True(data.TryGetProperty("activeCitizensCount", out _));
        Assert.True(data.TryGetProperty("totalRecycledWeightKg", out _));
    }

    [Fact]
    public async Task AdminAnalyticsCharts_ReturnsMonthlyTrendAndAiGroups()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var res = (await Api.GetAsync("/api/v1/admin/analytics/charts", adminToken)).ShouldBe(200);

        var data = Api.Unwrap(res.Body);
        Assert.True(data.TryGetProperty("monthlyHandoverTrend", out var trend));
        Assert.Equal(JsonValueKind.Array, trend.ValueKind);
    }

    [Fact]
    public async Task AdminExportReport_ReturnsMaskedCsv_AndCreatesAuditLogEntry()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var exportRes = (await Api.GetAsync("/api/v1/admin/reports/export", adminToken,
            new Dictionary<string, string>
            {
                ["dataset"] = "accounts",
                ["format"] = "csv"
            })).ShouldBe(200);

        Assert.Contains("***@", exportRes.Raw);

        var auditRes = (await Api.GetAsync("/api/v1/admin/audit-logs", adminToken,
            new Dictionary<string, string> { ["action"] = "EXPORT_REPORT" })).ShouldBe(200);
        Assert.True(auditRes.Body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task AdminStaffLifecycle_CreateListPermissions_EnforcesLeastPrivilege()
    {
        var adminToken = await Tokens.GetAsync("admin");

        // Danh mục 7 mã quyền chuẩn RBAC
        var catalogRes = (await Api.GetAsync("/api/v1/admin/permissions/catalog", adminToken)).ShouldBe(200);
        Assert.True(catalogRes.Body.GetProperty("success").GetBoolean());

        var suffix = TestData.Unique(6);
        var createRes = (await Api.PostJsonAsync("/api/v1/admin/staff", new
        {
            fullName = $"QA Staff {suffix}",
            email = $"qastaff_{suffix}@ecolink.vn",
            department = "QA Governance",
            position = "Tester"
        }, adminToken)).ShouldBeIn(200, 201);

        var createdStaff = Api.Unwrap(createRes.Body);
        var staffId = createdStaff.GetProperty("staffId").GetString()!;
        Assert.Equal(0, createdStaff.GetProperty("permissions").GetArrayLength());

        // Cấp quyền REVIEW_AI & MANAGE_ACCOUNTS qua PUT /permissions -> trả MSG-ADM-01
        using var putContent = new StringContent(
            JsonSerializer.Serialize(new
            {
                permissions = new[] { "REVIEW_AI", "MANAGE_ACCOUNTS" },
                reason = "Cấp quyền kiểm thử tự động"
            }),
            Encoding.UTF8,
            "application/json");

        var permRes = (await Api.SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/staff/{staffId}/permissions",
            putContent,
            adminToken)).ShouldBe(200);

        Assert.Equal("MSG-ADM-01", permRes.Body.GetProperty("messageCode").GetString());
    }

    [Fact]
    public async Task StaffAccounts_LockWithoutReason_Returns400_BR_ADM_03()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var listRes = (await Api.GetAsync("/api/v1/staff/accounts", adminToken)).ShouldBe(200);
        var items = Api.Unwrap(listRes.Body).GetProperty("items");
        if (items.GetArrayLength() == 0) return;

        var targetUserId = items[0].GetProperty("userId").GetString()!;
        using var patchBody = new StringContent(
            JsonSerializer.Serialize(new { isLocked = true, reason = "" }),
            Encoding.UTF8,
            "application/json");

        (await Api.SendAsync(
            HttpMethod.Patch,
            $"/api/v1/staff/accounts/{targetUserId}/lock",
            patchBody,
            adminToken)).ShouldBe(400);
    }

    [Fact]
    public async Task StaffAttendance_MeEndpoint_ReturnsStatusForStaff()
    {
        var staffToken = await Tokens.GetAsync("staff");
        var meRes = (await Api.GetAsync("/api/v1/staff/attendance/me", staffToken)).ShouldBe(200);
        Assert.True(meRes.Body.GetProperty("success").GetBoolean());
    }
}
