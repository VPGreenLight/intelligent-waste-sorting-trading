using System.IO.Compression;
using System.Net;
using System.Text;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Application.Validators;
using Ecolink.Domain.Constants;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Services;
using Ecolink.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ecolink.UnitTests.Module15_Governance;

/// <summary>
/// Bộ kiểm thử đơn vị toàn diện cho Module M-15 (Module 9 trong bộ Report-5.1):
/// Account, Staff &amp; System Governance / Analytics (FE-17 &amp; FE-18).
/// Chuẩn hóa 22 test cases (UTC001 -> UTC022: 10 Normal, 8 Abnormal, 4 Boundary)
/// đối chiếu trực tiếp với tài liệu đặc tả 09_unit_test_account_staff_system_governance_analytics.md.
/// </summary>
public class GovernanceModuleTests
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private static ICurrentUserService CreateCurrentUserMock(Guid userId, string role = "Admin")
    {
        var mock = Substitute.For<ICurrentUserService>();
        mock.UserId.Returns((Guid?)userId);
        mock.Role.Returns(role);
        mock.IsAuthenticated.Returns(true);
        mock.IpAddress.Returns("127.0.0.1");
        mock.UserAgent.Returns("EcoLink-UnitTest/1.0");
        return mock;
    }

    private static IHttpClientFactory CreateMockAiHttpFactory(
        HttpStatusCode status = HttpStatusCode.OK,
        string payload = """{"total_scans":120,"ood_count":6,"categories":{"plastic":80,"paper":40}}""")
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var handler = MockHttpMessageHandler.FromJson(status, payload);
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));
        return factory;
    }

    // =========================================================================
    // UTC001 [Normal] [A-01]: KPI điều hành chỉ tính đơn Completed, Recycler Verified & VietQR Paid (BR-ADM-07)
    // =========================================================================
    [Fact]
    public async Task UTC001_GetExecutiveKpiSummaryAsync_CountsOnlyCompletedDealsAndPaidVietQr_BrAdm07()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        var verifiedRecyclerId = Guid.NewGuid();
        var pendingRecyclerId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = adminId, Username = "admin1", Email = "admin@ecolink.vn", FullName = "Admin", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = citizenId, Username = "citizen1", Email = "citizen@email.vn", FullName = "Citizen One", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active },
            new User { Id = verifiedRecyclerId, Username = "rec_ok", Email = "rec_ok@thugom.vn", FullName = "Vựa Minh Khai", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active },
            new User { Id = pendingRecyclerId, Username = "rec_wait", Email = "rec_wait@thugom.vn", FullName = "Vựa Chờ Duyệt", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active }
        );

        db.RecyclerProfiles.AddRange(
            new RecyclerProfile { UserId = verifiedRecyclerId, FacilityName = "Vựa Minh Khai", BusinessAddress = "Quận 5", VerificationStatus = VerificationStatus.Verified, ActiveStatus = RecyclerActiveStatus.Online },
            new RecyclerProfile { UserId = pendingRecyclerId, FacilityName = "Vựa Chờ Duyệt", BusinessAddress = "Quận 1", VerificationStatus = VerificationStatus.Pending, ActiveStatus = RecyclerActiveStatus.Offline }
        );

        var completedReq1 = new HandoverRequest { Id = Guid.NewGuid(), RequestCode = "HND-001", UserId = citizenId, RecyclerId = verifiedRecyclerId, PickupAddress = "Q3", ProposedTimeSlot = DateTime.UtcNow, Status = HandoverStatus.Completed };
        var completedReq2 = new HandoverRequest { Id = Guid.NewGuid(), RequestCode = "HND-002", UserId = citizenId, RecyclerId = verifiedRecyclerId, PickupAddress = "Q3", ProposedTimeSlot = DateTime.UtcNow, Status = HandoverStatus.Completed };
        var cancelledReq = new HandoverRequest { Id = Guid.NewGuid(), RequestCode = "HND-003", UserId = citizenId, RecyclerId = verifiedRecyclerId, PickupAddress = "Q3", ProposedTimeSlot = DateTime.UtcNow, Status = HandoverStatus.Cancelled };

        db.HandoverRequests.AddRange(completedReq1, completedReq2, cancelledReq);
        db.HandoverReceipts.AddRange(
            new HandoverReceipt { Id = Guid.NewGuid(), RequestId = completedReq1.Id, ReceiptCode = "RCP-001", UserId = citizenId, RecyclerId = verifiedRecyclerId, TotalWeightKg = 6.5m, TotalAmountCash = 65_000m, ScaleProofPhotoUrl = "[]", CreatedAt = DateTime.UtcNow },
            new HandoverReceipt { Id = Guid.NewGuid(), RequestId = completedReq2.Id, ReceiptCode = "RCP-002", UserId = citizenId, RecyclerId = verifiedRecyclerId, TotalWeightKg = 3.5m, TotalAmountCash = 35_000m, ScaleProofPhotoUrl = "[]", CreatedAt = DateTime.UtcNow },
            new HandoverReceipt { Id = Guid.NewGuid(), RequestId = cancelledReq.Id, ReceiptCode = "RCP-003", UserId = citizenId, RecyclerId = verifiedRecyclerId, TotalWeightKg = 50.0m, TotalAmountCash = 500_000m, ScaleProofPhotoUrl = "[]", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var paymentService = Substitute.For<IPaymentService>();
        paymentService.GetAllOrdersAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new PayInOrderDto { OrderCode = "ORD-PAID", Amount = 199_000m, Status = "PAID", CreatedAt = DateTime.UtcNow },
            new PayInOrderDto { OrderCode = "ORD-PEND", Amount = 999_000m, Status = "PENDING", CreatedAt = DateTime.UtcNow }
        });

        var analyticsService = new AdminAnalyticsService(
            db,
            paymentService,
            new AuditLogService(db, CreateCurrentUserMock(adminId)),
            CreateMockAiHttpFactory(),
            TestDbContextFactory.CreateConfiguration(),
            NullLogger<AdminAnalyticsService>.Instance);

        var kpi = await analyticsService.GetExecutiveKpiSummaryAsync(new ExecutiveKpiQueryRequest { Period = "30d" });

        kpi.ActiveCitizensCount.Should().Be(1);
        kpi.VerifiedRecyclersCount.Should().Be(1);
        kpi.CompletedHandoversCount.Should().Be(2);
        kpi.TotalRecycledWeightKg.Should().Be(10.0m);
        kpi.GrossTradeValueVnd.Should().Be(100_000m);
        kpi.VietQrServiceRevenueVnd.Should().Be(199_000m);
        kpi.OperationalBacklog.PendingRecyclerKybCount.Should().Be(1);
    }

    // =========================================================================
    // UTC002 [Normal] [A-01]: Biểu đồ xu hướng tháng & cơ cấu phế liệu chỉ tổng hợp đơn Completed (BR-ADM-07)
    // =========================================================================
    [Fact]
    public async Task UTC002_GetAnalyticsChartsAsync_AggregatesCompletedMonthlyTrendAndCategoryShare_BrAdm07()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        var recyclerId = Guid.NewGuid();
        var catPlastic = new WasteCategory { Id = Guid.NewGuid(), Code = "plastic", Name = "Nhựa tái chế" };
        var catPaper = new WasteCategory { Id = Guid.NewGuid(), Code = "paper", Name = "Giấy & Carton" };

        var doneReq = new HandoverRequest { Id = Guid.NewGuid(), RequestCode = "HND-C1", UserId = citizenId, RecyclerId = recyclerId, PickupAddress = "Q1", ProposedTimeSlot = DateTime.UtcNow, Status = HandoverStatus.Completed };
        var pendReq = new HandoverRequest { Id = Guid.NewGuid(), RequestCode = "HND-P1", UserId = citizenId, RecyclerId = recyclerId, PickupAddress = "Q1", ProposedTimeSlot = DateTime.UtcNow, Status = HandoverStatus.Pending };

        db.WasteCategories.AddRange(catPlastic, catPaper);
        db.HandoverRequests.AddRange(doneReq, pendReq);
        db.HandoverRequestItems.AddRange(
            new HandoverRequestItem { Id = Guid.NewGuid(), RequestId = doneReq.Id, WasteCategoryId = catPlastic.Id, EstimatedWeightKg = 6m, ActualWeightKg = 6m },
            new HandoverRequestItem { Id = Guid.NewGuid(), RequestId = doneReq.Id, WasteCategoryId = catPaper.Id, EstimatedWeightKg = 4m, ActualWeightKg = 4m },
            new HandoverRequestItem { Id = Guid.NewGuid(), RequestId = pendReq.Id, WasteCategoryId = catPlastic.Id, EstimatedWeightKg = 90m }
        );
        db.HandoverReceipts.Add(new HandoverReceipt
        {
            Id = Guid.NewGuid(),
            RequestId = doneReq.Id,
            ReceiptCode = "RCP-C1",
            UserId = citizenId,
            RecyclerId = recyclerId,
            TotalWeightKg = 10m,
            TotalAmountCash = 75_000m,
            ScaleProofPhotoUrl = "[]",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var analyticsService = new AdminAnalyticsService(
            db,
            Substitute.For<IPaymentService>(),
            new AuditLogService(db, CreateCurrentUserMock(adminId)),
            CreateMockAiHttpFactory(),
            TestDbContextFactory.CreateConfiguration(),
            NullLogger<AdminAnalyticsService>.Instance);

        var charts = await analyticsService.GetAnalyticsChartsAsync(months: 6);

        charts.MonthlyHandoverTrend.Should().HaveCount(6);
        charts.MonthlyHandoverTrend[^1].CompletedHandovers.Should().Be(1);
        charts.MonthlyHandoverTrend[^1].RecycledWeightKg.Should().Be(10m);
        charts.WasteCategoryBreakdown.Should().HaveCount(2);
        charts.WasteCategoryBreakdown[0].CategoryCode.Should().Be("PLASTIC");
        charts.WasteCategoryBreakdown[0].SharePercent.Should().Be(60m);
    }

    // =========================================================================
    // UTC003 [Boundary] [A-01]: Kẹp biên tham số months trong khoảng [1..12] cho biểu đồ phân tích
    // =========================================================================
    [Fact]
    public async Task UTC003_GetAnalyticsChartsAsync_ClampsMonthsParameterToValidRange()
    {
        using var db = TestDbContextFactory.Create();
        var analyticsService = new AdminAnalyticsService(
            db,
            Substitute.For<IPaymentService>(),
            new AuditLogService(db, CreateCurrentUserMock(Guid.NewGuid())),
            CreateMockAiHttpFactory(),
            TestDbContextFactory.CreateConfiguration(),
            NullLogger<AdminAnalyticsService>.Instance);

        var minCharts = await analyticsService.GetAnalyticsChartsAsync(months: 0);
        var maxCharts = await analyticsService.GetAnalyticsChartsAsync(months: 36);

        minCharts.MonthlyHandoverTrend.Should().HaveCount(1);
        maxCharts.MonthlyHandoverTrend.Should().HaveCount(12);
    }

    // =========================================================================
    // UTC004 [Normal] [A-02]: Xuất CSV & XLSX ẩn danh PII + ghi Audit Log EXPORT_REPORT (BR-ADM-06, BR-ADM-05)
    // =========================================================================
    [Fact]
    public async Task UTC004_ExportSystemReportAsync_MasksPiiInCsvAndXlsx_AndAppendsAuditLog_BrAdm06()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = adminId, Username = "admin_exp", Email = "admin@ecolink.vn", FullName = "Admin Export", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = citizenId, Username = "ducnguyen", Email = "duc.nguyen@email.vn", PhoneNumber = "0912345678", FullName = "Nguyễn Đức", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active }
        );
        await db.SaveChangesAsync();

        var analyticsService = new AdminAnalyticsService(
            db,
            Substitute.For<IPaymentService>(),
            new AuditLogService(db, CreateCurrentUserMock(adminId)),
            CreateMockAiHttpFactory(),
            TestDbContextFactory.CreateConfiguration(),
            NullLogger<AdminAnalyticsService>.Instance);

        var csvFile = await analyticsService.ExportSystemReportAsync(
            adminId,
            new ReportExportQueryRequest { Dataset = "accounts", Format = "csv" });

        csvFile.Content[0].Should().Be(0xEF);
        csvFile.Content[1].Should().Be(0xBB);
        csvFile.Content[2].Should().Be(0xBF);
        var csvText = Encoding.UTF8.GetString(csvFile.Content);
        csvText.Should().Contain("du***@email.vn");
        csvText.Should().Contain("091****678");
        csvText.Should().NotContain("duc.nguyen@email.vn");
        csvText.Should().NotContain("0912345678");

        var xlsxFile = await analyticsService.ExportSystemReportAsync(
            adminId,
            new ReportExportQueryRequest { Dataset = "accounts", Format = "xlsx" });

        xlsxFile.ContentType.Should().Contain("spreadsheetml.sheet");
        using (var zipStream = new MemoryStream(xlsxFile.Content))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
        {
            var sheet = archive.GetEntry("xl/worksheets/sheet1.xml");
            sheet.Should().NotBeNull();
            using var reader = new StreamReader(sheet!.Open());
            var sheetXml = await reader.ReadToEndAsync();
            sheetXml.Should().Contain("du***@email.vn");
            sheetXml.Should().NotContain("duc.nguyen@email.vn");
        }

        (await db.AuditLogs.CountAsync(a => a.Action == "EXPORT_REPORT")).Should().Be(2);
    }

    // =========================================================================
    // UTC005 [Abnormal] [A-02]: Chặn trích xuất báo cáo sai định dạng hoặc dataset không hỗ trợ (BR-ADM-06)
    // =========================================================================
    [Fact]
    public async Task UTC005_ExportSystemReportAsync_UnsupportedFormatOrDataset_ThrowsBusinessRuleException()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var analyticsService = new AdminAnalyticsService(
            db,
            Substitute.For<IPaymentService>(),
            new AuditLogService(db, CreateCurrentUserMock(adminId)),
            CreateMockAiHttpFactory(),
            TestDbContextFactory.CreateConfiguration(),
            NullLogger<AdminAnalyticsService>.Instance);

        var badFormatAct = async () => await analyticsService.ExportSystemReportAsync(
            adminId,
            new ReportExportQueryRequest { Dataset = "accounts", Format = "pdf" });
        (await badFormatAct.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-ADM-06");

        var badDatasetAct = async () => await analyticsService.ExportSystemReportAsync(
            adminId,
            new ReportExportQueryRequest { Dataset = "secret_tokens", Format = "csv" });
        (await badDatasetAct.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-ADM-06");
    }

    // =========================================================================
    // UTC006 [Abnormal] [A-02]: AuditLogImmutabilityInterceptor chặn sửa/xóa bản ghi audit_logs (BR-ADM-05)
    // =========================================================================
    [Fact]
    public async Task UTC006_AuditLogImmutabilityInterceptor_BlocksModifyAndDelete_BrAdm05()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Username = "admin_imm", Email = "admin@ecolink.vn", FullName = "Admin", PasswordHash = "h", Role = UserRole.Admin });
        await db.SaveChangesAsync();

        var auditService = new AuditLogService(db, CreateCurrentUserMock(adminId));
        await auditService.LogAsync("TEST_ACTION", "User", adminId.ToString(), reason: "Ghi nhận kiểm toán gốc");

        var savedLog = await db.AuditLogs.FirstAsync();
        savedLog.Reason = "Cố tình chỉnh sửa dấu vết kiểm toán";

        var updateAct = async () => await db.SaveChangesAsync();
        await updateAct.Should().ThrowAsync<InvalidOperationException>().WithMessage("*BR-ADM-05*");

        db.Entry(savedLog).State = EntityState.Unchanged;
        db.AuditLogs.Remove(savedLog);

        var deleteAct = async () => await db.SaveChangesAsync();
        await deleteAct.Should().ThrowAsync<InvalidOperationException>().WithMessage("*BR-ADM-05*");
    }

    // =========================================================================
    // UTC007 [Normal] [A-02]: Truy vấn AuditLog theo Action, Keyword và phân trang ngược thời gian
    // =========================================================================
    [Fact]
    public async Task UTC007_AuditLogService_GetAuditLogsAsync_FiltersByActionAndKeyword()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Username = "admin_log", Email = "audit@ecolink.vn", FullName = "Audit Admin", PasswordHash = "h", Role = UserRole.Admin });
        await db.SaveChangesAsync();

        var auditService = new AuditLogService(db, CreateCurrentUserMock(adminId));
        await auditService.LogAsync("LOCK_ACCOUNT", "User", "u-1", reason: "Vi phạm cân đo");
        await auditService.LogAsync("EXPORT_REPORT", "SystemReport", "accounts", reason: "Báo cáo tuần 46");

        var filtered = await auditService.GetAuditLogsAsync(new AuditLogFilterRequest
        {
            Action = "export_report",
            Keyword = "tuần 46",
            PageNumber = 1,
            PageSize = 10
        });

        filtered.TotalRecords.Should().Be(1);
        filtered.Items.Should().ContainSingle(i => i.Action == "EXPORT_REPORT" && i.EntityId == "accounts");
    }

    // =========================================================================
    // UTC008 [Normal] [S-06]: Tra cứu danh sách & chi tiết tài khoản loại trừ Staff/Admin (BR-ADM-03)
    // =========================================================================
    [Fact]
    public async Task UTC008_AccountGovernanceService_GetAccountsAndDetail_ExcludesInternalUsersAndAggregatesStats()
    {
        using var db = TestDbContextFactory.Create();
        var staffId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        var recyclerId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = adminId, Username = "admin_ext", Email = "admin@ecolink.vn", FullName = "Admin", PasswordHash = "h", Role = UserRole.Admin },
            new User { Id = staffId, Username = "staff_ext", Email = "staff@ecolink.vn", FullName = "Staff", PasswordHash = "h", Role = UserRole.Staff },
            new User { Id = citizenId, Username = "an_citizen", Email = "an@email.vn", FullName = "Nguyễn An", PasswordHash = "h", Role = UserRole.User, WarningCount = 1 },
            new User { Id = recyclerId, Username = "rec_ext", Email = "rec@thugom.vn", FullName = "Vựa Xanh", PasswordHash = "h", Role = UserRole.Recycler }
        );
        db.UserEcoProfiles.Add(new UserEcoProfile { UserId = citizenId, CurrentGreenPoints = 150, TreeLevel = 3 });
        db.RecyclerProfiles.Add(new RecyclerProfile { UserId = recyclerId, FacilityName = "Vựa Xanh Bình Thạnh", BusinessAddress = "Bình Thạnh" });

        var reqId = Guid.NewGuid();
        db.HandoverRequests.Add(new HandoverRequest
        {
            Id = reqId,
            RequestCode = "HND-S06",
            UserId = citizenId,
            RecyclerId = recyclerId,
            PickupAddress = "Quận 1",
            ProposedTimeSlot = DateTime.UtcNow,
            Status = HandoverStatus.Completed
        });
        db.HandoverReceipts.Add(new HandoverReceipt
        {
            Id = Guid.NewGuid(),
            RequestId = reqId,
            ReceiptCode = "RCP-S06",
            UserId = citizenId,
            RecyclerId = recyclerId,
            TotalWeightKg = 14.5m,
            TotalAmountCash = 95_000m,
            ScaleProofPhotoUrl = "[]"
        });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AccountGovernanceService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(staffId, "Staff")),
            new MemoryAuthStateCacheInvalidator(cache),
            Substitute.For<IEmailService>(),
            NullLogger<AccountGovernanceService>.Instance);

        var list = await service.GetAccountsAsync(new AccountGovernanceFilterRequest());
        list.TotalRecords.Should().Be(2);
        list.Items.Should().NotContain(x => x.Role == "Staff" || x.Role == "Admin");

        var detail = await service.GetAccountDetailAsync(citizenId);
        detail.ActivityStats.CompletedDealsCount.Should().Be(1);
        detail.ActivityStats.TotalRecycledKg.Should().Be(14.5m);
        detail.ActivityStats.GreenPoints.Should().Be(150);
    }

    // =========================================================================
    // UTC009 [Boundary] [S-06]: Kẹp biên phân trang & lọc ngưỡng cảnh cáo MinWarnings
    // =========================================================================
    [Fact]
    public async Task UTC009_AccountGovernanceService_GetAccountsAsync_ClampsPaginationAndFiltersMinWarnings()
    {
        using var db = TestDbContextFactory.Create();
        db.Users.AddRange(
            new User { Id = Guid.NewGuid(), Username = "u0", Email = "u0@email.vn", FullName = "Zero Warn", PasswordHash = "h", Role = UserRole.User, WarningCount = 0 },
            new User { Id = Guid.NewGuid(), Username = "u2", Email = "u2@email.vn", FullName = "Two Warn", PasswordHash = "h", Role = UserRole.User, WarningCount = 2 }
        );
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AccountGovernanceService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(Guid.NewGuid(), "Staff")),
            new MemoryAuthStateCacheInvalidator(cache),
            Substitute.For<IEmailService>(),
            NullLogger<AccountGovernanceService>.Instance);

        var paged = await service.GetAccountsAsync(new AccountGovernanceFilterRequest
        {
            MinWarnings = 2,
            PageNumber = -3,
            PageSize = 500
        });

        paged.PageNumber.Should().Be(1);
        paged.PageSize.Should().Be(100);
        paged.TotalRecords.Should().Be(1);
        paged.Items[0].Username.Should().Be("u2");
    }

    // =========================================================================
    // UTC010 [Normal] [S-07]: Khóa & Mở khóa Recycler thu hồi token, tắt Online & xóa cache tức thời (BR-ADM-03)
    // =========================================================================
    [Fact]
    public async Task UTC010_LockAndUnlockRecyclerAccount_TogglesStatus_RevokesTokens_AndInvalidatesCache_BrAdm03()
    {
        using var db = TestDbContextFactory.Create();
        var staffId = Guid.NewGuid();
        var recyclerId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = staffId, Username = "staff_mod", Email = "staff@ecolink.vn", FullName = "Staff Kiểm Duyệt", PasswordHash = "h", Role = UserRole.Staff, Status = UserStatus.Active },
            new User { Id = recyclerId, Username = "minhkhai", Email = "minhkhai@thugom.vn", FullName = "Vựa Minh Khai", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active, FailedLoginAttempts = 4 }
        );
        db.RecyclerProfiles.Add(new RecyclerProfile
        {
            UserId = recyclerId,
            FacilityName = "Vựa Minh Khai",
            BusinessAddress = "512 Hai Bà Trưng, Quận 5",
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        });
        db.RefreshTokens.AddRange(
            new RefreshToken { Id = Guid.NewGuid(), UserId = recyclerId, TokenHash = "rt-1", ExpiresAt = DateTime.UtcNow.AddDays(7), IsRevoked = false },
            new RefreshToken { Id = Guid.NewGuid(), UserId = recyclerId, TokenHash = "rt-2", ExpiresAt = DateTime.UtcNow.AddDays(7), IsRevoked = false }
        );
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set($"{MemoryAuthStateCacheInvalidator.CacheKeyPrefix}{recyclerId}", "cached_active_state");

        var service = new AccountGovernanceService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(staffId, "Staff")),
            new MemoryAuthStateCacheInvalidator(cache),
            Substitute.For<IEmailService>(),
            NullLogger<AccountGovernanceService>.Instance);

        var locked = await service.LockOrUnlockAccountAsync(
            staffId,
            recyclerId,
            new LockOrUnlockAccountRequest
            {
                IsLocked = true,
                Reason = "Tái phạm cân thiếu 6kg so với biên nhận tại Quận 5",
                IncrementWarning = true
            });

        locked.Status.Should().Be(UserStatus.Suspended.ToString());
        locked.WarningCount.Should().Be(1);
        (await db.RefreshTokens.Where(t => t.UserId == recyclerId).ToListAsync()).Should().OnlyContain(t => t.IsRevoked);
        (await db.RecyclerProfiles.FirstAsync(r => r.UserId == recyclerId)).ActiveStatus.Should().Be(RecyclerActiveStatus.Offline);
        cache.TryGetValue($"{MemoryAuthStateCacheInvalidator.CacheKeyPrefix}{recyclerId}", out _).Should().BeFalse();

        // Mở khóa trở lại Active & đặt lại FailedLoginAttempts = 0
        var unlocked = await service.LockOrUnlockAccountAsync(
            staffId,
            recyclerId,
            new LockOrUnlockAccountRequest
            {
                IsLocked = false,
                Reason = "Cơ sở đã khắc phục cân điện tử và hoàn tất bồi thường"
            });

        unlocked.Status.Should().Be(UserStatus.Active.ToString());
        (await db.Users.FindAsync(recyclerId))!.FailedLoginAttempts.Should().Be(0);
        (await db.AccountModerationLogs.CountAsync(m => m.TargetUserId == recyclerId)).Should().Be(2);
    }

    // =========================================================================
    // UTC011 [Abnormal] [S-07]: Chặn khóa tài khoản khi lý do rỗng hoặc < 10 ký tự (BR-ADM-03)
    // =========================================================================
    [Fact]
    public async Task UTC011_LockAccount_WithShortOrEmptyReason_FailsBusinessRule_BrAdm03()
    {
        using var db = TestDbContextFactory.Create();
        var staffId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = staffId, Username = "staff1", Email = "staff@ecolink.vn", FullName = "Staff", PasswordHash = "h", Role = UserRole.Staff },
            new User { Id = citizenId, Username = "user1", Email = "user@email.vn", FullName = "User", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active }
        );
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AccountGovernanceService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(staffId, "Staff")),
            new MemoryAuthStateCacheInvalidator(cache),
            Substitute.For<IEmailService>(),
            NullLogger<AccountGovernanceService>.Instance);

        var act = async () => await service.LockOrUnlockAccountAsync(
            staffId,
            citizenId,
            new LockOrUnlockAccountRequest { IsLocked = true, Reason = "Quá ngắn" });

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-ADM-03");
        (await db.Users.FindAsync(citizenId))!.Status.Should().Be(UserStatus.Active);
    }

    // =========================================================================
    // UTC012 [Abnormal] [S-07]: Chặn leo thang đặc quyền khóa/vô hiệu hóa Admin hoặc Staff khác (BR-ADM-03)
    // =========================================================================
    [Fact]
    public async Task UTC012_StaffCannotLockOrDeactivateAdminOrOtherStaffAccount_ThrowsForbidden_BrAdm03()
    {
        using var db = TestDbContextFactory.Create();
        var staffId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = staffId, Username = "staff_a", Email = "staff_a@ecolink.vn", FullName = "Staff A", PasswordHash = "h", Role = UserRole.Staff, Status = UserStatus.Active },
            new User { Id = adminId, Username = "root_admin", Email = "admin@ecolink.vn", FullName = "Root Admin", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active }
        );
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AccountGovernanceService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(staffId, "Staff")),
            new MemoryAuthStateCacheInvalidator(cache),
            Substitute.For<IEmailService>(),
            NullLogger<AccountGovernanceService>.Instance);

        var lockAct = async () => await service.LockOrUnlockAccountAsync(
            staffId,
            adminId,
            new LockOrUnlockAccountRequest { IsLocked = true, Reason = "Cố tình khóa tài khoản Quản trị viên cấp cao" });

        (await lockAct.Should().ThrowAsync<ForbiddenAccessException>()).Which.MessageCode.Should().Be("MSG-ADM-04");

        var deactAct = async () => await service.DeactivateAccountAsync(
            staffId,
            adminId,
            new DeactivateAccountRequest { Reason = "Cố tình vô hiệu hóa Quản trị viên cấp cao" });

        (await deactAct.Should().ThrowAsync<ForbiddenAccessException>()).Which.MessageCode.Should().Be("MSG-ADM-04");
        (await db.Users.FindAsync(adminId))!.Status.Should().Be(UserStatus.Active);
    }

    // =========================================================================
    // UTC013 [Normal] [S-07]: Vô hiệu hóa mềm giữ nguyên User & toàn bộ 5 biên nhận lịch sử (BR-ADM-04)
    // =========================================================================
    [Fact]
    public async Task UTC013_DeactivateAccountAsync_SoftDeactivatesUser_PreservingHistoricalReceipts_BrAdm04()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        var recyclerId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = adminId, Username = "admin", Email = "admin@ecolink.vn", FullName = "Admin", PasswordHash = "h", Role = UserRole.Admin },
            new User { Id = citizenId, Username = "citizen_hist", Email = "hist@email.vn", FullName = "Người Dân Có Giao Dịch", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active },
            new User { Id = recyclerId, Username = "recycler_hist", Email = "rec@thugom.vn", FullName = "Vựa Thu Gom", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active }
        );

        for (int i = 1; i <= 5; i++)
        {
            var reqId = Guid.NewGuid();
            db.HandoverRequests.Add(new HandoverRequest
            {
                Id = reqId,
                RequestCode = $"HND-HIST-{i}",
                UserId = citizenId,
                RecyclerId = recyclerId,
                PickupAddress = "Quận 7",
                ProposedTimeSlot = DateTime.UtcNow,
                Status = HandoverStatus.Completed
            });
            db.HandoverReceipts.Add(new HandoverReceipt
            {
                Id = Guid.NewGuid(),
                RequestId = reqId,
                ReceiptCode = $"RCP-HIST-{i}",
                UserId = citizenId,
                RecyclerId = recyclerId,
                TotalWeightKg = 4.0m,
                TotalAmountCash = 20_000m,
                ScaleProofPhotoUrl = "[]"
            });
        }
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AccountGovernanceService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(adminId)),
            new MemoryAuthStateCacheInvalidator(cache),
            Substitute.For<IEmailService>(),
            NullLogger<AccountGovernanceService>.Instance);

        var result = await service.DeactivateAccountAsync(
            adminId,
            citizenId,
            new DeactivateAccountRequest { Reason = "Vi phạm nghiêm trọng điều khoản sử dụng nền tảng EcoLink" });

        result.Status.Should().Be(UserStatus.Deactivated.ToString());
        result.CompletedDealsCount.Should().Be(5);
        (await db.Users.AnyAsync(u => u.Id == citizenId && !u.IsDeleted)).Should().BeTrue();
        (await db.HandoverReceipts.CountAsync(r => r.UserId == citizenId)).Should().Be(5);
    }

    // =========================================================================
    // UTC014 [Normal] [A-03]: Tạo Staff với 0 quyền mặc định (BR-ADM-01) & cập nhật hồ sơ / mật khẩu
    // =========================================================================
    [Fact]
    public async Task UTC014_CreateAndUpdateStaffAccount_InitializesZeroPermissions_AndResetsPassword_BrAdm01()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Username = "admin_hr", Email = "admin@ecolink.vn", FullName = "Admin HR", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var staffService = new StaffManagementService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(adminId)),
            new MemoryAuthStateCacheInvalidator(cache),
            NullLogger<StaffManagementService>.Instance);

        var created = await staffService.CreateStaffAccountAsync(adminId, new CreateStaffAccountRequest
        {
            FullName = "Phạm Bảo Ngọc",
            Email = "ngoc.pham@ecolink.vn",
            Department = "Truyền thông & Đối tác",
            Position = "Chuyên viên vận hành"
        });

        created.StaffCode.Should().Be("NV-001");
        created.UserStatus.Should().Be(UserStatus.Active.ToString());
        created.Permissions.Should().BeEmpty();

        db.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), UserId = created.StaffId, TokenHash = "rt-staff", ExpiresAt = DateTime.UtcNow.AddDays(1), IsRevoked = false });
        await db.SaveChangesAsync();

        var updated = await staffService.UpdateStaffAccountAsync(
            adminId,
            created.StaffId,
            new UpdateStaffAccountRequest
            {
                FullName = "Phạm Bảo Ngọc (Lead)",
                Department = "Kiểm duyệt Chất lượng",
                Position = "Trưởng nhóm",
                ResetTemporaryPassword = "NewTemp@2026"
            });

        updated.FullName.Should().Be("Phạm Bảo Ngọc (Lead)");
        updated.Department.Should().Be("Kiểm duyệt Chất lượng");
        (await db.RefreshTokens.SingleAsync(t => t.UserId == created.StaffId)).IsRevoked.Should().BeTrue();
    }

    // =========================================================================
    // UTC015 [Abnormal] [A-03]: Chặn tạo Staff trùng Email hoặc trùng StaffCode
    // =========================================================================
    [Fact]
    public async Task UTC015_CreateStaffAccountAsync_DuplicateEmailOrStaffCode_ThrowsBusinessRuleException()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Username = "admin_dup", Email = "admin@ecolink.vn", FullName = "Admin", PasswordHash = "h", Role = UserRole.Admin });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var staffService = new StaffManagementService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(adminId)),
            new MemoryAuthStateCacheInvalidator(cache),
            NullLogger<StaffManagementService>.Instance);

        await staffService.CreateStaffAccountAsync(adminId, new CreateStaffAccountRequest
        {
            FullName = "Nhân Viên Một",
            Email = "nv1@ecolink.vn",
            StaffCode = "NV-088"
        });

        var dupEmailAct = async () => await staffService.CreateStaffAccountAsync(adminId, new CreateStaffAccountRequest
        {
            FullName = "Nhân Viên Hai",
            Email = "NV1@ECOLINK.VN"
        });
        (await dupEmailAct.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-AUTH-01");

        var dupCodeAct = async () => await staffService.CreateStaffAccountAsync(adminId, new CreateStaffAccountRequest
        {
            FullName = "Nhân Viên Ba",
            Email = "nv3@ecolink.vn",
            StaffCode = "NV-088"
        });
        (await dupCodeAct.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-ADM-01");
    }

    // =========================================================================
    // UTC016 [Abnormal] [A-03]: Chặn Admin tự khóa chính mình & từ chối trạng thái nhân sự sai (BR-ADM-03)
    // =========================================================================
    [Fact]
    public async Task UTC016_UpdateStaffStatusAsync_BlocksSelfLockAndInvalidStatus_BrAdm03()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Username = "admin_self", Email = "admin@ecolink.vn", FullName = "Admin", PasswordHash = "h", Role = UserRole.Admin });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var staffService = new StaffManagementService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(adminId)),
            new MemoryAuthStateCacheInvalidator(cache),
            NullLogger<StaffManagementService>.Instance);

        var staff = await staffService.CreateStaffAccountAsync(adminId, new CreateStaffAccountRequest
        {
            FullName = "Trần Thị Minh",
            Email = "minh.tran@ecolink.vn"
        });

        var selfLockAct = async () => await staffService.UpdateStaffStatusAsync(
            staff.StaffId,
            staff.StaffId,
            new UpdateStaffStatusRequest { Status = "Suspended", Reason = "Tự đình chỉ chính mình không hợp lệ" });

        (await selfLockAct.Should().ThrowAsync<ForbiddenAccessException>()).Which.MessageCode.Should().Be("MSG-ADM-04");

        var badStatusAct = async () => await staffService.UpdateStaffStatusAsync(
            adminId,
            staff.StaffId,
            new UpdateStaffStatusRequest { Status = "DeletedForever", Reason = "Thử trạng thái không được phép" });

        (await badStatusAct.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-ADM-03");
    }

    // =========================================================================
    // UTC017 [Normal] [A-04]: Cấp/thu hồi quyền RBAC chuẩn hóa alias, xóa cache tức thời & ghi diff (BR-ADM-02)
    // =========================================================================
    [Fact]
    public async Task UTC017_UpdateAndGetStaffPermissions_NormalizesAliasesInvalidatesCacheAndLogsDiff_BrAdm02()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Username = "admin_rbac", Email = "admin@ecolink.vn", FullName = "Admin RBAC", PasswordHash = "h", Role = UserRole.Admin });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var staffService = new StaffManagementService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(adminId)),
            new MemoryAuthStateCacheInvalidator(cache),
            NullLogger<StaffManagementService>.Instance);

        var staff = await staffService.CreateStaffAccountAsync(adminId, new CreateStaffAccountRequest
        {
            FullName = "Trần Đình Khoa",
            Email = "khoa.tran@ecolink.vn"
        });

        var granted = await staffService.UpdateStaffPermissionsAsync(
            adminId,
            staff.StaffId,
            new UpdateStaffPermissionsRequest
            {
                Permissions = new List<string> { "ai.dataset", "MANAGE_ACCOUNTS" },
                Reason = "Phân công ca sáng"
            });

        granted.Permissions.Should().BeEquivalentTo(new[] { "CURATE_DATASET", "MANAGE_ACCOUNTS" });

        var detail = await staffService.GetStaffPermissionsAsync(staff.StaffId);
        detail.AssignedPermissions.Should().HaveCount(2);

        cache.Set($"{MemoryAuthStateCacheInvalidator.CacheKeyPrefix}{staff.StaffId}", granted.Permissions);

        var afterRevoke = await staffService.UpdateStaffPermissionsAsync(
            adminId,
            staff.StaffId,
            new UpdateStaffPermissionsRequest
            {
                Permissions = new List<string> { "CURATE_DATASET" },
                Reason = "Thu hồi quyền quản lý tài khoản"
            });

        afterRevoke.Permissions.Should().ContainSingle().Which.Should().Be("CURATE_DATASET");
        cache.TryGetValue($"{MemoryAuthStateCacheInvalidator.CacheKeyPrefix}{staff.StaffId}", out _).Should().BeFalse();

        var permAudit = await db.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(a => a.EntityId == staff.StaffId.ToString());
        permAudit.Should().NotBeNull();
        permAudit!.OldValues.Should().Contain("MANAGE_ACCOUNTS");
    }

    // =========================================================================
    // UTC018 [Abnormal] [A-04]: Từ chối mã quyền RBAC ngoài danh mục chuẩn (BR-ADM-02)
    // =========================================================================
    [Fact]
    public async Task UTC018_StaffPermissions_RejectsUnknownCodesAndThrowsOnInvalidPermission_BrAdm02()
    {
        StaffPermissions.Normalize("ai.review").Should().Be(StaffPermissions.ReviewAi);
        StaffPermissions.Normalize("account.manage").Should().Be(StaffPermissions.ManageAccounts);
        StaffPermissions.IsValid("partner.verify").Should().BeTrue();
        StaffPermissions.IsValid("SUPER_ROOT_HACK").Should().BeFalse();

        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Username = "admin_inv", Email = "admin@ecolink.vn", FullName = "Admin", PasswordHash = "h", Role = UserRole.Admin });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var staffService = new StaffManagementService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(adminId)),
            new MemoryAuthStateCacheInvalidator(cache),
            NullLogger<StaffManagementService>.Instance);

        var staff = await staffService.CreateStaffAccountAsync(adminId, new CreateStaffAccountRequest
        {
            FullName = "Lê Hoàng Nam",
            Email = "nam.le@ecolink.vn"
        });

        var act = async () => await staffService.UpdateStaffPermissionsAsync(
            adminId,
            staff.StaffId,
            new UpdateStaffPermissionsRequest { Permissions = new List<string> { "SUPER_ROOT_HACK" } });

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-ADM-02");
    }

    // =========================================================================
    // UTC019 [Normal] [S-11]: Check-in & Check-out tách biệt ví công dân & đếm tác vụ ca trực (BR-GAM-08, BR-GAM-09)
    // =========================================================================
    [Fact]
    public async Task UTC019_StaffAttendance_CheckInCheckOutAndAdminReport_IsolatesCitizenWallet_BrGam08()
    {
        using var db = TestDbContextFactory.Create();
        var staffId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = staffId,
            Username = "staff_att",
            Email = "staff_att@ecolink.vn",
            FullName = "Lê Thu Hà",
            PasswordHash = "h",
            Role = UserRole.Staff,
            Status = UserStatus.Active
        });
        db.UserEcoProfiles.Add(new UserEcoProfile { UserId = staffId, CurrentGreenPoints = 0, TreeLevel = 1 });
        await db.SaveChangesAsync();

        var auditService = new AuditLogService(db, CreateCurrentUserMock(staffId, "Staff"));
        var attendanceService = new StaffAttendanceService(db, auditService);

        var checkIn = await attendanceService.CheckInAsync(
            staffId,
            new StaffCheckInRequest { ShiftNote = "Mở ca sáng xử lý hàng đợi AI" });

        checkIn.CheckedInToday.Should().BeTrue();
        checkIn.PointsEarnedToday.Should().Be(10);
        checkIn.CurrentStreakDay.Should().Be(1);
        (await db.UserEcoProfiles.FirstAsync(p => p.UserId == staffId)).CurrentGreenPoints.Should().Be(0);

        await auditService.LogAsync("REVIEW_AI_FEEDBACK", "AIFeedbackLog", "fb-1", reason: "Xác nhận nhãn chuẩn");

        var checkOut = await attendanceService.CheckOutAsync(
            staffId,
            new StaffCheckOutRequest { ShiftNote = "Đã xử lý xong 18 hồ sơ ca sáng" });

        checkOut.CheckedOutToday.Should().BeTrue();
        checkOut.TodayShiftNote.Should().Contain("18 hồ sơ");

        var allAttendance = await attendanceService.GetAllStaffAttendanceAsync(new AdminStaffAttendanceFilterRequest { StaffId = staffId });
        allAttendance.TotalRecords.Should().Be(1);
        allAttendance.Items[0].TasksCompletedSnapshot.Should().BeGreaterThanOrEqualTo(1);
    }

    // =========================================================================
    // UTC020 [Abnormal] [S-11]: Chặn Check-in lần 2/ngày & Check-out trái quy trình (BR-GAM-09)
    // =========================================================================
    [Fact]
    public async Task UTC020_StaffAttendance_DuplicateCheckInAndInvalidCheckOut_ThrowBrGam09()
    {
        using var db = TestDbContextFactory.Create();
        var staffId = Guid.NewGuid();
        db.Users.Add(new User { Id = staffId, Username = "staff_dup", Email = "staff_dup@ecolink.vn", FullName = "Staff Dup", PasswordHash = "h", Role = UserRole.Staff });
        await db.SaveChangesAsync();

        var attendanceService = new StaffAttendanceService(db, new AuditLogService(db, CreateCurrentUserMock(staffId, "Staff")));

        var earlyCheckout = async () => await attendanceService.CheckOutAsync(staffId, new StaffCheckOutRequest());
        (await earlyCheckout.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-GAM-09");

        await attendanceService.CheckInAsync(staffId, new StaffCheckInRequest());

        var dupCheckIn = async () => await attendanceService.CheckInAsync(staffId, new StaffCheckInRequest());
        (await dupCheckIn.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-GAM-09");

        await attendanceService.CheckOutAsync(staffId, new StaffCheckOutRequest());
        var dupCheckOut = async () => await attendanceService.CheckOutAsync(staffId, new StaffCheckOutRequest());
        (await dupCheckOut.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-GAM-09");
    }

    // =========================================================================
    // UTC021 [Boundary] [S-11]: Kiểm tra biên chuỗi 7 ngày (+15 điểm ngày 7, quay vòng ngày 8, đứt chuỗi về 1)
    // =========================================================================
    [Fact]
    public async Task UTC021_StaffAttendance_SevenDayStreakBonusWrapAndResetBoundary_BrGam09()
    {
        using var db = TestDbContextFactory.Create();
        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.Add(VietnamOffset));

        var day6StaffId = Guid.NewGuid();
        var day7StaffId = Guid.NewGuid();
        var brokenStaffId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = day6StaffId, Username = "s_d6", Email = "d6@ecolink.vn", FullName = "Day 6", PasswordHash = "h", Role = UserRole.Staff },
            new User { Id = day7StaffId, Username = "s_d7", Email = "d7@ecolink.vn", FullName = "Day 7", PasswordHash = "h", Role = UserRole.Staff },
            new User { Id = brokenStaffId, Username = "s_brk", Email = "brk@ecolink.vn", FullName = "Broken", PasswordHash = "h", Role = UserRole.Staff }
        );
        db.StaffProfiles.AddRange(
            new StaffProfile { UserId = day6StaffId, StaffCode = "NV-106", Status = "ACTIVE" },
            new StaffProfile { UserId = day7StaffId, StaffCode = "NV-107", Status = "ACTIVE" },
            new StaffProfile { UserId = brokenStaffId, StaffCode = "NV-108", Status = "ACTIVE" }
        );
        db.StaffAttendances.AddRange(
            new StaffAttendance { Id = Guid.NewGuid(), StaffId = day6StaffId, WorkDate = todayVn.AddDays(-1), StreakDay = 6, WorkPointsEarned = 10 },
            new StaffAttendance { Id = Guid.NewGuid(), StaffId = day7StaffId, WorkDate = todayVn.AddDays(-1), StreakDay = 7, WorkPointsEarned = 15 },
            new StaffAttendance { Id = Guid.NewGuid(), StaffId = brokenStaffId, WorkDate = todayVn.AddDays(-2), StreakDay = 5, WorkPointsEarned = 10 }
        );
        await db.SaveChangesAsync();

        var service = new StaffAttendanceService(db, new AuditLogService(db, CreateCurrentUserMock(day6StaffId, "Staff")));

        var day7Result = await service.CheckInAsync(day6StaffId, new StaffCheckInRequest());
        day7Result.CurrentStreakDay.Should().Be(7);
        day7Result.PointsEarnedToday.Should().Be(15);

        var wrapResult = await service.CheckInAsync(day7StaffId, new StaffCheckInRequest());
        wrapResult.CurrentStreakDay.Should().Be(1);
        wrapResult.PointsEarnedToday.Should().Be(10);

        var resetResult = await service.CheckInAsync(brokenStaffId, new StaffCheckInRequest());
        resetResult.CurrentStreakDay.Should().Be(1);
        resetResult.PointsEarnedToday.Should().Be(10);
    }

    // =========================================================================
    // UTC022 [Boundary] [A-03, A-04, S-07, S-11]: Kiểm tra biên độ dài cho cả 8 Governance FluentValidators
    // =========================================================================
    [Fact]
    public async Task UTC022_GovernanceValidators_VerifyBoundaryLengthsAcrossAllValidators()
    {
        var lockValidator = new LockOrUnlockAccountValidator();
        (await lockValidator.ValidateAsync(new LockOrUnlockAccountRequest { IsLocked = true, Reason = new string('a', 9) })).IsValid.Should().BeFalse();
        (await lockValidator.ValidateAsync(new LockOrUnlockAccountRequest { IsLocked = true, Reason = new string('a', 10) })).IsValid.Should().BeTrue();
        (await lockValidator.ValidateAsync(new LockOrUnlockAccountRequest { IsLocked = true, Reason = new string('a', 501) })).IsValid.Should().BeFalse();

        var deactivateValidator = new DeactivateAccountValidator();
        (await deactivateValidator.ValidateAsync(new DeactivateAccountRequest { Reason = new string('b', 9) })).IsValid.Should().BeFalse();
        (await deactivateValidator.ValidateAsync(new DeactivateAccountRequest { Reason = new string('b', 10) })).IsValid.Should().BeTrue();

        var createStaffValidator = new CreateStaffAccountValidator();
        (await createStaffValidator.ValidateAsync(new CreateStaffAccountRequest { FullName = "AB", Email = "staff@ecolink.vn" })).IsValid.Should().BeFalse();
        (await createStaffValidator.ValidateAsync(new CreateStaffAccountRequest { FullName = "ABC", Email = "staff@ecolink.vn", InitialPassword = "1234567" })).IsValid.Should().BeFalse();
        (await createStaffValidator.ValidateAsync(new CreateStaffAccountRequest { FullName = "ABC", Email = "staff@ecolink.vn", InitialPassword = "12345678", InitialPermissions = ["REVIEW_AI"] })).IsValid.Should().BeTrue();

        var updateStaffValidator = new UpdateStaffAccountValidator();
        (await updateStaffValidator.ValidateAsync(new UpdateStaffAccountRequest { FullName = "Valid Name", ResetTemporaryPassword = "short" })).IsValid.Should().BeFalse();
        (await updateStaffValidator.ValidateAsync(new UpdateStaffAccountRequest { FullName = "Valid Name", ResetTemporaryPassword = "Valid@123" })).IsValid.Should().BeTrue();

        var statusValidator = new UpdateStaffStatusValidator();
        (await statusValidator.ValidateAsync(new UpdateStaffStatusRequest { Status = "Locked", Reason = new string('c', 10) })).IsValid.Should().BeFalse();
        (await statusValidator.ValidateAsync(new UpdateStaffStatusRequest { Status = "Suspended", Reason = new string('c', 10) })).IsValid.Should().BeTrue();

        var permValidator = new UpdateStaffPermissionsValidator();
        (await permValidator.ValidateAsync(new UpdateStaffPermissionsRequest { Permissions = ["BAD_PERM"] })).IsValid.Should().BeFalse();
        (await permValidator.ValidateAsync(new UpdateStaffPermissionsRequest { Permissions = ["ai.review"] })).IsValid.Should().BeTrue();

        var checkInValidator = new StaffCheckInValidator();
        (await checkInValidator.ValidateAsync(new StaffCheckInRequest { ShiftNote = new string('d', 500) })).IsValid.Should().BeTrue();
        (await checkInValidator.ValidateAsync(new StaffCheckInRequest { ShiftNote = new string('d', 501) })).IsValid.Should().BeFalse();

        var checkOutValidator = new StaffCheckOutValidator();
        (await checkOutValidator.ValidateAsync(new StaffCheckOutRequest { ShiftNote = new string('e', 500) })).IsValid.Should().BeTrue();
        (await checkOutValidator.ValidateAsync(new StaffCheckOutRequest { ShiftNote = new string('e', 501) })).IsValid.Should().BeFalse();
    }
}
