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
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ecolink.UnitTests.Module10_GamificationAndBlog;

/// <summary>
/// Bộ unit test cho Module M-10 (Gamification, Virtual Tree, Leaderboard Cycles A-08, Clawback sync) &amp; M-14 (Blog &amp; Validators).
/// </summary>
[Trait("Module", "Module10_GamificationAndBlog")]
public class GamificationAndBlogUnitTests
{
    private static (LeaderboardService Service, IPointsService PointsMock, IAuditLogService Audit)
        CreateLeaderboardService(ApplicationDbContext db, Guid adminId)
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns((Guid?)adminId);
        currentUser.Role.Returns("Admin");
        currentUser.Email.Returns("admin@ecolink.vn");
        currentUser.IpAddress.Returns("127.0.0.1");
        currentUser.UserAgent.Returns("EcoLink-UnitTest/1.0");

        var pointsMock = Substitute.For<IPointsService>();
        pointsMock.CreditAsync(
                Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<PointActionType>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.ArgAt<long>(1)));

        var audit = new AuditLogService(db, currentUser);
        var svc = new LeaderboardService(db, pointsMock, audit, NullLogger<LeaderboardService>.Instance);
        return (svc, pointsMock, audit);
    }

    private static User MakeUser(string name, UserRole role = UserRole.User, UserStatus status = UserStatus.Active) => new()
    {
        Id = Guid.NewGuid(),
        Username = $"u_{Guid.NewGuid():N}"[..16],
        Email = $"u_{Guid.NewGuid():N}@ecolink.vn",
        FullName = name,
        Role = role,
        Status = status
    };

    // ============================================================
    // 1. A-08 Leaderboard Cycles & Ranking
    // ============================================================

    [Fact]
    public async Task CreateCycle_WithIsActiveTrue_ShouldDeactivatePreviousActiveCycle()
    {
        using var db = TestDbContextFactory.Create();
        var admin = MakeUser("Admin Eco", UserRole.Admin);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var (svc, _, _) = CreateLeaderboardService(db, admin.Id);
        var first = await svc.CreateCycleAsync(admin.Id, new SaveLeaderboardCycleRequest
        {
            Name = "Tuần 1",
            PeriodType = "Weekly",
            StartsAt = DateTime.UtcNow.AddDays(-7),
            EndsAt = DateTime.UtcNow.AddDays(1),
            IsActive = true
        });
        var second = await svc.CreateCycleAsync(admin.Id, new SaveLeaderboardCycleRequest
        {
            Name = "Tháng 11",
            PeriodType = "Monthly",
            StartsAt = DateTime.UtcNow.AddDays(-10),
            EndsAt = DateTime.UtcNow.AddDays(20),
            IsActive = true
        });

        var all = await svc.GetCyclesAsync();
        all.Count(c => c.IsActive).Should().Be(1);
        all.Single(c => c.Id == second.Id).IsActive.Should().BeTrue();
        all.Single(c => c.Id == first.Id).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateCycle_WhenEndsAtBeforeStartsAt_ShouldThrowMSGAuth02()
    {
        using var db = TestDbContextFactory.Create();
        var (svc, _, _) = CreateLeaderboardService(db, Guid.NewGuid());
        var now = DateTime.UtcNow;

        var act = () => svc.CreateCycleAsync(Guid.NewGuid(), new SaveLeaderboardCycleRequest
        {
            Name = "Chu kỳ sai ngày",
            PeriodType = "Weekly",
            StartsAt = now,
            EndsAt = now.AddHours(-1)
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.MessageCode.Should().Be("MSG-AUTH-02");
    }

    [Fact]
    public async Task CreateCycle_WhenBonusOrderInvalidOrNegative_ShouldThrowMSGAuth02()
    {
        using var db = TestDbContextFactory.Create();
        var (svc, _, _) = CreateLeaderboardService(db, Guid.NewGuid());

        var ex1 = await FluentActions.Invoking(() => svc.CreateCycleAsync(Guid.NewGuid(), new SaveLeaderboardCycleRequest
        {
            Name = "Âm điểm",
            PeriodType = "Monthly",
            StartsAt = DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddDays(7),
            Top1BonusPoints = 100,
            Top2BonusPoints = 200,
            Top3BonusPoints = 50
        })).Should().ThrowAsync<BusinessRuleException>();

        ex1.Which.MessageCode.Should().Be("MSG-AUTH-02");
    }

    [Fact]
    public async Task CreateCycle_WhenPeriodTypeInvalid_ShouldThrowMSGAuth02()
    {
        using var db = TestDbContextFactory.Create();
        var (svc, _, _) = CreateLeaderboardService(db, Guid.NewGuid());

        var ex = await FluentActions.Invoking(() => svc.CreateCycleAsync(Guid.NewGuid(), new SaveLeaderboardCycleRequest
        {
            Name = "Chu kỳ năm",
            PeriodType = "Yearly",
            StartsAt = DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddDays(365)
        })).Should().ThrowAsync<BusinessRuleException>();

        ex.Which.MessageCode.Should().Be("MSG-AUTH-02");
    }

    [Fact]
    public async Task ActivateCycle_ShouldSwitchActiveCycleAndWriteAuditLog()
    {
        using var db = TestDbContextFactory.Create();
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var (svc, _, _) = CreateLeaderboardService(db, admin.Id);
        var week = await svc.CreateCycleAsync(admin.Id, new SaveLeaderboardCycleRequest
        {
            Name = "Tuần 46",
            PeriodType = "Weekly",
            StartsAt = DateTime.UtcNow.AddDays(-3),
            EndsAt = DateTime.UtcNow.AddDays(4),
            IsActive = true
        });
        var month = await svc.CreateCycleAsync(admin.Id, new SaveLeaderboardCycleRequest
        {
            Name = "Tháng 11",
            PeriodType = "Monthly",
            StartsAt = DateTime.UtcNow.AddDays(-18),
            EndsAt = DateTime.UtcNow.AddDays(12),
            IsActive = false
        });

        var switched = await svc.ActivateCycleAsync(admin.Id, month.Id);
        switched.IsActive.Should().BeTrue();
        (await db.LeaderboardCycles.FirstAsync(c => c.Id == week.Id)).IsActive.Should().BeFalse();
        (await db.AuditLogs.AnyAsync(a => a.Action == "LEADERBOARD_CYCLE_ACTIVATE" && a.EntityId == month.Id.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task ActivateCycle_WhenAlreadyFinalized_ShouldThrowMSGGen01()
    {
        using var db = TestDbContextFactory.Create();
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.Add(admin);
        var cycle = new LeaderboardCycle
        {
            Id = Guid.NewGuid(),
            Name = "Kỳ cũ",
            PeriodType = LeaderboardPeriodType.Weekly,
            StartsAt = DateTime.UtcNow.AddDays(-14),
            EndsAt = DateTime.UtcNow.AddDays(-7),
            Status = LeaderboardCycleStatus.Finalized,
            IsActive = false
        };
        db.LeaderboardCycles.Add(cycle);
        await db.SaveChangesAsync();

        var (svc, _, _) = CreateLeaderboardService(db, admin.Id);
        var ex = await FluentActions.Invoking(() => svc.ActivateCycleAsync(admin.Id, cycle.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        ex.Which.MessageCode.Should().Be("MSG-GEN-01");
    }

    [Fact]
    public async Task GetLeaderboardView_ShouldRankByEarnedAndNotPenalizeRedemptionSpend()
    {
        using var db = TestDbContextFactory.Create();
        var spender = MakeUser("Nguyễn Thị Tiêu Điểm");
        var saver = MakeUser("Trần Văn Giữ Điểm");
        var recycler = MakeUser("Vựa Phế Liệu", UserRole.Recycler);
        db.Users.AddRange(spender, saver, recycler);

        db.UserPointWallets.AddRange(
            new UserPointWallet { UserId = spender.Id, CurrentBalance = 50, TotalEarned = 1500, TotalSpent = 1450 },
            new UserPointWallet { UserId = saver.Id, CurrentBalance = 900, TotalEarned = 900, TotalSpent = 0 },
            new UserPointWallet { UserId = recycler.Id, CurrentBalance = 5000, TotalEarned = 5000, TotalSpent = 0 }
        );
        db.LeaderboardCycles.Add(new LeaderboardCycle
        {
            Name = "Chu kỳ tháng",
            PeriodType = LeaderboardPeriodType.Monthly,
            StartsAt = DateTime.UtcNow.AddDays(-5),
            EndsAt = DateTime.UtcNow.AddDays(25),
            IsActive = true,
            Status = LeaderboardCycleStatus.Active
        });
        await db.SaveChangesAsync();

        var (svc, _, _) = CreateLeaderboardService(db, Guid.NewGuid());
        var view = await svc.GetLeaderboardViewAsync(10, null, null, saver.Id);

        view.Items.Should().HaveCount(2); // Không đưa Recycler vào BXH người dân
        view.Items[0].UserId.Should().Be(spender.Id);
        view.Items[0].Rank.Should().Be(1);
        view.Items[0].TotalPoints.Should().Be(1500);
        view.Items[0].SpendablePoints.Should().Be(50);

        view.Items[1].UserId.Should().Be(saver.Id);
        view.Items[1].Rank.Should().Be(2);
        view.Items[1].IsCurrentUser.Should().BeTrue();
        view.Items[1].PointsNeededForNextRank.Should().Be(600);
        view.MyEntry.Should().NotBeNull();
        view.MyEntry!.Rank.Should().Be(2);
    }

    [Fact]
    public async Task GetLeaderboardView_WhenPeriodTransactionsExist_ShouldRankByPeriodAndUseHandoverArea()
    {
        using var db = TestDbContextFactory.Create();
        var topInPeriod = MakeUser("Lê Công Dân 1");
        var secondInPeriod = MakeUser("Phạm Công Dân 2");
        var rec = MakeUser("Vựa 1", UserRole.Recycler);
        db.Users.AddRange(topInPeriod, secondInPeriod, rec);

        var start = DateTime.UtcNow.AddDays(-3);
        var end = DateTime.UtcNow.AddDays(4);
        var w1 = new UserPointWallet { Id = Guid.NewGuid(), UserId = topInPeriod.Id, CurrentBalance = 120, TotalEarned = 120 };
        var w2 = new UserPointWallet { Id = Guid.NewGuid(), UserId = secondInPeriod.Id, CurrentBalance = 500, TotalEarned = 500 };
        db.UserPointWallets.AddRange(w1, w2);

        db.PointTransactions.AddRange(
            new PointTransaction { WalletId = w1.Id, UserId = topInPeriod.Id, Points = 200, ActionType = PointActionType.HandoverReward, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new PointTransaction { WalletId = w2.Id, UserId = secondInPeriod.Id, Points = 80, ActionType = PointActionType.DailyCheckin, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            // Thưởng leaderboard kỳ trước không được tính lại vào điểm thi đua kỳ mới
            new PointTransaction { WalletId = w2.Id, UserId = secondInPeriod.Id, Points = 500, ActionType = PointActionType.LeaderboardBonus, CreatedAt = DateTime.UtcNow.AddDays(-1) }
        );
        db.HandoverRequests.Add(new HandoverRequest
        {
            RequestCode = "HND-AREA-01",
            UserId = topInPeriod.Id,
            RecyclerId = rec.Id,
            PickupAddress = "12 Nguyễn Trãi, Thạch Thất, Hà Nội",
            ProposedTimeSlot = DateTime.UtcNow,
            Status = HandoverStatus.Completed
        });
        db.LeaderboardCycles.Add(new LeaderboardCycle
        {
            Name = "Tuần hiện tại",
            PeriodType = LeaderboardPeriodType.Weekly,
            StartsAt = start,
            EndsAt = end,
            IsActive = true,
            Status = LeaderboardCycleStatus.Active
        });
        await db.SaveChangesAsync();

        var (svc, _, _) = CreateLeaderboardService(db, Guid.NewGuid());
        var view = await svc.GetLeaderboardViewAsync(10, "Weekly", null, secondInPeriod.Id);

        view.Items[0].UserId.Should().Be(topInPeriod.Id);
        view.Items[0].PeriodPoints.Should().Be(200);
        view.Items[0].Area.Should().Be("Thạch Thất, Hà Nội");
        view.Items[1].UserId.Should().Be(secondInPeriod.Id);
        view.Items[1].PeriodPoints.Should().Be(80);
    }

    [Fact]
    public async Task FinalizeCycle_ShouldCreditTop3BonusesSaveSnapshotAndWriteAuditLog()
    {
        using var db = TestDbContextFactory.Create();
        var admin = MakeUser("Admin", UserRole.Admin);
        var u1 = MakeUser("Hạng 1");
        var u2 = MakeUser("Hạng 2");
        var u3 = MakeUser("Hạng 3");
        db.Users.AddRange(admin, u1, u2, u3);
        db.UserPointWallets.AddRange(
            new UserPointWallet { UserId = u1.Id, CurrentBalance = 300, TotalEarned = 300 },
            new UserPointWallet { UserId = u2.Id, CurrentBalance = 200, TotalEarned = 200 },
            new UserPointWallet { UserId = u3.Id, CurrentBalance = 100, TotalEarned = 100 }
        );
        var cycle = new LeaderboardCycle
        {
            Id = Guid.NewGuid(),
            Name = "Đua Top Tháng 11",
            PeriodType = LeaderboardPeriodType.Monthly,
            StartsAt = DateTime.UtcNow.AddDays(-20),
            EndsAt = DateTime.UtcNow,
            IsActive = true,
            Status = LeaderboardCycleStatus.Active,
            Top1BonusPoints = 500,
            Top2BonusPoints = 300,
            Top3BonusPoints = 150
        };
        db.LeaderboardCycles.Add(cycle);
        await db.SaveChangesAsync();

        var (svc, pointsMock, _) = CreateLeaderboardService(db, admin.Id);
        var finalized = await svc.FinalizeCycleAsync(admin.Id, cycle.Id);

        finalized.Status.Should().Be("Finalized");
        finalized.IsActive.Should().BeFalse();
        finalized.Winners.Should().HaveCount(3);
        finalized.Winners[0].BonusPointsAwarded.Should().Be(500);
        finalized.Winners[1].BonusPointsAwarded.Should().Be(300);
        finalized.Winners[2].BonusPointsAwarded.Should().Be(150);

        await pointsMock.Received(1).CreditAsync(u1.Id, 500, PointActionType.LeaderboardBonus, "LeaderboardCycle", cycle.Id.ToString(), $"LEADERBOARD:{cycle.Id}:RANK:1:{u1.Id}", Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await pointsMock.Received(1).CreditAsync(u2.Id, 300, PointActionType.LeaderboardBonus, "LeaderboardCycle", cycle.Id.ToString(), $"LEADERBOARD:{cycle.Id}:RANK:2:{u2.Id}", Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await pointsMock.Received(1).CreditAsync(u3.Id, 150, PointActionType.LeaderboardBonus, "LeaderboardCycle", cycle.Id.ToString(), $"LEADERBOARD:{cycle.Id}:RANK:3:{u3.Id}", Arg.Any<string?>(), Arg.Any<CancellationToken>());
        (await db.AuditLogs.AnyAsync(a => a.Action == "LEADERBOARD_CYCLE_FINALIZE" && a.EntityId == cycle.Id.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task FinalizeCycle_WhenAlreadyFinalized_ShouldThrowMSGGen01AndNotDoubleCredit()
    {
        using var db = TestDbContextFactory.Create();
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.Add(admin);
        var cycle = new LeaderboardCycle
        {
            Id = Guid.NewGuid(),
            Name = "Đã chốt",
            PeriodType = LeaderboardPeriodType.Weekly,
            StartsAt = DateTime.UtcNow.AddDays(-7),
            EndsAt = DateTime.UtcNow,
            Status = LeaderboardCycleStatus.Finalized
        };
        db.LeaderboardCycles.Add(cycle);
        await db.SaveChangesAsync();

        var (svc, pointsMock, _) = CreateLeaderboardService(db, admin.Id);
        var ex = await FluentActions.Invoking(() => svc.FinalizeCycleAsync(admin.Id, cycle.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        ex.Which.MessageCode.Should().Be("MSG-GEN-01");
        await pointsMock.DidNotReceiveWithAnyArgs().CreditAsync(default, default, default, null, null, "", null, default);
    }

    // ============================================================
    // 2. GamificationService DB Integration
    // ============================================================

    [Fact]
    public async Task GamificationService_GetUserProfile_ShouldAggregateRealDbWalletTreeScansReceiptsAndStreak()
    {
        using var db = TestDbContextFactory.Create();
        var citizen = MakeUser("Nguyễn Sống Xanh");
        var recycler = MakeUser("Vựa Hòa Lạc", UserRole.Recycler);
        db.Users.AddRange(citizen, recycler);

        db.TreeLevelThresholds.AddRange(
            new TreeLevelThreshold { Level = 1, Title = "Mầm xanh", RequiredPoints = 0 },
            new TreeLevelThreshold { Level = 2, Title = "Cây non", RequiredPoints = 100 },
            new TreeLevelThreshold { Level = 3, Title = "Cây trưởng thành", RequiredPoints = 300 }
        );
        db.UserPointWallets.Add(new UserPointWallet { UserId = citizen.Id, CurrentBalance = 240, TotalEarned = 440, TotalSpent = 200 });
        db.VirtualTreeProgresses.Add(new VirtualTreeProgress { UserId = citizen.Id, TreeLevel = 2, GrowthPoints = 100 });
        db.AiScanHistories.AddRange(
            new AiScanHistory { UserId = citizen.Id, ImageUrl = "https://img/1.jpg", ImageSha256 = "abc" },
            new AiScanHistory { UserId = citizen.Id, ImageUrl = "https://img/2.jpg", ImageSha256 = "def" }
        );
        var h1 = new HandoverRequest { Id = Guid.NewGuid(), RequestCode = "HND-REAL-1", UserId = citizen.Id, RecyclerId = recycler.Id, PickupAddress = "Hà Nội", ProposedTimeSlot = DateTime.UtcNow, Status = HandoverStatus.Completed };
        db.HandoverRequests.Add(h1);
        db.HandoverReceipts.Add(new HandoverReceipt { RequestId = h1.Id, ReceiptCode = "RCP-REAL-1", UserId = citizen.Id, RecyclerId = recycler.Id, TotalWeightKg = 10m, TotalAmountCash = 70000m, PointsAwarded = 100, ScaleProofPhotoUrl = "[]" });
        var todayIct = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        db.DailyCheckinLogs.Add(new DailyCheckinLog { UserId = citizen.Id, CheckinDate = todayIct, StreakCount = 4, PointsAwarded = 8 });
        await db.SaveChangesAsync();

        var svc = new GamificationService(NullLogger<GamificationService>.Instance, db);
        var profile = await svc.GetUserProfileAsync(citizen.Id);

        profile.UserId.Should().Be(citizen.Id);
        profile.CurrentGreenPoints.Should().Be(240);
        profile.TotalLifetimePoints.Should().Be(440);
        profile.TreeLevel.Should().Be(2);
        profile.TreeStageTitle.Should().Be("Cây non");
        profile.TreeExperience.Should().Be(100);
        profile.ExperienceToNextLevel.Should().Be(300);
        profile.TotalWasteScanned.Should().Be(2);
        profile.TotalRecycledKg.Should().Be(10m);
        profile.CurrentStreak.Should().Be(4);
        profile.Co2SavedKg.Should().Be(18.5);
    }

    [Fact]
    public async Task GamificationService_GetUserProfile_WhenUserMissing_ShouldThrowNotFoundException()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new GamificationService(NullLogger<GamificationService>.Instance, db);

        await FluentActions.Invoking(() => svc.GetUserProfileAsync(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ============================================================
    // 3. ComplaintService Wallet Clawback Sync (M-11 + M-10)
    // ============================================================

    [Fact]
    public async Task ComplaintService_CancelDisputedOrder_ShouldClawBackBothEcoProfileAndPointWallet()
    {
        using var db = TestDbContextFactory.Create();
        var citizen = MakeUser("Citizen Khiếu Nại");
        citizen.EcoProfile = new UserEcoProfile { UserId = citizen.Id, CurrentGreenPoints = 120, TotalLifetimePoints = 120, TotalRecycledKg = 8m };
        var recycler = MakeUser("Vựa Thu Gom", UserRole.Recycler);
        recycler.EcoProfile = new UserEcoProfile { UserId = recycler.Id, CurrentGreenPoints = 80, TotalLifetimePoints = 80, TotalRecycledKg = 8m };
        var staff = MakeUser("Staff", UserRole.Staff);
        db.Users.AddRange(citizen, recycler, staff);

        var wCitizen = new UserPointWallet { Id = Guid.NewGuid(), UserId = citizen.Id, CurrentBalance = 120, TotalEarned = 120 };
        var wRecycler = new UserPointWallet { Id = Guid.NewGuid(), UserId = recycler.Id, CurrentBalance = 80, TotalEarned = 80 };
        db.UserPointWallets.AddRange(wCitizen, wRecycler);

        var req = new HandoverRequest { Id = Guid.NewGuid(), RequestCode = "HND-CMP-1", UserId = citizen.Id, RecyclerId = recycler.Id, PickupAddress = "Hà Nội", ProposedTimeSlot = DateTime.UtcNow, Status = HandoverStatus.Disputed };
        var rec = new HandoverReceipt { Id = Guid.NewGuid(), RequestId = req.Id, ReceiptCode = "RCP-CMP-1", UserId = citizen.Id, RecyclerId = recycler.Id, TotalWeightKg = 5m, TotalAmountCash = 35000m, PointsAwarded = 50, ScaleProofPhotoUrl = "[]" };
        var cmp = new TransactionComplaint
        {
            Id = Guid.NewGuid(),
            ComplaintCode = "CMP-TEST-001",
            RequestId = req.Id,
            ReceiptId = rec.Id,
            ComplainantId = citizen.Id,
            ComplainantRole = "Citizen",
            RespondentId = recycler.Id,
            Category = ComplaintCategory.ScaleDiscrepancy,
            Description = "Cân thiếu khối lượng",
            Status = ComplaintStatus.Open
        };
        db.HandoverRequests.Add(req);
        db.HandoverReceipts.Add(rec);
        db.TransactionComplaints.Add(cmp);
        await db.SaveChangesAsync();

        var cur = Substitute.For<ICurrentUserService>();
        cur.UserId.Returns((Guid?)staff.Id);
        cur.Role.Returns("Staff");
        var audit = new AuditLogService(db, cur);
        var reviewSvc = new ReviewService(db, audit, NullLogger<ReviewService>.Instance);
        var storage = Substitute.For<IFileStorageService>();
        var complaintSvc = new ComplaintService(db, storage, audit, reviewSvc, NullLogger<ComplaintService>.Instance);

        var res = await complaintSvc.ResolveComplaintAsync(staff.Id, cmp.Id, new ResolveComplaintRequest
        {
            ResolutionOutcome = "CancelDisputedOrder",
            ResolutionNote = "Xác nhận cân gian lận, hủy đơn và thu hồi điểm thưởng."
        });

        res.PointsRevoked.Should().BeTrue();
        (await db.UserPointWallets.FirstAsync(w => w.UserId == citizen.Id)).CurrentBalance.Should().Be(70);
        (await db.UserPointWallets.FirstAsync(w => w.UserId == recycler.Id)).CurrentBalance.Should().Be(30);
        (await db.UserEcoProfiles.FirstAsync(e => e.UserId == citizen.Id)).CurrentGreenPoints.Should().Be(70);
        (await db.PointTransactions.CountAsync(t => t.SourceType == "ComplaintClawback" && t.Points == -50)).Should().Be(2);
    }

    [Fact]
    public async Task ComplaintService_CancelDisputedOrder_WhenWalletBalanceLowerThanRevoke_ShouldClampWalletToZero()
    {
        using var db = TestDbContextFactory.Create();
        var citizen = MakeUser("Citizen Đã Tiêu");
        citizen.EcoProfile = new UserEcoProfile { UserId = citizen.Id, CurrentGreenPoints = 15, TotalLifetimePoints = 100 };
        var recycler = MakeUser("Vựa 2", UserRole.Recycler);
        var staff = MakeUser("Staff", UserRole.Staff);
        db.Users.AddRange(citizen, recycler, staff);
        db.UserPointWallets.Add(new UserPointWallet { UserId = citizen.Id, CurrentBalance = 15, TotalEarned = 100, TotalSpent = 85 });

        var req = new HandoverRequest { Id = Guid.NewGuid(), RequestCode = "HND-CMP-2", UserId = citizen.Id, RecyclerId = recycler.Id, PickupAddress = "Hà Nội", ProposedTimeSlot = DateTime.UtcNow, Status = HandoverStatus.Disputed };
        var rec = new HandoverReceipt { Id = Guid.NewGuid(), RequestId = req.Id, ReceiptCode = "RCP-CMP-2", UserId = citizen.Id, RecyclerId = recycler.Id, TotalWeightKg = 5m, TotalAmountCash = 35000m, PointsAwarded = 50, ScaleProofPhotoUrl = "[]" };
        var cmp = new TransactionComplaint { Id = Guid.NewGuid(), ComplaintCode = "CMP-TEST-002", RequestId = req.Id, ReceiptId = rec.Id, ComplainantId = citizen.Id, ComplainantRole = "Citizen", RespondentId = recycler.Id, Category = ComplaintCategory.ScaleDiscrepancy, Description = "Sai cân", Status = ComplaintStatus.Open };
        db.HandoverRequests.Add(req);
        db.HandoverReceipts.Add(rec);
        db.TransactionComplaints.Add(cmp);
        await db.SaveChangesAsync();

        var cur = Substitute.For<ICurrentUserService>();
        cur.UserId.Returns((Guid?)staff.Id);
        var audit = new AuditLogService(db, cur);
        var complaintSvc = new ComplaintService(db, Substitute.For<IFileStorageService>(), audit, new ReviewService(db, audit, NullLogger<ReviewService>.Instance), NullLogger<ComplaintService>.Instance);

        await complaintSvc.ResolveComplaintAsync(staff.Id, cmp.Id, new ResolveComplaintRequest
        {
            ResolutionOutcome = "CancelDisputedOrder",
            ResolutionNote = "Thu hồi không để âm số dư ví điểm."
        });

        var walletAfter = await db.UserPointWallets.FirstAsync(w => w.UserId == citizen.Id);
        walletAfter.CurrentBalance.Should().Be(0);
        var clawTx = await db.PointTransactions.FirstAsync(t => t.UserId == citizen.Id && t.SourceType == "ComplaintClawback");
        clawTx.Points.Should().Be(-15);
    }

    [Fact]
    public async Task ComplaintService_ConfirmReceiptAsIs_ShouldCallAwardHandoverPointsIfPointsServicePresent()
    {
        using var db = TestDbContextFactory.Create();
        var citizen = MakeUser("Citizen");
        var recycler = MakeUser("Vựa 3", UserRole.Recycler);
        var staff = MakeUser("Staff", UserRole.Staff);
        db.Users.AddRange(citizen, recycler, staff);

        var req = new HandoverRequest { Id = Guid.NewGuid(), RequestCode = "HND-CMP-3", UserId = citizen.Id, RecyclerId = recycler.Id, PickupAddress = "Hà Nội", ProposedTimeSlot = DateTime.UtcNow, Status = HandoverStatus.Disputed };
        var rec = new HandoverReceipt { Id = Guid.NewGuid(), RequestId = req.Id, ReceiptCode = "RCP-CMP-3", UserId = citizen.Id, RecyclerId = recycler.Id, TotalWeightKg = 6m, TotalAmountCash = 42000m, PointsAwarded = 0, ScaleProofPhotoUrl = "[]" };
        var cmp = new TransactionComplaint { Id = Guid.NewGuid(), ComplaintCode = "CMP-TEST-003", RequestId = req.Id, ReceiptId = rec.Id, ComplainantId = citizen.Id, ComplainantRole = "Citizen", RespondentId = recycler.Id, Category = ComplaintCategory.ScaleDiscrepancy, Description = "Kiểm tra lại cân", Status = ComplaintStatus.Open };
        db.HandoverRequests.Add(req);
        db.HandoverReceipts.Add(rec);
        db.TransactionComplaints.Add(cmp);
        await db.SaveChangesAsync();

        var pointsMock = Substitute.For<IPointsService>();
        var cur = Substitute.For<ICurrentUserService>();
        cur.UserId.Returns((Guid?)staff.Id);
        var audit = new AuditLogService(db, cur);
        var complaintSvc = new ComplaintService(db, Substitute.For<IFileStorageService>(), audit, new ReviewService(db, audit, NullLogger<ReviewService>.Instance), NullLogger<ComplaintService>.Instance, pointsMock);

        await complaintSvc.ResolveComplaintAsync(staff.Id, cmp.Id, new ResolveComplaintRequest
        {
            ResolutionOutcome = "ConfirmReceiptAsIs",
            ResolutionNote = "Biên nhận cân chuẩn xác."
        });

        await pointsMock.Received(1).AwardHandoverPointsAsync(rec.Id, Arg.Any<CancellationToken>());
    }

    // ============================================================
    // 4. Blog Slugify, FluentValidation & GamSettings Formula
    // ============================================================

    [Fact]
    public async Task BlogPostService_CreateDraft_ShouldSlugifyVietnameseTitleAndDisambiguateDuplicates()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Blog", UserRole.Staff);
        db.Users.Add(staff);
        await db.SaveChangesAsync();

        var blogSvc = new BlogPostService(db, NullLogger<BlogPostService>.Instance);
        var first = await blogSvc.CreateAsync(staff.Id, new SaveBlogPostRequest { Title = "Đổi rác nhựa nhận quà xanh!", SubmitForApproval = false });
        var second = await blogSvc.CreateAsync(staff.Id, new SaveBlogPostRequest { Title = "Đổi rác nhựa nhận quà xanh!", SubmitForApproval = false });

        first.Slug.Should().Be("doi-rac-nhua-nhan-qua-xanh");
        second.Slug.Should().Be("doi-rac-nhua-nhan-qua-xanh-2");
    }

    [Fact]
    public void Validators_SaveBlogPost_And_ReviewBlogPost_ShouldValidateCorrectly()
    {
        var blogVal = new SaveBlogPostRequestValidator();
        blogVal.Validate(new SaveBlogPostRequest { Title = "Ngắn", SubmitForApproval = true }).IsValid.Should().BeFalse();
        blogVal.Validate(new SaveBlogPostRequest
        {
            Title = "Hướng dẫn phân loại nhựa PET và HDPE tại nhà",
            Content = "<p>Nội dung đầy đủ</p>",
            CoverImageUrl = "/assets/blog/cover.jpg",
            SubmitForApproval = true
        }).IsValid.Should().BeTrue();

        var reviewVal = new ReviewBlogPostRequestValidator();
        reviewVal.Validate(new ReviewBlogPostRequest { Action = "Reject", Feedback = "" }).IsValid.Should().BeFalse();
        reviewVal.Validate(new ReviewBlogPostRequest { Action = "Reject", Feedback = "Cần bổ sung ảnh minh họa." }).IsValid.Should().BeTrue();
        reviewVal.Validate(new ReviewBlogPostRequest { Approve = true }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validators_SaveGift_And_SaveEcoFact_ShouldValidateCorrectly()
    {
        var giftVal = new SaveGiftRequestValidator();
        giftVal.Validate(new SaveGiftRequest { Name = "", Type = "Invalid", PointsRequired = -1, StockQuantity = 1 }).IsValid.Should().BeFalse();
        giftVal.Validate(new SaveGiftRequest { Name = "Túi vải", Type = "Physical", PointsRequired = 100, StockQuantity = 10 }).IsValid.Should().BeTrue();

        var factVal = new SaveEcoFactRequestValidator();
        factVal.Validate(new SaveEcoFactRequest { Title = "Nhựa PET", Content = "", UnlockTreeLevel = 0 }).IsValid.Should().BeFalse();
        factVal.Validate(new SaveEcoFactRequest { Title = "Nhựa PET", Content = "Chai PET có thể tái chế thành sợi vải.", UnlockTreeLevel = 2 }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validators_GamificationConfig_ShouldRejectNonConsecutiveOrNonIncreasingLevels()
    {
        var cfgVal = new GamificationConfigDtoValidator();
        var bad = new GamificationConfigDto
        {
            DailyNonTransactionCap = 50,
            CheckinBasePoints = 5,
            StreakBonusIncrement = 1,
            StreakBonusCap = 20,
            DailyTreeCareLimit = 3,
            VoucherValidityDays = 30,
            DefaultPointsPerKg = 10,
            TreeLevels = new List<TreeLevelConfigDto>
            {
                new() { Level = 1, Title = "Mầm", RequiredPoints = 0 },
                new() { Level = 3, Title = "Nhảy cóc", RequiredPoints = 100 }
            }
        };
        cfgVal.Validate(bad).IsValid.Should().BeFalse();

        bad.TreeLevels[1].Level = 2;
        cfgVal.Validate(bad).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validators_SaveLeaderboardCycle_ShouldValidateWindowAndRewards()
    {
        var val = new SaveLeaderboardCycleRequestValidator();
        var now = DateTime.UtcNow;

        val.Validate(new SaveLeaderboardCycleRequest
        {
            Name = "Tuần 1",
            PeriodType = "Weekly",
            StartsAt = now,
            EndsAt = now.AddDays(7),
            Top1BonusPoints = 500,
            Top2BonusPoints = 300,
            Top3BonusPoints = 150
        }).IsValid.Should().BeTrue();

        val.Validate(new SaveLeaderboardCycleRequest
        {
            Name = "Sai",
            PeriodType = "Daily",
            StartsAt = now,
            EndsAt = now.AddHours(-1)
        }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void GamSettings_CheckinReward_ShouldApplyStreakCapCorrectly()
    {
        var settings = new GamSettings(
            DailyCap: 50, CheckinBase: 5, StreakIncrement: 2, StreakCap: 10,
            TreeLimit: 3, VoucherDays: 30, DefaultPerKg: 10,
            Rates: new Dictionary<string, long>(),
            Levels: Array.Empty<TreeLevelConfigDto>());

        settings.CheckinReward(1).Should().Be(5);
        settings.CheckinReward(3).Should().Be(9);
        settings.CheckinReward(20).Should().Be(15); // 5 + min(19*2, 10)
    }
}
