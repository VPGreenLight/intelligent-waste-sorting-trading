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

namespace Ecolink.UnitTests.Module11_ReviewsComplaints;

/// <summary>
/// Bộ kiểm thử đơn vị toàn diện cho Module M-11 / FE-10 (Module 12 trong bộ Report-5.1):
/// Rating, Review &amp; Complaint Resolution (U-10, U-11, S-05).
/// Chuẩn hóa 22 test cases (UTC001 -&gt; UTC022: 10 Normal, 8 Abnormal, 4 Boundary).
/// </summary>
[Trait("Module", "Module11_ReviewsComplaints")]
public class ReviewComplaintTests
{
    private static (
        ReviewService ReviewSvc,
        ComplaintService ComplaintSvc,
        IFileStorageService StorageMock)
        CreateServices(ApplicationDbContext db, Guid actorUserId, string role = "Citizen")
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns((Guid?)actorUserId);
        currentUser.Role.Returns(role);
        currentUser.IsAuthenticated.Returns(true);
        currentUser.IpAddress.Returns("127.0.0.1");
        currentUser.UserAgent.Returns("EcoLink-UnitTest/1.0");

        var storageMock = Substitute.For<IFileStorageService>();
        storageMock.UploadRecyclerDocumentAsync(
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new RecyclerDocumentUploadResponseDto
            {
                FileUrl = $"https://cdn.ecolink.test/{ci.ArgAt<string>(3)}/{ci.ArgAt<string>(1)}",
                ObjectKey = $"{ci.ArgAt<string>(3)}/{ci.ArgAt<string>(1)}",
                ContentType = ci.ArgAt<string>(2),
                SizeBytes = 256
            }));

        var auditSvc = new AuditLogService(db, currentUser);
        var reviewSvc = new ReviewService(db, auditSvc, NullLogger<ReviewService>.Instance);
        var complaintSvc = new ComplaintService(
            db,
            storageMock,
            auditSvc,
            reviewSvc,
            NullLogger<ComplaintService>.Instance);

        return (reviewSvc, complaintSvc, storageMock);
    }

    private sealed record SeedContext(
        User Citizen,
        User RecyclerUser,
        RecyclerProfile RecyclerProfile,
        User Staff,
        WasteCategory PlasticCategory,
        HandoverRequest Handover,
        HandoverReceipt? Receipt);

    private static async Task<SeedContext> SeedHandoverAsync(
        ApplicationDbContext db,
        HandoverStatus status = HandoverStatus.Completed,
        int citizenPoints = 100,
        long receiptPoints = 50,
        DateTime? completedAt = null,
        bool withReceipt = true)
    {
        var now = completedAt ?? DateTime.UtcNow;
        var citizen = new User
        {
            Id = Guid.NewGuid(),
            Username = $"citizen_{Guid.NewGuid():N}"[..16],
            Email = $"citizen_{Guid.NewGuid():N}@ecolink.vn",
            FullName = "Nguyễn Văn An",
            Role = UserRole.Citizen,
            Status = UserStatus.Active,
            EcoProfile = new UserEcoProfile
            {
                Id = Guid.NewGuid(),
                CurrentGreenPoints = citizenPoints,
                TotalLifetimePoints = citizenPoints + 50,
                TotalRecycledKg = 10m
            }
        };

        var recyclerUser = new User
        {
            Id = Guid.NewGuid(),
            Username = $"recycler_{Guid.NewGuid():N}"[..16],
            Email = $"recycler_{Guid.NewGuid():N}@ecolink.vn",
            FullName = "Trần Thu Gom",
            Role = UserRole.Recycler,
            Status = UserStatus.Active,
            EcoProfile = new UserEcoProfile
            {
                Id = Guid.NewGuid(),
                CurrentGreenPoints = 200,
                TotalLifetimePoints = 200,
                TotalRecycledKg = 20m
            }
        };

        var recyclerProfile = new RecyclerProfile
        {
            UserId = recyclerUser.Id,
            User = recyclerUser,
            FacilityName = "Vựa Phế Liệu Xanh Hòa Lạc",
            BusinessAddress = "Thạch Hòa, Thạch Thất, Hà Nội",
            Latitude = 21.01m,
            Longitude = 105.53m,
            ContactPhone = "0901234567",
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        };

        var staff = new User
        {
            Id = Guid.NewGuid(),
            Username = $"staff_{Guid.NewGuid():N}"[..16],
            Email = $"staff_{Guid.NewGuid():N}@ecolink.vn",
            FullName = "Nhân Viên Trọng Tài",
            Role = UserRole.Staff,
            Status = UserStatus.Active
        };

        var plastic = await db.WasteCategories.FirstOrDefaultAsync(c => c.Code == "plastic")
            ?? new WasteCategory
            {
                Id = Guid.NewGuid(),
                Code = "plastic",
                Name = "Nhựa tái chế (Plastic)",
                StandardBinColor = "Cam",
                Status = "Active",
                IsRecyclable = true
            };

        if (!await db.WasteCategories.AnyAsync(c => c.Id == plastic.Id))
        {
            db.WasteCategories.Add(plastic);
        }

        var handover = new HandoverRequest
        {
            Id = Guid.NewGuid(),
            RequestCode = $"HND-{Random.Shared.Next(100000, 999999)}",
            UserId = citizen.Id,
            RecyclerId = recyclerUser.Id,
            PickupAddress = "Khu CNC Hòa Lạc",
            ProposedTimeSlot = now.AddHours(-2),
            ConfirmedTimeSlot = now.AddHours(-2),
            ActualHandoverAt = status == HandoverStatus.Completed ? now : null,
            Status = status,
            CreatedAt = now.AddHours(-3),
            UpdatedAt = now,
            Items = new List<HandoverRequestItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    WasteCategoryId = plastic.Id,
                    EstimatedWeightKg = 5m,
                    ActualWeightKg = 5m,
                    AgreedPricePerKg = 7000m
                }
            }
        };

        HandoverReceipt? receipt = null;
        if (withReceipt)
        {
            receipt = new HandoverReceipt
            {
                Id = Guid.NewGuid(),
                RequestId = handover.Id,
                ReceiptCode = $"RCP-{DateTime.UtcNow:yyyy}-{Random.Shared.Next(1000, 9999)}",
                UserId = citizen.Id,
                RecyclerId = recyclerUser.Id,
                TotalWeightKg = 5m,
                TotalAmountCash = 35000m,
                PointsAwarded = receiptPoints,
                ScaleProofPhotoUrl = "[\"https://cdn.ecolink.test/scale/proof-1.jpg\"]",
                CreatedAt = now.AddMinutes(-15),
                ConfirmedByUserAt = status == HandoverStatus.Completed ? now : null
            };
            db.HandoverReceipts.Add(receipt);
        }

        db.Users.AddRange(citizen, recyclerUser, staff);
        db.RecyclerProfiles.Add(recyclerProfile);
        db.HandoverRequests.Add(handover);
        await db.SaveChangesAsync();

        return new SeedContext(citizen, recyclerUser, recyclerProfile, staff, plastic, handover, receipt);
    }

    // ==================== 10 NORMAL CASES ====================

    [Fact]
    public async Task UTC001_CreateReviewAsync_CompletedHandoverWithin14Days_CreatesReviewAndUpdatesRecyclerAverage_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed);
        var (reviewSvc, _, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");

        var dto = await reviewSvc.CreateReviewAsync(ctx.Citizen.Id, ctx.Handover.Id, new CreateReviewRequest
        {
            RatingStars = 5,
            Comment = "Cân chuẩn, thu gom đúng giờ và rất nhiệt tình."
        });

        dto.RatingStars.Should().Be(5);
        dto.Status.Should().Be("Visible");
        dto.UpdatedRecyclerAverageRating.Should().Be(5.00m);
        dto.UpdatedRecyclerTotalReviews.Should().Be(1);

        var profile = await db.RecyclerProfiles.FirstAsync(p => p.UserId == ctx.RecyclerUser.Id);
        profile.AverageRating.Should().Be(5.00m);
        profile.TotalReviews.Should().Be(1);
    }

    [Fact]
    public async Task UTC002_CreateReviewAsync_MultipleReviews_CalculatesExactAverageAndStarDistribution_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx1 = await SeedHandoverAsync(db, HandoverStatus.Completed);
        var (reviewSvc1, _, _) = CreateServices(db, ctx1.Citizen.Id, "Citizen");
        await reviewSvc1.CreateReviewAsync(ctx1.Citizen.Id, ctx1.Handover.Id, new CreateReviewRequest { RatingStars = 5 });

        var secondCitizen = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_two",
            Email = "citizentwo@ecolink.vn",
            FullName = "Lê Thị Bình",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        var h2 = new HandoverRequest
        {
            Id = Guid.NewGuid(),
            RequestCode = $"HND-{Random.Shared.Next(100000, 999999)}",
            UserId = secondCitizen.Id,
            RecyclerId = ctx1.RecyclerUser.Id,
            PickupAddress = "Hòa Lạc",
            ProposedTimeSlot = DateTime.UtcNow.AddHours(-2),
            ActualHandoverAt = DateTime.UtcNow,
            Status = HandoverStatus.Completed
        };
        db.Users.Add(secondCitizen);
        db.HandoverRequests.Add(h2);
        await db.SaveChangesAsync();

        var (reviewSvc2, _, _) = CreateServices(db, secondCitizen.Id, "Citizen");
        var secondDto = await reviewSvc2.CreateReviewAsync(secondCitizen.Id, h2.Id, new CreateReviewRequest
        {
            RatingStars = 4,
            Comment = "Ổn định, nhanh gọn."
        });

        secondDto.UpdatedRecyclerAverageRating.Should().Be(4.50m);
        secondDto.UpdatedRecyclerTotalReviews.Should().Be(2);

        var summary = await reviewSvc2.GetRecyclerReviewsAsync(ctx1.RecyclerUser.Id, new RecyclerReviewFilterRequest());
        summary.AverageRating.Should().Be(4.50m);
        summary.TotalReviews.Should().Be(2);
        summary.StarDistribution.FiveStars.Should().Be(1);
        summary.StarDistribution.FourStars.Should().Be(1);
        summary.Reviews.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTC003_DeleteMyReviewAsync_AuthorSelfDeletes_SetsDeletedAndRecalculatesRating_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed);
        var (reviewSvc, _, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");
        var created = await reviewSvc.CreateReviewAsync(ctx.Citizen.Id, ctx.Handover.Id, new CreateReviewRequest
        {
            RatingStars = 3,
            Comment = "Cần cải thiện giờ hẹn."
        });

        var deleted = await reviewSvc.DeleteMyReviewAsync(ctx.Citizen.Id, created.ReviewId);

        deleted.Status.Should().Be("Deleted");
        deleted.UpdatedRecyclerAverageRating.Should().Be(0m);
        deleted.UpdatedRecyclerTotalReviews.Should().Be(0);

        var mine = await reviewSvc.GetMyReviewsAsync(ctx.Citizen.Id);
        mine.TotalRecords.Should().Be(0);
    }

    [Fact]
    public async Task UTC004_ReportAndModerateReviewAsync_StaffHidesReportedReview_RecalculatesRatingAndLogsAudit_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed);
        var (reviewSvc, _, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");
        var created = await reviewSvc.CreateReviewAsync(ctx.Citizen.Id, ctx.Handover.Id, new CreateReviewRequest
        {
            RatingStars = 1,
            Comment = "Nội dung spam quảng cáo số điện thoại lạ."
        });

        var (reporterSvc, _, _) = CreateServices(db, ctx.RecyclerUser.Id, "Recycler");
        var reported = await reporterSvc.ReportAbusiveReviewAsync(ctx.RecyclerUser.Id, created.ReviewId, new ReportReviewRequest
        {
            Reason = "Đánh giá spam số điện thoại cá nhân trái quy định."
        });
        reported.Status.Should().Be("Reported");
        reported.ReportCount.Should().Be(1);

        var (staffSvc, _, _) = CreateServices(db, ctx.Staff.Id, "Staff");
        var hidden = await staffSvc.ModerateReviewAsync(ctx.Staff.Id, created.ReviewId, new ModerateReviewRequest
        {
            Action = "Hide",
            ModerationReason = "Vi phạm tiêu chuẩn cộng đồng về quảng cáo spam."
        });

        hidden.Status.Should().Be("Hidden");
        hidden.UpdatedRecyclerAverageRating.Should().Be(0m);
        hidden.UpdatedRecyclerTotalReviews.Should().Be(0);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == "MODERATE_REVIEW" && a.EntityId == created.ReviewId.ToString());
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task UTC005_ModerateReviewAsync_StaffRestoresHiddenReview_IncludesBackInAverage_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed);
        var (reviewSvc, _, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");
        var created = await reviewSvc.CreateReviewAsync(ctx.Citizen.Id, ctx.Handover.Id, new CreateReviewRequest { RatingStars = 5 });

        var (staffSvc, _, _) = CreateServices(db, ctx.Staff.Id, "Staff");
        await staffSvc.ModerateReviewAsync(ctx.Staff.Id, created.ReviewId, new ModerateReviewRequest
        {
            Action = "Hide",
            ModerationReason = "Tạm ẩn kiểm tra nội dung khiếu nại."
        });

        var restored = await staffSvc.ModerateReviewAsync(ctx.Staff.Id, created.ReviewId, new ModerateReviewRequest
        {
            Action = "Restore",
            ModerationReason = "Nội dung hợp lệ sau xác minh đối chiếu."
        });

        restored.Status.Should().Be("Visible");
        restored.UpdatedRecyclerAverageRating.Should().Be(5.00m);
        restored.UpdatedRecyclerTotalReviews.Should().Be(1);
    }

    [Fact]
    public async Task UTC006_SubmitComplaintAsync_CitizenSubmitsScaleDiscrepancy_CreatesTicketAndMarksDisputed_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.AwaitingConfirmation, receiptPoints: 0);
        var (_, complaintSvc, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");

        var complaint = await complaintSvc.SubmitComplaintAsync(ctx.Citizen.Id, new SubmitComplaintRequest
        {
            HandoverRequestId = ctx.Handover.Id,
            Category = "ScaleDiscrepancy",
            Description = "Đồng hồ cân thực tế 15kg nhưng biên nhận chỉ ghi nhận 10kg.",
            EvidencePhotoUrls = new List<string> { "https://cdn.ecolink.test/complaint-evidences/scale.jpg" }
        });

        complaint.ComplaintCode.Should().StartWith("CMP-");
        complaint.ComplainantRole.Should().Be("Citizen");
        complaint.RespondentId.Should().Be(ctx.RecyclerUser.Id);
        complaint.Category.Should().Be("ScaleDiscrepancy");
        complaint.Status.Should().Be("Open");
        complaint.EvidencePhotoUrls.Should().ContainSingle();

        var refreshedHandover = await db.HandoverRequests.FirstAsync(h => h.Id == ctx.Handover.Id);
        refreshedHandover.Status.Should().Be(HandoverStatus.Disputed);
    }

    [Fact]
    public async Task UTC007_SubmitComplaintAsync_BilateralRecyclerSubmitsNoShow_SetsComplainantRoleRecycler_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Accepted, receiptPoints: 0, withReceipt: false);
        var (_, complaintSvc, _) = CreateServices(db, ctx.RecyclerUser.Id, "Recycler");

        var complaint = await complaintSvc.SubmitComplaintAsync(ctx.RecyclerUser.Id, new SubmitComplaintRequest
        {
            HandoverRequestId = ctx.Handover.Id,
            Category = "NoShow",
            Description = "Xe thu gom đã đến đúng điểm hẹn nhưng người dân khóa cửa không liên lạc được."
        });

        complaint.ComplainantRole.Should().Be("Recycler");
        complaint.ComplainantId.Should().Be(ctx.RecyclerUser.Id);
        complaint.RespondentId.Should().Be(ctx.Citizen.Id);
        complaint.Category.Should().Be("NoShow");
        complaint.Status.Should().Be("Open");
    }

    [Fact]
    public async Task UTC008_HandoverReceiptDisputeAsync_BridgesAutomaticallyToStaffComplaintQueue_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.AwaitingConfirmation, receiptPoints: 0);
        var receiptSvc = new HandoverReceiptService(
            db,
            new GamificationService(NullLogger<GamificationService>.Instance),
            NullLogger<HandoverReceiptService>.Instance);

        var receiptDto = await receiptSvc.DisputeAsync(ctx.Citizen.Id, ctx.Handover.Id, new DisputeReceiptRequest
        {
            Reason = "Sai lệch số cân kim loại thực tế trên biên nhận điện tử.",
            EvidencePhotoUrls = new List<string> { "https://cdn.ecolink.test/evidence/dispute-1.jpg" }
        });

        receiptDto.RequestStatus.Should().Be("Disputed");

        var ticket = await db.TransactionComplaints.FirstOrDefaultAsync(c => c.RequestId == ctx.Handover.Id);
        ticket.Should().NotBeNull();
        ticket!.Category.Should().Be(ComplaintCategory.ScaleDiscrepancy);
        ticket.Status.Should().Be(ComplaintStatus.Open);
        ticket.ComplainantId.Should().Be(ctx.Citizen.Id);
        ticket.RespondentId.Should().Be(ctx.RecyclerUser.Id);
    }

    [Fact]
    public async Task UTC009_GetStaffComplaintDossierAsync_ClaimsTicketAndAggregatesReceiptAndChatHistory_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.AwaitingConfirmation, receiptPoints: 0);

        db.TradeOrders.Add(new TradeOrder
        {
            Id = ctx.Handover.Id,
            UserId = ctx.Citizen.Id,
            PartnerId = ctx.RecyclerUser.Id,
            RecyclerUserId = ctx.RecyclerUser.Id,
            Status = OrderStatus.Disputed,
            ScheduledAt = DateTime.UtcNow
        });
        db.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            TradeOrderId = ctx.Handover.Id,
            SenderUserId = ctx.Citizen.Id,
            SenderRole = "Citizen",
            MessageType = ChatMessageType.Text,
            MessageContent = "Anh cân giúp em phần carton riêng nhé.",
            SentAt = DateTime.UtcNow.AddMinutes(-20)
        });
        await db.SaveChangesAsync();

        var (_, userCmpSvc, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");
        var created = await userCmpSvc.SubmitComplaintAsync(ctx.Citizen.Id, new SubmitComplaintRequest
        {
            HandoverRequestId = ctx.Handover.Id,
            Category = "ScaleDiscrepancy",
            Description = "Số cân thực tế chênh lệch 3kg so với thỏa thuận ban đầu."
        });

        var (_, staffCmpSvc, _) = CreateServices(db, ctx.Staff.Id, "Staff");
        var dossier = await staffCmpSvc.GetStaffComplaintDossierAsync(ctx.Staff.Id, created.ComplaintId, claimTicket: true);

        dossier.Complaint.Status.Should().Be("Investigating");
        dossier.Complaint.AssignedStaffId.Should().Be(ctx.Staff.Id);
        dossier.Receipt.Should().NotBeNull();
        dossier.Receipt!.ScaleProofPhotoUrls.Should().ContainSingle();
        dossier.NegotiationMessages.Should().HaveCount(2);
        dossier.NegotiationMessages.Should().Contain(m => m.Content.Contains("carton"));
        dossier.NegotiationMessages.Should().Contain(m => m.MessageType == "System");
    }

    [Fact]
    public async Task UTC010_ResolveComplaintAsync_CancelDisputedOrderWithWarning_RevokesPointsWarnsAndAudits_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed, citizenPoints: 120, receiptPoints: 50);

        var (_, userCmpSvc, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");
        var created = await userCmpSvc.SubmitComplaintAsync(ctx.Citizen.Id, new SubmitComplaintRequest
        {
            HandoverRequestId = ctx.Handover.Id,
            Category = "PaymentIssue",
            Description = "Cơ sở thu mua trả thiếu 50,000đ so với bảng giá niêm yết trên biên nhận."
        });

        var (_, staffCmpSvc, _) = CreateServices(db, ctx.Staff.Id, "Staff");
        var resolved = await staffCmpSvc.ResolveComplaintAsync(ctx.Staff.Id, created.ComplaintId, new ResolveComplaintRequest
        {
            ResolutionOutcome = "CancelDisputedOrder",
            ResolutionNote = "Xác nhận sai phạm bảng giá và hủy đơn giao dịch, thu hồi điểm và cảnh cáo đối tác.",
            IssueWarningToRespondent = true,
            WarningReason = "Trả sai đơn giá niêm yết trên hệ thống EcoLink."
        });

        resolved.Status.Should().Be("Resolved");
        resolved.ResolutionOutcome.Should().Be("CancelDisputedOrder");
        resolved.PointsRevoked.Should().BeTrue();
        resolved.WarningLogged.Should().BeTrue();

        var citizenEco = await db.UserEcoProfiles.FirstAsync(e => e.UserId == ctx.Citizen.Id);
        citizenEco.CurrentGreenPoints.Should().Be(70);

        var recycler = await db.Users.FirstAsync(u => u.Id == ctx.RecyclerUser.Id);
        recycler.WarningCount.Should().Be(1);

        var modLog = await db.AccountModerationLogs.FirstOrDefaultAsync(m => m.RelatedComplaintId == created.ComplaintId);
        modLog.Should().NotBeNull();
        modLog!.ActionType.Should().Be("WARN");

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == "RESOLVE_COMPLAINT" && a.EntityId == created.ComplaintId.ToString());
        audit.Should().NotBeNull();
    }

    // ==================== 8 ABNORMAL CASES ====================

    [Fact]
    public async Task UTC011_CreateReviewAsync_NonCompletedHandover_ThrowsBusinessRuleBrRvw01_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Accepted, withReceipt: false);
        var (reviewSvc, _, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");

        var act = () => reviewSvc.CreateReviewAsync(ctx.Citizen.Id, ctx.Handover.Id, new CreateReviewRequest
        {
            RatingStars = 5
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-RVW-01");
        ex.Which.MessageCode.Should().Be("MSG-RVW-02");
    }

    [Fact]
    public async Task UTC012_CreateReviewAsync_DuplicateReviewSameHandover_ThrowsConflictException_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed);
        var (reviewSvc, _, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");
        await reviewSvc.CreateReviewAsync(ctx.Citizen.Id, ctx.Handover.Id, new CreateReviewRequest { RatingStars = 5 });

        var act = () => reviewSvc.CreateReviewAsync(ctx.Citizen.Id, ctx.Handover.Id, new CreateReviewRequest { RatingStars = 4 });

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.MessageCode.Should().Be("MSG-RVW-02");
    }

    [Fact]
    public async Task UTC013_DeleteMyReviewAsync_CalledByRatedRecycler_ThrowsForbiddenAccess_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed);
        var (reviewSvc, _, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");
        var created = await reviewSvc.CreateReviewAsync(ctx.Citizen.Id, ctx.Handover.Id, new CreateReviewRequest { RatingStars = 2 });

        var (recyclerSvc, _, _) = CreateServices(db, ctx.RecyclerUser.Id, "Recycler");
        var act = () => recyclerSvc.DeleteMyReviewAsync(ctx.RecyclerUser.Id, created.ReviewId);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task UTC014_SubmitComplaintAsync_OutsiderNotInHandover_ThrowsForbiddenAccess_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed);
        var (_, cmpSvc, _) = CreateServices(db, ctx.Staff.Id, "Citizen");

        var act = () => cmpSvc.SubmitComplaintAsync(Guid.NewGuid(), new SubmitComplaintRequest
        {
            HandoverRequestId = ctx.Handover.Id,
            Category = "Other",
            Description = "Người ngoài cố tình gửi khiếu nại giả mạo cho đơn hàng khác."
        });

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task UTC015_SubmitComplaintAsync_SecondActiveComplaintSameRequest_ThrowsConflictMsgDsp04_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed);
        var (_, citizenCmpSvc, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");
        await citizenCmpSvc.SubmitComplaintAsync(ctx.Citizen.Id, new SubmitComplaintRequest
        {
            HandoverRequestId = ctx.Handover.Id,
            Category = "ScaleDiscrepancy",
            Description = "Khiếu nại thứ nhất hợp lệ về sai lệch cân nặng thực tế."
        });

        var (_, recyclerCmpSvc, _) = CreateServices(db, ctx.RecyclerUser.Id, "Recycler");
        var act = () => recyclerCmpSvc.SubmitComplaintAsync(ctx.RecyclerUser.Id, new SubmitComplaintRequest
        {
            HandoverRequestId = ctx.Handover.Id,
            Category = "Misconduct",
            Description = "Cơ sở thu mua cố tình mở phiếu thứ hai khi phiếu một còn Open."
        });

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.MessageCode.Should().Be("MSG-DSP-04");
    }

    [Fact]
    public async Task UTC016_SubmitComplaintAsync_Past7DaysWindow_ThrowsBusinessRuleMsgDsp03_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed, completedAt: DateTime.UtcNow.AddDays(-8));
        var (_, cmpSvc, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");

        var act = () => cmpSvc.SubmitComplaintAsync(ctx.Citizen.Id, new SubmitComplaintRequest
        {
            HandoverRequestId = ctx.Handover.Id,
            Category = "ScaleDiscrepancy",
            Description = "Khiếu nại gửi sau 8 ngày kể từ khi đơn đã hoàn tất xác nhận."
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-DSP-01");
        ex.Which.MessageCode.Should().Be("MSG-DSP-03");
    }

    [Fact]
    public async Task UTC017_ResolveComplaintAsync_AlreadyResolvedComplaint_ThrowsConflictException_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed);
        var (_, userCmpSvc, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");
        var created = await userCmpSvc.SubmitComplaintAsync(ctx.Citizen.Id, new SubmitComplaintRequest
        {
            HandoverRequestId = ctx.Handover.Id,
            Category = "Other",
            Description = "Yêu cầu kiểm tra lại thông tin hóa đơn cân tại điểm thu mua."
        });

        var (_, staffCmpSvc, _) = CreateServices(db, ctx.Staff.Id, "Staff");
        await staffCmpSvc.ResolveComplaintAsync(ctx.Staff.Id, created.ComplaintId, new ResolveComplaintRequest
        {
            ResolutionOutcome = "Dismissed",
            ResolutionNote = "Đã kiểm tra chứng cứ đầy đủ, bác bỏ yêu cầu sai lệch."
        });

        var act = () => staffCmpSvc.ResolveComplaintAsync(ctx.Staff.Id, created.ComplaintId, new ResolveComplaintRequest
        {
            ResolutionOutcome = "UpholdComplainant",
            ResolutionNote = "Thử thay đổi phán quyết sau khi hồ sơ đã đóng."
        });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UTC018_CreateReviewAsync_Past14DaysWindow_ThrowsBusinessRuleMsgRvw02_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var ctx = await SeedHandoverAsync(db, HandoverStatus.Completed, completedAt: DateTime.UtcNow.AddDays(-15));
        var (reviewSvc, _, _) = CreateServices(db, ctx.Citizen.Id, "Citizen");

        var act = () => reviewSvc.CreateReviewAsync(ctx.Citizen.Id, ctx.Handover.Id, new CreateReviewRequest
        {
            RatingStars = 5
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-RVW-01");
        ex.Which.MessageCode.Should().Be("MSG-RVW-02");
    }

    // ==================== 4 BOUNDARY CASES ====================

    [Fact]
    public void UTC019_CreateReviewRequestValidator_RatingStarsAndCommentLengthBoundaries_Boundary()
    {
        var validator = new CreateReviewRequestValidator();

        validator.Validate(new CreateReviewRequest { RatingStars = 1, Comment = new string('a', 1000) }).IsValid.Should().BeTrue();
        validator.Validate(new CreateReviewRequest { RatingStars = 5, Comment = null }).IsValid.Should().BeTrue();

        validator.Validate(new CreateReviewRequest { RatingStars = 0 }).IsValid.Should().BeFalse();
        validator.Validate(new CreateReviewRequest { RatingStars = 6 }).IsValid.Should().BeFalse();
        validator.Validate(new CreateReviewRequest { RatingStars = 5, Comment = new string('a', 1001) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UTC020_SubmitComplaintRequestValidator_Description15AndMax5EvidenceUrlsBoundaries_Boundary()
    {
        var validator = new SubmitComplaintRequestValidator();
        var validUrls5 = Enumerable.Range(1, 5).Select(i => $"https://cdn.ecolink.test/e/{i}.jpg").ToList();
        var invalidUrls6 = Enumerable.Range(1, 6).Select(i => $"https://cdn.ecolink.test/e/{i}.jpg").ToList();

        validator.Validate(new SubmitComplaintRequest
        {
            HandoverRequestId = Guid.NewGuid(),
            Category = "ScaleDiscrepancy",
            Description = new string('x', 15),
            EvidencePhotoUrls = validUrls5
        }).IsValid.Should().BeTrue();

        validator.Validate(new SubmitComplaintRequest
        {
            HandoverRequestId = Guid.NewGuid(),
            Category = "ScaleDiscrepancy",
            Description = new string('x', 14),
            EvidencePhotoUrls = validUrls5
        }).IsValid.Should().BeFalse();

        validator.Validate(new SubmitComplaintRequest
        {
            HandoverRequestId = Guid.NewGuid(),
            Category = "ScaleDiscrepancy",
            Description = new string('x', 20),
            EvidencePhotoUrls = invalidUrls6
        }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UTC021_ResolveComplaintRequestValidator_ResolutionNote10CharBoundary_Boundary()
    {
        var validator = new ResolveComplaintRequestValidator();

        validator.Validate(new ResolveComplaintRequest
        {
            ResolutionOutcome = "ConfirmReceiptAsIs",
            ResolutionNote = new string('k', 10)
        }).IsValid.Should().BeTrue();

        validator.Validate(new ResolveComplaintRequest
        {
            ResolutionOutcome = "ConfirmReceiptAsIs",
            ResolutionNote = new string('k', 9)
        }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UTC022_ResolveComplaintAsync_PointClawbackFloorZeroAndConfirmDisputedAwardsPoints_Boundary()
    {
        using var db = TestDbContextFactory.Create();
        // Citizen đã tiêu điểm chỉ còn 20 điểm (< 50 điểm cần thu hồi) -> kẹp chặt về 0 không bao giờ âm (BR-GAM-03)
        var ctx1 = await SeedHandoverAsync(db, HandoverStatus.Completed, citizenPoints: 20, receiptPoints: 50);
        var (_, cmpSvc1, _) = CreateServices(db, ctx1.Citizen.Id, "Citizen");
        var c1 = await cmpSvc1.SubmitComplaintAsync(ctx1.Citizen.Id, new SubmitComplaintRequest
        {
            HandoverRequestId = ctx1.Handover.Id,
            Category = "ScaleDiscrepancy",
            Description = "Hủy đơn tranh chấp khi người dân đã sử dụng bớt điểm thưởng."
        });

        var (_, staffSvc1, _) = CreateServices(db, ctx1.Staff.Id, "Staff");
        await staffSvc1.ResolveComplaintAsync(ctx1.Staff.Id, c1.ComplaintId, new ResolveComplaintRequest
        {
            ResolutionOutcome = "CancelDisputedOrder",
            ResolutionNote = "Thu hồi điểm đơn hủy nhưng giữ cận dưới không âm."
        });

        var ecoAfterClawback = await db.UserEcoProfiles.FirstAsync(p => p.UserId == ctx1.Citizen.Id);
        ecoAfterClawback.CurrentGreenPoints.Should().Be(0);

        // Trường hợp biên ConfirmReceiptAsIs trên đơn đang Disputed chưa từng cộng điểm -> hoàn tất & cộng 50 điểm
        var ctx2 = await SeedHandoverAsync(db, HandoverStatus.AwaitingConfirmation, citizenPoints: 0, receiptPoints: 0);
        var (_, cmpSvc2, _) = CreateServices(db, ctx2.Citizen.Id, "Citizen");
        var c2 = await cmpSvc2.SubmitComplaintAsync(ctx2.Citizen.Id, new SubmitComplaintRequest
        {
            HandoverRequestId = ctx2.Handover.Id,
            Category = "ScaleDiscrepancy",
            Description = "Yêu cầu đối chiếu lại ảnh đồng hồ cân biên nhận 5kg nhựa."
        });

        var (_, staffSvc2, _) = CreateServices(db, ctx2.Staff.Id, "Staff");
        var confirmed = await staffSvc2.ResolveComplaintAsync(ctx2.Staff.Id, c2.ComplaintId, new ResolveComplaintRequest
        {
            ResolutionOutcome = "ConfirmReceiptAsIs",
            ResolutionNote = "Ảnh đồng hồ cân xác nhận đúng 5.0kg, chốt hoàn tất đơn."
        });

        confirmed.Status.Should().Be("Resolved");
        confirmed.HandoverStatus.Should().Be("Completed");
        var receipt2 = await db.HandoverReceipts.FirstAsync(r => r.RequestId == ctx2.Handover.Id);
        receipt2.PointsAwarded.Should().Be(50);
        var ecoAfterConfirm = await db.UserEcoProfiles.FirstAsync(p => p.UserId == ctx2.Citizen.Id);
        ecoAfterConfirm.CurrentGreenPoints.Should().Be(50);
    }
}
