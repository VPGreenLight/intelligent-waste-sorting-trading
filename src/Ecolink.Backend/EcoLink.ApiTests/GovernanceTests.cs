using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>
/// Bộ kiểm thử tích hợp HTTP toàn diện cho Module M-15 (Module 9 trong Report-5.2):
/// Account, Staff &amp; System Governance / Analytics (19 endpoints: STT 150 - STT 168).
/// Đối chiếu trực tiếp 16 kịch bản ITC-GOV-001 -> ITC-GOV-016 trong
/// documents/test document/integration test/09_integration_test_account_staff_system_governance_analytics.md.
/// </summary>
[Trait("Category", "Governance")]
public class GovernanceTests
{
    private static StringContent JsonBody(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static async Task<(Guid UserId, string Username, string Token)> RegisterDisposableCitizenAsync()
    {
        var payload = TestData.ValidRegister();
        var regRes = (await Api.PostJsonAsync(Routes.Register, payload)).ShouldBeIn(200, 201);
        var token = Api.ExtractToken(regRes.Body)
            ?? throw new InvalidOperationException($"Missing token in register response: {regRes.Raw}");

        var meRes = (await Api.GetAsync(Routes.Me, token)).ShouldBe(200);
        var userId = Api.Unwrap(meRes.Body).GetProperty("id").GetGuid();
        return (userId, payload.Str("username"), token);
    }

    private static async Task<(Guid StaffId, string StaffCode, string Email, string Token)> CreateDisposableStaffAsync(
        string adminToken,
        string password = "StaffPass@123")
    {
        var suffix = TestData.Unique(8);
        var email = $"gov_staff_{suffix}@ecolink.vn";
        var createRes = (await Api.PostJsonAsync("/api/v1/admin/staff", new
        {
            fullName = $"QA Staff {suffix}",
            email,
            initialPassword = password,
            department = "QA Governance",
            position = "Automated Tester"
        }, adminToken)).ShouldBe(201);

        var created = Api.Unwrap(createRes.Body);
        var staffId = created.GetProperty("staffId").GetGuid();
        var staffCode = created.GetProperty("staffCode").GetString()!;

        var loginRes = (await Api.PostJsonAsync(Routes.Login, new
        {
            usernameOrEmail = email,
            password
        })).ShouldBe(200);

        var staffToken = Api.ExtractToken(loginRes.Body)
            ?? throw new InvalidOperationException($"Staff login returned no token: {loginRes.Raw}");

        return (staffId, staffCode, email, staffToken);
    }

    // ITC-GOV-001
    [Fact]
    public async Task GOV01_AdminGovernanceEndpoints_RequireAdminRole_Unauthenticated401AndNonAdmin403()
    {
        (await Api.GetAsync("/api/v1/admin/analytics/kpi-summary")).ShouldBe(401);

        var citizenToken = await Tokens.GetAsync("citizen");
        (await Api.GetAsync("/api/v1/admin/analytics/kpi-summary", citizenToken)).ShouldBe(403);
        (await Api.GetAsync("/api/v1/admin/staff", citizenToken)).ShouldBe(403);

        var staffToken = await Tokens.GetAsync("staff");
        (await Api.GetAsync("/api/v1/admin/analytics/kpi-summary", staffToken)).ShouldBe(403);
        (await Api.GetAsync("/api/v1/admin/staff", staffToken)).ShouldBe(403);
    }

    // ITC-GOV-002
    [Fact]
    public async Task GOV02_AdminKpiSummary_ReturnsValidMetricsEnvelopeAcrossPeriods()
    {
        var adminToken = await Tokens.GetAsync("admin");

        foreach (var period in new[] { "7d", "30d", "all" })
        {
            var res = (await Api.GetAsync("/api/v1/admin/analytics/kpi-summary", adminToken,
                new Dictionary<string, string> { ["period"] = period })).ShouldBe(200);

            Assert.True(res.Body.GetProperty("success").GetBoolean());
            var data = Api.Unwrap(res.Body);
            Assert.True(data.TryGetProperty("activeCitizensCount", out _));
            Assert.True(data.TryGetProperty("verifiedRecyclersCount", out _));
            Assert.True(data.TryGetProperty("completedHandoversCount", out _));
            Assert.True(data.TryGetProperty("totalRecycledWeightKg", out _));
            Assert.True(data.TryGetProperty("vietQrServiceRevenueVnd", out _));
            Assert.True(data.TryGetProperty("aiBenchmark", out _));
            Assert.True(data.TryGetProperty("operationalBacklog", out _));
        }
    }

    // ITC-GOV-003
    [Fact]
    public async Task GOV03_AdminAnalyticsCharts_ReturnsMonthlyTrendCategoryAndAiQuality()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var res = (await Api.GetAsync("/api/v1/admin/analytics/charts", adminToken,
            new Dictionary<string, string> { ["months"] = "6" })).ShouldBe(200);

        var data = Api.Unwrap(res.Body);
        Assert.True(data.TryGetProperty("monthlyHandoverTrend", out var trend));
        Assert.Equal(JsonValueKind.Array, trend.ValueKind);
        Assert.Equal(6, trend.GetArrayLength());

        Assert.True(data.TryGetProperty("wasteCategoryBreakdown", out var breakdown));
        Assert.Equal(JsonValueKind.Array, breakdown.ValueKind);

        Assert.True(data.TryGetProperty("aiQualityByGroup", out var aiQuality));
        Assert.Equal(JsonValueKind.Array, aiQuality.ValueKind);
    }

