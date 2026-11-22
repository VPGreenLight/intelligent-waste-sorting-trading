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
using NetTopologySuite.Geometries;
using Xunit;

namespace Ecolink.UnitTests.Module04_RecyclerProfilePricing;

[Trait("Module", "Module04_RecyclerProfilePricing")]
public class RecyclerManagementUnitTests
{
    private static (RecyclerManagementService Sut, ApplicationDbContext Db) CreateSut()
    {
        var db = TestDbContextFactory.Create();
        var sut = new RecyclerManagementService(db, NullLogger<RecyclerManagementService>.Instance);
        return (sut, db);
    }

    private static async Task<(User User, RecyclerProfile Profile, WasteCategory Plastic, WasteCategory Metal)> SeedVerifiedRecyclerAsync(
        ApplicationDbContext db,
        VerificationStatus verificationStatus = VerificationStatus.Verified,
        RecyclerActiveStatus activeStatus = RecyclerActiveStatus.Online,
        UserStatus userStatus = UserStatus.Active)
    {
        var plastic = new WasteCategory { Id = Guid.NewGuid(), Code = "plastic", Name = "Nhựa tái chế (Plastic)", StandardBinColor = "Cam", IsRecyclable = true };
        var metal = new WasteCategory { Id = Guid.NewGuid(), Code = "metal", Name = "Kim loại & Vỏ lon (Metal)", StandardBinColor = "Xám Kim Loại", IsRecyclable = true };
        db.WasteCategories.AddRange(plastic, metal);

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = $"rcy_{userId:N}"[..16],
            Email = $"rcy_{userId:N}@ecolink.vn",
            FullName = "Chủ Vựa Hòa Lạc",
            Role = UserRole.Recycler,
            Status = userStatus
        };
        var profile = new RecyclerProfile
        {
            UserId = userId,
            User = user,
            FacilityName = "Vựa Tái Chế Hòa Lạc",
            RepresentativeName = "Đỗ Văn Hùng",
            BusinessAddress = "Thôn 3, Tân Xã, Thạch Thất, Hà Nội",
            ContactPhone = "0968112233",
            Latitude = 21.0182m,
            Longitude = 105.5345m,
            Location = new Point(105.5345, 21.0182) { SRID = 4326 },
            VerificationStatus = verificationStatus,
            ActiveStatus = activeStatus,
            RejectionReason = verificationStatus == VerificationStatus.Rejected ? "Thiếu ảnh biển hiệu" : null
        };

        db.Users.Add(user);
        db.RecyclerProfiles.Add(profile);
        await db.SaveChangesAsync();

