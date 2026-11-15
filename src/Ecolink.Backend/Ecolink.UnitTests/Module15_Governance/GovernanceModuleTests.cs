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
/// Bộ kiểm thử đơn vị toàn diện cho Module M-15: Account, Staff &amp; System Governance / Analytics (FE-17 &amp; FE-18).
/// Bao phủ trọn vẹn 12 kịch bản chuẩn IEEE 829 RTM (TC-ADM-* &amp; TC-STF-*) và kiểm tra thêm Validator + XLSX OpenXML.
/// </summary>
public class GovernanceModuleTests
{
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

    private static IHttpClientFactory CreateMockAiHttpFactory()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var handler = MockHttpMessageHandler.FromJson(
            HttpStatusCode.OK,
            """{"total_scans":120,"ood_count":6,"categories":{"plastic":80,"paper":40}}""");
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));
        return factory;
    }

    // =========================================================================
    // 1. TC-ADM-01-01_P: Dashboard KPI chỉ tính đơn Completed & Recycler Verified (BR-ADM-07)
    // =========================================================================
    [Fact]
    public async Task TC_ADM_01_01_P_GetExecutiveKpiSummary_CountsOnlyCompletedDealsAndPaidVietQr_BR_ADM_07()
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
            // Biên nhận gắn đơn đã hủy (50kg) -> KHÔNG ĐƯỢC CỘNG VÀO KPI (BR-ADM-07)
            new HandoverReceipt { Id = Guid.NewGuid(), RequestId = cancelledReq.Id, ReceiptCode = "RCP-003", UserId = citizenId, RecyclerId = verifiedRecyclerId, TotalWeightKg = 50.0m, TotalAmountCash = 500_000m, ScaleProofPhotoUrl = "[]", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var paymentService = Substitute.For<IPaymentService>();
        paymentService.GetAllOrdersAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new PayInOrderDto { OrderCode = "ORD-PAID", Amount = 199_000m, Status = "PAID", CreatedAt = DateTime.UtcNow },
            new PayInOrderDto { OrderCode = "ORD-PEND", Amount = 999_000m, Status = "PENDING", CreatedAt = DateTime.UtcNow }
        });

        var auditService = new AuditLogService(db, CreateCurrentUserMock(adminId));
        var analyticsService = new AdminAnalyticsService(
            db,
            paymentService,
            auditService,
            CreateMockAiHttpFactory(),
            TestDbContextFactory.CreateConfiguration(),
            NullLogger<AdminAnalyticsService>.Instance);

        var kpi = await analyticsService.GetExecutiveKpiSummaryAsync(new ExecutiveKpiQueryRequest { Period = "30d" });

        kpi.ActiveCitizensCount.Should().Be(1);
        kpi.VerifiedRecyclersCount.Should().Be(1); // Không đếm cơ sở Pending
        kpi.CompletedHandoversCount.Should().Be(2);
        kpi.TotalRecycledWeightKg.Should().Be(10.0m); // 6.5 + 3.5 = 10.0kg, loại trừ 50kg đơn Cancelled
        kpi.GrossTradeValueVnd.Should().Be(100_000m);
        kpi.VietQrServiceRevenueVnd.Should().Be(199_000m); // Chỉ cộng đơn PAID
        kpi.OperationalBacklog.PendingRecyclerKybCount.Should().Be(1);
    }

    // =========================================================================
    // 2. TC-ADM-02-01_S: Xuất báo cáo CSV & XLSX ẩn danh PII + tự động ghi Audit Log (BR-ADM-06, BR-ADM-05)
    // =========================================================================
    [Fact]
    public async Task TC_ADM_02_01_S_ExportSystemReport_MasksPiiInCsvAndXlsx_AndAppendsAuditLog_BR_ADM_06()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = adminId, Username = "admin_exp", Email = "admin@ecolink.vn", FullName = "Admin Export", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = citizenId, Username = "ducnguyen", Email = "duc.nguyen@email.vn", PhoneNumber = "0912345678", FullName = "Nguyễn Đức", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active }
        );
        await db.SaveChangesAsync();

        var paymentService = Substitute.For<IPaymentService>();
        paymentService.GetAllOrdersAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<PayInOrderDto>());

        var auditService = new AuditLogService(db, CreateCurrentUserMock(adminId));
        var analyticsService = new AdminAnalyticsService(
            db,
            paymentService,
            auditService,
            CreateMockAiHttpFactory(),
            TestDbContextFactory.CreateConfiguration(),
            NullLogger<AdminAnalyticsService>.Instance);

        // 1) Kiểm tra CSV (UTF-8 BOM + PII Masked)
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

        // 2) Kiểm tra XLSX (Hợp lệ chuẩn OpenXML ZIP)
        var xlsxFile = await analyticsService.ExportSystemReportAsync(
            adminId,
            new ReportExportQueryRequest { Dataset = "accounts", Format = "xlsx" });

        xlsxFile.ContentType.Should().Contain("spreadsheetml.sheet");
        using (var zipStream = new MemoryStream(xlsxFile.Content))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
        {
            archive.GetEntry("xl/worksheets/sheet1.xml").Should().NotBeNull();
            using var reader = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            var sheetXml = await reader.ReadToEndAsync();
            sheetXml.Should().Contain("du***@email.vn");
            sheetXml.Should().NotContain("duc.nguyen@email.vn");
        }

        // 3) Bảng audit_logs sinh đúng 2 dòng EXPORT_REPORT
        var exportLogs = await db.AuditLogs.Where(a => a.Action == "EXPORT_REPORT").ToListAsync();
        exportLogs.Should().HaveCount(2);
    }

    // =========================================================================
    // 3. TC-ADM-02-02_S: Đảm bảo tính bất biến tuyệt đối của bảng audit_logs (BR-ADM-05)
    // =========================================================================
    [Fact]
    public async Task TC_ADM_02_02_S_AuditLogImmutabilityInterceptor_BlocksModifyAndDelete_BR_ADM_05()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Username = "admin_imm", Email = "admin@ecolink.vn", FullName = "Admin", PasswordHash = "h", Role = UserRole.Admin });
        await db.SaveChangesAsync();

        var auditService = new AuditLogService(db, CreateCurrentUserMock(adminId));
        await auditService.LogAsync("TEST_ACTION", "User", adminId.ToString(), reason: "Ghi nhận kiểm toán gốc");

        var savedLog = await db.AuditLogs.FirstAsync();

        // Cố tình sửa nội dung AuditLog -> Bắt buộc ném InvalidOperationException (BR-ADM-05)
        savedLog.Reason = "Cố tình chỉnh sửa dấu vết kiểm toán";
        var updateAct = async () => await db.SaveChangesAsync();
        await updateAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*BR-ADM-05*");

        db.Entry(savedLog).State = EntityState.Unchanged;

        // Cố tình xóa dòng AuditLog -> Bắt buộc ném InvalidOperationException (BR-ADM-05)
        db.AuditLogs.Remove(savedLog);
        var deleteAct = async () => await db.SaveChangesAsync();
        await deleteAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*BR-ADM-05*");
    }

    // =========================================================================
    // 4. TC-STF-06-01_S: Chuẩn hóa mã quyền & kiểm tra tập quyền Staff (BR-ADM-02)
    // =========================================================================
    [Fact]
    public void TC_STF_06_01_S_StaffPermissions_NormalizesLegacyAliasesAndRejectsUnknownCodes_BR_ADM_02()
    {
        StaffPermissions.Normalize("ai.review").Should().Be(StaffPermissions.ReviewAi);
        StaffPermissions.Normalize("account.manage").Should().Be(StaffPermissions.ManageAccounts);
        StaffPermissions.Normalize("MANAGE_ACCOUNTS").Should().Be(StaffPermissions.ManageAccounts);
        StaffPermissions.IsValid("partner.verify").Should().BeTrue();
        StaffPermissions.IsValid("SUPER_ROOT_HACK").Should().BeFalse();

        // Nhân viên chỉ có quyền REVIEW_AI không chứa quyền MANAGE_ACCOUNTS
        var assigned = new HashSet<string> { StaffPermissions.Normalize("ai.review")! };
        assigned.Contains(StaffPermissions.ManageAccounts).Should().BeFalse();
    }

    // =========================================================================
    // 5. TC-STF-07-01_N: Chặn khóa tài khoản khi bỏ trống hoặc nhập lý do < 10 ký tự (BR-ADM-03)
    // =========================================================================
    [Fact]
    public async Task TC_STF_07_01_N_LockAccount_WithShortOrEmptyReason_FailsValidationAndBusinessRule_BR_ADM_03()
    {
        var validator = new LockOrUnlockAccountValidator();
        var invalidResult = await validator.ValidateAsync(new LockOrUnlockAccountRequest
        {
            IsLocked = true,
            Reason = "Ngắn"
        });
        invalidResult.IsValid.Should().BeFalse();

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
            new LockOrUnlockAccountRequest { IsLocked = true, Reason = "" });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-ADM-03");

        (await db.Users.FindAsync(citizenId))!.Status.Should().Be(UserStatus.Active);
    }

    // =========================================================================
    // 6. TC-STF-07-02_P: Khóa tài khoản Recycler vi phạm, thu hồi RefreshToken & chuyển Offline (BR-ADM-03)
    // =========================================================================
    [Fact]
    public async Task TC_STF_07_02_P_LockRecyclerAccount_SuspendsUser_RevokesTokens_SetsOffline_AndLogsAudit_BR_ADM_03()
    {
        using var db = TestDbContextFactory.Create();
        var staffId = Guid.NewGuid();
        var recyclerId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = staffId, Username = "staff_mod", Email = "staff@ecolink.vn", FullName = "Staff Kiểm Duyệt", PasswordHash = "h", Role = UserRole.Staff, Status = UserStatus.Active },
            new User { Id = recyclerId, Username = "minhkhai", Email = "minhkhai@thugom.vn", FullName = "Vựa Minh Khai", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active }
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

        var summary = await service.LockOrUnlockAccountAsync(
            staffId,
            recyclerId,
            new LockOrUnlockAccountRequest
            {
                IsLocked = true,
                Reason = "Tái phạm cân thiếu 6kg so với biên nhận tại Quận 5",
                IncrementWarning = true
            });

        summary.Status.Should().Be(UserStatus.Suspended.ToString());
        summary.WarningCount.Should().Be(1);

        // Toàn bộ RefreshToken bị thu hồi
        var tokens = await db.RefreshTokens.Where(t => t.UserId == recyclerId).ToListAsync();
        tokens.Should().OnlyContain(t => t.IsRevoked);

        // Recycler bị chuyển sang Offline trên bản đồ
        var profile = await db.RecyclerProfiles.FirstAsync(r => r.UserId == recyclerId);
        profile.ActiveStatus.Should().Be(RecyclerActiveStatus.Offline);

        // Cache trạng thái xác thực bị xóa tức thời để chặn JWT cũ
        cache.TryGetValue($"{MemoryAuthStateCacheInvalidator.CacheKeyPrefix}{recyclerId}", out _).Should().BeFalse();

        // Ghi nhận đầy đủ ModerationLog & AuditLog
        (await db.AccountModerationLogs.CountAsync(m => m.TargetUserId == recyclerId)).Should().Be(1);
        (await db.AuditLogs.AnyAsync(a => a.EntityId == recyclerId.ToString())).Should().BeTrue();
    }

    // =========================================================================
    // 7. TC-STF-07-03_S: Chặn Staff leo thang đặc quyền khóa tài khoản Admin hoặc Staff khác (BR-ADM-03)
    // =========================================================================
    [Fact]
    public async Task TC_STF_07_03_S_StaffCannotLockAdminOrOtherStaffAccount_ThrowsForbidden_BR_ADM_03()
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

        var act = async () => await service.LockOrUnlockAccountAsync(
            staffId,
            adminId,
            new LockOrUnlockAccountRequest
            {
                IsLocked = true,
                Reason = "Cố tình khóa tài khoản Quản trị viên cấp cao"
            });

        var ex = await act.Should().ThrowAsync<ForbiddenAccessException>();
        ex.Which.MessageCode.Should().Be("MSG-ADM-04");
        (await db.Users.FindAsync(adminId))!.Status.Should().Be(UserStatus.Active);
    }

    // =========================================================================
    // 8. TC-STF-07-04_P: Vô hiệu hóa mềm tài khoản đã có 5 biên nhận lịch sử mà không mất đối soát (BR-ADM-04)
    // =========================================================================
    [Fact]
    public async Task TC_STF_07_04_P_DeactivateAccount_SoftDeactivatesUser_PreservingAllHistoricalReceipts_BR_ADM_04()
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

        // Bản ghi User và toàn bộ 5 biên nhận lịch sử vẫn nguyên vẹn 100% để đối soát tài chính
        (await db.Users.AnyAsync(u => u.Id == citizenId && !u.IsDeleted)).Should().BeTrue();
        (await db.HandoverReceipts.CountAsync(r => r.UserId == citizenId)).Should().Be(5);
    }

    // =========================================================================
    // 9. TC-ADM-03-01_P: Admin tạo mới tài khoản Staff với 0 quyền mặc định & chặn tự khóa mình (BR-ADM-01, BR-ADM-03)
    // =========================================================================
    [Fact]
    public async Task TC_ADM_03_01_P_CreateStaffAccount_InitializesZeroPermissions_AndBlocksSelfLock_BR_ADM_01()
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

        created.StaffCode.Should().StartWith("NV-");
        created.UserStatus.Should().Be(UserStatus.Active.ToString());
        created.Permissions.Should().BeEmpty(); // Least-privilege: 0 quyền mặc định (BR-ADM-01)

        // Kiểm tra chặn tự khóa chính mình (Self-Lock Guard)
        var selfLockAct = async () => await staffService.UpdateStaffStatusAsync(
            created.StaffId,
            created.StaffId,
            new UpdateStaffStatusRequest { Status = "Suspended", Reason = "Tự khóa chính mình để kiểm thử" });

        await selfLockAct.Should().ThrowAsync<ForbiddenAccessException>();
    }

    // =========================================================================
    // 10. TC-ADM-04-01_P: Cấp & thu hồi quyền Staff xóa cache tức thời và ghi diff vào AuditLog (BR-ADM-02, MSG-ADM-01)
    // =========================================================================
    [Fact]
    public async Task TC_ADM_04_01_P_UpdateStaffPermissions_InvalidatesCacheImmediately_AndLogsDiff_BR_ADM_02()
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

        // Cấp 2 quyền (gồm cả alias của FE "ai.dataset" và mã chuẩn "MANAGE_ACCOUNTS")
        var granted = await staffService.UpdateStaffPermissionsAsync(
            adminId,
            staff.StaffId,
            new UpdateStaffPermissionsRequest
            {
                Permissions = new List<string> { "ai.dataset", "MANAGE_ACCOUNTS" },
                Reason = "Phân công ca sáng"
            });

        granted.Permissions.Should().BeEquivalentTo(new[] { "CURATE_DATASET", "MANAGE_ACCOUNTS" });

        // Giả lập cache đang lưu quyền cũ của Staff, sau đó Admin gỡ quyền MANAGE_ACCOUNTS
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
        // Cache đã bị xóa tức thời mà không cần chờ hết hạn JWT (BR-ADM-02)
        cache.TryGetValue($"{MemoryAuthStateCacheInvalidator.CacheKeyPrefix}{staff.StaffId}", out _).Should().BeFalse();

        var permAudit = await db.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(a => a.EntityId == staff.StaffId.ToString());
        permAudit.Should().NotBeNull();
        permAudit!.OldValues.Should().Contain("MANAGE_ACCOUNTS");
    }

    // =========================================================================
    // 11. TC-STF-11-01_P & 12. TC-STF-11-02_N: Staff điểm danh 1 lần/ngày & tách biệt ví công dân (BR-GAM-09, BR-GAM-08)
    // =========================================================================
    [Fact]
    public async Task TC_STF_11_01_P_And_02_N_StaffCheckIn_AwardsShiftPoints_IsolatesCitizenWallet_AndBlocksDuplicateSameDay_BR_GAM_09()
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
        db.UserEcoProfiles.Add(new UserEcoProfile
        {
            UserId = staffId,
            CurrentGreenPoints = 0,
            TreeLevel = 1
        });
        await db.SaveChangesAsync();

        var attendanceService = new StaffAttendanceService(
            db,
            new AuditLogService(db, CreateCurrentUserMock(staffId, "Staff")));

        // Lần 1: Điểm danh đầu ca thành công (TC-STF-11-01_P)
        var checkInStatus = await attendanceService.CheckInAsync(
            staffId,
            new StaffCheckInRequest { ShiftNote = "Mở ca sáng xử lý hàng đợi AI" });

        checkInStatus.CheckedInToday.Should().BeTrue();
        checkInStatus.PointsEarnedToday.Should().Be(10);
        checkInStatus.CurrentStreakDay.Should().Be(1);

        // Ví điểm công dân CurrentGreenPoints tuyệt đối KHÔNG bị cộng lẫn (BR-GAM-08, BR-GAM-09)
        var ecoWallet = await db.UserEcoProfiles.FirstAsync(p => p.UserId == staffId);
        ecoWallet.CurrentGreenPoints.Should().Be(0);

        // Lần 2 trong cùng ngày (UTC+7): Bắt buộc ném BusinessRuleException BR-GAM-09 (TC-STF-11-02_N)
        var duplicateAct = async () => await attendanceService.CheckInAsync(
            staffId,
            new StaffCheckInRequest { ShiftNote = "Điểm danh lần 2 trái phép" });

        var dupEx = await duplicateAct.Should().ThrowAsync<BusinessRuleException>();
        dupEx.Which.RuleCode.Should().Be("BR-GAM-09");

        // Kết thúc ca trực (Check-out) thành công
        var checkOutStatus = await attendanceService.CheckOutAsync(
            staffId,
            new StaffCheckOutRequest { ShiftNote = "Đã xử lý xong 18 hồ sơ ca sáng" });

        checkOutStatus.CheckedOutToday.Should().BeTrue();
        checkOutStatus.TodayShiftNote.Should().Contain("18 hồ sơ");
    }
}
