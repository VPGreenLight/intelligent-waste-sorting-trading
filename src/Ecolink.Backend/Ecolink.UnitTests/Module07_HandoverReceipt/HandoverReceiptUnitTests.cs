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

namespace Ecolink.UnitTests.Module07_HandoverReceipt;

[Trait("Module", "Module07_HandoverCompletionAndReceipt")]
public class HandoverReceiptUnitTests
{
    private static async Task<(
        HandoverReceiptService Sut,
        ApplicationDbContext Db,
        IGamificationService Gamification,
        User Citizen,
        User RecyclerUser,
        WasteCategory Plastic,
        WasteCategory Metal,
        HandoverRequest Request,
        HandoverRequestItem PlasticItem)> CreateAcceptedHandoverSutAsync(decimal estimatedPlasticKg = 10.0m, decimal? agreedPrice = 7000m)
    {
        var db = TestDbContextFactory.Create();
        var gamification = Substitute.For<IGamificationService>();

        var plastic = new WasteCategory { Id = Guid.NewGuid(), Code = "plastic", Name = "Nhựa tái chế (Plastic)", StandardBinColor = "Cam", IsRecyclable = true };
        var metal = new WasteCategory { Id = Guid.NewGuid(), Code = "metal", Name = "Kim loại & Vỏ lon (Metal)", StandardBinColor = "Xám", IsRecyclable = true };
        db.WasteCategories.AddRange(plastic, metal);

        var citizen = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_rcp",
            Email = "citizen_rcp@ecolink.vn",
            FullName = "Nguyễn Văn Dân",
            PhoneNumber = "0901112233",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        var recyclerUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "recycler_rcp",
            Email = "recycler_rcp@ecolink.vn",
            FullName = "Đỗ Văn Hùng",
            PhoneNumber = "0968112233",
            Role = UserRole.Recycler,
            Status = UserStatus.Active
        };
        var profile = new RecyclerProfile
        {
            UserId = recyclerUser.Id,
            User = recyclerUser,
            FacilityName = "Trạm Tái Chế Hòa Lạc",
            BusinessAddress = "Tân Xã, Thạch Thất, Hà Nội",
            ContactPhone = "0968112233",
            Latitude = 21.0182m,
            Longitude = 105.5345m,
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        };

        db.Users.AddRange(citizen, recyclerUser);
        db.RecyclerProfiles.Add(profile);
        db.RecyclerPrices.AddRange(
            new RecyclerPrice
            {
                RecyclerId = recyclerUser.Id,
                WasteCategoryId = plastic.Id,
                WasteCategory = plastic,
                PricePerKg = 7000m,
                MinQuantityKg = 1.0m,
                Status = PriceStatus.Active
            },
            new RecyclerPrice
            {
                RecyclerId = recyclerUser.Id,
                WasteCategoryId = metal.Id,
                WasteCategory = metal,
                PricePerKg = 25000m,
                MinQuantityKg = 0.5m,
                Status = PriceStatus.Active
            });

        var item = new HandoverRequestItem
        {
            Id = Guid.NewGuid(),
            WasteCategoryId = plastic.Id,
            WasteCategory = plastic,
            EstimatedWeightKg = estimatedPlasticKg,
            AgreedPricePerKg = agreedPrice
        };
        var request = new HandoverRequest
        {
            Id = Guid.NewGuid(),
            RequestCode = "HND-20261110-0001",
            UserId = citizen.Id,
            RecyclerId = recyclerUser.Id,
            Recycler = profile,
            PickupAddress = "KTX ĐH FPT Hòa Lạc",
            WastePhotoUrl = "[\"https://cdn.ecolink.test/w.jpg\"]",
            ProposedTimeSlot = DateTime.UtcNow.AddHours(4),
            ConfirmedTimeSlot = DateTime.UtcNow.AddHours(4),
            Status = HandoverStatus.Accepted,
            Items = new List<HandoverRequestItem> { item }
        };
        item.RequestId = request.Id;

        db.HandoverRequests.Add(request);
        await db.SaveChangesAsync();

