using System.Net;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Ecolink.Infrastructure.Services;
using Ecolink.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Ecolink.UnitTests.Module03_RecyclerOnboarding;

[Trait("Module", "Module03_RecyclerOnboarding")]
public class RecyclerOnboardingUnitTests
{
    private static CloudflareR2StorageService CreateR2Sut(
        MockHttpMessageHandler handler,
        Dictionary<string, string?>? overrides = null)
    {
        var httpClient = new HttpClient(handler);
        var config = TestDbContextFactory.CreateConfiguration(overrides);
        return new CloudflareR2StorageService(
            httpClient,
            config,
            NullLogger<CloudflareR2StorageService>.Instance);
    }

    private static (
        RecyclerOnboardingService Sut,
        ApplicationDbContext Db,
        IEmailService EmailService) CreateOnboardingSut()
    {
        var db = TestDbContextFactory.Create();
        var emailService = Substitute.For<IEmailService>();
        var sut = new RecyclerOnboardingService(
            db,
            emailService,
            NullLogger<RecyclerOnboardingService>.Instance);
        return (sut, db, emailService);
    }

    private static ApplyRecyclerUpgradeRequest ValidUpgradeRequest() => new()
    {
        FacilityName = "  Trạm Tái Chế Hòa Lạc Xanh  ",
        RepresentativeName = "  Đỗ Văn Hùng  ",
        BusinessAddress = "  Thôn 3, Xã Tân Xã, Thạch Thất, Hà Nội  ",
        Latitude = 21.0182,
        Longitude = 105.5345,
        OperatingHours = "   ",
        ContactPhone = "0968112233",
        Description = "Thu mua nhựa [ACCEPTED:plastic,paper,metal]",
        LegalPermitUrl = "https://cdn.ecolink.test/recycler-kyb/permit.pdf",
        BusinessPhotoUrl = "https://cdn.ecolink.test/recycler-kyb/facility.jpg"
    };

    [Fact]
    public async Task UTC001_UploadRecyclerDocumentAsync_ValidPdfWithRealR2Config_SignsAwsSigV4PutAndReturnsPublicUrl()
    {
        var handler = MockHttpMessageHandler.FromJson(HttpStatusCode.OK, "");
        var sut = CreateR2Sut(handler);
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35 }; // %PDF-1.5
        using var stream = new MemoryStream(bytes);

        var res = await sut.UploadRecyclerDocumentAsync(stream, "giay_phep.PDF", "application/octet-stream", "recycler-kyb");

        res.FileUrl.Should().StartWith("https://cdn.ecolink.test/recycler-kyb/");
        res.ObjectKey.Should().EndWith(".pdf");
        res.ContentType.Should().Be("application/pdf");
        res.SizeBytes.Should().Be(bytes.Length);