    // ITC-GOV-004
    [Fact]
    public async Task GOV04_AdminExportReportCsv_MasksPiiAndCreatesAuditLogEntry_BrAdm06()
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

        var auditData = Api.Unwrap(auditRes.Body);
        Assert.True(auditData.GetProperty("items").GetArrayLength() >= 1);
    }

    // ITC-GOV-005
    [Fact]
    public async Task GOV05_AdminExportReportXlsx_ReturnsValidWorkbookAndRejectsUnsupportedFormat()
    {
        var adminToken = await Tokens.GetAsync("admin");

        var xlsxRes = (await Api.GetAsync("/api/v1/admin/reports/export", adminToken,
            new Dictionary<string, string>
            {
                ["dataset"] = "handovers",
                ["format"] = "xlsx"
            })).ShouldBe(200);

        Assert.StartsWith("PK", xlsxRes.Raw);
        Assert.Contains("sheet1.xml", xlsxRes.Raw);

        (await Api.GetAsync("/api/v1/admin/reports/export", adminToken,
            new Dictionary<string, string>
            {
                ["dataset"] = "accounts",
                ["format"] = "pdf"
            })).ShouldBe(400);
    }

    // ITC-GOV-006
    [Fact]
    public async Task GOV06_AdminAuditLogs_SupportsFilteringByActionKeywordAndPagination()
    {
        var adminToken = await Tokens.GetAsync("admin");

        // Bảo đảm có ít nhất 1 log EXPORT_REPORT mới nhất
        (await Api.GetAsync("/api/v1/admin/reports/export", adminToken,
            new Dictionary<string, string> { ["dataset"] = "audit_logs", ["format"] = "csv" })).ShouldBe(200);

        var logRes = (await Api.GetAsync("/api/v1/admin/audit-logs", adminToken,
            new Dictionary<string, string>
            {
                ["action"] = "EXPORT_REPORT",
                ["entityType"] = "SystemReport",
                ["pageNumber"] = "1",
                ["pageSize"] = "10"
            })).ShouldBe(200);

        var items = Api.Unwrap(logRes.Body).GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.Equal("EXPORT_REPORT", items[0].GetProperty("action").GetString());
        Assert.Equal("SystemReport", items[0].GetProperty("entityType").GetString());
    }

    // ITC-GOV-007
    [Fact]
    public async Task GOV07_StaffAccounts_ListAndDetail_ReturnsExternalAccountStatsAnd404ForMissing()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (citizenId, username, _) = await RegisterDisposableCitizenAsync();

        var listRes = (await Api.GetAsync("/api/v1/staff/accounts", adminToken,
            new Dictionary<string, string> { ["role"] = "User", ["keyword"] = username })).ShouldBe(200);

        var listItems = Api.Unwrap(listRes.Body).GetProperty("items");
        Assert.True(listItems.GetArrayLength() >= 1);
        Assert.Contains(listItems.EnumerateArray(), x => x.GetProperty("userId").GetGuid() == citizenId);

        var detailRes = (await Api.GetAsync($"/api/v1/staff/accounts/{citizenId}", adminToken)).ShouldBe(200);
        var detail = Api.Unwrap(detailRes.Body);
        Assert.Equal(citizenId, detail.GetProperty("userId").GetGuid());
        Assert.True(detail.TryGetProperty("activityStats", out _));
        Assert.True(detail.TryGetProperty("moderationHistory", out _));

        (await Api.GetAsync($"/api/v1/staff/accounts/{Guid.NewGuid()}", adminToken)).ShouldBe(404);
    }

    // ITC-GOV-008
    [Fact]
    public async Task GOV08_StaffAccounts_LockWithoutValidReason_Returns400_BrAdm03()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (citizenId, _, _) = await RegisterDisposableCitizenAsync();

        using var emptyReason = JsonBody(new { isLocked = true, reason = "" });
        (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/accounts/{citizenId}/lock", emptyReason, adminToken))
            .ShouldBe(400);

        using var shortReason = JsonBody(new { isLocked = true, reason = "Quá ngắn" });
        (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/accounts/{citizenId}/lock", shortReason, adminToken))
            .ShouldBe(400);
    }

    // ITC-GOV-009
    [Fact]
    public async Task GOV09_StaffAccounts_LockAndUnlockCitizen_ImmediatelyBlocksAndRestoresJwtAccess_BrAdm03()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (citizenId, _, citizenToken) = await RegisterDisposableCitizenAsync();

        (await Api.GetAsync(Routes.Me, citizenToken)).ShouldBe(200);

        using var lockBody = JsonBody(new
        {
            isLocked = true,
            reason = "Tài khoản kiểm thử vi phạm chính sách hẹn thu gom",
            incrementWarning = true
        });
        var lockRes = (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/accounts/{citizenId}/lock", lockBody, adminToken))
            .ShouldBe(200);

        Assert.Equal("MSG-ADM-03", lockRes.Body.GetProperty("messageCode").GetString());
        Assert.Equal("Suspended", Api.Unwrap(lockRes.Body).GetProperty("status").GetString());
        Assert.True(Api.Unwrap(lockRes.Body).GetProperty("warningCount").GetInt32() >= 1);

        // AccountStatusGuardMiddleware chặn ngay JWT cũ với 403 MSG-AUTH-04
        var blockedMe = (await Api.GetAsync(Routes.Me, citizenToken)).ShouldBe(403);
        Assert.Equal("MSG-AUTH-04", blockedMe.Body.GetProperty("messageCode").GetString());

        using var unlockBody = JsonBody(new
        {
            isLocked = false,
            reason = "Đã xác minh giải trình và khôi phục hoạt động tài khoản"
        });
        var unlockRes = (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/accounts/{citizenId}/lock", unlockBody, adminToken))
            .ShouldBe(200);

        Assert.Equal("Active", Api.Unwrap(unlockRes.Body).GetProperty("status").GetString());
        (await Api.GetAsync(Routes.Me, citizenToken)).ShouldBe(200);
    }

    // ITC-GOV-010
    [Fact]
    public async Task GOV10_StaffAccounts_DeactivateCitizen_SoftDeactivatesPreservingRecord_BrAdm04()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (citizenId, _, citizenToken) = await RegisterDisposableCitizenAsync();

        using var deactBody = JsonBody(new
        {
            reason = "Biên bản vô hiệu hóa mềm tài khoản kiểm thử theo quy định BR-ADM-04"
        });
        var res = (await Api.SendAsync(HttpMethod.Delete, $"/api/v1/staff/accounts/{citizenId}", deactBody, adminToken))
            .ShouldBe(200);

        Assert.Equal("MSG-ADM-03", res.Body.GetProperty("messageCode").GetString());
        Assert.Equal("Deactivated", Api.Unwrap(res.Body).GetProperty("status").GetString());

        (await Api.GetAsync(Routes.Me, citizenToken)).ShouldBe(403);
        (await Api.GetAsync($"/api/v1/staff/accounts/{citizenId}", adminToken)).ShouldBe(200);
    }

    // ITC-GOV-011
    [Fact]
    public async Task GOV11_AdminStaff_CreateAndList_InitializesZeroDefaultPermissions_BrAdm01()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (staffId, staffCode, _, _) = await CreateDisposableStaffAsync(adminToken);

        Assert.StartsWith("NV-", staffCode);

        var listRes = (await Api.GetAsync("/api/v1/admin/staff", adminToken,
            new Dictionary<string, string> { ["keyword"] = staffCode })).ShouldBe(200);

        var staffArray = listRes.Body.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, staffArray.ValueKind);
        var match = staffArray.EnumerateArray().FirstOrDefault(x => x.GetProperty("staffId").GetGuid() == staffId);
        Assert.Equal(staffCode, match.GetProperty("staffCode").GetString());
        Assert.Equal(0, match.GetProperty("permissions").GetArrayLength());
    }

    // ITC-GOV-012
    [Fact]
    public async Task GOV12_AdminStaff_UpdateProfileAndResetPassword_AllowsLoginWithNewPassword()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (staffId, _, email, _) = await CreateDisposableStaffAsync(adminToken, "OldPass@123");

        using var putBody = JsonBody(new
        {
            fullName = "QA Staff Updated",
            phoneNumber = "0909888777",
            department = "Kiểm soát nội bộ",
            position = "Trưởng ca",
            resetTemporaryPassword = "NewPass@2026!"
        });

        var updateRes = (await Api.SendAsync(HttpMethod.Put, $"/api/v1/admin/staff/{staffId}", putBody, adminToken))
            .ShouldBe(200);

        Assert.Equal("QA Staff Updated", Api.Unwrap(updateRes.Body).GetProperty("fullName").GetString());

        var newLogin = (await Api.PostJsonAsync(Routes.Login, new
        {
            usernameOrEmail = email,
            password = "NewPass@2026!"
        })).ShouldBe(200);

        Assert.False(string.IsNullOrEmpty(Api.ExtractToken(newLogin.Body)));
    }

    // ITC-GOV-013
    [Fact]
    public async Task GOV13_AdminPermissions_CatalogAndDynamicRbacUpdate_AppliesImmediatelyWithoutRelogin_BrAdm02()
    {
        var adminToken = await Tokens.GetAsync("admin");

        var catalogRes = (await Api.GetAsync("/api/v1/admin/permissions/catalog", adminToken)).ShouldBe(200);
        Assert.Equal(7, catalogRes.Body.GetProperty("data").GetArrayLength());

        var (staffId, _, _, staffToken) = await CreateDisposableStaffAsync(adminToken);

        // Chưa cấp MANAGE_ACCOUNTS -> 403 MSG-ADM-04
        var deniedRes = (await Api.GetAsync("/api/v1/staff/accounts", staffToken)).ShouldBe(403);
        Assert.Equal("MSG-ADM-04", deniedRes.Body.GetProperty("messageCode").GetString());

        // Cấp quyền MANAGE_ACCOUNTS (sử dụng alias FE "account.manage" để kiểm chuẩn hóa 2 chiều)
        using var grantBody = JsonBody(new
        {
            permissions = new[] { "account.manage", "REVIEW_AI" },
            reason = "Cấp quyền kiểm duyệt tài khoản tức thời"
        });
        var grantRes = (await Api.SendAsync(HttpMethod.Put, $"/api/v1/admin/staff/{staffId}/permissions", grantBody, adminToken))
            .ShouldBe(200);
        Assert.Equal("MSG-ADM-01", grantRes.Body.GetProperty("messageCode").GetString());

        var detailRes = (await Api.GetAsync($"/api/v1/admin/staff/{staffId}/permissions", adminToken)).ShouldBe(200);
        Assert.Equal(2, Api.Unwrap(detailRes.Body).GetProperty("assignedPermissions").GetArrayLength());

        // Cùng JWT đó truy cập ngay lập tức thành công 200 OK
        (await Api.GetAsync("/api/v1/staff/accounts", staffToken)).ShouldBe(200);

        // Thu hồi toàn bộ quyền -> lập tức bị chặn 403 trở lại
        using var revokeBody = JsonBody(new
        {
            permissions = Array.Empty<string>(),
            reason = "Thu hồi toàn bộ quyền khi kết thúc ca trực"
        });
        (await Api.SendAsync(HttpMethod.Put, $"/api/v1/admin/staff/{staffId}/permissions", revokeBody, adminToken))
            .ShouldBe(200);
        (await Api.GetAsync("/api/v1/staff/accounts", staffToken)).ShouldBe(403);
    }

    // ITC-GOV-014
    [Fact]
    public async Task GOV14_AdminStaff_StatusToggleAndSelfLockGuard_BrAdm03()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var adminMe = (await Api.GetAsync(Routes.Me, adminToken)).ShouldBe(200);
        var adminId = Api.Unwrap(adminMe.Body).GetProperty("id").GetGuid();

        using var selfLockBody = JsonBody(new
        {
            status = "Suspended",
            reason = "Thử tự khóa chính tài khoản quản trị viên đang đăng nhập"
        });
        (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/staff/{adminId}/status", selfLockBody, adminToken))
            .ShouldBe(403);

        var (staffId, _, _, staffToken) = await CreateDisposableStaffAsync(adminToken);

        using var suspendBody = JsonBody(new
        {
            status = "Suspended",
            reason = "Tạm đình chỉ nhân viên để kiểm tra phiên làm việc"
        });
        var suspendRes = (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/staff/{staffId}/status", suspendBody, adminToken))
            .ShouldBe(200);
        Assert.Equal("MSG-ADM-03", suspendRes.Body.GetProperty("messageCode").GetString());

        (await Api.GetAsync(Routes.Me, staffToken)).ShouldBe(403);

        using var activateBody = JsonBody(new
        {
            status = "Active",
            reason = "Mở lại quyền làm việc cho nhân viên sau kiểm tra"
        });
        (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/staff/{staffId}/status", activateBody, adminToken))
            .ShouldBe(200);
        (await Api.GetAsync(Routes.Me, staffToken)).ShouldBe(200);
    }

    // ITC-GOV-015
    [Fact]
    public async Task GOV15_StaffAttendance_CheckInAwardsShiftPointsOncePerDay_IsolatesCitizenWallet_BrGam09()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, _, _, staffToken) = await CreateDisposableStaffAsync(adminToken);

        var checkInRes = (await Api.PostJsonAsync("/api/v1/staff/attendance/check-in", new
        {
            shiftNote = "Bắt đầu ca sáng kiểm duyệt hồ sơ"
        }, staffToken)).ShouldBe(201);

        var status = Api.Unwrap(checkInRes.Body);
        Assert.True(status.GetProperty("checkedInToday").GetBoolean());
        Assert.Equal(10, status.GetProperty("pointsEarnedToday").GetInt32());
        Assert.Equal(1, status.GetProperty("currentStreakDay").GetInt32());

        // Ví điểm công dân CurrentGreenPoints giữ nguyên = 0 (BR-GAM-08)
        var meRes = (await Api.GetAsync(Routes.Me, staffToken)).ShouldBe(200);
        Assert.Equal(0, Api.Unwrap(meRes.Body).GetProperty("currentGreenPoints").GetInt32());

        var attMe = (await Api.GetAsync("/api/v1/staff/attendance/me", staffToken)).ShouldBe(200);
        Assert.True(Api.Unwrap(attMe.Body).GetProperty("checkedInToday").GetBoolean());

        // Check-in lần 2 cùng ngày -> 400 Bad Request (BR-GAM-09)
        (await Api.PostJsonAsync("/api/v1/staff/attendance/check-in", new
        {
            shiftNote = "Điểm danh trùng lần 2"
        }, staffToken)).ShouldBe(400);
    }

    // ITC-GOV-016
    [Fact]
    public async Task GOV16_StaffAttendance_CheckOutAndAdminAttendanceReport_PersistsShiftRecord()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (staffId, staffCode, _, staffToken) = await CreateDisposableStaffAsync(adminToken);

        (await Api.PostJsonAsync("/api/v1/staff/attendance/check-in", new
        {
            shiftNote = "Nhận ca trực chiều"
        }, staffToken)).ShouldBe(201);

        var checkOutRes = (await Api.PostJsonAsync("/api/v1/staff/attendance/check-out", new
        {
            shiftNote = "Bàn giao hoàn tất toàn bộ hàng đợi ca chiều"
        }, staffToken)).ShouldBe(200);

        var checkOutData = Api.Unwrap(checkOutRes.Body);
        Assert.True(checkOutData.GetProperty("checkedOutToday").GetBoolean());
        Assert.Contains("hàng đợi ca chiều", checkOutData.GetProperty("todayShiftNote").GetString());

        var adminList = (await Api.GetAsync("/api/v1/admin/staff/attendance", adminToken,
            new Dictionary<string, string> { ["staffId"] = staffId.ToString() })).ShouldBe(200);

        var items = Api.Unwrap(adminList.Body).GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(staffCode, items[0].GetProperty("staffCode").GetString());
        Assert.Contains("hàng đợi ca chiều", items[0].GetProperty("shiftNote").GetString());
    }
}
