using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Ecolink.Infrastructure.Services;
using Ecolink.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ecolink.UnitTests.Module06_HandoverRequest;

[Trait("Module", "Module06_HandoverRequestManagement")]
public class HandoverRequestUnitTests
{
    private static async Task<(
        HandoverRequestService Sut,
        ApplicationDbContext Db,
        User Citizen,
        User RecyclerUser,
        RecyclerProfile Recycler,
        WasteCategory Plastic,
        WasteCategory Metal)> CreateSeededSutAsync()
    {
        var db = TestDbContextFactory.Create();
        var plastic = new WasteCategory { Id = Guid.NewGuid(), Code = "plastic", Name = "Nhựa tái chế (Plastic)", StandardBinColor = "Cam", IsRecyclable = true };
        var metal = new WasteCategory { Id = Guid.NewGuid(), Code = "metal", Name = "Kim loại & Vỏ lon (Metal)", StandardBinColor = "Xám", IsRecyclable = true };
        db.WasteCategories.AddRange(plastic, metal);

        var citizen = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_hnd",
            Email = "citizen_hnd@ecolink.vn",
            FullName = "Nguyễn Văn Dân",
            PhoneNumber = "0901112233",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };

        var recyclerUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "recycler_hnd",
            Email = "recycler_hnd@ecolink.vn",
            FullName = "Đỗ Văn Hùng",
            PhoneNumber = "0968112233",
            Role = UserRole.Recycler,
            Status = UserStatus.Active
        };

        var recycler = new RecyclerProfile
        {
            UserId = recyclerUser.Id,
            User = recyclerUser,
            FacilityName = "Trạm Thu Gom Hòa Lạc",
            RepresentativeName = "Đỗ Văn Hùng",
            BusinessAddress = "Thôn 3, Tân Xã, Thạch Thất, Hà Nội",
            ContactPhone = "0968112233",
            Latitude = 21.0182m,
            Longitude = 105.5345m,
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        };

        db.Users.AddRange(citizen, recyclerUser);
        db.RecyclerProfiles.Add(recycler);
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
        await db.SaveChangesAsync();

        var sut = new HandoverRequestService(db, NullLogger<HandoverRequestService>.Instance);
        return (sut, db, citizen, recyclerUser, recycler, plastic, metal);
    }

    private static CreateHandoverRequest ValidCreateRequest(Guid recyclerId, Guid categoryId, decimal weightKg = 2.5m) => new()
    {
        RecyclerId = recyclerId,
        Items = new List<HandoverItemRequest>
        {
            new() { WasteCategoryId = categoryId, EstimatedWeightKg = weightKg, ItemNote = "Đã tráng sạch" }
        },
        PickupAddress = "KTX Khu B, ĐH FPT Hòa Lạc, Thạch Thất, Hà Nội",
        Latitude = 21.0124m,
        Longitude = 105.5253m,
        PhotoUrls = new List<string> { "https://cdn.ecolink.test/waste1.jpg" },
        ProposedTimeSlot = DateTimeOffset.UtcNow.AddHours(6),
        UserNote = "Gọi trước 15 phút"
    };

    [Fact]
    public async Task UTC001_CreateAsync_ValidRequest_CreatesPendingHandoverAndHidesPhoneBeforeAccepted()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();

        var dto = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 3.0m));

        dto.RequestCode.Should().StartWith("HND-");
        dto.Status.Should().Be(HandoverStatus.Pending.ToString());
        dto.TotalEstimatedWeightKg.Should().Be(3.0m);
        dto.IsContactRevealed.Should().BeFalse();
        dto.Recycler!.Phone.Should().BeNull();
        dto.CanEdit.Should().BeTrue();
        dto.CanCancel.Should().BeTrue();
    }

    [Fact]
    public async Task UTC002_CreateAsync_UserSendsToOwnFacility_ThrowsBrHnd01()
    {
        var (sut, _, _, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();

        var act = () => sut.CreateAsync(recyclerUser.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id));

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-01");
    }

    [Fact]
    public async Task UTC003_CreateAsync_ExceedsMax3ActiveRequestsPerUser_ThrowsBrHndLimit()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        for (var i = 0; i < HandoverRequestService.MaxActiveRequestsPerUser; i++)
        {
            await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));
        }

        var act4th = () => sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));

        var ex = await act4th.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-LIMIT");
    }

    [Fact]
    public async Task UTC004_CreateAsync_RecyclerOfflineOrUnverified_ThrowsBrHnd01()
    {
        var (sut, db, citizen, recyclerUser, recycler, plastic, _) = await CreateSeededSutAsync();
        recycler.ActiveStatus = RecyclerActiveStatus.Offline;
        await db.SaveChangesAsync();

        var act = () => sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id));

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-01");
    }

    [Fact]
    public async Task UTC005_CreateAsync_EmptyItemsOrDuplicateCategoryOrUnacceptedCategory_ThrowsBrHnd01()
    {
        var (sut, db, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();

        // 1. Danh sách rỗng
        var emptyReq = ValidCreateRequest(recyclerUser.Id, plastic.Id);
        emptyReq.Items.Clear();
        (await ((Func<Task>)(() => sut.CreateAsync(citizen.Id, emptyReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-01");

        // 2. Trùng loại phế liệu trong cùng yêu cầu
        var dupReq = ValidCreateRequest(recyclerUser.Id, plastic.Id);
        dupReq.Items.Add(new HandoverItemRequest { WasteCategoryId = plastic.Id, EstimatedWeightKg = 2.0m });
        (await ((Func<Task>)(() => sut.CreateAsync(citizen.Id, dupReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-01");

        // 3. Loại phế liệu cơ sở không nhận mua
        var glass = new WasteCategory { Id = Guid.NewGuid(), Code = "glass", Name = "Thủy tinh", StandardBinColor = "Trắng", IsRecyclable = true };
        db.WasteCategories.Add(glass);
        await db.SaveChangesAsync();
        var unacceptedReq = ValidCreateRequest(recyclerUser.Id, glass.Id, 5.0m);
        (await ((Func<Task>)(() => sut.CreateAsync(citizen.Id, unacceptedReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-01");
    }

    [Fact]
    public async Task UTC006_CreateAsync_WeightBelowMinQuantityOrMoreThan2Decimals_ThrowsBrHnd01()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();

        // Nhỏ hơn MinQuantityKg (0.5 < 1.0)
        var belowMinReq = ValidCreateRequest(recyclerUser.Id, plastic.Id, 0.5m);
        (await ((Func<Task>)(() => sut.CreateAsync(citizen.Id, belowMinReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-01");

        // Quá 2 chữ số thập phân (1.255 kg)
        var threeDecimalsReq = ValidCreateRequest(recyclerUser.Id, plastic.Id, 1.255m);
        (await ((Func<Task>)(() => sut.CreateAsync(citizen.Id, threeDecimalsReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-01");
    }

    [Fact]
    public async Task UTC007_CreateAsync_ProposedTimeSlotLessThan2HoursOrMoreThan7DaysAhead_ThrowsBrNeg04()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();

        var tooSoonReq = ValidCreateRequest(recyclerUser.Id, plastic.Id);
        tooSoonReq.ProposedTimeSlot = DateTimeOffset.UtcNow.AddMinutes(45);
        (await ((Func<Task>)(() => sut.CreateAsync(citizen.Id, tooSoonReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-NEG-04");

        var tooFarReq = ValidCreateRequest(recyclerUser.Id, plastic.Id);
        tooFarReq.ProposedTimeSlot = DateTimeOffset.UtcNow.AddDays(8);
        (await ((Func<Task>)(() => sut.CreateAsync(citizen.Id, tooFarReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-NEG-04");
    }

    [Fact]
    public async Task UTC008_CreateAsync_ProposedTimeSlotOnClosedDayOrOutsideOperatingHours_ThrowsBrNeg04()
    {
        var (sut, db, citizen, recyclerUser, recycler, plastic, _) = await CreateSeededSutAsync();
        var targetUtc = DateTime.UtcNow.AddDays(2);
        var targetLocal = targetUtc.AddHours(7);
        var closedSchedule = new List<ScheduleDayDto>
        {
            new() { DayOfWeek = (int)targetLocal.DayOfWeek, IsClosed = true }
        };
        recycler.WeeklyScheduleJson = JsonSerializer.Serialize(closedSchedule, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await db.SaveChangesAsync();

        var req = ValidCreateRequest(recyclerUser.Id, plastic.Id);
        req.ProposedTimeSlot = new DateTimeOffset(targetUtc, TimeSpan.Zero);

        var act = () => sut.CreateAsync(citizen.Id, req);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-NEG-04");
    }

    [Fact]
    public async Task UTC009_CreateAsync_InvalidPhotoCountOrInvalidUrlOrBlankAddress_ThrowsBrHnd01()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();

        // 0 ảnh
        var noPhotoReq = ValidCreateRequest(recyclerUser.Id, plastic.Id);
        noPhotoReq.PhotoUrls = new List<string>();
        (await ((Func<Task>)(() => sut.CreateAsync(citizen.Id, noPhotoReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-01");

        // URL không phải http/https
        var badUrlReq = ValidCreateRequest(recyclerUser.Id, plastic.Id);
        badUrlReq.PhotoUrls = new List<string> { "ftp://invalid.server/file.jpg" };
        (await ((Func<Task>)(() => sut.CreateAsync(citizen.Id, badUrlReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-01");

        // Địa chỉ trống
        var blankAddrReq = ValidCreateRequest(recyclerUser.Id, plastic.Id);
        blankAddrReq.PickupAddress = "   ";
        (await ((Func<Task>)(() => sut.CreateAsync(citizen.Id, blankAddrReq))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleCode.Should().Be("BR-HND-01");
    }

    [Fact]
    public async Task UTC010_UpdateAsync_PendingRequest_UpdatesItemsAddressAndTimeSlot()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, metal) = await CreateSeededSutAsync();
        var created = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));

        var updated = await sut.UpdateAsync(citizen.Id, created.Id, new UpdateHandoverRequest
        {
            Items = new List<HandoverItemRequest>
            {
                new() { WasteCategoryId = metal.Id, EstimatedWeightKg = 4.5m, ItemNote = "Lon nhôm sạch" }
            },
            PickupAddress = "Địa chỉ mới tại Hòa Lạc",
            PhotoUrls = new List<string> { "https://cdn.ecolink.test/new_metal.jpg" },
            ProposedTimeSlot = DateTimeOffset.UtcNow.AddHours(8),
            UserNote = "Đã cập nhật sang kim loại"
        });

        updated.TotalEstimatedWeightKg.Should().Be(4.5m);
        updated.Items.Should().ContainSingle().Which.WasteCategoryCode.Should().Be("metal");
        updated.PickupAddress.Should().Be("Địa chỉ mới tại Hòa Lạc");
    }

    [Fact]
    public async Task UTC011_UpdateAsync_NonPendingRequestAcceptedOrCancelled_ThrowsBrHnd02()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var created = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));
        await sut.AcceptAsync(recyclerUser.Id, created.Id, "Đồng ý");

        var act = () => sut.UpdateAsync(citizen.Id, created.Id, new UpdateHandoverRequest
        {
            Items = new List<HandoverItemRequest> { new() { WasteCategoryId = plastic.Id, EstimatedWeightKg = 3.0m } },
            PickupAddress = "Địa chỉ khác",
            PhotoUrls = new List<string> { "https://cdn.ecolink.test/p.jpg" },
            ProposedTimeSlot = DateTimeOffset.UtcNow.AddHours(5)
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-02");
    }

    [Fact]
    public async Task UTC012_AcceptAsync_ValidPendingRequest_LocksConfirmedSlotAndPriceAndRevealsContact_BrNeg05_BrHnd08()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var created = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));

        var accepted = await sut.AcceptAsync(recyclerUser.Id, created.Id, "Sẽ đến đúng giờ");

        accepted.Status.Should().Be(HandoverStatus.Accepted.ToString());
        accepted.ConfirmedTimeSlot.Should().Be(accepted.ProposedTimeSlot);
        accepted.IsContactRevealed.Should().BeTrue();
        accepted.Requester.Should().NotBeNull();
        accepted.Requester!.Phone.Should().Be("0901112233");
        accepted.PickupAddress.Should().NotBeNullOrWhiteSpace();
        accepted.Items.First().AgreedPricePerKg.Should().Be(7000m);
    }

    [Fact]
    public async Task UTC013_AcceptAsync_ProposedTimeSlotAlreadyInPast_ThrowsBrNeg04()
    {
        var (sut, db, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var created = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));

        var entity = await db.HandoverRequests.FirstAsync(h => h.Id == created.Id);
        entity.ProposedTimeSlot = DateTime.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();

        var act = () => sut.AcceptAsync(recyclerUser.Id, created.Id, null);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-NEG-04");
    }

    [Fact]
    public async Task UTC014_AcceptAsync_AlreadyAcceptedOrCancelledRequest_ThrowsBrHnd02()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var created = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));
        await sut.AcceptAsync(recyclerUser.Id, created.Id, null);

        var actAgain = () => sut.AcceptAsync(recyclerUser.Id, created.Id, null);

        var ex = await actAgain.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-02");
    }

    [Fact]
    public async Task UTC015_RejectAsync_PendingRequestWithValidReason_TransitionsToRejected()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var created = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));

        var rejected = await sut.RejectAsync(recyclerUser.Id, created.Id, "  Xe tải thu gom đang bảo dưỡng  ");

        rejected.Status.Should().Be(HandoverStatus.Rejected.ToString());
        rejected.CancelReason.Should().Be("Xe tải thu gom đang bảo dưỡng");
        rejected.CancelledByRole.Should().Be("Recycler");
    }

    [Fact]
    public async Task UTC016_RejectAsync_BlankOrOver500CharsReason_ThrowsBrHnd06()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var created = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));

        var actBlank = () => sut.RejectAsync(recyclerUser.Id, created.Id, "   ");
        (await actBlank.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-HND-06");

        var actLong = () => sut.RejectAsync(recyclerUser.Id, created.Id, new string('R', 501));
        (await actLong.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-HND-06");
    }

    [Fact]
    public async Task UTC017_CancelByUserAsync_AcceptedWithSlotMoreThan4HoursAway_CancelsWithIsLateCancellationFalse()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var req = ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m);
        req.ProposedTimeSlot = DateTimeOffset.UtcNow.AddHours(6); // > 4h
        var created = await sut.CreateAsync(citizen.Id, req);
        await sut.AcceptAsync(recyclerUser.Id, created.Id, null);

        var cancelled = await sut.CancelByUserAsync(citizen.Id, created.Id, "Tôi có việc đột xuất phải đi công tác");

        cancelled.Status.Should().Be(HandoverStatus.Cancelled.ToString());
        cancelled.CancelledByRole.Should().Be("User");
        cancelled.IsLateCancellation.Should().BeFalse();
    }

    [Fact]
    public async Task UTC018_CancelByUserAsync_AcceptedWithin4HourWindow_FlagsIsLateCancellationTrue_BrHnd06()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var req = ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m);
        req.ProposedTimeSlot = DateTimeOffset.UtcNow.AddHours(2.5); // >= 2h lead time, nhưng <= 4h LateCancelWindowHours
        var created = await sut.CreateAsync(citizen.Id, req);
        await sut.AcceptAsync(recyclerUser.Id, created.Id, null);

        var cancelled = await sut.CancelByUserAsync(citizen.Id, created.Id, "Hủy sát giờ hẹn");

        cancelled.Status.Should().Be(HandoverStatus.Cancelled.ToString());
        cancelled.IsLateCancellation.Should().BeTrue();
    }

    [Fact]
    public async Task UTC019_CancelByUserAsync_WhenStatusIsAwaitingConfirmationOrCompleted_ThrowsBrHnd02()
    {
        var (sut, db, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var created = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));

        var entity = await db.HandoverRequests.FirstAsync(h => h.Id == created.Id);
        entity.Status = HandoverStatus.AwaitingConfirmation;
        await db.SaveChangesAsync();

        var act = () => sut.CancelByUserAsync(citizen.Id, created.Id, "Muốn hủy sau khi đã cân");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-02");
    }

    [Fact]
    public async Task UTC020_CancelByRecyclerAsync_AllowedOnlyWhenAcceptedOrScheduledAndFlagsLateCancel_BrHnd02_BrHnd06()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var req = ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m);
        req.ProposedTimeSlot = DateTimeOffset.UtcNow.AddHours(3); // <= 4h window
        var created = await sut.CreateAsync(citizen.Id, req);

        // Khi còn Pending -> không được gọi CancelByRecyclerAsync (phải dùng RejectAsync)
        var actPending = () => sut.CancelByRecyclerAsync(recyclerUser.Id, created.Id, "Hủy khi chưa Accept");
        (await actPending.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-HND-02");

        // Sau khi Accept -> hủy thành công và gắn cờ hủy muộn
        await sut.AcceptAsync(recyclerUser.Id, created.Id, null);
        var cancelled = await sut.CancelByRecyclerAsync(recyclerUser.Id, created.Id, "Xe hỏng dọc đường");

        cancelled.Status.Should().Be(HandoverStatus.Cancelled.ToString());
        cancelled.CancelledByRole.Should().Be("Recycler");
        cancelled.IsLateCancellation.Should().BeTrue();
    }

    [Fact]
    public async Task UTC021_GetRecyclerRequestAsync_OtherRecyclerAttemptsAccess_ThrowsNotFoundException_BrHnd03()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var created = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));
        var otherRecyclerId = Guid.NewGuid();

        var act = () => sut.GetRecyclerRequestAsync(otherRecyclerId, created.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UTC022_GetRecyclerRequestAsync_ContactAndAddressMaskedWhilePendingAndRevealedAfterAccepted_BrHnd08()
    {
        var (sut, _, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var created = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));

        var viewPending = await sut.GetRecyclerRequestAsync(recyclerUser.Id, created.Id);
        viewPending.IsContactRevealed.Should().BeFalse();
        viewPending.PickupAddress.Should().BeNull();
        viewPending.Requester!.Phone.Should().BeNull();

        await sut.AcceptAsync(recyclerUser.Id, created.Id, null);

        var viewAccepted = await sut.GetRecyclerRequestAsync(recyclerUser.Id, created.Id);
        viewAccepted.IsContactRevealed.Should().BeTrue();
        viewAccepted.PickupAddress.Should().Be("KTX Khu B, ĐH FPT Hòa Lạc, Thạch Thất, Hà Nội");
        viewAccepted.Requester!.Phone.Should().Be("0901112233");
    }

    [Fact]
    public async Task UTC023_ExpirePendingAsync_Job03_ExpiresOnlyStalePendingRequestsAndLeavesAcceptedUntouched()
    {
        var (sut, db, citizen, recyclerUser, _, plastic, _) = await CreateSeededSutAsync();
        var stalePending = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));
        var acceptedReq = await sut.CreateAsync(citizen.Id, ValidCreateRequest(recyclerUser.Id, plastic.Id, 2.0m));
        await sut.AcceptAsync(recyclerUser.Id, acceptedReq.Id, null);

        var staleEntity = await db.HandoverRequests.FirstAsync(h => h.Id == stalePending.Id);
        staleEntity.CreatedAt = DateTime.UtcNow.AddHours(-25);
        var acceptedEntity = await db.HandoverRequests.FirstAsync(h => h.Id == acceptedReq.Id);
        acceptedEntity.CreatedAt = DateTime.UtcNow.AddHours(-25);
        await db.SaveChangesAsync();

        var count = await sut.ExpirePendingAsync(TimeSpan.FromHours(24));

        count.Should().Be(1);
        (await sut.GetUserRequestAsync(citizen.Id, stalePending.Id)).Status.Should().Be(HandoverStatus.Expired.ToString());
        (await sut.GetUserRequestAsync(citizen.Id, acceptedReq.Id)).Status.Should().Be(HandoverStatus.Accepted.ToString());
    }

    [Fact]
    public async Task UTC024_GetUserRequestsAsync_InvalidStatusFilterString_ThrowsBrHnd02()
    {
        var (sut, _, citizen, _, _, _, _) = await CreateSeededSutAsync();

        var act = () => sut.GetUserRequestsAsync(citizen.Id, "InvalidStatusXYZ", 1, 10);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-HND-02");
    }
}

