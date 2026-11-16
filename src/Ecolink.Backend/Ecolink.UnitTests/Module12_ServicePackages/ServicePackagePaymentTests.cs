using System.IO.Compression;
using System.Text;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Application.Validators;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
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
/// Bộ kiểm thử đơn vị toàn diện cho Module M-12: In-System Service Packages &amp; VietQR Integration (FE-14).
/// Bao phủ trọn vẹn 16 kịch bản chuẩn IEEE 829 RTM (TC-PAY-*) + kiểm tra Validator &amp; XLSX OpenXML.
/// </summary>
public class ServicePackagePaymentTests
{
    private static ICurrentUserService CreateCurrentUserMock(Guid userId, string role = "Admin")
    {
        var mock = Substitute.For<ICurrentUserService>();
        mock.UserId.Returns((Guid?)userId);
        mock.Role.Returns(role);
        mock.Email.Returns("admin@ecolink.vn");
        mock.IsAuthenticated.Returns(true);
        mock.IpAddress.Returns("127.0.0.1");
        mock.UserAgent.Returns("EcoLink-UnitTest/1.0");
        return mock;
    }

    private static (
        PaymentService PaymentService,
        ServicePackageService PackageService,
        AdminReconciliationService ReconciliationService,
        SubscriptionEntitlementService EntitlementService) CreateServices(
        Ecolink.Infrastructure.Data.ApplicationDbContext db,
        Guid actorId,
        string actorRole = "Admin",
        Dictionary<string, string?>? configOverrides = null)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var entitlementService = new SubscriptionEntitlementService(db, cache);
        var currentUser = CreateCurrentUserMock(actorId, actorRole);
        var auditService = new AuditLogService(db, currentUser);
        var config = TestDbContextFactory.CreateConfiguration(configOverrides ?? new Dictionary<string, string?>
        {
            ["VietQr:BankCode"] = "TPB",
            ["VietQr:BankName"] = "TPBank",
            ["VietQr:AccountNo"] = "05773216801",
            ["VietQr:AccountName"] = "ECOLINK PLATFORM SERVICE",
            ["VietQr:EnableSimulator"] = "true"
        });

