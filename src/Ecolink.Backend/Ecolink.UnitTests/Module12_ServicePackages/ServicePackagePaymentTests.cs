using System.IO.Compression;
using System.Text;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Application.Validators;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Ecolink.Infrastructure.Services;
using Ecolink.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ecolink.UnitTests.Module12_ServicePackages;

/// <summary>
/// Bộ kiểm thử đơn vị toàn diện cho Module M-12 / FE-14 (Module 10 trong bộ Report-5.1):
/// In-System Service Packages &amp; VietQR Integration (U-16, R-07, A-10, A-11).
/// Chuẩn hóa 22 test cases (UTC001 -&gt; UTC022: 10 Normal, 8 Abnormal, 4 Boundary)
/// đối chiếu trực tiếp với tài liệu đặc tả 10_unit_test_in_system_service_packages_vietqr.md.
/// </summary>
[Trait("Module", "M12_ServicePackages")]
public class ServicePackagePaymentTests
{
    private static (
        PaymentService PaymentSvc,
        ServicePackageService PackageSvc,
        AdminReconciliationService ReconSvc,
        SubscriptionEntitlementService EntitlementSvc)
        CreateServices(
            ApplicationDbContext db,
            Guid currentUserId,
            string currentRole = "User",
            Dictionary<string, string?>? configOverrides = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["VietQr:BankCode"] = "TPB",
            ["VietQr:BankName"] = "TPBank",
            ["VietQr:AccountNo"] = "05773216801",
            ["VietQr:AccountName"] = "ECOLINK PLATFORM"
        };

        if (configOverrides != null)
        {
            foreach (var kv in configOverrides)
            {
                defaults[kv.Key] = kv.Value;
            }
        }

        var config = TestDbContextFactory.CreateConfiguration(defaults);
        var cache = new MemoryCache(new MemoryCacheOptions());

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns((Guid?)currentUserId);
        currentUser.Role.Returns(currentRole);
        currentUser.IsAuthenticated.Returns(true);
        currentUser.IpAddress.Returns("127.0.0.1");
        currentUser.UserAgent.Returns("EcoLink-UnitTest/1.0");

        var payOsSvc = Substitute.For<IPayOsService>();
        var auditSvc = new AuditLogService(db, currentUser);
        var entitlementSvc = new SubscriptionEntitlementService(db, cache);
        var packageSvc = new ServicePackageService(db, entitlementSvc, auditSvc);
        var reconSvc = new AdminReconciliationService(db, entitlementSvc, auditSvc);
        var paymentSvc = new PaymentService(
            db,
            entitlementSvc,
            config,
            NullLogger<PaymentService>.Instance,
            payOsSvc);