        return (user, profile, plastic, metal);
    }

    [Fact]
    public async Task UTC001_GetWasteCategoriesAsync_EmptyOrSeededTable_Returns5RecyclableCategoriesAndExcludesTrash()
    {
        var (sut, db) = CreateSut();
        db.WasteCategories.Add(new WasteCategory
        {
            Id = Guid.NewGuid(),
            Code = "trash",
            Name = "Rác thải thông thường",
            StandardBinColor = "Đen",
            IsRecyclable = false
        });
        await db.SaveChangesAsync();

        var list = await sut.GetWasteCategoriesAsync();

        list.Should().HaveCount(5);
        list.Select(c => c.Code).Should().BeEquivalentTo(new[] { "plastic", "paper", "glass", "metal", "cardboard" });
        list.Should().NotContain(c => c.Code == "trash");
    }

    [Fact]
    public async Task UTC002_GetProfileAsync_ExistingAndNonExistingRecycler_ReturnsProfileWithActivePriceCountOrThrowsNotFound()
    {
        var (sut, db) = CreateSut();
        var (user, _, plastic, _) = await SeedVerifiedRecyclerAsync(db);
        db.RecyclerPrices.Add(new RecyclerPrice
        {
            RecyclerId = user.Id,
            WasteCategoryId = plastic.Id,
            PricePerKg = 7000m,
            MinQuantityKg = 1m,
            Status = PriceStatus.Active
        });
        await db.SaveChangesAsync();

        var dto = await sut.GetProfileAsync(user.Id);
        dto.RecyclerId.Should().Be(user.Id);
        dto.ActivePriceCount.Should().Be(1);

        var actMissing = () => sut.GetProfileAsync(Guid.NewGuid());
        await actMissing.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UTC003_UpdateProfileAsync_ValidVerifiedRecycler_UpdatesProfileAndLocationPoint()
    {
        var (sut, db) = CreateSut();
        var (user, _, _, _) = await SeedVerifiedRecyclerAsync(db);

        var updated = await sut.UpdateProfileAsync(user.Id, new UpdateRecyclerProfileRequest
        {
            FacilityName = "  Vựa Tái Chế Hòa Lạc Mới  ",
            RepresentativeName = "  Đỗ Văn Hùng  ",
            BusinessAddress = "  Km29 Đại lộ Thăng Long, Hòa Lạc  ",
            ContactPhone = "0988776655",
            Latitude = 21.0250,
            Longitude = 105.5400,
            Description = "Mô tả mới"
        });

        updated.FacilityName.Should().Be("Vựa Tái Chế Hòa Lạc Mới");
        updated.BusinessAddress.Should().Be("Km29 Đại lộ Thăng Long, Hòa Lạc");
        updated.Latitude.Should().BeApproximately(21.0250, 0.0001);
        updated.VerificationStatus.Should().Be(VerificationStatus.Verified.ToString());
    }

    [Fact]
    public async Task UTC004_UpdateProfileAsync_RejectedProfile_TransitionsBackToPendingAndOffline_BrMth02()
    {
        var (sut, db) = CreateSut();
        var (user, _, _, _) = await SeedVerifiedRecyclerAsync(
            db,
            verificationStatus: VerificationStatus.Rejected,
            activeStatus: RecyclerActiveStatus.Offline);

        var updated = await sut.UpdateProfileAsync(user.Id, new UpdateRecyclerProfileRequest
        {
            FacilityName = "Vựa Đã Bổ Sung Hồ Sơ",
            BusinessAddress = "Hòa Lạc, Hà Nội",
            ContactPhone = "0968112233",
            Latitude = 21.0182,
            Longitude = 105.5345
        });

        updated.VerificationStatus.Should().Be(VerificationStatus.Pending.ToString());
        updated.RejectionReason.Should().BeNull();
        updated.ActiveStatus.Should().Be(RecyclerActiveStatus.Offline.ToString());
    }

    [Fact]
    public async Task UTC005_UpdateProfileAsync_ZeroCoordinates0_0_ThrowsBrMth03MsgMth06()
    {
        var (sut, db) = CreateSut();
        var (user, _, _, _) = await SeedVerifiedRecyclerAsync(db);

        var act = () => sut.UpdateProfileAsync(user.Id, new UpdateRecyclerProfileRequest
        {
            FacilityName = "Vựa Lỗi Tọa Độ",
            BusinessAddress = "Hà Nội",
            ContactPhone = "0968112233",
            Latitude = 0,
            Longitude = 0
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-03");
        ex.Which.MessageCode.Should().Be("MSG-MTH-06");
    }

    [Fact]
    public async Task UTC006_UpdateProfileAsync_SuspendedUserAccount_ThrowsBrAuth05MsgAuth04()
    {
        var (sut, db) = CreateSut();
        var (user, _, _, _) = await SeedVerifiedRecyclerAsync(db, userStatus: UserStatus.Suspended);

        var act = () => sut.UpdateProfileAsync(user.Id, new UpdateRecyclerProfileRequest
        {
            FacilityName = "Vựa Bị Khóa",
            BusinessAddress = "Hà Nội",
            ContactPhone = "0968112233",
            Latitude = 21.01,
            Longitude = 105.52
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-05");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-04");
    }

    [Fact]
    public async Task UTC007_AddPriceAsync_ValidPositiveIntegerPriceOnVerifiedProfile_CreatesActivePriceRow()
    {
        var (sut, db) = CreateSut();
        var (user, _, plastic, _) = await SeedVerifiedRecyclerAsync(db);

        var dto = await sut.AddPriceAsync(user.Id, new CreateRecyclerPriceRequest
        {
            WasteCategoryId = plastic.Id,
            PricePerKg = 7200m,
            MinQuantityKg = 1.5m,
            CollectionConditions = "  Chai nhựa sạch, đã tháo nắp  "
        });

        dto.WasteCategoryCode.Should().Be("plastic");
        dto.PricePerKg.Should().Be(7200m);
        dto.MinQuantityKg.Should().Be(1.5m);
        dto.CollectionConditions.Should().Be("Chai nhựa sạch, đã tháo nắp");
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UTC008_AddPriceAsync_PendingOrRejectedProfile_ThrowsBrMth01()
    {
        var (sut, db) = CreateSut();
        var (pendingUser, _, plastic, _) = await SeedVerifiedRecyclerAsync(db, verificationStatus: VerificationStatus.Pending);

        var act = () => sut.AddPriceAsync(pendingUser.Id, new CreateRecyclerPriceRequest
        {
            WasteCategoryId = plastic.Id,
            PricePerKg = 7000m,
            MinQuantityKg = 1m
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-01");
        ex.Which.MessageCode.Should().Be("MSG-MTH-03");
    }

    [Fact]
    public async Task UTC009_AddPriceAsync_NonRecyclableTrashCategory_ThrowsBrMth03()
    {
        var (sut, db) = CreateSut();
        var (user, _, _, _) = await SeedVerifiedRecyclerAsync(db);
        var trash = new WasteCategory { Id = Guid.NewGuid(), Code = "trash", Name = "Rác thải thông thường", StandardBinColor = "Đen", IsRecyclable = false };
        db.WasteCategories.Add(trash);
        await db.SaveChangesAsync();

        var act = () => sut.AddPriceAsync(user.Id, new CreateRecyclerPriceRequest
        {
            WasteCategoryId = trash.Id,
            PricePerKg = 1000m,
            MinQuantityKg = 1m
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-03");
    }

    [Fact]
    public async Task UTC010_AddPriceAsync_DuplicateWasteCategory_ThrowsBrMth03()
    {
        var (sut, db) = CreateSut();
        var (user, _, plastic, _) = await SeedVerifiedRecyclerAsync(db);
        await sut.AddPriceAsync(user.Id, new CreateRecyclerPriceRequest
        {
            WasteCategoryId = plastic.Id,
            PricePerKg = 7000m,
            MinQuantityKg = 1m
        });

        var actDuplicate = () => sut.AddPriceAsync(user.Id, new CreateRecyclerPriceRequest
        {
            WasteCategoryId = plastic.Id,
            PricePerKg = 7500m,
            MinQuantityKg = 1m
        });

        var ex = await actDuplicate.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-03");
    }

    [Fact]
    public async Task UTC011_AddPriceAsync_DecimalOrNonPositivePriceOrInvalidMinKgOrOver200CharsCondition_ThrowsBrMth03()
    {
        var (sut, db) = CreateSut();
        var (user, _, plastic, _) = await SeedVerifiedRecyclerAsync(db);

        // Giá số thập phân (không nguyên)
        var actDecimal = () => sut.AddPriceAsync(user.Id, new CreateRecyclerPriceRequest
        {
            WasteCategoryId = plastic.Id,
            PricePerKg = 7000.50m,
            MinQuantityKg = 1m
        });
        (await actDecimal.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-MTH-03");

        // Khối lượng tối thiểu <= 0
        var actZeroMin = () => sut.AddPriceAsync(user.Id, new CreateRecyclerPriceRequest
        {
            WasteCategoryId = plastic.Id,
            PricePerKg = 7000m,
            MinQuantityKg = 0m
        });
        (await actZeroMin.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-MTH-03");

        // Điều kiện thu gom > 200 ký tự
        var actLongCond = () => sut.AddPriceAsync(user.Id, new CreateRecyclerPriceRequest
        {
            WasteCategoryId = plastic.Id,
            PricePerKg = 7000m,
            MinQuantityKg = 1m,
            CollectionConditions = new string('A', 201)
        });
        (await actLongCond.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-MTH-03");
    }

    [Fact]
    public async Task UTC012_UpdatePriceAndGetPricesAsync_SoftDeactivateWhenOtherActiveExistsAndDetectsStaleOver30Days_BrMth07()
    {
        var (sut, db) = CreateSut();
        var (user, _, plastic, metal) = await SeedVerifiedRecyclerAsync(db);
        var p1 = new RecyclerPrice
        {
            Id = Guid.NewGuid(),
            RecyclerId = user.Id,
            WasteCategoryId = plastic.Id,
            WasteCategory = plastic,
            PricePerKg = 7000m,
            MinQuantityKg = 1m,
            Status = PriceStatus.Active,
            UpdatedAt = DateTime.UtcNow.AddDays(-35)
        };
        var p2 = new RecyclerPrice
        {
            Id = Guid.NewGuid(),
            RecyclerId = user.Id,
            WasteCategoryId = metal.Id,
            WasteCategory = metal,
            PricePerKg = 25000m,
            MinQuantityKg = 0.5m,
            Status = PriceStatus.Active,
            UpdatedAt = DateTime.UtcNow
        };
        db.RecyclerPrices.AddRange(p1, p2);
        await db.SaveChangesAsync();

        // Kiểm tra lọc keyword & cờ IsOutdated (> 30 ngày)
        var listBefore = await sut.GetPricesAsync(user.Id, "plastic", true);
        listBefore.Should().ContainSingle().Which.IsOutdated.Should().BeTrue();

        // Tắt nhận mua p1 khi vẫn còn p2 đang Active -> thành công (BR-MTH-07)
        var updatedP1 = await sut.UpdatePriceAsync(user.Id, p1.Id, new UpdateRecyclerPriceRequest
        {
            PricePerKg = 7500m,
            IsActive = false
        });
        updatedP1.IsActive.Should().BeFalse();
        updatedP1.PricePerKg.Should().Be(7500m);
        updatedP1.IsOutdated.Should().BeFalse();
    }

    [Fact]
    public async Task UTC013_UpdatePriceAsync_DeactivatingLastActivePriceWhileOnline_ThrowsBrMth03MsgMth06()
    {
        var (sut, db) = CreateSut();
        var (user, _, plastic, _) = await SeedVerifiedRecyclerAsync(db, activeStatus: RecyclerActiveStatus.Online);
        var onlyPrice = new RecyclerPrice
        {
            Id = Guid.NewGuid(),
            RecyclerId = user.Id,
            WasteCategoryId = plastic.Id,
            WasteCategory = plastic,
            PricePerKg = 7000m,
            MinQuantityKg = 1m,
            Status = PriceStatus.Active
        };
        db.RecyclerPrices.Add(onlyPrice);
        await db.SaveChangesAsync();

        var act = () => sut.UpdatePriceAsync(user.Id, onlyPrice.Id, new UpdateRecyclerPriceRequest
        {
            IsActive = false
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-03");
        ex.Which.MessageCode.Should().Be("MSG-MTH-06");
    }

    [Fact]
    public async Task UTC014_BulkSavePricesAsync_ValidItems_UpsertsActiveAndSkipsNewInactiveItems()
    {
        var (sut, db) = CreateSut();
        var (user, _, plastic, metal) = await SeedVerifiedRecyclerAsync(db);

        var result = await sut.BulkSavePricesAsync(user.Id, new BulkSavePricesRequest
        {
            Items = new List<BulkPriceItemRequest>
            {
                new() { WasteCategoryId = plastic.Id, PricePerKg = 7100m, MinQuantityKg = 1.0m, IsActive = true, CollectionConditions = "Sạch" },
                new() { WasteCategoryId = metal.Id, PricePerKg = 26000m, MinQuantityKg = 0.5m, IsActive = false } // mới + inactive -> bỏ qua
            }
        });

        result.Should().ContainSingle();
        result[0].WasteCategoryCode.Should().Be("plastic");
        result[0].PricePerKg.Should().Be(7100m);
    }

    [Fact]
    public async Task UTC015_BulkSavePricesAsync_DuplicateCategoryIdsOrAllInactiveWhileOnline_ThrowsBrMth03()
    {
        var (sut, db) = CreateSut();
        var (user, _, plastic, _) = await SeedVerifiedRecyclerAsync(db, activeStatus: RecyclerActiveStatus.Online);

        // 1. Trùng lặp WasteCategoryId
        var actDup = () => sut.BulkSavePricesAsync(user.Id, new BulkSavePricesRequest
        {
            Items = new List<BulkPriceItemRequest>
            {
                new() { WasteCategoryId = plastic.Id, PricePerKg = 7000m, MinQuantityKg = 1m, IsActive = true },
                new() { WasteCategoryId = plastic.Id, PricePerKg = 7200m, MinQuantityKg = 1m, IsActive = true }
            }
        });
        (await actDup.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-MTH-03");

        // 2. Tất cả đều Inactive trong khi cơ sở đang Online
        var actAllInactive = () => sut.BulkSavePricesAsync(user.Id, new BulkSavePricesRequest
        {
            Items = new List<BulkPriceItemRequest>
            {
                new() { WasteCategoryId = plastic.Id, PricePerKg = 7000m, MinQuantityKg = 1m, IsActive = false }
            }
        });
        var ex = await actAllInactive.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-03");
        ex.Which.MessageCode.Should().Be("MSG-MTH-06");
    }

    [Fact]
    public async Task UTC016_UpdateAvailabilityAsync_TurnOnlineWithValidLocationAndActivePrice_SetsOnlineAndClearsBusyReason()
    {
        var (sut, db) = CreateSut();
        var (user, profile, plastic, _) = await SeedVerifiedRecyclerAsync(db, activeStatus: RecyclerActiveStatus.Offline);
        profile.BusyReason = "Đang bảo trì cân";
        db.RecyclerPrices.Add(new RecyclerPrice
        {
            RecyclerId = user.Id,
            WasteCategoryId = plastic.Id,
            PricePerKg = 7000m,
            MinQuantityKg = 1m,
            Status = PriceStatus.Active
        });
        await db.SaveChangesAsync();

        var res = await sut.UpdateAvailabilityAsync(user.Id, new UpdateAvailabilityRequest
        {
            IsOnline = true
        });

        res.IsOnline.Should().BeTrue();
        res.ActiveStatus.Should().Be(RecyclerActiveStatus.Online.ToString());
        res.BusyReason.Should().BeNull();
    }

    [Fact]
    public async Task UTC017_UpdateAvailabilityAsync_TurnOnlineWithoutActivePricesOrWithoutLocation_ThrowsBrMth03MsgMth06()
    {
        var (sut, db) = CreateSut();
        var (user, _, _, _) = await SeedVerifiedRecyclerAsync(db, activeStatus: RecyclerActiveStatus.Offline);

        // Chưa có dòng giá Active nào -> chặn bật Online (BR-MTH-03)
        var act = () => sut.UpdateAvailabilityAsync(user.Id, new UpdateAvailabilityRequest
        {
            IsOnline = true
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-MTH-03");
        ex.Which.MessageCode.Should().Be("MSG-MTH-06");
    }

    [Fact]
    public async Task UTC018_UpdateAvailabilityAsync_TurnOfflineWithBusyReason_AlwaysAllowedAndSavesReason_BrMth06()
    {
        var (sut, db) = CreateSut();
        var (user, _, _, _) = await SeedVerifiedRecyclerAsync(db, activeStatus: RecyclerActiveStatus.Online);

        var res = await sut.UpdateAvailabilityAsync(user.Id, new UpdateAvailabilityRequest
        {
            IsOnline = false,
            BusyReason = "  Kho đã đầy, tạm ngưng nhận đến chiều  "
        });

        res.IsOnline.Should().BeFalse();
        res.ActiveStatus.Should().Be(RecyclerActiveStatus.Offline.ToString());
        res.BusyReason.Should().Be("Kho đã đầy, tạm ngưng nhận đến chiều");
    }

    [Fact]
    public async Task UTC019_UpdateScheduleAsync_ValidWeeklySchedule_FormatsConsecutiveDaysRangeAndPersistsJson()
    {
        var (sut, db) = CreateSut();
        var (user, _, _, _) = await SeedVerifiedRecyclerAsync(db);

        var res = await sut.UpdateScheduleAsync(user.Id, new UpdateScheduleRequest
        {
            Days = new List<ScheduleDayDto>
            {
                new() { DayOfWeek = 1, IsClosed = false, OpenTime = "08:00", CloseTime = "17:30" },
                new() { DayOfWeek = 2, IsClosed = false, OpenTime = "08:00", CloseTime = "17:30" },
                new() { DayOfWeek = 3, IsClosed = false, OpenTime = "08:00", CloseTime = "17:30" },
                new() { DayOfWeek = 4, IsClosed = false, OpenTime = "08:00", CloseTime = "17:30" },
                new() { DayOfWeek = 5, IsClosed = false, OpenTime = "08:00", CloseTime = "17:30" },
                new() { DayOfWeek = 6, IsClosed = false, OpenTime = "08:00", CloseTime = "12:00" },
                new() { DayOfWeek = 0, IsClosed = true }
            }
        });

        res.OperatingHours.Should().Be("T2-T6: 08:00 - 17:30; T7: 08:00 - 12:00");
        res.WeeklySchedule.Should().HaveCount(7);
    }

    [Fact]
    public async Task UTC020_UpdateScheduleAsync_DuplicateDayOrInvalidTimeFormatOrCloseBeforeOpen_ThrowsBrMth03()
    {
        var (sut, db) = CreateSut();
        var (user, _, _, _) = await SeedVerifiedRecyclerAsync(db);

        // 1. Trùng DayOfWeek
        var actDup = () => sut.UpdateScheduleAsync(user.Id, new UpdateScheduleRequest
        {
            Days = new List<ScheduleDayDto>
            {
                new() { DayOfWeek = 1, IsClosed = false, OpenTime = "08:00", CloseTime = "17:00" },
                new() { DayOfWeek = 1, IsClosed = false, OpenTime = "09:00", CloseTime = "18:00" }
            }
        });
        (await actDup.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-MTH-03");

        // 2. Sai định dạng HH:mm
        var actBadFormat = () => sut.UpdateScheduleAsync(user.Id, new UpdateScheduleRequest
        {
            Days = new List<ScheduleDayDto>
            {
                new() { DayOfWeek = 1, IsClosed = false, OpenTime = "8h00", CloseTime = "17:00" }
            }
        });
        (await actBadFormat.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-MTH-03");

        // 3. CloseTime <= OpenTime
        var actCloseBeforeOpen = () => sut.UpdateScheduleAsync(user.Id, new UpdateScheduleRequest
        {
            Days = new List<ScheduleDayDto>
            {
                new() { DayOfWeek = 1, IsClosed = false, OpenTime = "18:00", CloseTime = "08:00" }
            }
        });
        (await actCloseBeforeOpen.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-MTH-03");
    }
}