        handler.CapturedRequests.Should().ContainSingle();
        var req = handler.CapturedRequests[0];
        req.Method.Should().Be(HttpMethod.Put);
        req.Headers.TryGetValues("Authorization", out var authHeaders).Should().BeTrue();
        authHeaders!.First().Should().StartWith("AWS4-HMAC-SHA256 Credential=test-r2-access-key/");
    }

    [Fact]
    public async Task UTC002_UploadRecyclerDocumentAsync_BoundaryExact10MbFile_UploadsSuccessfully()
    {
        var handler = MockHttpMessageHandler.FromJson(HttpStatusCode.OK, "");
        var sut = CreateR2Sut(handler);
        var exact10Mb = new byte[10 * 1024 * 1024];
        exact10Mb[0] = 0xFF;
        using var stream = new MemoryStream(exact10Mb);

        var res = await sut.UploadRecyclerDocumentAsync(stream, "large_photo.jpg", "image/jpeg");

        res.SizeBytes.Should().Be(10 * 1024 * 1024);
        res.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task UTC003_UploadRecyclerDocumentAsync_BoundaryExceeds10MbBy1Byte_ThrowsBrMth01()
    {
        var sut = CreateR2Sut(MockHttpMessageHandler.FromJson(HttpStatusCode.OK, ""));
        var over10Mb = new byte[(10 * 1024 * 1024) + 1];
        using var stream = new MemoryStream(over10Mb);

        var act = () => sut.UploadRecyclerDocumentAsync(stream, "over_limit.png", "image/png");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-01");
        ex.Which.MessageCode.Should().Be("MSG-GEN-01");
    }

    [Fact]
    public async Task UTC004_UploadRecyclerDocumentAsync_NullOrZeroByteStream_ThrowsBrMth01()
    {
        var sut = CreateR2Sut(MockHttpMessageHandler.FromJson(HttpStatusCode.OK, ""));

        var actNull = () => sut.UploadRecyclerDocumentAsync(null!, "doc.pdf", "application/pdf");
        (await actNull.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-MTH-01");

        using var emptyStream = new MemoryStream(Array.Empty<byte>());
        var actEmpty = () => sut.UploadRecyclerDocumentAsync(emptyStream, "empty.pdf", "application/pdf");
        (await actEmpty.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-MTH-01");
    }

    [Fact]
    public async Task UTC005_UploadRecyclerDocumentAsync_UnsupportedExtensionExeOrDocx_ThrowsBrMth01()
    {
        var sut = CreateR2Sut(MockHttpMessageHandler.FromJson(HttpStatusCode.OK, ""));
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var act = () => sut.UploadRecyclerDocumentAsync(stream, "script.exe", "application/octet-stream");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-01");
        ex.Which.MessageCode.Should().Be("MSG-GEN-01");
    }

    [Fact]
    public async Task UTC006_UploadRecyclerDocumentAsync_MissingR2CredentialsFallbackMode_ReturnsMockUrlWithoutHttpCall()
    {
        var handler = MockHttpMessageHandler.FromJson(HttpStatusCode.OK, "");
        var sut = CreateR2Sut(handler, new Dictionary<string, string?>
        {
            ["CloudflareR2:AccountId"] = "",
            ["CloudflareR2:AccessKeyId"] = "local-dev-key"
        });
        using var stream = new MemoryStream(new byte[] { 10, 20, 30 });

        var res = await sut.UploadRecyclerDocumentAsync(stream, "permit.webp", "image/webp");

        res.FileUrl.Should().Contain("recycler-kyb/");
        res.ContentType.Should().Be("image/webp");
        handler.CapturedRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task UTC007_UploadRecyclerDocumentAsync_R2Returns403Forbidden_ThrowsBrMth01MsgSys500()
    {
        var handler = MockHttpMessageHandler.FromJson(HttpStatusCode.Forbidden, "<Error><Code>SignatureDoesNotMatch</Code></Error>");
        var sut = CreateR2Sut(handler);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var act = () => sut.UploadRecyclerDocumentAsync(stream, "permit.jpg", "image/jpeg");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-01");
        ex.Which.MessageCode.Should().Be("MSG-SYS-500");
    }

    [Fact]
    public async Task UTC008_SubmitUpgradeApplicationAsync_FirstTimeActiveCitizen_CreatesPendingOfflineProfileWithDefaultHours()
    {
        var (sut, db, _) = CreateOnboardingSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_up_08",
            Email = "citizen08@ecolink.vn",
            FullName = "Đỗ Văn Hùng",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var dto = await sut.SubmitUpgradeApplicationAsync(user.Id, ValidUpgradeRequest());

        dto.UserId.Should().Be(user.Id);
        dto.FacilityName.Should().Be("Trạm Tái Chế Hòa Lạc Xanh");
        dto.OperatingHours.Should().Be("08:00 - 18:00");
        dto.VerificationStatus.Should().Be(VerificationStatus.Pending.ToString());
        dto.ActiveStatus.Should().Be(RecyclerActiveStatus.Offline.ToString());

        var profile = await db.RecyclerProfiles.FirstAsync(r => r.UserId == user.Id);
        profile.Location.Should().NotBeNull();
        profile.Location!.SRID.Should().Be(4326);
    }

    [Fact]
    public async Task UTC009_SubmitUpgradeApplicationAsync_ResubmitAfterRejected_UpdatesExistingProfileToPendingAndClearsReason_BrMth02()
    {
        var (sut, db, _) = CreateOnboardingSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_resub_09",
            Email = "citizen09@ecolink.vn",
            FullName = "Nguyễn Văn Nộp Lại",
            Role = UserRole.Citizen,
            Status = UserStatus.Active,
            RecyclerProfile = new RecyclerProfile
            {
                FacilityName = "Cơ sở Cũ",
                BusinessAddress = "Địa chỉ cũ",
                ContactPhone = "0900000001",
                Latitude = 21.0m,
                Longitude = 105.5m,
                VerificationStatus = VerificationStatus.Rejected,
                RejectionReason = "Ảnh giấy phép bị mờ",
                ActiveStatus = RecyclerActiveStatus.Offline
            }
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var dto = await sut.SubmitUpgradeApplicationAsync(user.Id, ValidUpgradeRequest());

        dto.VerificationStatus.Should().Be(VerificationStatus.Pending.ToString());
        dto.RejectionReason.Should().BeNull();
        dto.FacilityName.Should().Be("Trạm Tái Chế Hòa Lạc Xanh");
    }

    [Fact]
    public async Task UTC010_SubmitUpgradeApplicationAsync_AlreadyPending_ThrowsBrMth01MsgMth03()
    {
        var (sut, db, _) = CreateOnboardingSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_pending_10",
            Email = "citizen10@ecolink.vn",
            FullName = "Đang Chờ Duyệt",
            Role = UserRole.Citizen,
            Status = UserStatus.Active,
            RecyclerProfile = new RecyclerProfile
            {
                FacilityName = "Cơ sở Đang Chờ",
                BusinessAddress = "Hà Nội",
                ContactPhone = "0901112222",
                VerificationStatus = VerificationStatus.Pending
            }
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var act = () => sut.SubmitUpgradeApplicationAsync(user.Id, ValidUpgradeRequest());

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-01");
        ex.Which.MessageCode.Should().Be("MSG-MTH-03");
    }

    [Fact]
    public async Task UTC011_SubmitUpgradeApplicationAsync_AlreadyVerified_ThrowsBrMth01MsgMth04()
    {
        var (sut, db, _) = CreateOnboardingSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "recycler_verified_11",
            Email = "recycler11@ecolink.vn",
            FullName = "Đã Duyệt",
            Role = UserRole.Recycler,
            Status = UserStatus.Active,
            RecyclerProfile = new RecyclerProfile
            {
                FacilityName = "Cơ sở Đã Duyệt",
                BusinessAddress = "Hà Nội",
                ContactPhone = "0901112222",
                VerificationStatus = VerificationStatus.Verified
            }
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var act = () => sut.SubmitUpgradeApplicationAsync(user.Id, ValidUpgradeRequest());

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-01");
        ex.Which.MessageCode.Should().Be("MSG-MTH-04");
    }

    [Fact]
    public async Task UTC012_SubmitUpgradeApplicationAsync_SuspendedOrLockedUser_ThrowsBrAuth05()
    {
        var (sut, db, _) = CreateOnboardingSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "suspended_12",
            Email = "suspended12@ecolink.vn",
            FullName = "Bị Đình Chỉ",
            Role = UserRole.Citizen,
            Status = UserStatus.Suspended
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var act = () => sut.SubmitUpgradeApplicationAsync(user.Id, ValidUpgradeRequest());

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-05");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-04");
    }

    [Fact]
    public async Task UTC013_SubmitUpgradeApplicationAsync_NonExistentUser_ThrowsNotFoundException()
    {
        var (sut, _, _) = CreateOnboardingSut();

        var act = () => sut.SubmitUpgradeApplicationAsync(Guid.NewGuid(), ValidUpgradeRequest());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UTC014_GetMyApplicationAndGetApplicationByUserIdAsync_ReturnsDetailOrThrowsNotFound()
    {
        var (sut, db, _) = CreateOnboardingSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_get_14",
            Email = "citizen14@ecolink.vn",
            FullName = "Người Dùng 14",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Chưa nộp hồ sơ -> NotFoundException
        await ((Func<Task>)(() => sut.GetMyApplicationAsync(user.Id))).Should().ThrowAsync<NotFoundException>();
        await ((Func<Task>)(() => sut.GetApplicationByUserIdAsync(user.Id))).Should().ThrowAsync<NotFoundException>();

        // Sau khi nộp -> trả về DTO đầy đủ
        await sut.SubmitUpgradeApplicationAsync(user.Id, ValidUpgradeRequest());
        var myApp = await sut.GetMyApplicationAsync(user.Id);
        var staffView = await sut.GetApplicationByUserIdAsync(user.Id);

        myApp.FacilityName.Should().Be("Trạm Tái Chế Hòa Lạc Xanh");
        staffView.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task UTC015_GetApplicationsForStaffAsync_FiltersByStatusAndKeywordAndClampsPagination()
    {
        var (sut, db, _) = CreateOnboardingSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "filter_target_15",
            Email = "unique_filter15@ecolink.vn",
            FullName = "Trần Văn Lọc",
            Role = UserRole.Citizen,
            Status = UserStatus.Active,
            RecyclerProfile = new RecyclerProfile
            {
                FacilityName = "Vựa Tái Chế Đặc Biệt 15",
                BusinessAddress = "Khu CNC Hòa Lạc",
                ContactPhone = "0999888777",
                VerificationStatus = VerificationStatus.Pending,
                ActiveStatus = RecyclerActiveStatus.Offline
            }
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var page = await sut.GetApplicationsForStaffAsync(new RecyclerApplicationFilterRequest
        {
            Status = "Pending",
            Keyword = "Đặc Biệt 15",
            PageNumber = 0,   // clamp -> 1
            PageSize = 500,   // clamp -> 100
            SortOrder = "desc"
        });

        page.PageNumber.Should().Be(1);
        page.PageSize.Should().Be(100);
        page.Items.Should().ContainSingle(i => i.UserId == user.Id);
    }

    [Fact]
    public async Task UTC016_VerifyApplicationAsync_StaffApprovesPendingProfile_UpgradesRoleToRecyclerOnlineAndSeedsPrices()
    {
        var (sut, db, emailService) = CreateOnboardingSut();
        var staffId = Guid.NewGuid();
        var applicant = new User
        {
            Id = Guid.NewGuid(),
            Username = "applicant_approve_16",
            Email = "approve16@ecolink.vn",
            FullName = "Chủ Cơ Sở 16",
            Role = UserRole.Citizen,
            Status = UserStatus.PendingVerification,
            RecyclerProfile = new RecyclerProfile
            {
                FacilityName = "Trạm Thu Gom 16",
                BusinessAddress = "Hòa Lạc, Hà Nội",
                ContactPhone = "0911223344",
                Latitude = 21.0182m,
                Longitude = 105.5345m,
                Description = "Thu mua [ACCEPTED:plastic,metal]",
                VerificationStatus = VerificationStatus.Pending,
                ActiveStatus = RecyclerActiveStatus.Offline
            }
        };
        db.Users.Add(applicant);
        await db.SaveChangesAsync();

        var res = await sut.VerifyApplicationAsync(staffId, applicant.Id, new VerifyRecyclerApplicationRequest
        {
            IsApproved = true
        });

        res.VerificationStatus.Should().Be(VerificationStatus.Verified.ToString());
        res.ActiveStatus.Should().Be(RecyclerActiveStatus.Online.ToString());
        res.CurrentRole.Should().Be(UserRole.Recycler.ToString());

        var activePrices = await db.RecyclerPrices.Where(p => p.RecyclerId == applicant.Id).ToListAsync();
        activePrices.Should().HaveCountGreaterThanOrEqualTo(2);
        await emailService.Received(1).SendEmailAsync(applicant.Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTC017_VerifyApplicationAsync_StaffRejectsWithReason_SetsRejectedStatusAndSavesReason_BrMth02()
    {
        var (sut, db, emailService) = CreateOnboardingSut();
        var applicant = new User
        {
            Id = Guid.NewGuid(),
            Username = "applicant_reject_17",
            Email = "reject17@ecolink.vn",
            FullName = "Chủ Cơ Sở 17",
            Role = UserRole.Citizen,
            Status = UserStatus.Active,
            RecyclerProfile = new RecyclerProfile
            {
                FacilityName = "Trạm Thu Gom 17",
                BusinessAddress = "Hà Nội",
                ContactPhone = "0911223355",
                VerificationStatus = VerificationStatus.Pending,
                ActiveStatus = RecyclerActiveStatus.Offline
            }
        };
        db.Users.Add(applicant);
        await db.SaveChangesAsync();

        var res = await sut.VerifyApplicationAsync(Guid.NewGuid(), applicant.Id, new VerifyRecyclerApplicationRequest
        {
            IsApproved = false,
            RejectionReason = "  Giấy phép kinh doanh hết hạn hiệu lực  "
        });

        res.VerificationStatus.Should().Be(VerificationStatus.Rejected.ToString());
        res.RejectionReason.Should().Be("Giấy phép kinh doanh hết hạn hiệu lực");
        res.ActiveStatus.Should().Be(RecyclerActiveStatus.Offline.ToString());
        res.CurrentRole.Should().Be(UserRole.Citizen.ToString());
        await emailService.Received(1).SendEmailAsync(applicant.Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTC018_VerifyApplicationAsync_StaffRejectsWithoutReason_ThrowsBrMth02MsgMth05()
    {
        var (sut, db, _) = CreateOnboardingSut();
        var applicant = new User
        {
            Id = Guid.NewGuid(),
            Username = "applicant_noreason_18",
            Email = "noreason18@ecolink.vn",
            FullName = "Chủ Cơ Sở 18",
            Role = UserRole.Citizen,
            Status = UserStatus.Active,
            RecyclerProfile = new RecyclerProfile
            {
                FacilityName = "Trạm Thu Gom 18",
                BusinessAddress = "Hà Nội",
                ContactPhone = "0911223366",
                VerificationStatus = VerificationStatus.Pending
            }
        };
        db.Users.Add(applicant);
        await db.SaveChangesAsync();

        var act = () => sut.VerifyApplicationAsync(Guid.NewGuid(), applicant.Id, new VerifyRecyclerApplicationRequest
        {
            IsApproved = false,
            RejectionReason = "   "
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-02");
        ex.Which.MessageCode.Should().Be("MSG-MTH-05");
    }

    [Fact]
    public async Task UTC019_VerifyApplicationAsync_ProfileAlreadyVerifiedOrRejected_ThrowsBrMth01()
    {
        var (sut, db, _) = CreateOnboardingSut();
        var applicant = new User
        {
            Id = Guid.NewGuid(),
            Username = "already_verified_19",
            Email = "verified19@ecolink.vn",
            FullName = "Chủ Cơ Sở 19",
            Role = UserRole.Recycler,
            Status = UserStatus.Active,
            RecyclerProfile = new RecyclerProfile
            {
                FacilityName = "Trạm Thu Gom 19",
                BusinessAddress = "Hà Nội",
                ContactPhone = "0911223377",
                VerificationStatus = VerificationStatus.Verified
            }
        };
        db.Users.Add(applicant);
        await db.SaveChangesAsync();

        var act = () => sut.VerifyApplicationAsync(Guid.NewGuid(), applicant.Id, new VerifyRecyclerApplicationRequest
        {
            IsApproved = true
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-01");
        ex.Which.MessageCode.Should().Be("MSG-GEN-01");
    }

    [Fact]
    public async Task UTC020_VerifyApplicationAsync_EmailServiceThrowsException_DoesNotRollbackApprovalDecision_BrNtf06()
    {
        var (sut, db, emailService) = CreateOnboardingSut();
        emailService.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP server unavailable"));

        var applicant = new User
        {
            Id = Guid.NewGuid(),
            Username = "smtp_fail_20",
            Email = "smtpfail20@ecolink.vn",
            FullName = "Chủ Cơ Sở 20",
            Role = UserRole.Citizen,
            Status = UserStatus.Active,
            RecyclerProfile = new RecyclerProfile
            {
                FacilityName = "Trạm Thu Gom 20",
                BusinessAddress = "Hà Nội",
                ContactPhone = "0911223388",
                Latitude = 21.01m,
                Longitude = 105.52m,
                VerificationStatus = VerificationStatus.Pending
            }
        };
        db.Users.Add(applicant);
        await db.SaveChangesAsync();

        var res = await sut.VerifyApplicationAsync(Guid.NewGuid(), applicant.Id, new VerifyRecyclerApplicationRequest
        {
            IsApproved = true
        });

        res.VerificationStatus.Should().Be(VerificationStatus.Verified.ToString());
        (await db.RecyclerProfiles.FirstAsync(r => r.UserId == applicant.Id))
            .VerificationStatus.Should().Be(VerificationStatus.Verified);
    }
}
