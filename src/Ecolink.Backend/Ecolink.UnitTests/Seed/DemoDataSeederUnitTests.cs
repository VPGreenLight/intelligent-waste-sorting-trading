using Ecolink.Application.Interfaces;
using Ecolink.Domain.Enums;
using Ecolink.Infrastructure.Data;
using Ecolink.Infrastructure.Persistence.Seed;
using Ecolink.Infrastructure.Services;
using Ecolink.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ecolink.UnitTests.Seed;

[Trait("Module", "DemoDataSeeder")]
public class DemoDataSeederUnitTests
{
    private static readonly Guid CitizenId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RecyclerProId = Guid.Parse("22222223-2222-2222-2222-222222222223");
    private static readonly Guid StaffId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private static (DemoDataSeeder Sut, ApplicationDbContext Db) CreateSut()
    {
        var db = TestDbContextFactory.Create();
        var tokenService = Substitute.For<ITokenService>();
        var emailService = Substitute.For<IEmailService>();
        var config = TestDbContextFactory.CreateConfiguration();

        var authService = new AuthService(
            db,
            tokenService,
            emailService,
            config,
            NullLogger<AuthService>.Instance);

        var wasteRuleEngine = new WasteRuleEngineService(
            NullLogger<WasteRuleEngineService>.Instance,
            db);

        var platformPolicyService = new PlatformPolicyService(db);

        var geoMatchingService = new GeoMatchingService(
            NullLogger<GeoMatchingService>.Instance,
            db,
            wasteRuleEngine);

        var onboardingService = new RecyclerOnboardingService(
            db,
            emailService,
            NullLogger<RecyclerOnboardingService>.Instance);

        var blogPostService = new BlogPostService(
            db,
            NullLogger<BlogPostService>.Instance);

        var gamificationConfigService = new GamificationConfigService(db);

        var sut = new DemoDataSeeder(
            db,
            authService,
            wasteRuleEngine,
            platformPolicyService,
            geoMatchingService,
            onboardingService,
            blogPostService,
            gamificationConfigService,
            NullLogger<DemoDataSeeder>.Instance);

        return (sut, db);
    }

    [Fact]
    public async Task UTC_SEED_01_SeedAsync_PopulatesAllCoreDemoEntitiesAndPreservesCouncilInvariants()
    {
        var (sut, db) = CreateSut();

        var report = await sut.SeedAsync(resetExisting: false);

        report.WasteCategories.Should().Be(10);
        report.SortingRules.Should().Be(10);
        report.PlatformPolicies.Should().Be(5);
        report.ServicePackages.Should().Be(3);
        report.BlogPosts.Should().Be(6);
        report.EcoFacts.Should().Be(10);
        report.Gifts.Should().Be(6);
        report.GiftRedemptions.Should().Be(2);
        report.HandoverRequests.Should().Be(70);
        report.TransactionReviews.Should().Be(61);
        report.TransactionComplaints.Should().Be(3);
        report.AiScanHistories.Should().Be(8);
        report.AiFeedbackLogs.Should().Be(4);
        report.AiDatasetItems.Should().Be(8);
        report.AiDatasetVersions.Should().Be(1);
        report.ServiceOrders.Should().Be(5);
        report.UserSubscriptions.Should().Be(3);
        report.PaymentWebhookLogs.Should().Be(3);
        report.StaffAttendances.Should().Be(8);

        var activeStatuses = new[]
        {
            HandoverStatus.Pending,
            HandoverStatus.Negotiating,
            HandoverStatus.Accepted,
            HandoverStatus.Scheduled,
            HandoverStatus.AwaitingConfirmation
        };

        var citizenActiveCount = await db.HandoverRequests
            .AsNoTracking()
            .CountAsync(h => h.UserId == CitizenId && activeStatuses.Contains(h.Status));
        citizenActiveCount.Should().Be(2);

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var citizenCheckins = await db.DailyCheckinLogs
            .AsNoTracking()
            .Where(c => c.UserId == CitizenId)
            .ToListAsync();
        citizenCheckins.Should().HaveCount(3);
        citizenCheckins.Should().NotContain(c => c.CheckinDate == todayVn);

        var staffAttendances = await db.StaffAttendances
            .AsNoTracking()
            .Where(a => a.StaffId == StaffId)
            .ToListAsync();
        staffAttendances.Should().HaveCount(5);
        staffAttendances.Should().NotContain(a => a.WorkDate == todayVn);

        var recyclerPro = await db.RecyclerProfiles
            .AsNoTracking()
            .FirstAsync(r => r.UserId == RecyclerProId);
        recyclerPro.TotalReviews.Should().Be(52);
        recyclerPro.AverageRating.Should().Be(4.81m);

        var proHasActiveSub = await db.UserSubscriptions
            .AsNoTracking()
            .AnyAsync(s => s.UserId == RecyclerProId && s.Status == SubscriptionStatus.Active);
        proHasActiveSub.Should().BeFalse();

        (await db.HandoverReceipts.AsNoTracking().AllAsync(r => r.PointsAwarded == 0)).Should().BeTrue();
        (await db.TransactionReceipts.AsNoTracking().AllAsync(r => r.PointsAwarded == 0)).Should().BeTrue();
        (await db.PointTransactions.AsNoTracking().AnyAsync(t => t.ActionType == PointActionType.HandoverReward)).Should().BeFalse();
    }

    [Fact]
    public async Task UTC_SEED_02_SeedAsync_CalledTwiceWithoutReset_IsIdempotent()
    {
        var (sut, _) = CreateSut();

        var first = await sut.SeedAsync(resetExisting: false);
        var second = await sut.SeedAsync(resetExisting: false);

        second.Should().Be(first);
    }
}
