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
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using Xunit;

namespace Ecolink.UnitTests.Module05_LocationDiscovery;

[Trait("Module", "Module05_LocationBasedRecyclerDiscovery")]
public class GeoMatchingUnitTests
{
    private readonly NearbyRecyclerFilterValidator _validator = new();

    private static (GeoMatchingService Sut, ApplicationDbContext Db, IWasteRuleEngineService RuleEngine) CreateSut(bool withDb = true)
    {
        var db = withDb ? TestDbContextFactory.Create() : null!;
        var ruleEngine = new WasteRuleEngineService(NullLogger<WasteRuleEngineService>.Instance);
        var sut = new GeoMatchingService(
            NullLogger<GeoMatchingService>.Instance,
            withDb ? db : null,
            ruleEngine);
        return (sut, db, ruleEngine);
    }

    [Fact]
    public void UTC001_NearbyRecyclerFilterValidator_ValidFilterRequest_PassesValidation()
    {
        var res = _validator.Validate(new NearbyRecyclerFilterRequest
        {
            Lat = 21.0124,
            Lng = 105.5253,
            RadiusKm = 10.0,
            Category = "plastic"
        });

        res.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UTC002_NearbyRecyclerFilterValidator_ZeroLatOrZeroLng_FailsWithMsgMth02()
    {
        var res = _validator.Validate(new NearbyRecyclerFilterRequest
        {
            Lat = 0,
            Lng = 0,
            RadiusKm = 10.0
        });

        res.IsValid.Should().BeFalse();
        res.Errors.Should().Contain(e => e.ErrorMessage.Contains("MSG-MTH-02"));
    }

    [Fact]
    public void UTC003_NearbyRecyclerFilterValidator_OutOfRangeLatitudeOrLongitude_FailsValidation()
    {
        var resLat = _validator.Validate(new NearbyRecyclerFilterRequest { Lat = 91.5, Lng = 105.5, RadiusKm = 10 });
        var resLng = _validator.Validate(new NearbyRecyclerFilterRequest { Lat = 21.0, Lng = -185.0, RadiusKm = 10 });

        resLat.IsValid.Should().BeFalse();
        resLng.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UTC004_NearbyRecyclerFilterValidator_BoundaryRadius05KmAnd50Km_PassesValidation()
    {
        var minRes = _validator.Validate(new NearbyRecyclerFilterRequest { Lat = 21.01, Lng = 105.52, RadiusKm = 0.5 });
        var maxRes = _validator.Validate(new NearbyRecyclerFilterRequest { Lat = 21.01, Lng = 105.52, RadiusKm = 50.0 });

        minRes.IsValid.Should().BeTrue();
        maxRes.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UTC005_NearbyRecyclerFilterValidator_BoundaryRadiusBelow05OrAbove50_FailsValidation()
    {
        var belowMin = _validator.Validate(new NearbyRecyclerFilterRequest { Lat = 21.01, Lng = 105.52, RadiusKm = 0.49 });
        var aboveMax = _validator.Validate(new NearbyRecyclerFilterRequest { Lat = 21.01, Lng = 105.52, RadiusKm = 50.1 });

        belowMin.IsValid.Should().BeFalse();
        aboveMax.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UTC006_NearbyRecyclerFilterValidator_UnsupportedCategoryTrashOrUnknown_FailsWhileCaseInsensitiveMetalPasses()
    {
        var invalidCat = _validator.Validate(new NearbyRecyclerFilterRequest { Lat = 21.01, Lng = 105.52, RadiusKm = 10, Category = "trash" });
        var validUpper = _validator.Validate(new NearbyRecyclerFilterRequest { Lat = 21.01, Lng = 105.52, RadiusKm = 10, Category = "  METAL  " });

        invalidCat.IsValid.Should().BeFalse();
        validUpper.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UTC007_FindNearestPartnersAsync_HoaLacCoordinates_ReturnsVerifiedOnlinePartnersSortedByDistanceAsc_BrMth04()
    {
        var (sut, _, _) = CreateSut();

        var list = (await sut.FindNearestPartnersAsync(21.0124, 105.5253, null, 10.0)).ToList();

        list.Should().NotBeEmpty();
        list.Should().BeInAscendingOrder(p => p.DistanceKm);
        list.All(p => p.DistanceKm <= 10.0).Should().BeTrue();
    }

    [Fact]
    public async Task UTC008_FindNearestPartnersAsync_ExcludesPendingAndRejectedFacilities_BrMth01()
    {
        var (sut, db, _) = CreateSut();
        var pendingId = Guid.NewGuid();
        var rejectedId = Guid.NewGuid();
        var cat = new WasteCategory { Id = Guid.NewGuid(), Code = "plastic", Name = "Nhựa", StandardBinColor = "Cam", IsRecyclable = true };
        db.WasteCategories.Add(cat);

        foreach (var (id, status) in new[] { (pendingId, VerificationStatus.Pending), (rejectedId, VerificationStatus.Rejected) })
        {
            var u = new User { Id = id, Username = $"u_{id:N}"[..12], Email = $"{id:N}@e.vn", FullName = "Test", Role = UserRole.Recycler, Status = UserStatus.Active };
            var r = new RecyclerProfile
            {
                UserId = id,
                User = u,
                FacilityName = $"Cơ sở {status}",
                BusinessAddress = "Hòa Lạc",
                ContactPhone = "0900000000",
                Latitude = 21.0125m,
                Longitude = 105.5254m,
                Location = new Point(105.5254, 21.0125) { SRID = 4326 },
                VerificationStatus = status,
                ActiveStatus = RecyclerActiveStatus.Online
            };
            db.Users.Add(u);
            db.RecyclerProfiles.Add(r);
            db.RecyclerPrices.Add(new RecyclerPrice { RecyclerId = id, WasteCategoryId = cat.Id, PricePerKg = 7000m, MinQuantityKg = 1m, Status = PriceStatus.Active });
        }
        await db.SaveChangesAsync();

        var list = (await sut.FindNearestPartnersAsync(21.0124, 105.5253, null, 10.0)).ToList();

        list.Should().NotContain(p => p.Id == pendingId || p.Id == rejectedId);
    }

    [Fact]
    public async Task UTC009_FindNearestPartnersAsync_ExcludesOfflineFacilitiesAndSuspendedUsers_BrMth06()
    {
        var (sut, db, _) = CreateSut();
        var offlineId = Guid.NewGuid();
        var suspendedId = Guid.NewGuid();
        var cat = new WasteCategory { Id = Guid.NewGuid(), Code = "metal", Name = "Kim loại", StandardBinColor = "Xám", IsRecyclable = true };
        db.WasteCategories.Add(cat);

        var uOffline = new User { Id = offlineId, Username = $"off_{offlineId:N}"[..12], Email = $"off_{offlineId:N}@e.vn", FullName = "Offline", Role = UserRole.Recycler, Status = UserStatus.Active };
        db.Users.Add(uOffline);
        db.RecyclerProfiles.Add(new RecyclerProfile
        {
            UserId = offlineId,
            User = uOffline,
            FacilityName = "Cơ sở Đang Offline",
            BusinessAddress = "Hòa Lạc",
            ContactPhone = "0901112222",
            Latitude = 21.0125m,
            Longitude = 105.5254m,
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Offline
        });
        db.RecyclerPrices.Add(new RecyclerPrice { RecyclerId = offlineId, WasteCategoryId = cat.Id, PricePerKg = 25000m, MinQuantityKg = 1m, Status = PriceStatus.Active });

        var uSusp = new User { Id = suspendedId, Username = $"sus_{suspendedId:N}"[..12], Email = $"sus_{suspendedId:N}@e.vn", FullName = "Suspended", Role = UserRole.Recycler, Status = UserStatus.Suspended };
        db.Users.Add(uSusp);
        db.RecyclerProfiles.Add(new RecyclerProfile
        {
            UserId = suspendedId,
            User = uSusp,
            FacilityName = "Cơ sở Bị Đình Chỉ",
            BusinessAddress = "Hòa Lạc",
            ContactPhone = "0901113333",
            Latitude = 21.0125m,
            Longitude = 105.5254m,
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        });
        db.RecyclerPrices.Add(new RecyclerPrice { RecyclerId = suspendedId, WasteCategoryId = cat.Id, PricePerKg = 25000m, MinQuantityKg = 1m, Status = PriceStatus.Active });
        await db.SaveChangesAsync();

        var list = (await sut.FindNearestPartnersAsync(21.0124, 105.5253, null, 10.0)).ToList();

        list.Should().NotContain(p => p.Id == offlineId || p.Id == suspendedId);
    }

    [Fact]
    public async Task UTC010_FindNearestPartnersAsync_CategoryFilterGlass_ReturnsOnlyFacilitiesWithActiveGlassPrice_BrMth07()
    {
        var (sut, _, _) = CreateSut();

        var list = (await sut.FindNearestPartnersAsync(21.0124, 105.5253, "glass", 10.0)).ToList();

        list.Should().NotBeEmpty();
        list.Should().OnlyContain(p => p.Materials.Any(m => m.CategoryCode == "glass"));
    }

    [Fact]
    public async Task UTC011_FindNearestPartnersAsync_ClampsRadiusBetween05KmAnd50Km()
    {
        var (sut, _, _) = CreateSut();

        // Truyền radiusKm = 0.01 (sẽ được clamp lên 0.5km) tại đúng tọa độ cơ sở Hòa Lạc 1 (21.0182, 105.5345)
        var exactHit = (await sut.FindNearestPartnersAsync(21.0182, 105.5345, null, 0.01)).ToList();
        exactHit.Should().Contain(p => p.DistanceKm == 0);

        // Truyền radiusKm = 500 (sẽ được clamp xuống 50km) -> không lấy các cơ sở TP.HCM cách > 1000km
        var clampedMax = (await sut.FindNearestPartnersAsync(21.0182, 105.5345, null, 500.0)).ToList();
        clampedMax.Should().OnlyContain(p => p.DistanceKm <= 50.0);
    }

    [Fact]
    public async Task UTC012_FindNearestPartnersAsync_HaversineExactSameCoordinates_ComputesZeroDistanceKm()
    {
        var (sut, _, _) = CreateSut();

        var list = (await sut.FindNearestPartnersAsync(21.0182, 105.5345, null, 5.0)).ToList();

        list.First().DistanceKm.Should().Be(0.0);
    }

    [Fact]
    public async Task UTC013_SearchNearbyRecyclersAsync_KeywordFilterMinhTam_FiltersByFacilityNameOrAddress()
    {
        var (sut, _, _) = CreateSut();

        var res = await sut.SearchNearbyRecyclersAsync(new NearbyRecyclerFilterRequest
        {
            Lat = 21.0124,
            Lng = 105.5253,
            RadiusKm = 10.0,
            Keyword = "Minh Tâm"
        });

        res.TotalFound.Should().BeGreaterThanOrEqualTo(1);
        res.Items.Should().OnlyContain(p => p.FacilityName.Contains("Minh Tâm") || p.Address.Contains("Minh Tâm"));
        res.FallbackSuggestion.Should().BeNull();
    }

    [Fact]
    public async Task UTC014_SearchNearbyRecyclersAsync_SortByPriceDescWithCategoryMetal_OrdersByHighestMetalPriceFirst()
    {
        var (sut, _, _) = CreateSut();

        var res = await sut.SearchNearbyRecyclersAsync(new NearbyRecyclerFilterRequest
        {
            Lat = 21.0124,
            Lng = 105.5253,
            RadiusKm = 15.0,
            Category = "metal",
            SortBy = "price_desc"
        });

        res.Items.Count.Should().BeGreaterThanOrEqualTo(2);
        var metalPrices = res.Items
            .Select(p => p.Materials.First(m => m.CategoryCode == "metal").PricePerKg)
            .ToList();
        metalPrices.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task UTC015_SearchNearbyRecyclersAsync_ZeroResultsInRadius_PopulatesFallbackSuggestionAndSortingGuide()
    {
        var (sut, _, _) = CreateSut();

        // Tọa độ ngoài biển Đông cách xa mọi cơ sở thu gom
        var res = await sut.SearchNearbyRecyclersAsync(new NearbyRecyclerFilterRequest
        {
            Lat = 15.0000,
            Lng = 112.0000,
            RadiusKm = 4.0,
            Category = "paper"
        });

        res.TotalFound.Should().Be(0);
        res.Items.Should().BeEmpty();
        res.FallbackSuggestion.Should().NotBeNull();
        res.FallbackSuggestion!.SuggestedExpandedRadiusKm.Should().Be(10.0); // 4.0 * 2.5
        res.FallbackSuggestion.AllowCommunityPickupPost.Should().BeTrue();
        res.FallbackSuggestion.PublicBinSortingGuide.Should().NotBeNull();
        res.FallbackSuggestion.PublicBinSortingGuide!.CategoryCode.Should().Be("paper");
    }

    [Fact]
    public async Task UTC016_SearchNearbyRecyclersAsync_ZeroResultsWithLargeRadius_CapsSuggestedExpandedRadiusAt50Km()
    {
        var (sut, _, _) = CreateSut();

        var res = await sut.SearchNearbyRecyclersAsync(new NearbyRecyclerFilterRequest
        {
            Lat = 15.0000,
            Lng = 112.0000,
            RadiusKm = 30.0
        });

        res.TotalFound.Should().Be(0);
        res.FallbackSuggestion.Should().NotBeNull();
        res.FallbackSuggestion!.SuggestedExpandedRadiusKm.Should().Be(50.0); // min(50, 30 * 2.5)
    }

    [Fact]
    public async Task UTC017_GetRecyclerPublicDetailAsync_ValidVerifiedRecyclerWithUserCoords_ReturnsDetailAndDistanceKm()
    {
        var (sut, _, _) = CreateSut();
        var hoaLac1Id = Guid.Parse("99999991-9999-9999-9999-999999999991");

        var detail = await sut.GetRecyclerPublicDetailAsync(hoaLac1Id, 21.0124, 105.5253);

        detail.Id.Should().Be(hoaLac1Id);
        detail.FacilityName.Should().Contain("Hòa Lạc");
        detail.DistanceKm.Should().BeGreaterThan(0);
        detail.Materials.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UTC018_GetRecyclerPublicDetailAsync_NonExistentRecyclerId_ThrowsNotFoundException()
    {
        var (sut, _, _) = CreateSut();

        var act = () => sut.GetRecyclerPublicDetailAsync(Guid.NewGuid(), 21.0124, 105.5253);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UTC019_FindNearestPartnersAsync_WithoutDatabaseConnection_UsesInMemoryMockPartnersFallback()
    {
        var (sut, _, _) = CreateSut(withDb: false);

        var list = (await sut.FindNearestPartnersAsync(21.0124, 105.5253, "plastic", 10.0)).ToList();

        list.Should().NotBeEmpty();
        list.Should().BeInAscendingOrder(p => p.DistanceKm);
        list.Should().OnlyContain(p => p.Materials.Any(m => m.CategoryCode == "plastic"));
    }

    [Fact]
    public async Task UTC020_GetRecyclerPublicDetailAsync_WithoutUserCoordinates_ReturnsZeroDistanceKmAndMaterials()
    {
        var (sut, _, _) = CreateSut(withDb: false);
        var hoaLac2Id = Guid.Parse("99999992-9999-9999-9999-999999999992");

        var detail = await sut.GetRecyclerPublicDetailAsync(hoaLac2Id, null, null);

        detail.Id.Should().Be(hoaLac2Id);
        detail.DistanceKm.Should().Be(0.0);
        detail.Materials.Should().NotBeEmpty();
    }
}

