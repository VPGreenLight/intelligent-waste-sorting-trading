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
/// Bộ kiểm thử đơn vị chuẩn hóa cho Module 13 / M-10 (FE-13): Gamification &amp; Rewards
/// (LeaderboardService, GamificationService, CheckinService, PointsService, GiftService,
/// VirtualTreeService, EcoFactService, GamificationConfigService, ComplaintService Clawback &amp; Validators).
/// Gồm 22 Unit Test Cases (UTC001 -> UTC022: 10 Normal, 8 Abnormal, 4 Boundary).
/// </summary>
[Trait("Module", "Module13_GamificationAndRewards")]
public class GamificationAndBlogUnitTests
{
    private static IAuditLogService CreateAudit(ApplicationDbContext db, Guid actorId, string role = "Admin")
    {
        var cur = Substitute.For<ICurrentUserService>();
        cur.UserId.Returns((Guid?)actorId);
        cur.Role.Returns(role);
        cur.Email.Returns($"{role.ToLowerInvariant()}@ecolink.vn");
        cur.IpAddress.Returns("127.0.0.1");
        cur.UserAgent.Returns("EcoLink-UnitTest/1.0");
        return new AuditLogService(db, cur);
    }

    private static (
        GamificationConfigService Config,
        PointsService Points,
        CheckinService Checkin,
        GiftService Gifts,
        VirtualTreeService Tree,
        EcoFactService Facts,
        LeaderboardService Leaderboard,
        GamificationService Gamification)
        CreateSuite(ApplicationDbContext db, Guid actorId, string role = "Admin")
    {
        var audit = CreateAudit(db, actorId, role);
        var config = new GamificationConfigService(db, audit);
        var points = new PointsService(db, config, NullLogger<PointsService>.Instance);
        var checkin = new CheckinService(db, points, config);
        var gifts = new GiftService(db, points, config);
        var tree = new VirtualTreeService(db, points, config);
        var facts = new EcoFactService(db, config);
        var leaderboard = new LeaderboardService(db, points, audit, NullLogger<LeaderboardService>.Instance);
        var gamification = new GamificationService(NullLogger<GamificationService>.Instance, db, leaderboard);
        return (config, points, checkin, gifts, tree, facts, leaderboard, gamification);
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
    // 1. A-08 Leaderboard Cycles & Ranking (UTC001 -> UTC007)
    // ============================================================

    [Fact]
    public async Task UTC001_CreateCycle_WithIsActiveTrue_ShouldDeactivatePreviousActiveCycle()
    {
        using var db = TestDbContextFactory.Create();
        var admin = MakeUser("Admin Eco", UserRole.Admin);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, admin.Id);
        var first = await suite.Leaderboard.CreateCycleAsync(admin.Id, new SaveLeaderboardCycleRequest
        {
            Name = "Tuần 1",
            PeriodType = "Weekly",
            StartsAt = DateTime.UtcNow.AddDays(-7),
            EndsAt = DateTime.UtcNow.AddDays(1),
            IsActive = true
        });
        var second = await suite.Leaderboard.CreateCycleAsync(admin.Id, new SaveLeaderboardCycleRequest
        {
            Name = "Tháng 11",
            PeriodType = "Monthly",
            StartsAt = DateTime.UtcNow.AddDays(-10),
            EndsAt = DateTime.UtcNow.AddDays(20),
            IsActive = true
        });

        var all = await suite.Leaderboard.GetCyclesAsync();
        all.Count(c => c.IsActive).Should().Be(1);
        all.Single(c => c.Id == second.Id).IsActive.Should().BeTrue();
        all.Single(c => c.Id == first.Id).IsActive.Should().BeFalse();
        (await db.AuditLogs.CountAsync(a => a.Action == "LEADERBOARD_CYCLE_CREATE")).Should().Be(2);
    }

    [Fact]
    public async Task UTC002_CreateCycle_WhenEndsAtBeforeStartsAt_ShouldThrowMSGAuth02()
    {
        using var db = TestDbContextFactory.Create();
        var suite = CreateSuite(db, Guid.NewGuid());
        var now = DateTime.UtcNow;

        var ex = await FluentActions.Invoking(() => suite.Leaderboard.CreateCycleAsync(Guid.NewGuid(), new SaveLeaderboardCycleRequest
        {
            Name = "Chu kỳ sai ngày",
            PeriodType = "Weekly",
            StartsAt = now,
            EndsAt = now.AddHours(-1)
        })).Should().ThrowAsync<BusinessRuleException>();

        ex.Which.MessageCode.Should().Be("MSG-AUTH-02");
    }

    [Fact]
    public async Task UTC003_CreateCycle_WhenPeriodTypeOrBonusOrderInvalid_ShouldThrowMSGAuth02()
    {
        using var db = TestDbContextFactory.Create();
        var suite = CreateSuite(db, Guid.NewGuid());

        var exPeriod = await FluentActions.Invoking(() => suite.Leaderboard.CreateCycleAsync(Guid.NewGuid(), new SaveLeaderboardCycleRequest
        {
            Name = "Chu kỳ năm",
            PeriodType = "Yearly",
            StartsAt = DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddDays(365)
        })).Should().ThrowAsync<BusinessRuleException>();
        exPeriod.Which.MessageCode.Should().Be("MSG-AUTH-02");

        var exBonus = await FluentActions.Invoking(() => suite.Leaderboard.CreateCycleAsync(Guid.NewGuid(), new SaveLeaderboardCycleRequest
        {
            Name = "Thưởng sai thứ tự",
            PeriodType = "Monthly",
            StartsAt = DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddDays(30),
            Top1BonusPoints = 100,
            Top2BonusPoints = 200,
            Top3BonusPoints = 50
        })).Should().ThrowAsync<BusinessRuleException>();
        exBonus.Which.MessageCode.Should().Be("MSG-AUTH-02");
    }