        var sut = new HandoverReceiptService(db, gamification, NullLogger<HandoverReceiptService>.Instance);
        return (sut, db, gamification, citizen, recyclerUser, plastic, metal, request, item);
    }

    private static CreateReceiptRequest ValidReceiptRequest(Guid itemId, decimal actualKg = 10.0m) => new()
    {
        Items = new List<ReceiptItemRequest>
        {
            new() { ItemId = itemId, ActualWeightKg = actualKg }
        },
        ScaleProofPhotoUrls = new List<string> { "https://cdn.ecolink.test/scale_proof.jpg" },
        ReceiptImageUrl = "https://cdn.ecolink.test/receipt_paper.jpg"
    };

    [Fact]
    public async Task UTC001_PreviewAsync_ValidAcceptedRequestWithin20PercentDeviation_CalculatesTotalsAndNoWarning()
    {
        var (sut, _, _, _, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync(10.0m, 7000m);

        var preview = await sut.PreviewAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 11.0m));

        preview.ReceiptId.Should().BeNull();
        preview.TotalWeightKg.Should().Be(11.0m);
        preview.TotalAmountCash.Should().Be(77000m);
        preview.DeviationPercent.Should().Be(10.0m);
        preview.HasDeviationWarning.Should().BeFalse();
        preview.Points.Should().Be(110);
        preview.PointsCredited.Should().BeFalse();
    }

    [Fact]
    public async Task UTC002_PreviewAsync_BoundaryExact20PercentDeviation_DoesNotTriggerDeviationWarning()
    {
        var (sut, _, _, _, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync(10.0m, 7000m);

        var previewPlus20 = await sut.PreviewAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 12.0m));
        var previewMinus20 = await sut.PreviewAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 8.0m));

        previewPlus20.DeviationPercent.Should().Be(20.0m);
        previewPlus20.HasDeviationWarning.Should().BeFalse();
        previewMinus20.DeviationPercent.Should().Be(-20.0m);
        previewMinus20.HasDeviationWarning.Should().BeFalse();
    }

    [Fact]
    public async Task UTC003_PreviewAsync_BoundaryOver20PercentDeviation_TriggersDeviationWarningAndMessage_MsgHnd04()
    {
        var (sut, _, _, _, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync(10.0m, 7000m);

        var preview = await sut.PreviewAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 12.1m)); // +21%

        preview.DeviationPercent.Should().Be(21.0m);
        preview.HasDeviationWarning.Should().BeTrue();
        preview.DeviationMessage.Should().Contain("20%");
    }

    [Fact]
    public async Task UTC004_CreateAsync_ValidAcceptedRequest_CreatesReceiptAndMovesStatusToAwaitingConfirmation_BrHnd04()
    {
        var (sut, db, _, _, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync(10.0m, 7000m);

        var dto = await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 9.5m));

        dto.ReceiptId.Should().NotBeNull();
        dto.ReceiptCode.Should().StartWith("RCP-");
        dto.RequestStatus.Should().Be(HandoverStatus.AwaitingConfirmation.ToString());
        dto.TotalWeightKg.Should().Be(9.5m);
        dto.TotalAmountCash.Should().Be(66500m);

        var dbReq = await db.HandoverRequests.FirstAsync(h => h.Id == req.Id);
        dbReq.Status.Should().Be(HandoverStatus.AwaitingConfirmation);
    }

    [Fact]
    public async Task UTC005_CreateAsync_WithExtraOnSiteWasteItem_IncludesExtraInTotalButExcludesFromDeviationPercent()
    {
        var (sut, _, _, _, recyclerUser, _, metal, req, item) = await CreateAcceptedHandoverSutAsync(10.0m, 7000m);
        var createReq = ValidReceiptRequest(item.Id, 10.0m);
        createReq.Items.Add(new ReceiptItemRequest
        {
            ItemId = null,
            WasteCategoryId = metal.Id,
            ActualWeightKg = 2.0m // 2kg * 25,000 = 50,000
        });

        var dto = await sut.CreateAsync(recyclerUser.Id, req.Id, createReq);

        dto.Lines.Should().HaveCount(2);
        dto.Lines.Should().Contain(l => l.IsExtra && l.WasteCategoryCode == "metal" && l.SubTotal == 50000m);
        dto.TotalWeightKg.Should().Be(12.0m);
        dto.TotalAmountCash.Should().Be(70000m + 50000m);
        dto.DeviationPercent.Should().Be(0m); // chỉ tính trên dòng có trong yêu cầu ban đầu (10kg -> 10kg)
    }

    [Fact]
    public async Task UTC006_CreateAsync_RequestNotInAcceptedOrScheduledStatus_ThrowsBrHnd02()
    {
        var (sut, db, _, _, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync();
        req.Status = HandoverStatus.Pending;
        await db.SaveChangesAsync();

        var act = () => sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 5.0m));

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-02");
    }

    [Fact]
    public async Task UTC007_CreateAsync_RequestAlreadyHasReceipt_ThrowsBrHnd04()
    {
        var (sut, db, _, _, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync();
        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 10.0m));

        // Giả lập trạng thái vẫn là Accepted nhưng đã tồn tại bản ghi biên nhận
        req.Status = HandoverStatus.Accepted;
        await db.SaveChangesAsync();

        var act = () => sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 10.0m));

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-04");
    }

    [Fact]
    public async Task UTC008_CreateAsync_EmptyScaleProofPhotosOrOver5OrInvalidUrl_ThrowsBrHnd04()
    {
        var (sut, _, _, _, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync();

        var noPhoto = ValidReceiptRequest(item.Id, 10.0m);
        noPhoto.ScaleProofPhotoUrls = new List<string>();
        (await ((Func<Task>)(() => sut.CreateAsync(recyclerUser.Id, req.Id, noPhoto))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-04");

        var over5Photos = ValidReceiptRequest(item.Id, 10.0m);
        over5Photos.ScaleProofPhotoUrls = Enumerable.Range(1, 6).Select(i => $"https://cdn.ecolink.test/p{i}.jpg").ToList();
        (await ((Func<Task>)(() => sut.CreateAsync(recyclerUser.Id, req.Id, over5Photos))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-04");

        var badUrl = ValidReceiptRequest(item.Id, 10.0m);
        badUrl.ScaleProofPhotoUrls = new List<string> { "not-a-valid-http-url" };
        (await ((Func<Task>)(() => sut.CreateAsync(recyclerUser.Id, req.Id, badUrl))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-04");
    }

    [Fact]
    public async Task UTC009_CreateAsync_MissingActualWeightForRequestedItem_ThrowsBrHnd05MsgAuth02()
    {
        var (sut, db, _, _, recyclerUser, _, metal, req, item) = await CreateAcceptedHandoverSutAsync();
        db.HandoverRequestItems.Add(new HandoverRequestItem
        {
            Id = Guid.NewGuid(),
            RequestId = req.Id,
            WasteCategoryId = metal.Id,
            EstimatedWeightKg = 2.0m
        });
        await db.SaveChangesAsync();

        // Chỉ gửi 1/2 dòng trong yêu cầu
        var act = () => sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 10.0m));

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-05");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-02");
    }

    [Fact]
    public async Task UTC010_CreateAsync_ActualWeightZeroOrNegativeOrMoreThan2Decimals_ThrowsBrHnd05()
    {
        var (sut, _, _, _, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync();

        var actZero = () => sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 0m));
        (await actZero.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-HND-05");

        var act3Dec = () => sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 5.125m));
        (await act3Dec.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-HND-05");
    }

    [Fact]
    public async Task UTC011_CreateAsync_ForeignItemIdOrDuplicateItemIdOrExtraDuplicateCategory_ThrowsBrHnd05()
    {
        var (sut, _, _, _, recyclerUser, plastic, _, req, item) = await CreateAcceptedHandoverSutAsync();

        // 1. ItemId không thuộc request
        var foreignReq = ValidReceiptRequest(Guid.NewGuid(), 5.0m);
        (await ((Func<Task>)(() => sut.CreateAsync(recyclerUser.Id, req.Id, foreignReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-05");

        // 2. Nhập trùng ItemId 2 lần
        var dupItemReq = ValidReceiptRequest(item.Id, 5.0m);
        dupItemReq.Items.Add(new ReceiptItemRequest { ItemId = item.Id, ActualWeightKg = 5.0m });
        (await ((Func<Task>)(() => sut.CreateAsync(recyclerUser.Id, req.Id, dupItemReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-05");

        // 3. Dòng phát sinh trùng loại phế liệu đã có sẵn trong yêu cầu
        var dupCatExtraReq = ValidReceiptRequest(item.Id, 5.0m);
        dupCatExtraReq.Items.Add(new ReceiptItemRequest { ItemId = null, WasteCategoryId = plastic.Id, ActualWeightKg = 2.0m });
        (await ((Func<Task>)(() => sut.CreateAsync(recyclerUser.Id, req.Id, dupCatExtraReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-05");
    }

    [Fact]
    public async Task UTC012_CreateAsync_ExtraItemCategoryNotActivelyPurchasedByRecycler_ThrowsBrHnd05()
    {
        var (sut, db, _, _, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync();
        var glass = new WasteCategory { Id = Guid.NewGuid(), Code = "glass", Name = "Thủy tinh", StandardBinColor = "Trắng", IsRecyclable = true };
        db.WasteCategories.Add(glass);
        await db.SaveChangesAsync();

        var extraUnpurchased = ValidReceiptRequest(item.Id, 10.0m);
        extraUnpurchased.Items.Add(new ReceiptItemRequest
        {
            ItemId = null,
            WasteCategoryId = glass.Id,
            ActualWeightKg = 3.0m
        });

        var act = () => sut.CreateAsync(recyclerUser.Id, req.Id, extraUnpurchased);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-05");
    }

    [Fact]
    public async Task UTC013_CreateAsync_TransactionPriceLocking_UsesLockedAgreedPriceEvenIfRecyclerChangesPublicPriceLater_BrHnd05()
    {
        var (sut, db, _, _, recyclerUser, plastic, _, req, item) = await CreateAcceptedHandoverSutAsync(10.0m, agreedPrice: 8000m);

        // Recycler đổi giá công khai xuống 5000m sau khi đơn đã khóa giá 8000m
        var priceRow = await db.RecyclerPrices.FirstAsync(p => p.RecyclerId == recyclerUser.Id && p.WasteCategoryId == plastic.Id);
        priceRow.PricePerKg = 5000m;
        await db.SaveChangesAsync();

        var receipt = await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 10.0m));

        receipt.Lines.First().UnitPricePerKg.Should().Be(8000m);
        receipt.TotalAmountCash.Should().Be(80000m);
    }

    [Fact]
    public async Task UTC014_ConfirmAsync_CitizenConfirmsAwaitingReceipt_CompletesOrderAndAwardsPointsToBothParties_BrGam01_BrHnd07()
    {
        var (sut, _, gamification, citizen, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync(10.0m, 7000m);
        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 10.0m));

        var confirmed = await sut.ConfirmAsync(citizen.Id, req.Id);

        confirmed.RequestStatus.Should().Be(HandoverStatus.Completed.ToString());
        confirmed.Points.Should().Be(100);
        confirmed.PointsCredited.Should().BeTrue();
        confirmed.IsLocked.Should().BeTrue();
        confirmed.CanConfirm.Should().BeFalse();
        confirmed.ConfirmedByUserAt.Should().NotBeNull();

        await gamification.Received(1).AwardPointsForRecyclingAsync(citizen.Id, 10.0m, Arg.Any<CancellationToken>());
        await gamification.Received(1).AwardPointsForRecyclingAsync(recyclerUser.Id, 10.0m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTC015_ConfirmAsync_AlreadyCompletedOrBeforeReceiptCreated_ThrowsBrHnd04()
    {
        var (sut, _, _, citizen, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync();

        // 1. Chưa lập biên nhận (đang Accepted)
        var actBefore = () => sut.ConfirmAsync(citizen.Id, req.Id);
        (await actBefore.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-HND-04");

        // 2. Sau khi đã Completed -> gọi lần 2 bị chặn (BR-HND-07 / BR-GAM-01 chống cộng điểm trùng)
        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 10.0m));
        await sut.ConfirmAsync(citizen.Id, req.Id);

        var actTwice = () => sut.ConfirmAsync(citizen.Id, req.Id);
        (await actTwice.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-HND-04");
    }

    [Fact]
    public async Task UTC016_ConfirmAsync_GamificationServiceThrowsException_CompletesTransactionWithoutFailing()
    {
        var (sut, _, gamification, citizen, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync(5.0m, 7000m);
        gamification.AwardPointsForRecyclingAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Gamification temporary error"));

        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 5.0m));
        var confirmed = await sut.ConfirmAsync(citizen.Id, req.Id);

        confirmed.RequestStatus.Should().Be(HandoverStatus.Completed.ToString());
        confirmed.Points.Should().Be(50);
        confirmed.PointsCredited.Should().BeTrue();
    }

    [Fact]
    public async Task UTC017_DisputeAsync_ValidReasonAndEvidencePhotosOnAwaitingConfirmation_TransitionsToDisputed_BrDsp06()
    {
        var (sut, db, _, citizen, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync(10.0m, 7000m);
        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 6.0m));

        var disputed = await sut.DisputeAsync(citizen.Id, req.Id, new DisputeReceiptRequest
        {
            Reason = "  Cân tại chỗ hiển thị 9.5kg nhưng biên nhận chỉ ghi 6.0kg  ",
            EvidencePhotoUrls = new List<string> { "https://cdn.ecolink.test/user_scale_photo.jpg" }
        });

        disputed.RequestStatus.Should().Be(HandoverStatus.Disputed.ToString());
        var dbReq = await db.HandoverRequests.FirstAsync(h => h.Id == req.Id);
        dbReq.Status.Should().Be(HandoverStatus.Disputed);
        dbReq.CancelReason.Should().Be("Cân tại chỗ hiển thị 9.5kg nhưng biên nhận chỉ ghi 6.0kg");
    }

    [Fact]
    public async Task UTC018_DisputeAsync_EmptyOrOver500CharsReasonOrOver5EvidencePhotos_ThrowsBrDsp06()
    {
        var (sut, _, _, citizen, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync();
        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 10.0m));

        var actEmpty = () => sut.DisputeAsync(citizen.Id, req.Id, new DisputeReceiptRequest { Reason = "   " });
        (await actEmpty.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-DSP-06");

        var actLong = () => sut.DisputeAsync(citizen.Id, req.Id, new DisputeReceiptRequest { Reason = new string('X', 501) });
        (await actLong.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-DSP-06");

        var actOver5Photos = () => sut.DisputeAsync(citizen.Id, req.Id, new DisputeReceiptRequest
        {
            Reason = "Lệch cân",
            EvidencePhotoUrls = Enumerable.Range(1, 6).Select(i => $"https://cdn.ecolink.test/ev{i}.jpg").ToList()
        });
        (await actOver5Photos.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-DSP-06");
    }

    [Fact]
    public async Task UTC019_DisputeAsync_WhenAlreadyCompleted_ThrowsBrHnd07()
    {
        var (sut, _, _, citizen, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync();
        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 10.0m));
        await sut.ConfirmAsync(citizen.Id, req.Id);

        var act = () => sut.DisputeAsync(citizen.Id, req.Id, new DisputeReceiptRequest
        {
            Reason = "Khiếu nại muộn sau khi đã bấm xác nhận"
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-07");
    }

    [Fact]
    public async Task UTC020_AutoCompleteExpiredAsync_Job01_CompletesStaleAwaitingConfirmationReceiptsAndAwardsPoints()
    {
        var (sut, db, gamification, citizen, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync(8.0m, 7000m);
        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 8.0m));

        var receiptEntity = await db.HandoverReceipts.FirstAsync(r => r.RequestId == req.Id);
        receiptEntity.CreatedAt = DateTime.UtcNow.AddHours(-25);
        await db.SaveChangesAsync();

        var completedCount = await sut.AutoCompleteExpiredAsync(TimeSpan.FromHours(24));

        completedCount.Should().Be(1);
        var dto = await sut.GetForUserAsync(citizen.Id, req.Id);
        dto.RequestStatus.Should().Be(HandoverStatus.Completed.ToString());
        dto.PointsCredited.Should().BeTrue();
        dto.Points.Should().Be(80);
        await gamification.Received(1).AwardPointsForRecyclingAsync(citizen.Id, 8.0m, Arg.Any<CancellationToken>());
        await gamification.Received(1).AwardPointsForRecyclingAsync(recyclerUser.Id, 8.0m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTC021_GetForUserAndGetForRecyclerAsync_UnauthorizedParty_ThrowsNotFoundException_BrHnd03()
    {
        var (sut, _, _, citizen, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync();
        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 10.0m));

        (await sut.GetForUserAsync(citizen.Id, req.Id)).CanConfirm.Should().BeTrue();
        (await sut.GetForRecyclerAsync(recyclerUser.Id, req.Id)).CanConfirm.Should().BeFalse();

        await ((Func<Task>)(() => sut.GetForUserAsync(Guid.NewGuid(), req.Id))).Should().ThrowAsync<NotFoundException>();
        await ((Func<Task>)(() => sut.GetForRecyclerAsync(Guid.NewGuid(), req.Id))).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UTC022_GetUserAndRecyclerHistoryAsync_ReturnsOnlyConfirmedTransactionsWithAggregatedTotals()
    {
        var (sut, _, _, citizen, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync(10.0m, 7000m);
        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 10.0m));

        // Khi chưa xác nhận -> lịch sử hoàn tất còn trống
        var historyBefore = await sut.GetUserHistoryAsync(citizen.Id, null, null, 1, 10);
        historyBefore.TotalCount.Should().Be(0);

        // Sau khi xác nhận -> hiển thị trong cả lịch sử người dân và Recycler
        await sut.ConfirmAsync(citizen.Id, req.Id);

        var userHist = await sut.GetUserHistoryAsync(citizen.Id, null, null, 1, 10);
        userHist.TotalCount.Should().Be(1);
        userHist.TotalWeightKg.Should().Be(10.0m);
        userHist.TotalAmountCash.Should().Be(70000m);
        userHist.TotalPoints.Should().Be(100);

        var rcyHist = await sut.GetRecyclerHistoryAsync(recyclerUser.Id, null, null, 1, 10);
        rcyHist.TotalCount.Should().Be(1);
        rcyHist.Items.First().CounterpartName.Should().Be("Nguyễn Văn Dân");
    }

    [Fact]
    public async Task UTC023_GetUserHistoryAsync_DateRangeFilterWithDateOnlyTo_IncludesEntireEndDay()
    {
        var (sut, _, _, citizen, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync(5.0m, 7000m);
        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 5.0m));
        await sut.ConfirmAsync(citizen.Id, req.Id);

        var today = DateTime.UtcNow.Date;
        var inRange = await sut.GetUserHistoryAsync(citizen.Id, today.AddDays(-1), today, 1, 10);
        var outOfRange = await sut.GetUserHistoryAsync(citizen.Id, today.AddDays(-10), today.AddDays(-2), 1, 10);

        inRange.TotalCount.Should().Be(1);
        outOfRange.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task UTC024_PreviewAndConfirmAsync_MidpointRoundingAwayFromZero_CalculatesSubTotalAndPointsAccurately()
    {
        var (sut, _, _, citizen, recyclerUser, _, _, req, item) = await CreateAcceptedHandoverSutAsync(1.25m, 7001m);

        await sut.CreateAsync(recyclerUser.Id, req.Id, ValidReceiptRequest(item.Id, 1.25m));
        var confirmed = await sut.ConfirmAsync(citizen.Id, req.Id);

        // 1.25 * 7001 = 8751.25 -> 8751; Points: 1.25 * 10 = 12.5 -> 13 (AwayFromZero)
        confirmed.TotalAmountCash.Should().Be(8751m);
        confirmed.Points.Should().Be(13);
    }
}