        return (paymentSvc, packageSvc, reconSvc, entitlementSvc);
    }

    // =========================================================================
    // NORMAL TEST CASES (UTC001 -> UTC010) — 10 TCs
    // =========================================================================

    // UTC001 [Normal] [U-16]: Khởi tạo đơn PREMIUM & kích hoạt 30 ngày Ad-Free qua Webhook khớp tiền
    [Fact]
    public async Task UTC001_CreatePayInOrder_ValidPremiumPackage_AndExactWebhookActivates30DayAdFree_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var citizenId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = citizenId,
            Username = "cit01",
            Email = "cit01@email.vn",
            FullName = "Nguyễn An",
            PasswordHash = "h",
            Role = UserRole.User,
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, entitlementSvc) = CreateServices(db, citizenId, "User");

        var order = await paymentSvc.CreatePayInOrderAsync(
            citizenId,
            UserRole.User,
            new CreatePayInRequest { PackageCode = "PREMIUM" });

        order.PackageCode.Should().Be("PREMIUM");
        order.Amount.Should().Be(59_000m);
        order.DurationDays.Should().Be(30);
        order.Status.Should().Be("PENDING");
        order.QrCodeUrl.Should().Contain("img.vietqr.io");
        order.TransferContent.Should().Contain(order.OrderCode);
        order.ExpiresAt.Should().BeCloseTo(order.CreatedAt.AddMinutes(15), TimeSpan.FromSeconds(5));

        var paidOrder = await paymentSvc.SimulateTransferAsync(order.OrderCode, new SimulateTransferRequest { Scenario = "Exact" });
        paidOrder.Status.Should().Be("PAID");
        paidOrder.PaidAmount.Should().Be(59_000m);
        paidOrder.ReconciliationStatus.Should().Be("Matched");
        paidOrder.ActivatedSubscription.Should().NotBeNull();
        paidOrder.ActivatedSubscription!.RemainingDays.Should().BeInRange(29, 30);

        var mySubs = await packageSvc.GetMySubscriptionsAsync(citizenId);
        mySubs.CurrentTier.Should().Be("Premium");
        mySubs.Entitlements.IsAdFree.Should().BeTrue();
        mySubs.Entitlements.HasEcoPlusBadge.Should().BeTrue();
        mySubs.Entitlements.HasVerifiedBlueBadge.Should().BeFalse();

        var ent = await entitlementSvc.GetUserEntitlementsAsync(citizenId);
        ent.IsAdFree.Should().BeTrue();
    }

    // UTC002 [Normal] [U-16]: Gia hạn sớm cùng gói PREMIUM cộng dồn nối tiếp từ ngày hết hạn cũ (BR-PAY-04)
    [Fact]
    public async Task UTC002_EarlyRenewalSamePackage_StacksRemainingDaysFromOldExpiry_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var citizenId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = citizenId,
            Username = "cit02",
            Email = "cit02@email.vn",
            FullName = "Trần Bình",
            PasswordHash = "h",
            Role = UserRole.User,
            Status = UserStatus.Active
        });

        var oldOrder = new ServiceOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ECOOLD0001",
            UserId = citizenId,
            PackageId = CanonicalServicePackages.PremiumId,
            PackageCodeSnapshot = CanonicalServicePackages.PremiumCode,
            PackageNameSnapshot = "Premium (Tắt quảng cáo)",
            DurationDaysSnapshot = 30,
            FeaturesSnapshot = CanonicalServicePackages.PremiumFeaturesJson,
            ExpectedAmountVnd = 59_000m,
            PaidAmountVnd = 59_000m,
            QrCodeUrl = "https://qr.test/old",
            TransferContent = "ECOOLD0001",
            BankCode = "TPB",
            BankName = "TPBank",
            AccountNo = "05773216801",
            AccountName = "ECOLINK",
            OrderStatus = ServiceOrderStatus.Paid,
            PaymentStatus = ServicePaymentStatus.Success,
            ReconciliationStatus = ReconciliationStatus.Matched,
            QrExpiresAt = DateTime.UtcNow.AddDays(-19),
            PaidAt = DateTime.UtcNow.AddDays(-20)
        };
        var initialExpiry = DateTime.UtcNow.AddDays(10);
        db.ServiceOrders.Add(oldOrder);
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = citizenId,
            PackageId = CanonicalServicePackages.PremiumId,
            SourceOrderId = oldOrder.Id,
            PackageCode = CanonicalServicePackages.PremiumCode,
            FeaturesSnapshot = CanonicalServicePackages.PremiumFeaturesJson,
            StartsAt = DateTime.UtcNow.AddDays(-20),
            ExpiresAt = initialExpiry,
            Status = SubscriptionStatus.Active
        });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, citizenId, "User");
        var renewOrder = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await paymentSvc.SimulateTransferAsync(renewOrder.OrderCode);

        var mySubs = await packageSvc.GetMySubscriptionsAsync(citizenId);
        var maxExpiry = mySubs.ActiveSubscriptions.Max(s => s.ExpiresAt);
        maxExpiry.Should().BeCloseTo(initialExpiry.AddDays(30), TimeSpan.FromMinutes(1));
    }

    // UTC003 [Normal] [R-07]: Nâng bậc tức thì từ PREMIUM lên PRO (365 ngày) cho Recycler đã duyệt KYB (BR-PAY-04)
    [Fact]
    public async Task UTC003_UpgradeFromActivePremiumToPro_VerifiedRecycler_ActivatesProImmediately_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var recId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = recId,
            Username = "rec_up",
            Email = "recup@rec.vn",
            FullName = "Vựa Upgrade",
            PasswordHash = "h",
            Role = UserRole.Recycler,
            Status = UserStatus.Active
        });
        db.RecyclerProfiles.Add(new RecyclerProfile
        {
            UserId = recId,
            FacilityName = "Vựa Upgrade",
            RepresentativeName = "C",
            BusinessAddress = "HN",
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, recId, "Recycler");

        var premOrder = await paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await paymentSvc.SimulateTransferAsync(premOrder.OrderCode);

        var proOrder = await paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        proOrder.Amount.Should().Be(1_000_000m);
        proOrder.DurationDays.Should().Be(365);
        await paymentSvc.SimulateTransferAsync(proOrder.OrderCode);

        var subs = await packageSvc.GetMySubscriptionsAsync(recId);
        subs.CurrentTier.Should().Be("Pro");
        subs.Entitlements.HasVerifiedBlueBadge.Should().BeTrue();
        subs.Entitlements.IsFeaturedMapMarker.Should().BeTrue();
        subs.Entitlements.MapPriorityBoost.Should().Be(100);

        var proSub = subs.ActiveSubscriptions.First(s => s.PackageCode == "PRO");
        proSub.StartsAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        proSub.RemainingDays.Should().BeInRange(364, 365);
    }

    // UTC004 [Normal] [R-07]: Gói PRO cấp Tích xanh & đẩy ưu tiên bản đồ GeoMatching trước cơ sở Basic gần hơn
    [Fact]
    public async Task UTC004_ProSubscriptionEntitlement_GrantsVerifiedBadgeAndFeaturedGeoSort_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var catId = Guid.NewGuid();
        db.WasteCategories.Add(new WasteCategory
        {
            Id = catId,
            Code = "plastic",
            Name = "Nhựa",
            StandardBinColor = "Cam",
            IsRecyclable = true,
            Status = "Active"
        });

        var nearBasicId = Guid.NewGuid();
        var farProId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = nearBasicId, Username = "rec_basic", Email = "basic@rec.vn", FullName = "Vựa Gần Basic", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active },
            new User { Id = farProId, Username = "rec_pro", Email = "pro@rec.vn", FullName = "Vựa Xa Pro", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active });

        db.RecyclerProfiles.AddRange(
            new RecyclerProfile
            {
                UserId = nearBasicId,
                FacilityName = "Vựa Gần Basic",
                RepresentativeName = "A",
                BusinessAddress = "Cách 1km",
                Latitude = 21.0200m,
                Longitude = 105.5253m,
                VerificationStatus = VerificationStatus.Verified,
                ActiveStatus = RecyclerActiveStatus.Online
            },
            new RecyclerProfile
            {
                UserId = farProId,
                FacilityName = "Vựa Xa Pro",
                RepresentativeName = "B",
                BusinessAddress = "Cách 3km",
                Latitude = 21.0380m,
                Longitude = 105.5253m,
                VerificationStatus = VerificationStatus.Verified,
                ActiveStatus = RecyclerActiveStatus.Online
            });

        db.RecyclerPrices.AddRange(
            new RecyclerPrice { Id = Guid.NewGuid(), RecyclerId = nearBasicId, WasteCategoryId = catId, PricePerKg = 7000m, MinQuantityKg = 1m, Status = PriceStatus.Active },
            new RecyclerPrice { Id = Guid.NewGuid(), RecyclerId = farProId, WasteCategoryId = catId, PricePerKg = 7000m, MinQuantityKg = 1m, Status = PriceStatus.Active });

        await db.SaveChangesAsync();

        var (paymentSvc, _, _, entitlementSvc) = CreateServices(db, farProId, "Recycler");
        var proOrder = await paymentSvc.CreatePayInOrderAsync(farProId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        await paymentSvc.SimulateTransferAsync(proOrder.OrderCode);

        var geoSvc = new GeoMatchingService(NullLogger<GeoMatchingService>.Instance, db, null, entitlementSvc);
        var partners = (await geoSvc.FindNearestPartnersAsync(21.0124, 105.5253, "plastic", 10.0)).ToList();

        partners.Should().HaveCount(2);
        partners[0].Id.Should().Be(farProId);
        partners[0].IsFeatured.Should().BeTrue();
        partners[0].HasVerifiedBadge.Should().BeTrue();
        partners[1].Id.Should().Be(nearBasicId);
        partners[1].IsFeatured.Should().BeFalse();
        partners[0].DistanceKm.Should().BeGreaterThan(partners[1].DistanceKm);
    }

    // UTC005 [Normal] [U-16]: Xử lý Webhook Idempotent khi gửi trùng 3 lần liên tiếp (BR-PAY-02)
    [Fact]
    public async Task UTC005_DuplicateWebhookDelivery_ActivatesSubscriptionExactlyOnce_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "wh01",
            Email = "wh01@email.vn",
            FullName = "Lê Trùng",
            PasswordHash = "h",
            Role = UserRole.User,
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();

        var (paymentSvc, _, _, _) = CreateServices(db, userId, "User");
        var order = await paymentSvc.CreatePayInOrderAsync(userId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });

        var payload = new VietQrWebhookPayload
        {
            OrderCode = order.OrderCode,
            Amount = 59_000m,
            Reference = "FT_IDEMPOTENT_999"
        };

        var r1 = await paymentSvc.ProcessWebhookAsync(payload);
        var r2 = await paymentSvc.ProcessWebhookAsync(payload);
        var r3 = await paymentSvc.ProcessWebhookAsync(payload);

        r1.Status.Should().Be("PAID");
        r2.Status.Should().Be("ALREADY_PROCESSED");
        r3.Status.Should().Be("ALREADY_PROCESSED");
        (await db.UserSubscriptions.CountAsync(s => s.SourceOrderId == order.Id)).Should().Be(1);
    }

    // UTC006 [Normal] [U-16]: Tra cứu bảng giá gói dịch vụ & Hủy đơn VietQR đang chờ Pending
    [Fact]
    public async Task UTC006_GetAvailablePackagesAndCancelPendingOrder_MarksOrderCancelled_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var recId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = recId,
            Username = "rec_cat",
            Email = "rec_cat@thugom.vn",
            FullName = "Vựa Danh Mục",
            PasswordHash = "h",
            Role = UserRole.Recycler,
            Status = UserStatus.Active
        });
        db.RecyclerProfiles.Add(new RecyclerProfile
        {
            UserId = recId,
            FacilityName = "Vựa Danh Mục",
            RepresentativeName = "Q",
            BusinessAddress = "Đà Nẵng",
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, recId, "Recycler");

        var catalog = await packageSvc.GetAvailablePackagesAsync(recId, UserRole.Recycler);
        catalog.Select(c => c.PackageCode).Should().ContainInOrder("BASIC", "PREMIUM", "PRO");
        catalog.First(c => c.PackageCode == "PRO").IsEligible.Should().BeTrue();
        catalog.First(c => c.PackageCode == "BASIC").IsCurrentPlan.Should().BeTrue();

        var order = await paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        var cancelled = await paymentSvc.CancelPendingOrderAsync(recId, order.OrderCode);
        cancelled.Status.Should().Be("CANCELLED");

        var refreshed = await paymentSvc.GetOrderForUserAsync(recId, false, order.OrderCode);
        refreshed.Status.Should().Be("CANCELLED");

        var list = await paymentSvc.GetUserOrdersAsync(recId, "Cancelled");
        list.Items.Should().ContainSingle(o => o.OrderCode == order.OrderCode);
    }

    // UTC007 [Normal] [A-10]: Admin tạo gói mới, cập nhật giá với Grandfathering giữ nguyên snapshot cũ & ghi AuditLog
    [Fact]
    public async Task UTC007_AdminCreateAndUpdateServicePackage_GrandfathersExistingSubscriptions_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = adminId, Username = "admin10", Email = "admin10@ecolink.vn", FullName = "Admin 10", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = citizenId, Username = "cit10", Email = "cit10@email.vn", FullName = "Citizen 10", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, adminId, "Admin");

        // 1. Citizen mua PREMIUM ở mức giá cũ 59,000đ
        var oldOrder = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await paymentSvc.SimulateTransferAsync(oldOrder.OrderCode);

        // 2. Admin tạo mới một gói thương mại và tăng giá PREMIUM lên 79,000đ
        var createdPkg = await packageSvc.CreatePackageAsync(adminId, new UpsertServicePackageRequest
        {
            PackageCode = "RECYCLER_VIP",
            PackageName = "Gói VIP Đẩy Tin",
            TargetRole = "Recycler",
            RequiresVerifiedKyb = true,
            PriceVnd = 299_000m,
            DurationDays = 90,
            DisplayOrder = 40,
            IsActive = true,
            Features = new ServicePackageFeaturesDto { AdFree = true, VerifiedBlueBadge = true, FeaturedMapMarker = true, MapPriorityBoost = 80, DailyPointMultiplier = 1.4m }
        });
        createdPkg.PackageCode.Should().Be("RECYCLER_VIP");

        await packageSvc.UpdatePackageAsync(adminId, CanonicalServicePackages.PremiumId, new UpsertServicePackageRequest
        {
            PackageCode = "PREMIUM",
            PackageName = "Premium (Điều chỉnh giá)",
            TargetRole = "All",
            PriceVnd = 79_000m,
            DurationDays = 45,
            DisplayOrder = 20,
            IsActive = true,
            Features = new ServicePackageFeaturesDto { AdFree = true, EcoPlusBadge = true, DailyPointMultiplier = 1.3m }
        });

        // 3. Đơn cũ giữ nguyên giá & thời hạn 30d; đơn mới áp giá 79,000đ & 45d (BR-PAY-05)
        var oldOrderReloaded = await paymentSvc.GetOrderForUserAsync(citizenId, false, oldOrder.OrderCode);
        oldOrderReloaded.Amount.Should().Be(59_000m);
        oldOrderReloaded.DurationDays.Should().Be(30);

        var newOrder = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });
        newOrder.Amount.Should().Be(79_000m);
        newOrder.DurationDays.Should().Be(45);

        (await db.AuditLogs.AnyAsync(a => a.Action == "CREATE_SERVICE_PACKAGE")).Should().BeTrue();
        (await db.AuditLogs.AnyAsync(a => a.Action == "UPDATE_SERVICE_PACKAGE")).Should().BeTrue();
    }

    // UTC008 [Normal] [A-11]: Admin lọc sổ cái & dùng ActivateOrder đối soát thủ công đơn Underpaid
    [Fact]
    public async Task UTC008_AdminResolveUnderpaidOrderWithActivateOrder_ActivatesPackageAndLogsAudit_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = adminId, Username = "admin_u", Email = "admin_u@ecolink.vn", FullName = "Admin Rec", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = citizenId, Username = "cit_u", Email = "cit_u@email.vn", FullName = "Nguyễn Thiếu", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, reconSvc, _) = CreateServices(db, adminId, "Admin");
        var order = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await paymentSvc.SimulateTransferAsync(order.OrderCode, new SimulateTransferRequest { Scenario = "Underpaid", SimulatedAmount = 40_000m });

        var anomalyLedger = await reconSvc.GetPaymentLedgerAsync(new ReconciliationLedgerQuery { OnlyAnomalies = true });
        anomalyLedger.Items.Items.Should().ContainSingle(i => i.ReferenceCode == order.OrderCode && i.ReconciliationStatus == "Underpaid");

        var resolved = await reconSvc.ResolvePaymentAnomalyAsync(adminId, order.OrderCode, new ResolvePaymentAnomalyRequest
        {
            Action = "ApproveAndActivate",
            ResolutionBankRef = "FT_COMPENSATE_19000",
            ResolutionNote = "Đã nhận 19.000đ chuyển bù, duyệt kích hoạt gói."
        });

        resolved.ReconciliationStatus.Should().Be("Resolved");
        resolved.OrderStatus.Should().Be("Paid");

        var subs = await packageSvc.GetMySubscriptionsAsync(citizenId);
        subs.CurrentTier.Should().Be("Premium");
        (await db.AuditLogs.AnyAsync(a => a.Action == "RESOLVE_PAYMENT_ANOMALY" && a.EntityId == order.OrderCode)).Should().BeTrue();
    }

    // UTC009 [Normal] [A-11]: Admin khớp lệnh thủ công Webhook mồ côi UnmatchedReference với đơn Pending (BR-PAY-06)
    [Fact]
    public async Task UTC009_AdminManualMatchOrphanWebhook_MatchesPendingOrderAndActivatesSubscription_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var recId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = adminId, Username = "admin_m", Email = "admin_m@ecolink.vn", FullName = "Admin Match", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = recId, Username = "rec_m", Email = "rec_m@thugom.vn", FullName = "Vựa Quên Nội Dung", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active });
        db.RecyclerProfiles.Add(new RecyclerProfile
        {
            UserId = recId,
            FacilityName = "Vựa Quên ND",
            RepresentativeName = "E",
            BusinessAddress = "HN",
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, reconSvc, _) = CreateServices(db, adminId, "Admin");
        var order = await paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });

        var orphanWh = await paymentSvc.ProcessWebhookAsync(new VietQrWebhookPayload
        {
            OrderCode = null,
            Amount = 1_000_000m,
            Description = "CHUYEN TIEN DICH VU KHONG CO MA DON",
            Reference = "FT_ORPHAN_2026"
        });
        orphanWh.Status.Should().Be("ANOMALY_FLAGGED");
        orphanWh.ReconciliationStatus.Should().Be("UnmatchedReference");
        orphanWh.WebhookCode.Should().StartWith("WH-");

        var resolved = await reconSvc.ResolvePaymentAnomalyAsync(adminId, orphanWh.WebhookCode!, new ResolvePaymentAnomalyRequest
        {
            Action = "ManualMatchOrder",
            TargetOrderCode = order.OrderCode,
            ResolutionNote = "Xác nhận sao kê khớp với đơn PRO của Vựa Quên ND."
        });

        resolved.ReferenceCode.Should().Be(order.OrderCode);
        resolved.OrderStatus.Should().Be("Paid");
        resolved.ReconciliationStatus.Should().Be("Resolved");

        var subs = await packageSvc.GetMySubscriptionsAsync(recId);
        subs.CurrentTier.Should().Be("Pro");
    }

    // UTC010 [Normal] [A-11]: Admin xuất bảng kê CSV & XLSX che mờ PII Email/SĐT & bảo vệ AuditLog bất biến (BR-ADM-06)
    [Fact]
    public async Task UTC010_AdminExportReconciliationReport_CsvAndXlsx_MasksPiiAndProtectsAuditLog_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = adminId, Username = "admin_exp", Email = "admin_exp@ecolink.vn", FullName = "Admin Exp", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = userId, Username = "pii_user", Email = "nguyen.secret.pii@gmail.com", PhoneNumber = "0987654321", FullName = "Nguyễn Bảo Mật", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, _, reconSvc, _) = CreateServices(db, adminId, "Admin");
        var order = await paymentSvc.CreatePayInOrderAsync(userId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await paymentSvc.SimulateTransferAsync(order.OrderCode);

        var csvResult = await reconSvc.ExportReconciliationReportAsync(adminId, new ReconciliationExportQuery { Format = "csv" });
        csvResult.ContentType.Should().StartWith("text/csv");
        csvResult.RowCount.Should().BeGreaterThanOrEqualTo(1);
        var csvText = Encoding.UTF8.GetString(csvResult.Content);
        csvText.Should().Contain(order.OrderCode);
        csvText.Should().Contain("ng***@gmail.com");
        csvText.Should().Contain("098****321");
        csvText.Should().NotContain("nguyen.secret.pii@gmail.com");
        csvText.Should().NotContain("0987654321");

        var xlsxResult = await reconSvc.ExportReconciliationReportAsync(adminId, new ReconciliationExportQuery { Format = "xlsx" });
        xlsxResult.ContentType.Should().Contain("spreadsheetml.sheet");
        xlsxResult.Content[0].Should().Be((byte)'P');
        xlsxResult.Content[1].Should().Be((byte)'K');
        using var zip = new ZipArchive(new MemoryStream(xlsxResult.Content), ZipArchiveMode.Read);
        zip.Entries.Should().Contain(e => e.FullName == "xl/worksheets/sheet1.xml");

        // BR-ADM-05: Bảo vệ nhật ký xuất báo cáo bất biến
        var auditEntry = await db.AuditLogs.FirstAsync(a => a.Action == "EXPORT_RECONCILIATION");
        db.AuditLogs.Remove(auditEntry);
        var mutateAudit = () => db.SaveChangesAsync();
        await mutateAudit.Should().ThrowAsync<InvalidOperationException>();
    }

    // =========================================================================
    // ABNORMAL TEST CASES (UTC011 -> UTC018) — 8 TCs
    // =========================================================================

    // UTC011 [Abnormal] [U-16]: Chặn giả mạo giảm số tiền từ Client so với giá gói trong DB (BR-PAY-01)
    [Fact]
    public async Task UTC011_CreatePayInOrder_TamperedClientAmount_ThrowsBrPay01_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var citizenId = Guid.NewGuid();
        db.Users.Add(new User { Id = citizenId, Username = "cit03", Email = "cit03@email.vn", FullName = "Lê Gian", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, _, _, _) = CreateServices(db, citizenId, "User");

        var act = () => paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest
        {
            PackageCode = "PREMIUM",
            Amount = 1_000m
        });

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.RuleCode == "BR-PAY-01");
    }

    // UTC012 [Abnormal] [U-16]: Bảo mật sở hữu đơn thanh toán theo UserId chống IDOR (BR-AUTH-08)
    [Fact]
    public async Task UTC012_OtherCitizenViewOrCancelOrder_ThrowsForbiddenIdor_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = userA, Username = "usera", Email = "a@email.vn", FullName = "User A", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active },
            new User { Id = userB, Username = "userb", Email = "b@email.vn", FullName = "User B", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, _, _, _) = CreateServices(db, userA, "User");
        var orderA = await paymentSvc.CreatePayInOrderAsync(userA, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });

        var listB = await paymentSvc.GetUserOrdersAsync(userB, null);
        listB.Items.Should().BeEmpty();

        var viewAct = () => paymentSvc.GetOrderForUserAsync(userB, false, orderA.OrderCode);
        await viewAct.Should().ThrowAsync<ForbiddenAccessException>();

        var cancelAct = () => paymentSvc.CancelPendingOrderAsync(userB, orderA.OrderCode);
        await cancelAct.Should().ThrowAsync<ForbiddenAccessException>();
    }

    // UTC013 [Abnormal] [U-16]: Chặn tạo QR cho gói miễn phí BASIC (0đ) & Citizen cố mua gói PRO của Recycler
    [Fact]
    public async Task UTC013_PurchaseFreeBasicOrCitizenBuyPro_Rejected_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var citizenId = Guid.NewGuid();
        db.Users.Add(new User { Id = citizenId, Username = "cit05", Email = "cit05@email.vn", FullName = "Phạm Mai", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, _, _, _) = CreateServices(db, citizenId, "User");

        var buyBasic = () => paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "BASIC" });
        await buyBasic.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.RuleCode == "BR-PAY-01");

        var buyPro = () => paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PRO" });
        await buyPro.Should().ThrowAsync<ForbiddenAccessException>();
    }

    // UTC014 [Abnormal] [R-07]: Chặn Recycler chưa duyệt KYB mua gói PRO (BR-PAY-03 / MSG-PAY-06)
    [Fact]
    public async Task UTC014_UnverifiedKybRecyclerBuyPro_ThrowsMsgPay06_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var recyclerId = Guid.NewGuid();
        db.Users.Add(new User { Id = recyclerId, Username = "rec_wait", Email = "recwait@thugom.vn", FullName = "Vựa Chờ", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active });
        db.RecyclerProfiles.Add(new RecyclerProfile
        {
            UserId = recyclerId,
            FacilityName = "Vựa Chờ Duyệt",
            RepresentativeName = "Chủ Vựa",
            BusinessAddress = "Hà Nội",
            VerificationStatus = VerificationStatus.Pending,
            ActiveStatus = RecyclerActiveStatus.Offline
        });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, recyclerId, "Recycler");

        var catalog = await packageSvc.GetAvailablePackagesAsync(recyclerId, UserRole.Recycler);
        catalog.First(c => c.PackageCode == "PRO").IsEligible.Should().BeFalse();

        var act = () => paymentSvc.CreatePayInOrderAsync(recyclerId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.MessageCode == "MSG-PAY-06");
    }

    // UTC015 [Abnormal] [U-16]: Chuyển thiếu tiền -> Gắn cờ Underpaid, giữ Pending, KHÔNG kích hoạt gói
    [Fact]
    public async Task UTC015_UnderpaidWebhook_KeepsPendingAndFlagsAnomalyWithoutActivation_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Username = "wh02", Email = "wh02@email.vn", FullName = "Trần Thiếu", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, userId, "User");
        var order = await paymentSvc.CreatePayInOrderAsync(userId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });

        var updated = await paymentSvc.SimulateTransferAsync(order.OrderCode, new SimulateTransferRequest
        {
            Scenario = "Underpaid",
            SimulatedAmount = 50_000m
        });

        updated.Status.Should().Be("PENDING");
        updated.ReconciliationStatus.Should().Be("Underpaid");
        updated.PaidAmount.Should().Be(50_000m);

        var mySubs = await packageSvc.GetMySubscriptionsAsync(userId);
        mySubs.CurrentTier.Should().Be("Basic");
        mySubs.ActiveSubscriptions.Should().BeEmpty();
    }

    // UTC016 [Abnormal] [U-16]: Chặn Webhook giả mạo chữ ký / Webhook Secret sai (MSG-PAY-05) & ghi log kiểm toán
    [Fact]
    public async Task UTC016_InvalidWebhookSignature_RejectedWithMsgPay05AndLogsAudit_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Username = "wh05", Email = "wh05@email.vn", FullName = "Fake Sign", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, _, _, _) = CreateServices(db, userId, "User", new Dictionary<string, string?>
        {
            ["VietQr:BankCode"] = "TPB",
            ["VietQr:WebhookSecret"] = "ecolink-super-secret"
        });

        var order = await paymentSvc.CreatePayInOrderAsync(userId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });

        var whRes = await paymentSvc.ProcessWebhookAsync(new VietQrWebhookPayload
        {
            OrderCode = order.OrderCode,
            Amount = 59_000m,
            Signature = "BAD_SIGNATURE"
        }, headerSecret: "wrong-header");

        whRes.Success.Should().BeFalse();
        whRes.Status.Should().Be("INVALID_SIGNATURE");

        var reloaded = await paymentSvc.GetOrderForUserAsync(userId, false, order.OrderCode);
        reloaded.Status.Should().Be("PENDING");
        (await db.PaymentWebhookLogs.AnyAsync(w => w.ProcessingResult == "InvalidSignature" && !w.SignatureValid)).Should().BeTrue();
    }

    // UTC017 [Abnormal] [A-10]: Chặn Admin tắt gói mặc định BASIC & chặn hủy đơn đã Paid (409 Conflict)
    [Fact]
    public async Task UTC017_DisableBasicPackageAndCancelPaidOrder_ThrowExpectedRules_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = adminId, Username = "admin_lock", Email = "admin_lock@ecolink.vn", FullName = "Admin Lock", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = citizenId, Username = "cit_paid", Email = "cit_paid@email.vn", FullName = "Cit Paid", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, adminId, "Admin");

        var disableBasic = () => packageSvc.TogglePackageStatusAsync(adminId, CanonicalServicePackages.BasicId, new ToggleServicePackageStatusRequest
        {
            IsActive = false,
            Reason = "Thử tắt gói nền BASIC"
        });
        await disableBasic.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.RuleCode == "BR-PAY-05");

        // Mua & thanh toán xong -> không thể tự ý hủy đơn nữa
        var paidOrder = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await paymentSvc.SimulateTransferAsync(paidOrder.OrderCode);

        var cancelPaid = () => paymentSvc.CancelPendingOrderAsync(citizenId, paidOrder.OrderCode);
        await cancelPaid.Should().ThrowAsync<ConflictException>()
            .Where(ex => ex.RuleCode == "BR-PAY-01");
    }

    // UTC018 [Abnormal] [A-11]: Chặn đối soát lặp giao dịch đã Resolved & thiếu tiền khi ManualMatchOrder (409 / 400)
    [Fact]
    public async Task UTC018_AdminDoubleResolveOrUnderfundedManualMatch_Rejected_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var recId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = adminId, Username = "admin_dbl", Email = "admin_dbl@ecolink.vn", FullName = "Admin Double", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = recId, Username = "rec_dbl", Email = "rec_dbl@thugom.vn", FullName = "Vựa Double", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active });
        db.RecyclerProfiles.Add(new RecyclerProfile
        {
            UserId = recId,
            FacilityName = "Vựa Double",
            RepresentativeName = "F",
            BusinessAddress = "HN",
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        });
        await db.SaveChangesAsync();

        var (paymentSvc, _, reconSvc, _) = CreateServices(db, adminId, "Admin");

        // Tạo đơn PRO 1,000,000đ nhưng có webhook mồ côi chỉ nhận 500,000đ
        var proOrder = await paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        var orphanWh = await paymentSvc.ProcessWebhookAsync(new VietQrWebhookPayload
        {
            Amount = 500_000m,
            Description = "KHONG KHOP",
            Reference = "FT_SHORT_ORPHAN"
        });

        var underfundedMatch = () => reconSvc.ResolvePaymentAnomalyAsync(adminId, orphanWh.WebhookCode!, new ResolvePaymentAnomalyRequest
        {
            Action = "ManualMatchOrder",
            TargetOrderCode = proOrder.OrderCode,
            ResolutionNote = "Thử khớp số tiền thiếu vào đơn PRO"
        });
        await underfundedMatch.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.RuleCode == "BR-PAY-06");

        // Đóng bất thường 1 lần -> lần 2 bị chặn ConflictException
        await reconSvc.ResolvePaymentAnomalyAsync(adminId, orphanWh.WebhookCode!, new ResolvePaymentAnomalyRequest
        {
            Action = "CloseAnomaly",
            ResolutionNote = "Đã hoàn trả trực tiếp qua ngân hàng."
        });

        var doubleResolve = () => reconSvc.ResolvePaymentAnomalyAsync(adminId, orphanWh.WebhookCode!, new ResolvePaymentAnomalyRequest
        {
            Action = "MarkRefunded",
            ResolutionNote = "Thử đối soát lần 2."
        });
        await doubleResolve.Should().ThrowAsync<ConflictException>()
            .Where(ex => ex.RuleCode == "BR-PAY-06");
    }

    // =========================================================================
    // BOUNDARY TEST CASES (UTC019 -> UTC022) — 4 TCs
    // =========================================================================

    // UTC019 [Boundary] [U-16]: Chuyển dư tiền -> Kích hoạt ngay & gắn cờ Overpaid chờ hoàn dư
    [Fact]
    public async Task UTC019_OverpaidWebhook_ActivatesPackageImmediatelyAndFlagsOverpaid_Boundary()
    {
        using var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Username = "wh03", Email = "wh03@email.vn", FullName = "Vũ Dư", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, userId, "User");
        var order = await paymentSvc.CreatePayInOrderAsync(userId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });

        var updated = await paymentSvc.SimulateTransferAsync(order.OrderCode, new SimulateTransferRequest
        {
            Scenario = "Overpaid",
            SimulatedAmount = 65_000m
        });

        updated.Status.Should().Be("PAID");
        updated.ReconciliationStatus.Should().Be("Overpaid");
        updated.PaidAmount.Should().Be(65_000m);

        var mySubs = await packageSvc.GetMySubscriptionsAsync(userId);
        mySubs.CurrentTier.Should().Be("Premium");
    }

    // UTC020 [Boundary] [R-07]: Chuyển khoản muộn sau hạn QR 15 phút -> Gắn cờ LateTransfer & tự quét quá hạn
    [Fact]
    public async Task UTC020_LateTransferOnExpiredQr_MarksExpiredAndFlagsLateTransfer_Boundary()
    {
        using var db = TestDbContextFactory.Create();
        var recId = Guid.NewGuid();
        db.Users.Add(new User { Id = recId, Username = "wh04", Email = "wh04@rec.vn", FullName = "Vựa Trễ", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active });
        db.RecyclerProfiles.Add(new RecyclerProfile { UserId = recId, FacilityName = "Vựa Trễ", RepresentativeName = "D", BusinessAddress = "HN", VerificationStatus = VerificationStatus.Verified, ActiveStatus = RecyclerActiveStatus.Online });
        await db.SaveChangesAsync();

        var (paymentSvc, _, _, _) = CreateServices(db, recId, "Recycler");
        var order = await paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });

        var updated = await paymentSvc.SimulateTransferAsync(order.OrderCode, new SimulateTransferRequest
        {
            Scenario = "LateTransfer"
        });

        updated.Status.Should().Be("EXPIRED");
        updated.ReconciliationStatus.Should().Be("LateTransfer");
        updated.PaidAmount.Should().Be(1_000_000m);
        (await db.PaymentWebhookLogs.AnyAsync(w => w.MatchedOrderId == order.Id && w.ProcessingResult == "LateTransfer")).Should().BeTrue();
    }

    // UTC021 [Boundary] [U-16]: Vòng đời IMemoryCache quyền lợi & Vô hiệu hóa tức thì ngay sau khi kích hoạt gói
    [Fact]
    public async Task UTC021_EntitlementCacheHitAndInvalidation_UpdatesImmediatelyAfterActivation_Boundary()
    {
        using var db = TestDbContextFactory.Create();
        var citizenId = Guid.NewGuid();
        db.Users.Add(new User { Id = citizenId, Username = "cache_u", Email = "cache@email.vn", FullName = "Cache User", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, _, _, entitlementSvc) = CreateServices(db, citizenId, "User");

        // 1. Đọc lần đầu -> lưu Basic vào cache
        var initialEnt = await entitlementSvc.GetUserEntitlementsAsync(citizenId);
        initialEnt.CurrentTier.Should().Be("Basic");
        initialEnt.IsAdFree.Should().BeFalse();

        // 2. Sau khi thanh toán PREMIUM, PaymentService gọi InvalidateUserEntitlements nên đọc lại ra Premium ngay
        var order = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await paymentSvc.SimulateTransferAsync(order.OrderCode);

        var updatedEnt = await entitlementSvc.GetUserEntitlementsAsync(citizenId);
        updatedEnt.CurrentTier.Should().Be("Premium");
        updatedEnt.IsAdFree.Should().BeTrue();

        // 3. Batch lookup tập rỗng trả về từ điển rỗng không ném ngoại lệ
        var emptyBatch = await entitlementSvc.GetBatchEntitlementsAsync(Array.Empty<Guid>());
        emptyBatch.Should().BeEmpty();
    }

    // UTC022 [Boundary] [A-10 & A-11]: Ranh giới biên UpsertServicePackageRequestValidator & ResolvePaymentAnomalyRequestValidator
    [Fact]
    public void UTC022_ServicePackageValidators_EnforceBoundaryPricesDurationsAndResolutionNote_Boundary()
    {
        var pkgValidator = new UpsertServicePackageRequestValidator();
        var resolveValidator = new ResolvePaymentAnomalyRequestValidator();

        // Biên dưới hợp lệ: giá 2,000đ, chu kỳ 1 ngày
        var minValidPkg = pkgValidator.Validate(new UpsertServicePackageRequest
        {
            PackageCode = "STARTER_2K",
            PackageName = "Gói 2K",
            TargetRole = "User",
            PriceVnd = 2_000m,
            DurationDays = 1,
            Features = new ServicePackageFeaturesDto { AdFree = true, DailyPointMultiplier = 1.0m, MapPriorityBoost = 0 }
        });
        minValidPkg.IsValid.Should().BeTrue();

        // Dưới ngưỡng PayOS 2,000đ hoặc chu kỳ 0 ngày -> lỗi
        pkgValidator.Validate(new UpsertServicePackageRequest
        {
            PackageCode = "CHEAP_1K",
            PackageName = "Gói dưới chuẩn",
            TargetRole = "All",
            PriceVnd = 1_500m,
            DurationDays = 30
        }).IsValid.Should().BeFalse();

        pkgValidator.Validate(new UpsertServicePackageRequest
        {
            PackageCode = "ZERO_DAY",
            PackageName = "Chu kỳ 0 ngày",
            TargetRole = "All",
            PriceVnd = 5_000m,
            DurationDays = 0
        }).IsValid.Should().BeFalse();

        // Biên trên chu kỳ hợp lệ (730 ngày) vs vượt quá (731 ngày)
        pkgValidator.Validate(new UpsertServicePackageRequest
        {
            PackageCode = "TWO_YEAR_PRO",
            PackageName = "Gói 2 năm",
            TargetRole = "Recycler",
            PriceVnd = 1_800_000m,
            DurationDays = 730
        }).IsValid.Should().BeTrue();

        pkgValidator.Validate(new UpsertServicePackageRequest
        {
            PackageCode = "OVER_LIMIT",
            PackageName = "Quá hạn 730 ngày",
            TargetRole = "Recycler",
            PriceVnd = 2_000_000m,
            DurationDays = 731
        }).IsValid.Should().BeFalse();

        // ResolvePaymentAnomalyRequestValidator: Note >= 10 ký tự, ManualMatchOrder cần TargetOrderCode, MarkRefunded cần BankRef
        resolveValidator.Validate(new ResolvePaymentAnomalyRequest
        {
            Action = "CloseAnomaly",
            ResolutionNote = "0123456789"
        }).IsValid.Should().BeTrue();

        resolveValidator.Validate(new ResolvePaymentAnomalyRequest
        {
            Action = "CloseAnomaly",
            ResolutionNote = "123456789"
        }).IsValid.Should().BeFalse();

        resolveValidator.Validate(new ResolvePaymentAnomalyRequest
        {
            Action = "ManualMatchOrder",
            ResolutionNote = "Ghi chú đủ dài trên 10 ký tự nhưng thiếu mã đơn.",
            TargetOrderCode = null
        }).IsValid.Should().BeFalse();

        resolveValidator.Validate(new ResolvePaymentAnomalyRequest
        {
            Action = "MarkRefunded",
            ResolutionNote = "Đã hoàn tiền nhưng chưa điền mã tham chiếu ngân hàng.",
            ResolutionBankRef = null
        }).IsValid.Should().BeFalse();
    }

    // UTC023 [Abnormal/Business] [R-07]: Đang dùng PRO -> Ẩn/Chặn mua gói thấp hơn (PREMIUM) & Chặn gia hạn cộng dồn PRO
    [Fact]
    public async Task UTC023_ActivePro_MarksLowerPackagesIncluded_DisablesRenewal_AndBlocksPremiumOrProRepurchase()
    {
        using var db = TestDbContextFactory.Create();
        var recId = Guid.NewGuid();
        db.Users.Add(new User { Id = recId, Username = "rec_pro_active", Email = "recpro@thugom.vn", FullName = "Vựa Pro Active", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active });
        db.RecyclerProfiles.Add(new RecyclerProfile
        {
            UserId = recId,
            FacilityName = "Vựa Pro Active",
            RepresentativeName = "K",
            BusinessAddress = "HCM",
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, recId, "Recycler");
        var proOrder = await paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        await paymentSvc.SimulateTransferAsync(proOrder.OrderCode);

        var catalog = await packageSvc.GetAvailablePackagesAsync(recId, UserRole.Recycler);
        var basicPkg = catalog.First(p => p.PackageCode == "BASIC");
        var premPkg = catalog.First(p => p.PackageCode == "PREMIUM");
        var proPkg = catalog.First(p => p.PackageCode == "PRO");

        basicPkg.IsIncludedInHigherPlan.Should().BeTrue();
        basicPkg.CanRenew.Should().BeFalse();
        premPkg.IsIncludedInHigherPlan.Should().BeTrue();
        premPkg.CanRenew.Should().BeFalse();
        proPkg.IsCurrentPlan.Should().BeTrue();
        proPkg.CanRenew.Should().BeFalse();
        proPkg.IsEligible.Should().BeFalse();
        proPkg.ProEligibility.Should().NotBeNull();
        proPkg.ProEligibility!.IsNotCurrentlyActivePro.Should().BeFalse();

        // Chặn mua gói thấp hơn (PREMIUM)
        var buyPremAct = () => paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await buyPremAct.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.RuleCode == "BR-PAY-04");

        // Chặn gia hạn cộng dồn gói PRO khi đang hiệu lực
        var buyProAgainAct = () => paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        await buyProAgainAct.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.MessageCode == "MSG-PAY-06");
    }

    // UTC024 [Boundary] [R-07]: Cho phép đăng ký PRO khi WarningCount <= 5 (bỏ qua khiếu nại) & Chặn khi WarningCount > 5
    [Fact]
    public async Task UTC024_ProEligibility_AllowsUpTo5WarningsAndIgnoresComplaints_BlocksAt6Warnings()
    {
        using var db = TestDbContextFactory.Create();
        var recOkId = Guid.NewGuid();
        var recTooManyId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = recOkId, Username = "rec_w5", Email = "w5@rec.vn", FullName = "Vựa 5 Cảnh Cáo", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active, WarningCount = 5 },
            new User { Id = recTooManyId, Username = "rec_w6", Email = "w6@rec.vn", FullName = "Vựa 6 Cảnh Cáo", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active, WarningCount = 6 });
        db.RecyclerProfiles.AddRange(
            new RecyclerProfile { UserId = recOkId, FacilityName = "Vựa 5 CC", BusinessAddress = "HN", VerificationStatus = VerificationStatus.Verified, ActiveStatus = RecyclerActiveStatus.Online },
            new RecyclerProfile { UserId = recTooManyId, FacilityName = "Vựa 6 CC", BusinessAddress = "HN", VerificationStatus = VerificationStatus.Verified, ActiveStatus = RecyclerActiveStatus.Online });
        await db.SaveChangesAsync();

        var (payOk, _, _, _) = CreateServices(db, recOkId, "Recycler");
        var okOrder = await payOk.CreatePayInOrderAsync(recOkId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        okOrder.PackageCode.Should().Be("PRO");

        var (payBlocked, pkgBlocked, _, _) = CreateServices(db, recTooManyId, "Recycler");
        var blockedCatalog = await pkgBlocked.GetAvailablePackagesAsync(recTooManyId, UserRole.Recycler);
        var proDto = blockedCatalog.First(c => c.PackageCode == "PRO");
        proDto.IsEligible.Should().BeFalse();
        proDto.ProEligibility!.IsWarningLimitOk.Should().BeFalse();
        proDto.ProEligibility.CurrentWarningCount.Should().Be(6);

        var blockedAct = () => payBlocked.CreatePayInOrderAsync(recTooManyId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        await blockedAct.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.MessageCode == "MSG-PAY-06");
    }

    // UTC025 [Abnormal] [R-07]: Yêu cầu phản hồi >= 4.0 sao & Bắt buộc có ít nhất 1 đánh giá khi đăng ký lại PRO
    [Fact]
    public async Task UTC025_ProEligibility_BlocksLowRating_AndRequiresPositiveReviewOnReRegistration()
    {
        using var db = TestDbContextFactory.Create();
        var recId = Guid.NewGuid();
        db.Users.Add(new User { Id = recId, Username = "rec_rate", Email = "rate@rec.vn", FullName = "Vựa Rating", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active });
        var profile = new RecyclerProfile
        {
            UserId = recId,
            FacilityName = "Vựa Rating",
            BusinessAddress = "Đà Nẵng",
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online,
            AverageRating = 3.5m,
            TotalReviews = 3
        };
        db.RecyclerProfiles.Add(profile);
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, recId, "Recycler");

        // 1. Chặn nếu có đánh giá nhưng < 4.0 sao
        var lowRatingAct = () => paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        await lowRatingAct.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.MessageCode == "MSG-PAY-06");

        // 2. Trường hợp tái đăng ký PRO sau khi kỳ cũ hết hạn nhưng chưa có review (TotalReviews = 0) -> chặn
        profile.AverageRating = 0m;
        profile.TotalReviews = 0;
        var pastOrder = new ServiceOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ECOOLDPRO01",
            UserId = recId,
            PackageId = CanonicalServicePackages.ProId,
            PackageCodeSnapshot = CanonicalServicePackages.ProCode,
            PackageNameSnapshot = "Pro",
            DurationDaysSnapshot = 365,
            FeaturesSnapshot = CanonicalServicePackages.ProFeaturesJson,
            ExpectedAmountVnd = 1_000_000m,
            PaidAmountVnd = 1_000_000m,
            QrCodeUrl = "https://qr.test/oldpro",
            TransferContent = "ECOOLDPRO01",
            BankCode = "TPB",
            BankName = "TPBank",
            AccountNo = "05773216801",
            AccountName = "ECOLINK",
            OrderStatus = ServiceOrderStatus.Paid,
            PaymentStatus = ServicePaymentStatus.Success,
            ReconciliationStatus = ReconciliationStatus.Matched,
            QrExpiresAt = DateTime.UtcNow.AddDays(-400),
            PaidAt = DateTime.UtcNow.AddDays(-400)
        };
        db.ServiceOrders.Add(pastOrder);
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = recId,
            PackageId = CanonicalServicePackages.ProId,
            SourceOrderId = pastOrder.Id,
            PackageCode = CanonicalServicePackages.ProCode,
            FeaturesSnapshot = CanonicalServicePackages.ProFeaturesJson,
            StartsAt = DateTime.UtcNow.AddDays(-400),
            ExpiresAt = DateTime.UtcNow.AddDays(-35),
            Status = SubscriptionStatus.Expired
        });
        await db.SaveChangesAsync();

        var renewNoReviewCatalog = await packageSvc.GetAvailablePackagesAsync(recId, UserRole.Recycler);
        renewNoReviewCatalog.First(c => c.PackageCode == "PRO").ProEligibility!.IsReRegistration.Should().BeTrue();
        renewNoReviewCatalog.First(c => c.PackageCode == "PRO").ProEligibility!.IsPositiveFeedbackMet.Should().BeFalse();

        // 3. Đạt >= 1 đánh giá >= 4.0 sao -> đủ điều kiện tái đăng ký PRO
        profile.TotalReviews = 2;
        profile.AverageRating = 4.6m;
        await db.SaveChangesAsync();

        var eligibleOrder = await paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        eligibleOrder.PackageCode.Should().Be("PRO");
    }

    // UTC026 [Normal] [U-16]: Tài khoản Citizen (User) chỉ nhìn thấy BASIC & PREMIUM, ẩn hoàn toàn gói PRO của Recycler
    [Fact]
    public async Task UTC026_GetAvailablePackages_WhenCitizenUser_FiltersOutRecyclerProPackage()
    {
        using var db = TestDbContextFactory.Create();
        var citizenId = Guid.NewGuid();
        db.Users.Add(new User { Id = citizenId, Username = "cit_view", Email = "citv@email.vn", FullName = "Citizen View", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (_, packageSvc, _, _) = CreateServices(db, citizenId, "User");
        var catalog = await packageSvc.GetAvailablePackagesAsync(citizenId, UserRole.User);

        catalog.Select(p => p.PackageCode).Should().BeEquivalentTo(new[] { "BASIC", "PREMIUM" });
        catalog.Should().NotContain(p => p.PackageCode == "PRO");
    }
}