        var paymentService = new PaymentService(db, entitlementService, config, NullLogger<PaymentService>.Instance);
        var packageService = new ServicePackageService(db, entitlementService, auditService);
        var reconciliationService = new AdminReconciliationService(db, entitlementService, auditService);
        return (paymentService, packageService, reconciliationService, entitlementService);
    }

    // =========================================================================
    // 1. TC-PAY-16-01_P: Citizen tạo đơn PREMIUM (59k) & webhook khớp kích hoạt 30 ngày
    // =========================================================================
    [Fact]
    public async Task TC_PAY_16_01_P_CitizenCreatesPremiumOrder_AndExactWebhookActivates30DayAdFree_BR_PAY_01()
    {
        using var db = TestDbContextFactory.Create();
        var citizenId = Guid.NewGuid();
        db.Users.Add(new User { Id = citizenId, Username = "cit01", Email = "cit01@email.vn", FullName = "Nguyễn Dân", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, citizenId, "User");

        var order = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest
        {
            PackageCode = "PREMIUM"
        });

        order.Amount.Should().Be(59_000m);
        order.Status.Should().Be("PENDING");
        order.QrCodeUrl.Should().Contain("img.vietqr.io");
        order.QrCodeUrl.Should().Contain("59000");

        var whRes = await paymentSvc.ProcessWebhookAsync(new VietQrWebhookPayload
        {
            OrderCode = order.OrderCode,
            Amount = 59_000m,
            Reference = "FT_EXACT_001"
        });

        whRes.Success.Should().BeTrue();
        whRes.Status.Should().Be("PAID");

        var mySubs = await packageSvc.GetMySubscriptionsAsync(citizenId);
        mySubs.CurrentTier.Should().Be("Premium");
        mySubs.Entitlements.IsAdFree.Should().BeTrue();
        mySubs.Entitlements.HasEcoPlusBadge.Should().BeTrue();
        mySubs.Entitlements.DailyPointMultiplier.Should().Be(1.2m);
        mySubs.ActiveSubscriptions.Should().HaveCount(1);
        mySubs.ActiveSubscriptions[0].RemainingDays.Should().BeInRange(29, 30);
    }

    // =========================================================================
    // 2. TC-PAY-16-02_P: Gia hạn sớm cùng gói PREMIUM cộng dồn thời hạn (BR-PAY-04)
    // =========================================================================
    [Fact]
    public async Task TC_PAY_16_02_P_EarlyRenewalSamePackage_StacksRemainingDays_BR_PAY_04()
    {
        using var db = TestDbContextFactory.Create();
        var citizenId = Guid.NewGuid();
        db.Users.Add(new User { Id = citizenId, Username = "cit02", Email = "cit02@email.vn", FullName = "Trần An", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, citizenId, "User");

        // Tạo đơn 1 đã kích hoạt còn ~10 ngày hiệu lực
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

        // Gia hạn thêm 1 lần PREMIUM 30 ngày
        var renewOrder = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await paymentSvc.SimulateTransferAsync(renewOrder.OrderCode);

        var mySubs = await packageSvc.GetMySubscriptionsAsync(citizenId);
        var maxExpiry = mySubs.ActiveSubscriptions.Max(s => s.ExpiresAt);
        maxExpiry.Should().BeCloseTo(initialExpiry.AddDays(30), TimeSpan.FromMinutes(1));
    }

    // =========================================================================
    // 3. TC-PAY-16-03_S: Chặn giả mạo số tiền từ Client (BR-PAY-01)
    // =========================================================================
    [Fact]
    public async Task TC_PAY_16_03_S_TamperedClientAmount_ThrowsBusinessRuleException_BR_PAY_01()
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

    // =========================================================================
    // 4. TC-PAY-16-04_S: Bảo mật sở hữu đơn thanh toán theo UserId (BR-AUTH-08)
    // =========================================================================
    [Fact]
    public async Task TC_PAY_16_04_S_OtherCitizenCannotViewOrCancelOrder_BR_AUTH_08()
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

    // =========================================================================
    // 5. TC-PAY-16-05_N: Chặn tạo đơn QR cho gói BASIC (0đ) & Citizen mua PRO
    // =========================================================================
    [Fact]
    public async Task TC_PAY_16_05_N_RejectsBasicFreeOrder_AndCitizenBuyingProPackage()
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

    // =========================================================================
    // 6. TC-PAY-07-01_N: Chặn Recycler chưa duyệt KYB mua gói PRO (BR-PAY-03)
    // =========================================================================
    [Fact]
    public async Task TC_PAY_07_01_N_UnverifiedRecyclerCannotPurchaseProPackage_MSG_PAY_06()
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
        var proItem = catalog.First(c => c.PackageCode == "PRO");
        proItem.IsEligible.Should().BeFalse();

        var act = () => paymentSvc.CreatePayInOrderAsync(recyclerId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.MessageCode == "MSG-PAY-06");
    }

    // =========================================================================
    // 7. TC-PAY-07-02_P: Gói PRO cấp Tích xanh & đẩy ưu tiên bản đồ PostGIS
    // =========================================================================
    [Fact]
    public async Task TC_PAY_07_02_P_ProSubscriptionGrantsBlueBadgeAndFeaturedGeoSort_BR_MTH_04()
    {
        using var db = TestDbContextFactory.Create();
        var catId = Guid.NewGuid();
        db.WasteCategories.Add(new WasteCategory { Id = catId, Code = "plastic", Name = "Nhựa", StandardBinColor = "Cam", IsRecyclable = true, Status = "Active" });

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
        partners[1].HasVerifiedBadge.Should().BeFalse();
        partners[0].DistanceKm.Should().BeGreaterThan(partners[1].DistanceKm);
    }

    // =========================================================================
    // 8. TC-PAY-07-03_P: Nâng bậc tức thì từ PREMIUM đang chạy lên PRO (BR-PAY-04)
    // =========================================================================
    [Fact]
    public async Task TC_PAY_07_03_P_UpgradeFromActivePremiumToPro_ActivatesProImmediately_BR_PAY_04()
    {
        using var db = TestDbContextFactory.Create();
        var recId = Guid.NewGuid();
        db.Users.Add(new User { Id = recId, Username = "rec_up", Email = "recup@rec.vn", FullName = "Vựa Upgrade", PasswordHash = "h", Role = UserRole.Recycler, Status = UserStatus.Active });
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

        // Mua trước PREMIUM
        var premOrder = await paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await paymentSvc.SimulateTransferAsync(premOrder.OrderCode);

        // Nâng cấp ngay lên PRO (1tr / 365 ngày)
        var proOrder = await paymentSvc.CreatePayInOrderAsync(recId, UserRole.Recycler, new CreatePayInRequest { PackageCode = "PRO" });
        proOrder.Amount.Should().Be(1_000_000m);
        await paymentSvc.SimulateTransferAsync(proOrder.OrderCode);

        var subs = await packageSvc.GetMySubscriptionsAsync(recId);
        subs.CurrentTier.Should().Be("Pro");
        subs.Entitlements.HasVerifiedBlueBadge.Should().BeTrue();
        subs.Entitlements.IsFeaturedMapMarker.Should().BeTrue();
        var proSub = subs.ActiveSubscriptions.First(s => s.PackageCode == "PRO");
        proSub.StartsAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        proSub.RemainingDays.Should().BeInRange(364, 365);
    }

    // =========================================================================
    // 9. TC-PAY-WH-01_P: Xử lý Webhook Idempotent khi gửi trùng 3 lần (BR-PAY-02)
    // =========================================================================
    [Fact]
    public async Task TC_PAY_WH_01_P_DuplicateWebhookDelivery_ActivatesSubscriptionExactlyOnce_BR_PAY_02()
    {
        using var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Username = "wh01", Email = "wh01@email.vn", FullName = "Lê Trùng", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
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

    // =========================================================================
    // 10. TC-PAY-WH-02_N: Chuyển thiếu tiền -> Gắn Underpaid, KHÔNG kích hoạt gói
    // =========================================================================
    [Fact]
    public async Task TC_PAY_WH_02_N_UnderpaidWebhook_FlagsAnomalyAndDoesNotActivatePackage()
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

    // =========================================================================
    // 11. TC-PAY-WH-03_B: Chuyển dư tiền -> Kích hoạt ngay + gắn cờ Overpaid
    // =========================================================================
    [Fact]
    public async Task TC_PAY_WH_03_B_OverpaidWebhook_ActivatesPackageImmediatelyAndFlagsOverpaid()
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

    // =========================================================================
    // 12. TC-PAY-WH-04_B: Chuyển khoản muộn sau hạn QR -> Gắn cờ LateTransfer
    // =========================================================================
    [Fact]
    public async Task TC_PAY_WH_04_B_LateTransferOnExpiredQr_FlagsLateTransferWithoutDroppingFunds()
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

    // =========================================================================
    // 13. TC-PAY-WH-05_N: Chặn Webhook sai chữ ký / Webhook Secret (MSG-PAY-05)
    // =========================================================================
    [Fact]
    public async Task TC_PAY_WH_05_N_InvalidWebhookSecretOrSignature_RejectedWithMsgPay05()
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

        (await paymentSvc.GetOrderAsync(order.OrderCode))!.Status.Should().Be("PENDING");
        (await db.PaymentWebhookLogs.AnyAsync(w => !w.SignatureValid && w.ProcessingResult == "InvalidSignature")).Should().BeTrue();
    }

    // =========================================================================
    // 14. TC-PAY-10-01_P: Cô lập giá cũ (Grandfathering), chặn tắt BASIC & ghi AuditLog
    // =========================================================================
    [Fact]
    public async Task TC_PAY_10_01_P_AdminPriceUpdateGrandfathersOldSubscribers_AndBlocksDisablingBasic()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = adminId, Username = "adm10", Email = "adm10@ecolink.vn", FullName = "Admin 10", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = citizenId, Username = "cit10", Email = "cit10@email.vn", FullName = "Cit 10", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, _, _) = CreateServices(db, adminId, "Admin");

        // Citizen mua PREMIUM lúc giá đang là 59,000đ
        var oldOrder = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await paymentSvc.SimulateTransferAsync(oldOrder.OrderCode);

        // Admin tăng giá PREMIUM lên 69,000đ & 45 ngày
        var updatedPkg = await packageSvc.UpdatePackageAsync(adminId, CanonicalServicePackages.PremiumId, new UpsertServicePackageRequest
        {
            PackageCode = "PREMIUM",
            PackageName = "Premium Plus",
            TargetRole = "All",
            PriceVnd = 69_000m,
            DurationDays = 45,
            IsActive = true,
            DisplayOrder = 20,
            Features = new ServicePackageFeaturesDto { TierName = "Premium", AdFree = true, EcoPlusBadge = true, DailyPointMultiplier = 1.2m }
        });

        updatedPkg.PriceVnd.Should().Be(69_000m);
        (await db.AuditLogs.AnyAsync(a => a.Action == "UPDATE_SERVICE_PACKAGE")).Should().BeTrue();

        // Đơn cũ giữ nguyên 59,000đ & 30 ngày (Grandfathering)
        var oldOrderAfter = await paymentSvc.GetOrderAsync(oldOrder.OrderCode);
        oldOrderAfter!.Amount.Should().Be(59_000m);
        oldOrderAfter.DurationDays.Should().Be(30);

        // Đơn mới áp dụng giá 69,000đ
        var newOrder = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });
        newOrder.Amount.Should().Be(69_000m);

        // Chặn tắt BASIC miễn phí
        var toggleBasic = () => packageSvc.TogglePackageStatusAsync(adminId, CanonicalServicePackages.BasicId, new ToggleServicePackageStatusRequest
        {
            IsActive = false,
            Reason = "Thử tắt gói nền"
        });
        await toggleBasic.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.RuleCode == "BR-PAY-05");
    }

    // =========================================================================
    // 15. TC-PAY-11-01_P: Admin khớp thủ công Webhook mồ côi WH-... -> kích hoạt gói + AuditLog
    // =========================================================================
    [Fact]
    public async Task TC_PAY_11_01_P_AdminManualMatchOrphanWebhook_ActivatesOrderAndLogsAudit()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = adminId, Username = "adm11", Email = "adm11@ecolink.vn", FullName = "Admin 11", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = citizenId, Username = "cit11", Email = "cit11@email.vn", FullName = "Cit 11", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, packageSvc, recSvc, _) = CreateServices(db, adminId, "Admin");

        // Tạo đơn PENDING 59,000đ
        var pendingOrder = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });

        // Webhook mồ côi đến thiếu mã đơn trong nội dung
        var whRes = await paymentSvc.ProcessWebhookAsync(new VietQrWebhookPayload
        {
            OrderCode = null,
            Amount = 59_000m,
            Description = "NGUYEN VAN A CHUYEN TIEN GOI PREMIUM",
            Reference = "FT_ORPHAN_888"
        });
        whRes.Status.Should().Be("ANOMALY_FLAGGED");
        whRes.WebhookCode.Should().StartWith("WH-");

        // Admin khớp lệnh thủ công WH-... vào pendingOrder.OrderCode
        var resolved = await recSvc.ResolvePaymentAnomalyAsync(adminId, whRes.WebhookCode!, new ResolvePaymentAnomalyRequest
        {
            Action = nameof(ReconciliationAction.ManualMatchOrder),
            TargetOrderCode = pendingOrder.OrderCode,
            ResolutionNote = "Đã kiểm tra sao kê khớp chính xác giao dịch của khách hàng."
        });

        resolved.ReconciliationStatus.Should().Be("Resolved");
        resolved.OrderStatus.Should().Be("Paid");

        var subs = await packageSvc.GetMySubscriptionsAsync(citizenId);
        subs.CurrentTier.Should().Be("Premium");
        (await db.AuditLogs.AnyAsync(a => a.Action == "RESOLVE_PAYMENT_ANOMALY")).Should().BeTrue();
    }

    // =========================================================================
    // 16. TC-PAY-11-02_S: Xuất CSV/XLSX che PII & chặn xóa/sửa thô PaymentWebhookLog
    // =========================================================================
    [Fact]
    public async Task TC_PAY_11_02_S_ExportMasksPiiAndProtectsWebhookLogImmutability_BR_ADM_05_06()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = adminId, Username = "adm12", Email = "adm12@ecolink.vn", FullName = "Admin 12", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active },
            new User { Id = citizenId, Username = "cit12", Email = "duc.nguyen@email.vn", PhoneNumber = "0901234567", FullName = "Nguyễn Đức", PasswordHash = "h", Role = UserRole.User, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var (paymentSvc, _, recSvc, _) = CreateServices(db, adminId, "Admin");
        var order = await paymentSvc.CreatePayInOrderAsync(citizenId, UserRole.User, new CreatePayInRequest { PackageCode = "PREMIUM" });
        await paymentSvc.SimulateTransferAsync(order.OrderCode);

        // 1. Xuất CSV kiểm tra BOM + Che PII Email & SĐT
        var csvFile = await recSvc.ExportReconciliationReportAsync(adminId, new ReconciliationExportQuery { Format = "csv" });
        var csvText = Encoding.UTF8.GetString(csvFile.Content);
        csvText.Should().NotContain("duc.nguyen@email.vn");
        csvText.Should().NotContain("0901234567");
        csvText.Should().Contain("***@email.vn");
        csvText.Should().Contain("090****567");
        (await db.AuditLogs.AnyAsync(a => a.Action == "EXPORT_RECONCILIATION")).Should().BeTrue();

        // 2. Xuất XLSX OpenXML hợp lệ
        var xlsxFile = await recSvc.ExportReconciliationReportAsync(adminId, new ReconciliationExportQuery { Format = "xlsx" });
        using var archive = new ZipArchive(new MemoryStream(xlsxFile.Content), ZipArchiveMode.Read);
        archive.GetEntry("xl/worksheets/sheet1.xml").Should().NotBeNull();

        // 3. Thử xóa PaymentWebhookLog -> Interceptor phải ném InvalidOperationException (BR-ADM-05)
        var whLog = await db.PaymentWebhookLogs.FirstAsync();
        db.PaymentWebhookLogs.Remove(whLog);
        var deleteAct = () => db.SaveChangesAsync();
        await deleteAct.Should().ThrowAsync<InvalidOperationException>();
    }

    // =========================================================================
    // 17. Kiểm tra Validators chuẩn A-10 & A-11
    // =========================================================================
    [Fact]
    public void ServicePackageValidators_EnforcePayOsMinAmount_AndMandatoryResolutionNote()
    {
        var pkgValidator = new UpsertServicePackageRequestValidator();
        pkgValidator.Validate(new UpsertServicePackageRequest
        {
            PackageCode = "TEST_LOW",
            PackageName = "Gói dưới ngưỡng PayOS",
            TargetRole = "All",
            PriceVnd = 500m,
            DurationDays = 10
        }).IsValid.Should().BeFalse();

        pkgValidator.Validate(new UpsertServicePackageRequest
        {
            PackageCode = "VALID_TIER",
            PackageName = "Gói Hợp Lệ",
            TargetRole = "Recycler",
            PriceVnd = 2000m,
            DurationDays = 30
        }).IsValid.Should().BeTrue();

        var recValidator = new ResolvePaymentAnomalyRequestValidator();
        recValidator.Validate(new ResolvePaymentAnomalyRequest
        {
            Action = nameof(ReconciliationAction.MarkRefunded),
            ResolutionBankRef = null,
            ResolutionNote = "ngắn"
        }).IsValid.Should().BeFalse();
    }
}