    [Fact]
    public async Task UTC004_ActivateCycle_ShouldSwitchActiveCycleAndWriteAuditLog()
    {
        using var db = TestDbContextFactory.Create();
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, admin.Id);
        var week = await suite.Leaderboard.CreateCycleAsync(admin.Id, new SaveLeaderboardCycleRequest
        {
            Name = "Tuần 46",
            PeriodType = "Weekly",
            StartsAt = DateTime.UtcNow.AddDays(-3),
            EndsAt = DateTime.UtcNow.AddDays(4),
            IsActive = true
        });
        var month = await suite.Leaderboard.CreateCycleAsync(admin.Id, new SaveLeaderboardCycleRequest
        {
            Name = "Tháng 11",
            PeriodType = "Monthly",
            StartsAt = DateTime.UtcNow.AddDays(-18),
            EndsAt = DateTime.UtcNow.AddDays(12),
            IsActive = false
        });

        var switched = await suite.Leaderboard.ActivateCycleAsync(admin.Id, month.Id);
        switched.IsActive.Should().BeTrue();
        (await db.LeaderboardCycles.FirstAsync(c => c.Id == week.Id)).IsActive.Should().BeFalse();
        (await db.AuditLogs.AnyAsync(a => a.Action == "LEADERBOARD_CYCLE_ACTIVATE" && a.EntityId == month.Id.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task UTC005_GetLeaderboardView_ShouldRankByPeriodEarnedWithoutRedeemPenalty()
    {
        using var db = TestDbContextFactory.Create();
        var topInPeriod = MakeUser("Lê Công Dân 1");
        var secondInPeriod = MakeUser("Phạm Công Dân 2");
        var recycler = MakeUser("Vựa Thu Gom", UserRole.Recycler);
        db.Users.AddRange(topInPeriod, secondInPeriod, recycler);

        var w1 = new UserPointWallet { Id = Guid.NewGuid(), UserId = topInPeriod.Id, CurrentBalance = 20, TotalEarned = 200, TotalSpent = 180 };
        var w2 = new UserPointWallet { Id = Guid.NewGuid(), UserId = secondInPeriod.Id, CurrentBalance = 500, TotalEarned = 500, TotalSpent = 0 };
        var wRec = new UserPointWallet { Id = Guid.NewGuid(), UserId = recycler.Id, CurrentBalance = 9999, TotalEarned = 9999 };
        db.UserPointWallets.AddRange(w1, w2, wRec);

        db.PointTransactions.AddRange(
            new PointTransaction { WalletId = w1.Id, UserId = topInPeriod.Id, Points = 200, ActionType = PointActionType.HandoverReward, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new PointTransaction { WalletId = w1.Id, UserId = topInPeriod.Id, Points = -180, ActionType = PointActionType.RedeemGift, CreatedAt = DateTime.UtcNow.AddHours(-5) },
            new PointTransaction { WalletId = w2.Id, UserId = secondInPeriod.Id, Points = 80, ActionType = PointActionType.DailyCheckin, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new PointTransaction { WalletId = w2.Id, UserId = secondInPeriod.Id, Points = 500, ActionType = PointActionType.LeaderboardBonus, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new PointTransaction { WalletId = wRec.Id, UserId = recycler.Id, Points = 150, ActionType = PointActionType.HandoverReward, CreatedAt = DateTime.UtcNow.AddDays(-1) }
        );
        db.HandoverRequests.Add(new HandoverRequest
        {
            RequestCode = "HND-AREA-01",
            UserId = topInPeriod.Id,
            RecyclerId = recycler.Id,
            PickupAddress = "12 Nguyễn Trãi, Thạch Thất, Hà Nội",
            ProposedTimeSlot = DateTime.UtcNow,
            Status = HandoverStatus.Completed
        });
        db.LeaderboardCycles.Add(new LeaderboardCycle
        {
            Name = "Tuần hiện tại",
            PeriodType = LeaderboardPeriodType.Weekly,
            StartsAt = DateTime.UtcNow.AddDays(-3),
            EndsAt = DateTime.UtcNow.AddDays(4),
            IsActive = true,
            Status = LeaderboardCycleStatus.Active
        });
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, Guid.NewGuid());
        var view = await suite.Leaderboard.GetLeaderboardViewAsync(10, "Weekly", null, secondInPeriod.Id);

        view.Items.Should().HaveCount(3); // Bảng xếp hạng áp dụng cho cả Người dân (User) và Người thu gom (Recycler)
        view.Items[0].UserId.Should().Be(topInPeriod.Id);
        view.Items[0].Role.Should().Be("User");
        view.Items[0].PeriodPoints.Should().Be(200);
        view.Items[0].SpendablePoints.Should().Be(20);
        view.Items[0].Area.Should().Be("Thạch Thất, Hà Nội");
        view.Items[1].UserId.Should().Be(recycler.Id);
        view.Items[1].Role.Should().Be("Recycler");
        view.Items[1].PeriodPoints.Should().Be(150);
        view.Items[2].UserId.Should().Be(secondInPeriod.Id);
        view.Items[2].PeriodPoints.Should().Be(80); // Không cộng dồn LeaderboardBonus kỳ trước vào điểm thi đua kỳ mới
        view.Items[2].PointsNeededForNextRank.Should().Be(70);
        view.MyEntry!.Rank.Should().Be(3);

        var citizensOnly = await suite.Leaderboard.GetLeaderboardViewAsync(10, "Weekly", null, secondInPeriod.Id, "user");
        citizensOnly.Items.Should().HaveCount(2);
        citizensOnly.Items.Should().OnlyContain(x => x.Role == "User");
    }

    [Fact]
    public async Task UTC006_FinalizeCycle_ShouldCreditTop3BonusesSaveSnapshotAndWriteAuditLog()
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

        var suite = CreateSuite(db, admin.Id);
        var finalized = await suite.Leaderboard.FinalizeCycleAsync(admin.Id, cycle.Id);

        finalized.Status.Should().Be("Finalized");
        finalized.IsActive.Should().BeFalse();
        finalized.Winners.Should().HaveCount(3);
        finalized.Winners[0].BonusPointsAwarded.Should().Be(500);
        finalized.Winners[1].BonusPointsAwarded.Should().Be(300);
        finalized.Winners[2].BonusPointsAwarded.Should().Be(150);

        (await suite.Points.GetWalletAsync(u1.Id)).Balance.Should().Be(800);
        (await suite.Points.GetWalletAsync(u2.Id)).Balance.Should().Be(500);
        (await suite.Points.GetWalletAsync(u3.Id)).Balance.Should().Be(250);
        (await db.AuditLogs.AnyAsync(a => a.Action == "LEADERBOARD_CYCLE_FINALIZE" && a.EntityId == cycle.Id.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task UTC007_FinalizeOrActivateCycle_WhenAlreadyFinalized_ShouldThrowMSGGen01()
    {
        using var db = TestDbContextFactory.Create();
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.Add(admin);
        var cycle = new LeaderboardCycle
        {
            Id = Guid.NewGuid(),
            Name = "Đã chốt",
            PeriodType = LeaderboardPeriodType.Weekly,
            StartsAt = DateTime.UtcNow.AddDays(-14),
            EndsAt = DateTime.UtcNow.AddDays(-7),
            Status = LeaderboardCycleStatus.Finalized,
            IsActive = false
        };
        db.LeaderboardCycles.Add(cycle);
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, admin.Id);
        var exAct = await FluentActions.Invoking(() => suite.Leaderboard.ActivateCycleAsync(admin.Id, cycle.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        exAct.Which.MessageCode.Should().Be("MSG-GEN-01");

        var exFin = await FluentActions.Invoking(() => suite.Leaderboard.FinalizeCycleAsync(admin.Id, cycle.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        exFin.Which.MessageCode.Should().Be("MSG-GEN-01");
    }

    // ============================================================
    // 2. EcoProfile Aggregation & Complaint Clawback (UTC008 -> UTC010)
    // ============================================================

    [Fact]
    public async Task UTC008_GamificationService_GetUserProfile_ShouldAggregateRealDbWalletTreeScansAndStreak()
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

        var suite = CreateSuite(db, citizen.Id, "Citizen");
        var profile = await suite.Gamification.GetUserProfileAsync(citizen.Id);

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
    public async Task UTC009_ComplaintService_CancelDisputedOrder_ShouldClawBackBothEcoProfileAndPointWallet()
    {
        using var db = TestDbContextFactory.Create();
        var citizen = MakeUser("Citizen Khiếu Nại");
        citizen.EcoProfile = new UserEcoProfile { UserId = citizen.Id, CurrentGreenPoints = 120, TotalLifetimePoints = 120, TotalRecycledKg = 8m };
        var recycler = MakeUser("Vựa Thu Gom", UserRole.Recycler);
        recycler.EcoProfile = new UserEcoProfile { UserId = recycler.Id, CurrentGreenPoints = 80, TotalLifetimePoints = 80, TotalRecycledKg = 8m };
        var staff = MakeUser("Staff", UserRole.Staff);
        db.Users.AddRange(citizen, recycler, staff);
        db.UserPointWallets.AddRange(
            new UserPointWallet { UserId = citizen.Id, CurrentBalance = 120, TotalEarned = 120 },
            new UserPointWallet { UserId = recycler.Id, CurrentBalance = 80, TotalEarned = 80 }
        );

        var req = new HandoverRequest { Id = Guid.NewGuid(), RequestCode = "HND-CMP-1", UserId = citizen.Id, RecyclerId = recycler.Id, PickupAddress = "Hà Nội", ProposedTimeSlot = DateTime.UtcNow, Status = HandoverStatus.Disputed };
        var rec = new HandoverReceipt { Id = Guid.NewGuid(), RequestId = req.Id, ReceiptCode = "RCP-CMP-1", UserId = citizen.Id, RecyclerId = recycler.Id, TotalWeightKg = 5m, TotalAmountCash = 35000m, PointsAwarded = 50, ScaleProofPhotoUrl = "[]" };
        var cmp = new TransactionComplaint { Id = Guid.NewGuid(), ComplaintCode = "CMP-TEST-001", RequestId = req.Id, ReceiptId = rec.Id, ComplainantId = citizen.Id, ComplainantRole = "Citizen", RespondentId = recycler.Id, Category = ComplaintCategory.ScaleDiscrepancy, Description = "Cân thiếu khối lượng", Status = ComplaintStatus.Open };
        db.HandoverRequests.Add(req);
        db.HandoverReceipts.Add(rec);
        db.TransactionComplaints.Add(cmp);
        await db.SaveChangesAsync();

        var audit = CreateAudit(db, staff.Id, "Staff");
        var complaintSvc = new ComplaintService(db, Substitute.For<IFileStorageService>(), audit, new ReviewService(db, audit, NullLogger<ReviewService>.Instance), NullLogger<ComplaintService>.Instance);

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
    public async Task UTC010_ComplaintService_CancelDisputedOrder_WhenWalletLowerThanRevoke_ShouldClampToZero()
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

        var audit = CreateAudit(db, staff.Id, "Staff");
        var complaintSvc = new ComplaintService(db, Substitute.For<IFileStorageService>(), audit, new ReviewService(db, audit, NullLogger<ReviewService>.Instance), NullLogger<ComplaintService>.Instance);

        await complaintSvc.ResolveComplaintAsync(staff.Id, cmp.Id, new ResolveComplaintRequest
        {
            ResolutionOutcome = "CancelDisputedOrder",
            ResolutionNote = "Thu hồi không để âm số dư ví điểm."
        });

        var walletAfter = await db.UserPointWallets.FirstAsync(w => w.UserId == citizen.Id);
        walletAfter.CurrentBalance.Should().Be(0); // BR-GAM-03: Số dư ví không bao giờ âm
        var clawTx = await db.PointTransactions.FirstAsync(t => t.UserId == citizen.Id && t.SourceType == "ComplaintClawback");
        clawTx.Points.Should().Be(-15);
    }

    // ============================================================
    // 3. Daily Check-in & Non-Transaction Daily Cap (UTC011 -> UTC013)
    // ============================================================

    [Fact]
    public async Task UTC011_CheckinService_ConsecutiveDayAndMissedDayReset_ShouldComputeStreakAndCreditWallet()
    {
        using var db = TestDbContextFactory.Create();
        var userConsecutive = MakeUser("Chuỗi Liên Tiếp");
        var userMissed = MakeUser("Bỏ Lỡ Hôm Qua");
        db.Users.AddRange(userConsecutive, userMissed);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        db.DailyCheckinLogs.AddRange(
            new DailyCheckinLog { UserId = userConsecutive.Id, CheckinDate = today.AddDays(-1), StreakCount = 3, PointsAwarded = 7 },
            new DailyCheckinLog { UserId = userMissed.Id, CheckinDate = today.AddDays(-3), StreakCount = 5, PointsAwarded = 9 }
        );
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, userConsecutive.Id, "Citizen");
        var statusBefore = await suite.Checkin.GetStatusAsync(userMissed.Id);
        statusBefore.StreakWillReset.Should().BeTrue();
        statusBefore.NextStreak.Should().Be(1);

        var r1 = await suite.Checkin.CheckInAsync(userConsecutive.Id);
        r1.StreakCount.Should().Be(4);
        r1.PointsAwarded.Should().Be(8); // mặc định base=5 + (4-1)*1 = 8
        r1.WalletBalance.Should().Be(8);

        var r2 = await suite.Checkin.CheckInAsync(userMissed.Id);
        r2.StreakCount.Should().Be(1);
        r2.PointsAwarded.Should().Be(5);
    }

    [Fact]
    public async Task UTC012_CheckinService_DuplicateSameDay_ShouldThrowBRGam09MSGGen01()
    {
        using var db = TestDbContextFactory.Create();
        var user = MakeUser("Điểm Danh Đúp");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, user.Id, "Citizen");
        await suite.Checkin.CheckInAsync(user.Id);

        var ex = await FluentActions.Invoking(() => suite.Checkin.CheckInAsync(user.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-GAM-09");
        ex.Which.MessageCode.Should().Be("MSG-GEN-01");
    }

    [Fact]
    public async Task UTC013_CheckinService_WhenDailyCapRemainingTight_ShouldClampOrThrowMSGGam05()
    {
        using var db = TestDbContextFactory.Create();
        var admin = MakeUser("Admin", UserRole.Admin);
        var uClamp = MakeUser("Gần Chạm Trần");
        var uFull = MakeUser("Đã Hết Trần");
        db.Users.AddRange(admin, uClamp, uFull);

        var startUtc = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToDateTime(TimeOnly.MinValue).AddHours(-7);
        startUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        var wClamp = new UserPointWallet { Id = Guid.NewGuid(), UserId = uClamp.Id, CurrentBalance = 47, TotalEarned = 47 };
        var wFull = new UserPointWallet { Id = Guid.NewGuid(), UserId = uFull.Id, CurrentBalance = 50, TotalEarned = 50 };
        db.UserPointWallets.AddRange(wClamp, wFull);

        db.PointTransactions.AddRange(
            new PointTransaction { WalletId = wClamp.Id, UserId = uClamp.Id, Points = 47, ActionType = PointActionType.DailyCheckin, CreatedAt = startUtc.AddMinutes(5) },
            new PointTransaction { WalletId = wFull.Id, UserId = uFull.Id, Points = 50, ActionType = PointActionType.DailyCheckin, CreatedAt = startUtc.AddMinutes(5) }
        );
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, admin.Id);
        // Mặc định DailyNonTransactionCap = 50: uClamp chỉ còn room 3 điểm (mặc dù thưởng cơ bản = 5)
        var clamped = await suite.Checkin.CheckInAsync(uClamp.Id);
        clamped.PointsAwarded.Should().Be(3);
        clamped.LimitedByDailyCap.Should().BeTrue();
        clamped.WalletBalance.Should().Be(50);

        // uFull đã đạt 50/50 -> ném MSG-GAM-05
        var ex = await FluentActions.Invoking(() => suite.Checkin.CheckInAsync(uFull.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        ex.Which.MessageCode.Should().Be("MSG-GAM-05");
    }

    // ============================================================
    // 4. Points, Gift Store & Staff Fulfill/Cancel (UTC014 -> UTC017)
    // ============================================================

    [Fact]
    public async Task UTC014_GiftService_RedeemVoucherAndPhysical_ShouldDebitWalletDecrementStockAndSetStatus()
    {
        using var db = TestDbContextFactory.Create();
        var user = MakeUser("Khách Đổi Quà");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, user.Id, "Citizen");
        await suite.Points.CreditAsync(user.Id, 500, PointActionType.HandoverReward, "Test", "1", "IDEM-U14-1", "Thưởng hoàn tất");

        var voucherGift = await suite.Gifts.CreateAsync(new SaveGiftRequest { Name = "E-Voucher 50k", Type = "Voucher", PointsRequired = 150, StockQuantity = 3 });
        var physicalGift = await suite.Gifts.CreateAsync(new SaveGiftRequest { Name = "Túi Vải Eco", Type = "Physical", PointsRequired = 100, StockQuantity = 5 });

        var rVoucher = await suite.Gifts.RedeemAsync(user.Id, voucherGift.Id, new RedeemGiftRequest { Quantity = 1 });
        rVoucher.Redemption.Status.Should().Be("Redeemed");
        rVoucher.Redemption.VoucherCode.Should().StartWith("ECO-");
        rVoucher.Redemption.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(29));
        rVoucher.WalletBalance.Should().Be(350);

        var rPhysical = await suite.Gifts.RedeemAsync(user.Id, physicalGift.Id, new RedeemGiftRequest { Quantity = 2, DeliveryAddress = "Số 1 Đại Cồ Việt, Hà Nội" });
        rPhysical.Redemption.Status.Should().Be("Pending");
        rPhysical.WalletBalance.Should().Be(150);
        (await suite.Gifts.GetInventoryGiftAsync(voucherGift.Id)).StockQuantity.Should().Be(2);
        (await suite.Gifts.GetInventoryGiftAsync(physicalGift.Id)).StockQuantity.Should().Be(3);
    }

    [Fact]
    public async Task UTC015_GiftService_RedeemAsync_WhenInsufficientBalanceOrOutOfStock_ShouldThrowProperMsgCode()
    {
        using var db = TestDbContextFactory.Create();
        var user = MakeUser("Ít Điểm");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, user.Id, "Citizen");
        await suite.Points.CreditAsync(user.Id, 20, PointActionType.DailyCheckin, "Test", "1", "IDEM-U15", "Checkin");

        var expensive = await suite.Gifts.CreateAsync(new SaveGiftRequest { Name = "Bình Giữ Nhiệt", Type = "Voucher", PointsRequired = 200, StockQuantity = 5 });
        var emptyStock = await suite.Gifts.CreateAsync(new SaveGiftRequest { Name = "Hết Hàng", Type = "Voucher", PointsRequired = 10, StockQuantity = 0 });

        var exPoints = await FluentActions.Invoking(() => suite.Gifts.RedeemAsync(user.Id, expensive.Id, new RedeemGiftRequest { Quantity = 1 }))
            .Should().ThrowAsync<BusinessRuleException>();
        exPoints.Which.MessageCode.Should().Be("MSG-GAM-03");
        (await suite.Gifts.GetInventoryGiftAsync(expensive.Id)).StockQuantity.Should().Be(5); // không trừ tồn kho nếu thiếu điểm

        var exStock = await FluentActions.Invoking(() => suite.Gifts.RedeemAsync(user.Id, emptyStock.Id, new RedeemGiftRequest { Quantity = 1 }))
            .Should().ThrowAsync<BusinessRuleException>();
        exStock.Which.MessageCode.Should().Be("MSG-GAM-04");
    }

    [Fact]
    public async Task UTC016_GiftService_ImportVouchersAndFulfillCancel_ShouldIncreaseStockAndRefundPoints()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Kho", UserRole.Staff);
        var user = MakeUser("Đổi Quà Rồi Hủy");
        db.Users.AddRange(staff, user);
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, staff.Id, "Staff");
        await suite.Points.CreditAsync(user.Id, 300, PointActionType.HandoverReward, "Handover", "H1", "IDEM-S10-1", "Bàn giao");

        var voucher = await suite.Gifts.CreateAsync(new SaveGiftRequest { Name = "Voucher Highland", Type = "Voucher", PointsRequired = 50, StockQuantity = 1 });
        var importRes = await suite.Gifts.ImportVouchersAsync(voucher.Id, new ImportVouchersRequest { Codes = new List<string> { "HL-01", "HL-02", "hl-02", "HL-03" } });
        importRes.ImportedCount.Should().Be(3); // khử trùng lặp không phân biệt hoa thường
        importRes.NewStockQuantity.Should().Be(4);

        var gift = await suite.Gifts.CreateAsync(new SaveGiftRequest { Name = "Áo Thun Xanh", Type = "Physical", PointsRequired = 120, StockQuantity = 4 });
        var redeemed = await suite.Gifts.RedeemAsync(user.Id, gift.Id, new RedeemGiftRequest { Quantity = 1, DeliveryAddress = "Hòa Lạc, Hà Nội" });
        redeemed.WalletBalance.Should().Be(180);

        var cancelled = await suite.Gifts.FulfillAsync(redeemed.Redemption.Id, new FulfillRedemptionRequest { Action = "Cancel", Reason = "Khách đổi ý" });
        cancelled.Status.Should().Be("Cancelled");
        (await suite.Points.GetWalletAsync(user.Id)).Balance.Should().Be(300);
        (await suite.Gifts.GetInventoryGiftAsync(gift.Id)).StockQuantity.Should().Be(4);
    }

    [Fact]
    public async Task UTC017_GiftService_ExpireVouchersAsync_ShouldExpireOnlyVouchersPastExpiresAt()
    {
        using var db = TestDbContextFactory.Create();
        var user = MakeUser("Voucher Boundary");
        var gift = new Gift { Name = "Voucher Test", Type = GiftType.Voucher, PointsRequired = 10, StockQuantity = 5 };
        db.Users.Add(user);
        db.Gifts.Add(gift);

        var now = DateTime.UtcNow;
        db.GiftRedemptions.AddRange(
            new GiftRedemption { Id = Guid.NewGuid(), UserId = user.Id, GiftId = gift.Id, PointsSpent = 10, RedemptionCode = "RDM-EXP-1", VoucherCode = "ECO-1111", Status = RedemptionStatus.Redeemed, ExpiresAt = now.AddSeconds(-1) },
            new GiftRedemption { Id = Guid.NewGuid(), UserId = user.Id, GiftId = gift.Id, PointsSpent = 10, RedemptionCode = "RDM-ACT-1", VoucherCode = "ECO-2222", Status = RedemptionStatus.Redeemed, ExpiresAt = now.AddHours(24) }
        );
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, user.Id, "Citizen");
        var expiredCount = await suite.Gifts.ExpireVouchersAsync();

        expiredCount.Should().Be(1);
        (await db.GiftRedemptions.FirstAsync(r => r.RedemptionCode == "RDM-EXP-1")).Status.Should().Be(RedemptionStatus.Expired);
        (await db.GiftRedemptions.FirstAsync(r => r.RedemptionCode == "RDM-ACT-1")).Status.Should().Be(RedemptionStatus.Redeemed);
    }

    // ============================================================
    // 5. Virtual Tree, Eco Facts & Config (UTC018 -> UTC022)
    // ============================================================

    [Fact]
    public async Task UTC018_VirtualTreeService_WaterAndUpgradeAsync_ShouldUpgradeLevelDebitPointsAndUnlockFacts()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Content", UserRole.Staff);
        var user = MakeUser("Tưới Cây");
        db.Users.AddRange(staff, user);
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, staff.Id, "Staff");
        await suite.Points.CreditAsync(user.Id, 250, PointActionType.HandoverReward, "Handover", "H2", "IDEM-TREE-1", "Bàn giao");
        await suite.Facts.CreateAsync(staff.Id, new SaveEcoFactRequest { Title = "Sự thật Nhựa PET", Content = "Một chai PET mất 450 năm để phân hủy.", UnlockTreeLevel = 2 });

        // Ngưỡng mặc định level 2 = 100 điểm
        var upgraded = await suite.Tree.WaterAndUpgradeAsync(user.Id);
        upgraded.PointsSpent.Should().Be(100);
        upgraded.Tree.Level.Should().Be(2);
        upgraded.Tree.WalletBalance.Should().Be(150);
        upgraded.UnlockedFacts.Should().ContainSingle(f => f.Title == "Sự thật Nhựa PET" && f.UnlockTreeLevel == 2);
    }

    [Fact]
    public async Task UTC019_VirtualTreeService_WhenDailyLimitReachedOrInsufficientPoints_ShouldThrowRuleException()
    {
        using var db = TestDbContextFactory.Create();
        var poorUser = MakeUser("Chưa Đủ Điểm");
        var cappedUser = MakeUser("Vượt Giới Hạn Ngày");
        db.Users.AddRange(poorUser, cappedUser);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        db.VirtualTreeProgresses.Add(new VirtualTreeProgress { UserId = cappedUser.Id, TreeLevel = 1, DailyWaterCount = 3, LastWaterDate = today });
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, poorUser.Id, "Citizen");
        var exPoor = await FluentActions.Invoking(() => suite.Tree.WaterAndUpgradeAsync(poorUser.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        exPoor.Which.MessageCode.Should().Be("MSG-GAM-03");

        var exCap = await FluentActions.Invoking(() => suite.Tree.CareAsync(cappedUser.Id, "water"))
            .Should().ThrowAsync<BusinessRuleException>();
        exCap.Which.MessageCode.Should().Be("MSG-GEN-01");

        // Dù hết lượt Tưới/Bón phân miễn phí (3/3), người dùng đủ Điểm Xanh vẫn nâng cấp được cây ngay
        await suite.Points.CreditAsync(cappedUser.Id, 150, PointActionType.HandoverReward, "Handover", "H-CAP", "IDEM-TREE-CAP", "Bàn giao");
        var upgraded = await suite.Tree.WaterAndUpgradeAsync(cappedUser.Id);
        upgraded.Tree.Level.Should().Be(2);
        upgraded.Tree.DailyWaterCount.Should().Be(3);
    }

    [Fact]
    public async Task UTC020_EcoFactService_DeleteAsync_ShouldSoftDeactivateIfUnlockedAndHardDeleteOtherwise()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff", UserRole.Staff);
        var uLevel2 = MakeUser("Cây Cấp 2");
        db.Users.AddRange(staff, uLevel2);
        db.VirtualTreeProgresses.Add(new VirtualTreeProgress { UserId = uLevel2.Id, TreeLevel = 2 });
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, staff.Id, "Staff");
        var factUnlocked = await suite.Facts.CreateAsync(staff.Id, new SaveEcoFactRequest { Title = "Đã có người đạt cấp 2", Content = "Giữ lịch sử mở khóa.", UnlockTreeLevel = 2 });
        var factUnused = await suite.Facts.CreateAsync(staff.Id, new SaveEcoFactRequest { Title = "Chưa ai đạt cấp 4", Content = "Xóa hẳn khỏi bảng.", UnlockTreeLevel = 4 });

        await suite.Facts.DeleteAsync(factUnlocked.Id);
        await suite.Facts.DeleteAsync(factUnused.Id);

        (await db.EcoFacts.FirstOrDefaultAsync(f => f.Id == factUnlocked.Id))!.Status.Should().Be("INACTIVE");
        (await db.EcoFacts.AnyAsync(f => f.Id == factUnused.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task UTC021_GamificationConfigService_WhenRemovingLevelWithActiveEcoFact_ShouldThrowBRGam06()
    {
        using var db = TestDbContextFactory.Create();
        var admin = MakeUser("Admin", UserRole.Admin);
        var staff = MakeUser("Staff", UserRole.Staff);
        db.Users.AddRange(admin, staff);
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, admin.Id, "Admin");
        await suite.Facts.CreateAsync(staff.Id, new SaveEcoFactRequest { Title = "Fact Cấp 4", Content = "Khóa xóa cấp 4.", UnlockTreeLevel = 4 });

        var shrinkReq = new GamificationConfigDto
        {
            DailyNonTransactionCap = 50, CheckinBasePoints = 5, StreakBonusIncrement = 1, StreakBonusCap = 20,
            DailyTreeCareLimit = 3, VoucherValidityDays = 30, DefaultPointsPerKg = 10,
            TreeLevels = new List<TreeLevelConfigDto>
            {
                new() { Level = 1, Title = "Hạt mầm", RequiredPoints = 0 },
                new() { Level = 2, Title = "Cây non", RequiredPoints = 100 }
            }
        };

        var ex = await FluentActions.Invoking(() => suite.Config.UpdateAsync(admin.Id, shrinkReq))
            .Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-GAM-06");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-02");
    }

    [Fact]
    public void UTC022_GamificationValidators_AndCheckinFormula_ShouldValidatePayloadsCorrectly()
    {
        var giftVal = new SaveGiftRequestValidator();
        giftVal.Validate(new SaveGiftRequest { Name = "", Type = "Invalid", PointsRequired = -1, StockQuantity = 1 }).IsValid.Should().BeFalse();
        giftVal.Validate(new SaveGiftRequest { Name = "Túi vải", Type = "Physical", PointsRequired = 100, StockQuantity = 10 }).IsValid.Should().BeTrue();

        var factVal = new SaveEcoFactRequestValidator();
        factVal.Validate(new SaveEcoFactRequest { Title = "Nhựa PET", Content = "", UnlockTreeLevel = 0 }).IsValid.Should().BeFalse();
        factVal.Validate(new SaveEcoFactRequest { Title = "Nhựa PET", Content = "Chai PET tái chế.", UnlockTreeLevel = 2 }).IsValid.Should().BeTrue();

        var cfgVal = new GamificationConfigDtoValidator();
        var cfg = new GamificationConfigDto
        {
            DailyNonTransactionCap = 50, CheckinBasePoints = 5, StreakBonusIncrement = 1, StreakBonusCap = 20,
            DailyTreeCareLimit = 3, VoucherValidityDays = 30, DefaultPointsPerKg = 10,
            TreeLevels = new List<TreeLevelConfigDto> { new() { Level = 1, Title = "Mầm", RequiredPoints = 0 }, new() { Level = 3, Title = "Nhảy cóc", RequiredPoints = 100 } }
        };
        cfgVal.Validate(cfg).IsValid.Should().BeFalse();
        cfg.TreeLevels[1].Level = 2;
        cfgVal.Validate(cfg).IsValid.Should().BeTrue();

        var cycleVal = new SaveLeaderboardCycleRequestValidator();
        var now = DateTime.UtcNow;
        cycleVal.Validate(new SaveLeaderboardCycleRequest { Name = "Sai", PeriodType = "Daily", StartsAt = now, EndsAt = now.AddHours(-1) }).IsValid.Should().BeFalse();
        cycleVal.Validate(new SaveLeaderboardCycleRequest { Name = "Tuần 1", PeriodType = "Weekly", StartsAt = now, EndsAt = now.AddDays(7), Top1BonusPoints = 500, Top2BonusPoints = 300, Top3BonusPoints = 150 }).IsValid.Should().BeTrue();

        var settings = new GamSettings(50, 5, 2, 10, 3, 30, 10, new Dictionary<string, long>(), Array.Empty<TreeLevelConfigDto>());
        settings.CheckinReward(1).Should().Be(5);
        settings.CheckinReward(3).Should().Be(9);
        settings.CheckinReward(20).Should().Be(15);
    }

    [Fact]
    public async Task UTC023_VirtualTreeService_WaterRevivesWiltedTree_AndRespectsSaturationGuard()
    {
        using var db = TestDbContextFactory.Create();
        var user = MakeUser("Héo Cây");
        db.Users.Add(user);
        // Lưu 75% cách đây 40 giờ -> mất 80% (-2%/giờ) => tụt về 0% (khô héo)
        db.VirtualTreeProgresses.Add(new VirtualTreeProgress
        {
            UserId = user.Id,
            TreeLevel = 2,
            HydrationPercent = 75,
            LastHydrationAt = DateTime.UtcNow.AddHours(-40)
        });
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, user.Id, "Citizen");
        var initial = await suite.Tree.GetAsync(user.Id);
        initial.IsWilted.Should().BeTrue();
        initial.HydrationPercent.Should().Be(0);
        initial.Level.Should().Be(2); // không bị tụt cấp theo BR-GAM-06

        var exFertWilted = await FluentActions.Invoking(() => suite.Tree.CareAsync(user.Id, "fertilize"))
            .Should().ThrowAsync<BusinessRuleException>();
        exFertWilted.Which.MessageCode.Should().Be("MSG-GEN-01");

        // Tưới lần 1: hồi sinh 0% -> 35%, +10 EXP
        var w1 = await suite.Tree.CareAsync(user.Id, "water");
        w1.IsWilted.Should().BeFalse();
        w1.HydrationPercent.Should().Be(35);
        w1.CareExp.Should().Be(10);

        // Tưới lần 2: 35% -> 70%, lần 3: 70% -> 100%
        await suite.Tree.CareAsync(user.Id, "water");
        var w3 = await suite.Tree.CareAsync(user.Id, "water");
        w3.HydrationPercent.Should().Be(100);

        // Thử tưới ở ngưỡng bão hòa >= 95% -> bị chặn ngay (ngay cả khi limit còn)
        db.VirtualTreeProgresses.First(t => t.UserId == user.Id).DailyWaterCount = 0;
        await db.SaveChangesAsync();
        var exSaturated = await FluentActions.Invoking(() => suite.Tree.CareAsync(user.Id, "water"))
            .Should().ThrowAsync<BusinessRuleException>();
        exSaturated.Which.MessageCode.Should().Be("MSG-GEN-01");
    }

    [Fact]
    public async Task UTC024_VirtualTreeService_FertilizeOptimalZoneDryGuardAndHarvest_ShouldAwardWalletPointsUnderCap()
    {
        using var db = TestDbContextFactory.Create();
        var user = MakeUser("Bón Phân & Thu Hoạch");
        db.Users.Add(user);
        db.VirtualTreeProgresses.Add(new VirtualTreeProgress
        {
            UserId = user.Id,
            TreeLevel = 2, // MaxCareExp = 100, HarvestReward = 20
            HydrationPercent = 25,
            LastHydrationAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, user.Id, "Citizen");
        // Độ ẩm 25% < 30% -> chặn bón phân tránh xót rễ
        var exDry = await FluentActions.Invoking(() => suite.Tree.CareAsync(user.Id, "fertilize"))
            .Should().ThrowAsync<BusinessRuleException>();
        exDry.Which.MessageCode.Should().Be("MSG-GEN-01");

        // Tưới +35% -> 60% (vào vùng ẩm vàng 60–90%) và +10 EXP
        var watered = await suite.Tree.CareAsync(user.Id, "water");
        watered.HydrationPercent.Should().Be(60);
        watered.InOptimalHydrationZone.Should().BeTrue();
        watered.CareExp.Should().Be(10);

        // Bón ở ẩm 60% -> +35 EXP (45/100) & -10% ẩm (50%)
        var f1 = await suite.Tree.CareAsync(user.Id, "fertilize");
        f1.HydrationPercent.Should().Be(50);
        f1.InOptimalHydrationZone.Should().BeFalse();
        f1.CareExp.Should().Be(45);

        // Tưới tiếp lên 85% (vùng vàng, +10 EXP => 55) -> Bón tiếp (+35 EXP => 90) -> Tưới (+10 EXP => 100 = HarvestReady!)
        await suite.Tree.CareAsync(user.Id, "water");
        await suite.Tree.CareAsync(user.Id, "fertilize");
        var ready = await suite.Tree.CareAsync(user.Id, "water");
        ready.CareExp.Should().Be(100);
        ready.HarvestReady.Should().BeTrue();

        var harvested = await suite.Tree.HarvestAsync(user.Id);
        harvested.PointsAwarded.Should().Be(20);
        harvested.LimitedByDailyCap.Should().BeFalse();
        harvested.Tree.CareExp.Should().Be(0);
        harvested.Tree.HarvestReady.Should().BeFalse();
        harvested.Tree.TotalHarvested.Should().Be(1);
        harvested.Tree.WalletBalance.Should().Be(20);
    }

    [Fact]
    public async Task UTC025_VirtualTreeService_EcoActionsAwardFertilizerPacks_AndBypassFreeDailyLimit()
    {
        using var db = TestDbContextFactory.Create();
        var citizen = MakeUser("Citizen Eco Pack");
        var recycler = MakeUser("Recycler Partner", UserRole.Recycler);
        var category = new WasteCategory { Code = "pet", Name = "Nhựa PET", StandardBinColor = "Cam" };
        db.Users.AddRange(citizen, recycler);
        db.WasteCategories.Add(category);

        var req = new HandoverRequest
        {
            RequestCode = "HND-FERT-1",
            UserId = citizen.Id,
            RecyclerId = recycler.Id,
            PickupAddress = "123 Lê Lợi",
            ProposedTimeSlot = DateTime.UtcNow,
            Status = HandoverStatus.Completed
        };
        var item = new HandoverRequestItem
        {
            RequestId = req.Id,
            WasteCategoryId = category.Id,
            EstimatedWeightKg = 2m,
            ActualWeightKg = 2m
        };
        var receipt = new HandoverReceipt
        {
            RequestId = req.Id,
            UserId = citizen.Id,
            RecyclerId = recycler.Id,
            ReceiptCode = "RCP-FERT-1",
            TotalWeightKg = 2m,
            TotalAmountCash = 10000m,
            ScaleProofPhotoUrl = "[]"
        };
        db.HandoverRequests.Add(req);
        db.HandoverRequestItems.Add(item);
        db.HandoverReceipts.Add(receipt);
        await db.SaveChangesAsync();

        var suite = CreateSuite(db, citizen.Id, "Citizen");
        await suite.Points.AwardHandoverPointsAsync(receipt.Id);
        await suite.Points.AwardHandoverPointsAsync(receipt.Id); // Idempotency: không cộng túi trùng lần 2

        var afterHandover = await suite.Tree.GetAsync(citizen.Id);
        afterHandover.FertilizerPacks.Should().Be(2);

        // Dùng hết 2 lượt bón phân miễn phí trong ngày, lượt thứ 3 tự động dùng 1 túi phân dự trữ
        await suite.Tree.CareAsync(citizen.Id, "fertilize");
        await suite.Tree.CareAsync(citizen.Id, "fertilize");
        var usedPack = await suite.Tree.CareAsync(citizen.Id, "fertilize");
        usedPack.DailyFertilizeCount.Should().Be(2);
        usedPack.TotalFertilized.Should().Be(3);
        usedPack.FertilizerPacks.Should().Be(1);
    }
}
