using Ecolink.Application.Dtos;
using Ecolink.Application.Validators;
using FluentAssertions;
using Xunit;
using static Ecolink.UnitTests.Validators.ValidatorAssert;

namespace Ecolink.UnitTests.Validators;

[Trait("Layer", "Unit")]
[Trait("Area", "Validators")]
public class RecyclerReviewPackageWasteValidatorTests
{
    // ═════════ Recycler onboarding ═════════
    private readonly ApplyRecyclerUpgradeValidator _apply = new();

    private static ApplyRecyclerUpgradeRequest Apply() => new()
    {
        FacilityName = "Co so A", RepresentativeName = "Nguyen B", BusinessAddress = "1 Le Loi",
        Latitude = 10.77, Longitude = 106.69, ContactPhone = "0912345678",
        LegalPermitUrl = "https://x/permit.jpg", BusinessPhotoUrl = "https://x/photo.jpg"
    };

    [Fact] public void ApplyUpgrade_Valid_Passes() => IsValid(_apply, Apply()).Should().BeTrue();

    [Theory]
    [InlineData(0.0, true)]          // 0 = chưa chọn tọa độ
    [InlineData(0.0001, false)]
    [InlineData(90.0, false)]
    [InlineData(90.01, true)]
    [InlineData(-90.0, false)]
    public void ApplyUpgrade_Latitude(double v, bool expectError)
    {
        var m = Apply(); m.Latitude = v;
        Has(_apply, m, nameof(m.Latitude)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(180.0, false)]
    [InlineData(-180.0, false)]
    [InlineData(180.01, true)]
    public void ApplyUpgrade_Longitude(double v, bool expectError)
    {
        var m = Apply(); m.Longitude = v;
        Has(_apply, m, nameof(m.Longitude)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("0912345678", false)]
    [InlineData("+84912345678", false)]
    [InlineData("091234567", false)]      // 0 + 8 chữ số: cho phép (8–10)
    [InlineData("09123456", true)]        // 0 + 7 chữ số
    [InlineData("091234567890", true)]    // 0 + 11 chữ số
    [InlineData("84912345678", true)]
    [InlineData("abc", true)]
    public void ApplyUpgrade_ContactPhone(string phone, bool expectError)
    {
        var m = Apply(); m.ContactPhone = phone;
        Has(_apply, m, nameof(m.ContactPhone)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(150, false)]
    [InlineData(151, true)]
    [InlineData(0, true)]
    public void ApplyUpgrade_FacilityName(int len, bool expectError)
    {
        var m = Apply(); m.FacilityName = Str(len);
        Has(_apply, m, nameof(m.FacilityName)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("https://x/a.jpg", false)]
    public void ApplyUpgrade_Documents_Required(string url, bool expectError)
    {
        var m = Apply(); m.LegalPermitUrl = url; m.BusinessPhotoUrl = url;
        Has(_apply, m, nameof(m.LegalPermitUrl)).Should().Be(expectError);
        Has(_apply, m, nameof(m.BusinessPhotoUrl)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(true, "", true)]
    [InlineData(false, null, false)]
    [InlineData(false, "", false)]
    [InlineData(false, "ngắn", false)]            // < 10 ký tự
    [InlineData(false, "123456789", false)]       // 9 ký tự
    [InlineData(false, "1234567890", true)]       // 10 ký tự
    public void VerifyRecycler_RejectionReason_RequiredWhenRejected(bool approved, string? reason, bool valid)
        => IsValid(new VerifyRecyclerApplicationValidator(), new VerifyRecyclerApplicationRequest { IsApproved = approved, RejectionReason = reason }).Should().Be(valid);

    // ═════════ Recycler profile / pricing ═════════
    private readonly UpdateRecyclerProfileRequestValidator _prof = new();

    private static UpdateRecyclerProfileRequest Prof() => new()
    { FacilityName = "Co so A", BusinessAddress = "1 Le Loi", Latitude = 10, Longitude = 106, ContactPhone = "0912345678" };

    [Fact] public void Profile_Valid_Passes() => IsValid(_prof, Prof()).Should().BeTrue();

    [Theory]
    [InlineData(20, false)]
    [InlineData(21, true)]
    [InlineData(0, true)]
    public void Profile_ContactPhone_Max20(int len, bool expectError)
    {
        var m = Prof(); m.ContactPhone = Str(len, '1');
        Has(_prof, m, nameof(m.ContactPhone)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void Profile_OperatingHours_Max100(int len, bool expectError)
    {
        var m = Prof(); m.OperatingHours = Str(len);
        Has(_prof, m, nameof(m.OperatingHours)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(91, true)]
    [InlineData(-91, true)]
    [InlineData(0, false)]
    public void Profile_Latitude(double v, bool expectError)
    {
        var m = Prof(); m.Latitude = v;
        Has(_prof, m, nameof(m.Latitude)).Should().Be(expectError);
    }

    // ───── CreateRecyclerPriceRequestValidator ─────
    private readonly CreateRecyclerPriceRequestValidator _cp = new();
    private static CreateRecyclerPriceRequest CP() => new() { WasteCategoryId = Guid.NewGuid(), PricePerKg = 5000m, MinQuantityKg = 1m };

    [Fact] public void CreatePrice_Valid_Passes() => IsValid(_cp, CP()).Should().BeTrue();

    [Fact]
    public void CreatePrice_NullCategory_Fails()
    {
        var m = CP(); m.WasteCategoryId = null;
        Has(_cp, m, nameof(m.WasteCategoryId)).Should().BeTrue();
    }

    [Fact]
    public void CreatePrice_EmptyGuidCategory_Fails()
    {
        var m = CP(); m.WasteCategoryId = Guid.Empty;
        Has(_cp, m, nameof(m.WasteCategoryId)).Should().BeTrue();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("0", true)]
    [InlineData("-5", true)]
    [InlineData("1", false)]
    [InlineData("5000", false)]
    [InlineData("5000.5", true)]     // phải là số nguyên
    [InlineData("5000.00", false)]
    public void CreatePrice_PricePerKg_PositiveInteger(string? price, bool expectError)
    {
        var m = CP();
        m.PricePerKg = price == null ? null : decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);
        Has(_cp, m, nameof(m.PricePerKg)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("0.1", false)]
    [InlineData("-1", true)]
    public void CreatePrice_MinQuantity_GreaterThanZero(string q, bool expectError)
    {
        var m = CP(); m.MinQuantityKg = decimal.Parse(q, System.Globalization.CultureInfo.InvariantCulture);
        Has(_cp, m, nameof(m.MinQuantityKg)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(200, false)]
    [InlineData(201, true)]
    public void CreatePrice_Conditions_Max200(int len, bool expectError)
    {
        var m = CP(); m.CollectionConditions = Str(len);
        Has(_cp, m, nameof(m.CollectionConditions)).Should().Be(expectError);
    }

    // ───── UpdateRecyclerPriceRequestValidator (tất cả optional) ─────
    private readonly UpdateRecyclerPriceRequestValidator _up = new();

    [Fact] public void UpdatePrice_AllNull_Passes() => IsValid(_up, new UpdateRecyclerPriceRequest()).Should().BeTrue();

    [Theory]
    [InlineData("0", true)]
    [InlineData("-1", true)]
    [InlineData("100", false)]
    [InlineData("100.5", true)]
    public void UpdatePrice_PricePerKg_WhenProvided(string price, bool expectError)
    {
        var m = new UpdateRecyclerPriceRequest { PricePerKg = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture) };
        Has(_up, m, nameof(m.PricePerKg)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("0.5", false)]
    public void UpdatePrice_MinQuantity_WhenProvided(string q, bool expectError)
    {
        var m = new UpdateRecyclerPriceRequest { MinQuantityKg = decimal.Parse(q, System.Globalization.CultureInfo.InvariantCulture) };
        Has(_up, m, nameof(m.MinQuantityKg)).Should().Be(expectError);
    }

    // ───── BulkSavePricesRequestValidator ─────
    private readonly BulkSavePricesRequestValidator _bulk = new();

    [Fact]
    public void Bulk_EmptyList_Fails()
        => IsValid(_bulk, new BulkSavePricesRequest { Items = new() }).Should().BeFalse();

    [Fact]
    public void Bulk_ValidItems_Pass()
        => IsValid(_bulk, new BulkSavePricesRequest
        {
            Items = new() { new() { WasteCategoryId = Guid.NewGuid(), PricePerKg = 1000, MinQuantityKg = 1 } }
        }).Should().BeTrue();

    [Fact]
    public void Bulk_InvalidSecondItem_IsReportedWithIndex()
    {
        var m = new BulkSavePricesRequest
        {
            Items = new()
            {
                new() { WasteCategoryId = Guid.NewGuid(), PricePerKg = 1000, MinQuantityKg = 1 },
                new() { WasteCategoryId = Guid.NewGuid(), PricePerKg = 0, MinQuantityKg = 1 }
            }
        };
        var r = _bulk.Validate(m);
        r.Errors.Should().Contain(e => e.PropertyName == "Items[1].PricePerKg");
        r.Errors.Should().NotContain(e => e.PropertyName.StartsWith("Items[0]"));
    }

    [Fact]
    public void Bulk_FractionalPrice_Fails()
    {
        var m = new BulkSavePricesRequest { Items = new() { new() { WasteCategoryId = Guid.NewGuid(), PricePerKg = 10.5m, MinQuantityKg = 1 } } };
        IsValid(_bulk, m).Should().BeFalse();
    }

    // ───── Availability / Schedule ─────
    [Fact]
    public void Availability_NullIsOnline_Fails()
        => IsValid(new UpdateAvailabilityRequestValidator(), new UpdateAvailabilityRequest { IsOnline = null }).Should().BeFalse();

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Availability_BusyReason_Max100(int len, bool valid)
        => IsValid(new UpdateAvailabilityRequestValidator(), new UpdateAvailabilityRequest { IsOnline = false, BusyReason = Str(len) }).Should().Be(valid);

    private readonly UpdateScheduleRequestValidator _sch = new();

    private static List<ScheduleDayDto> Days(int count) =>
        Enumerable.Range(0, count).Select(i => new ScheduleDayDto { DayOfWeek = i % 7, OpenTime = "08:00", CloseTime = "17:00" }).ToList();

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    public void Schedule_DayCount_1To7(int count, bool valid)
        => IsValid(_sch, new UpdateScheduleRequest { Days = Days(count) }).Should().Be(valid);

    [Theory]
    [InlineData(0, true)]
    [InlineData(6, true)]
    [InlineData(-1, false)]
    [InlineData(7, false)]
    public void Schedule_DayOfWeek_0To6(int dow, bool valid)
        => IsValid(_sch, new UpdateScheduleRequest { Days = new() { new ScheduleDayDto { DayOfWeek = dow } } }).Should().Be(valid);

    [Fact]
    public void Schedule_NullDays_ReturnsValidationError_NotException()
    {
        var m = new UpdateScheduleRequest { Days = null! };
        var act = () => _sch.Validate(m);
        act.Should().NotThrow();
        act().IsValid.Should().BeFalse();
    }

    // ───── NearbyRecyclerFilterValidator ─────
    private readonly NearbyRecyclerFilterValidator _near = new();
    private static NearbyRecyclerFilterRequest Near() => new() { Lat = 10.77, Lng = 106.69, RadiusKm = 10 };

    [Fact] public void Nearby_Valid_Passes() => IsValid(_near, Near()).Should().BeTrue();

    [Theory]
    [InlineData(0.0, true)]     // chưa cấp quyền vị trí (MSG-MTH-02)
    [InlineData(10.0, false)]
    [InlineData(90.1, true)]
    public void Nearby_Lat(double v, bool expectError)
    {
        var m = Near(); m.Lat = v;
        Has(_near, m, nameof(m.Lat)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(106.0, false)]
    [InlineData(-180.1, true)]
    public void Nearby_Lng(double v, bool expectError)
    {
        var m = Near(); m.Lng = v;
        Has(_near, m, nameof(m.Lng)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0.49, true)]
    [InlineData(0.5, false)]
    [InlineData(50.0, false)]
    [InlineData(50.01, true)]
    public void Nearby_Radius_HalfKmTo50Km(double r, bool expectError)
    {
        var m = Near(); m.RadiusKm = r;
        Has(_near, m, nameof(m.RadiusKm)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("plastic", false)]
    [InlineData(" METAL ", false)]
    [InlineData("glass", false)]
    [InlineData("cardboard", false)]
    [InlineData("paper", false)]
    [InlineData("trash", true)]       // không nằm trong danh sách lọc
    [InlineData("organic", true)]
    public void Nearby_Category_Whitelist(string? cat, bool expectError)
    {
        var m = Near(); m.Category = cat;
        Has(_near, m, nameof(m.Category)).Should().Be(expectError);
    }

    // ═════════ Review & Complaint ═════════
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(-1, false)]
    public void CreateReview_Stars_1To5(int stars, bool valid)
        => IsValid(new CreateReviewRequestValidator(), new CreateReviewRequest { RatingStars = stars }).Should().Be(valid);

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void CreateReview_Comment_Max1000(int len, bool valid)
        => IsValid(new CreateReviewRequestValidator(), new CreateReviewRequest { RatingStars = 5, Comment = Str(len) }).Should().Be(valid);

    [Theory]
    [InlineData("", false)]
    [InlineData("    ", false)]
    [InlineData("abcd", false)]
    [InlineData("abcde", true)]
    [InlineData("  abcde  ", true)]
    public void ReportReview_Reason_Min5(string reason, bool valid)
        => IsValid(new ReportReviewRequestValidator(), new ReportReviewRequest { Reason = reason }).Should().Be(valid);

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void ReportReview_Reason_Max500(int len, bool valid)
        => IsValid(new ReportReviewRequestValidator(), new ReportReviewRequest { Reason = Str(len) }).Should().Be(valid);

    [Theory]
    [InlineData("Hide", "Vi pham quy dinh", true)]
    [InlineData("restore", "Vi pham quy dinh", true)]
    [InlineData(" HIDE ", "Vi pham quy dinh", true)]
    [InlineData("Delete", "Vi pham quy dinh", false)]
    [InlineData("", "Vi pham quy dinh", false)]
    [InlineData("Hide", "abcd", false)]
    [InlineData("Hide", "", false)]
    public void ModerateReview_Matrix(string action, string reason, bool valid)
        => IsValid(new ModerateReviewRequestValidator(), new ModerateReviewRequest { Action = action, ModerationReason = reason }).Should().Be(valid);

    // ───── SubmitComplaintRequestValidator ─────
    private readonly SubmitComplaintRequestValidator _cmp = new();

    private static SubmitComplaintRequest Cmp() => new()
    {
        HandoverRequestId = Guid.NewGuid(), Category = "ScaleDiscrepancy",
        Description = "Can bi lech 2kg so voi thuc te", EvidencePhotoUrls = new() { "https://cdn/a.jpg" }
    };

    [Fact] public void Complaint_Valid_Passes() => IsValid(_cmp, Cmp()).Should().BeTrue();

    [Fact]
    public void Complaint_EmptyHandoverId_Fails()
    {
        var m = Cmp(); m.HandoverRequestId = Guid.Empty;
        Has(_cmp, m, nameof(m.HandoverRequestId)).Should().BeTrue();
    }

    [Theory]
    [InlineData("ScaleDiscrepancy", false)]
    [InlineData("paymentissue", false)]
    [InlineData("NoShow", false)]
    [InlineData("Misconduct", false)]
    [InlineData("Other", false)]
    [InlineData("Fraud", true)]
    [InlineData("", true)]
    public void Complaint_Category_MustBeEnum(string cat, bool expectError)
    {
        var m = Cmp(); m.Category = cat;
        Has(_cmp, m, nameof(m.Category)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(14, true)]
    [InlineData(15, false)]
    [InlineData(2000, false)]
    [InlineData(2001, true)]
    public void Complaint_Description_15To2000(int len, bool expectError)
    {
        var m = Cmp(); m.Description = Str(len);
        Has(_cmp, m, nameof(m.Description)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void Complaint_Evidence_Max5(int count, bool expectError)
    {
        var m = Cmp();
        m.EvidencePhotoUrls = Enumerable.Range(0, count).Select(i => $"https://cdn/{i}.jpg").ToList();
        Has(_cmp, m, nameof(m.EvidencePhotoUrls)).Should().Be(expectError);
    }

    [Fact]
    public void Complaint_NullEvidence_IsAllowed()
    {
        var m = Cmp(); m.EvidencePhotoUrls = null;
        IsValid(_cmp, m).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://cdn/a.jpg", true)]
    [InlineData("http://cdn/a.jpg", true)]
    [InlineData("ftp://cdn/a.jpg", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/relative/path.jpg", false)]
    [InlineData("not a url", false)]
    [InlineData("", false)]
    public void Complaint_EvidenceUrl_MustBeHttpAbsolute(string url, bool valid)
    {
        var m = Cmp(); m.EvidencePhotoUrls = new() { url };
        IsValid(_cmp, m).Should().Be(valid);
    }

    [Fact]
    public void Complaint_EvidenceUrl_Over1000Chars_Fails()
    {
        var m = Cmp(); m.EvidencePhotoUrls = new() { "https://x/" + Str(995) };
        IsValid(_cmp, m).Should().BeFalse();
    }

    // ───── ResolveComplaintRequestValidator ─────
    private readonly ResolveComplaintRequestValidator _res = new();

    private static ResolveComplaintRequest Res() => new() { ResolutionOutcome = "CompensateUser", ResolutionNote = "Da doi chieu can va xu ly" };

    [Fact] public void ResolveComplaint_Valid_Passes() => IsValid(_res, Res()).Should().BeTrue();

    [Theory]
    [InlineData("ConfirmReceiptAsIs", false)]
    [InlineData("compensateuser", false)]
    [InlineData("UpholdComplainant", false)]
    [InlineData("CancelDisputedOrder", false)]
    [InlineData("Dismissed", false)]
    [InlineData("Refund", true)]
    [InlineData("", true)]
    public void ResolveComplaint_Outcome_MustBeEnum(string outcome, bool expectError)
    {
        var m = Res(); m.ResolutionOutcome = outcome;
        Has(_res, m, nameof(m.ResolutionOutcome)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(2000, false)]
    [InlineData(2001, true)]
    public void ResolveComplaint_Note_10To2000(int len, bool expectError)
    {
        var m = Res(); m.ResolutionNote = Str(len);
        Has(_res, m, nameof(m.ResolutionNote)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(500, false)]
    [InlineData(501, true)]
    public void ResolveComplaint_WarningReason_Max500(int len, bool expectError)
    {
        var m = Res(); m.WarningReason = Str(len);
        Has(_res, m, nameof(m.WarningReason)).Should().Be(expectError);
    }

    // ═════════ Service package ═════════
    private readonly UpsertServicePackageRequestValidator _pkg = new();

    private static UpsertServicePackageRequest Paid() => new()
    { PackageCode = "PREMIUM_PLUS", PackageName = "Premium Plus", TargetRole = "All", PriceVnd = 59000m, DurationDays = 30 };

    private static UpsertServicePackageRequest Basic() => new()
    { PackageCode = "BASIC", PackageName = "Basic", TargetRole = "All", PriceVnd = 0m, DurationDays = 0 };

    [Fact] public void Package_PaidValid_Passes() => IsValid(_pkg, Paid()).Should().BeTrue();
    [Fact] public void Package_BasicValid_Passes() => IsValid(_pkg, Basic()).Should().BeTrue();

    [Theory]
    [InlineData("PRO", false)]
    [InlineData("PREMIUM_30", false)]
    [InlineData("AB", true)]               // < 3
    [InlineData("pro", true)]              // chữ thường
    [InlineData("PRO-1", true)]            // dấu gạch ngang
    [InlineData("PRO 1", true)]
    [InlineData("", true)]
    public void Package_Code_Format(string code, bool expectError)
    {
        var m = Paid(); m.PackageCode = code;
        Has(_pkg, m, nameof(m.PackageCode)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(150, false)]
    [InlineData(151, true)]
    public void Package_Name_2To150(int len, bool expectError)
    {
        var m = Paid(); m.PackageName = Str(len);
        Has(_pkg, m, nameof(m.PackageName)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("User", false)]
    [InlineData("recycler", false)]
    [InlineData(" All ", false)]
    [InlineData("Admin", true)]
    [InlineData("", true)]
    public void Package_TargetRole(string role, bool expectError)
    {
        var m = Paid(); m.TargetRole = role;
        Has(_pkg, m, nameof(m.TargetRole)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("1999", true)]
    [InlineData("2000", false)]
    [InlineData("1000000", false)]
    public void Package_Paid_MinPrice2000(string price, bool expectError)
    {
        var m = Paid(); m.PriceVnd = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);
        Has(_pkg, m, nameof(m.PriceVnd)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(730, false)]
    [InlineData(731, true)]
    public void Package_Paid_Duration1To730(int days, bool expectError)
    {
        var m = Paid(); m.DurationDays = days;
        Has(_pkg, m, nameof(m.DurationDays)).Should().Be(expectError);
    }

    [Fact]
    public void Package_Basic_MustBeFreeAndPermanent()
    {
        var m = Basic(); m.PriceVnd = 1000m;
        Has(_pkg, m, nameof(m.PriceVnd)).Should().BeTrue();
        m = Basic(); m.DurationDays = 30;
        Has(_pkg, m, nameof(m.DurationDays)).Should().BeTrue();
    }

    [Fact]
    public void Package_BasicCode_IsCaseInsensitiveForPricingRules()
    {
        // "basic" vi phạm regex mã gói (chữ hoa) nhưng các rule giá vẫn coi là BASIC → không báo lỗi giá/chu kỳ.
        var m = Basic(); m.PackageCode = " basic ";
        Has(_pkg, m, nameof(m.PriceVnd)).Should().BeFalse();
        Has(_pkg, m, nameof(m.DurationDays)).Should().BeFalse();
    }

    // ───── ResolvePaymentAnomalyRequestValidator ─────
    private readonly ResolvePaymentAnomalyRequestValidator _pay = new();

    [Theory]
    [InlineData("ApproveAndActivate", null, null, true)]
    [InlineData("CloseAnomaly", null, null, true)]
    [InlineData("ManualMatchOrder", "ORD-1", null, true)]
    [InlineData("ManualMatchOrder", null, null, false)]       // thiếu targetOrderCode
    [InlineData("MarkRefunded", null, "BANKREF1", true)]
    [InlineData("MarkRefunded", null, null, false)]           // thiếu bank ref
    [InlineData("MarkExcessRefunded", null, null, false)]
    [InlineData("MarkExcessRefunded", null, "REF", true)]
    [InlineData("markrefunded", null, "REF", true)]
    [InlineData("Explode", null, null, false)]
    [InlineData("", null, null, false)]
    public void PaymentAnomaly_ActionMatrix(string action, string? targetOrder, string? bankRef, bool valid)
    {
        var m = new ResolvePaymentAnomalyRequest
        {
            Action = action, TargetOrderCode = targetOrder, ResolutionBankRef = bankRef,
            ResolutionNote = "Da doi soat xong voi ngan hang"
        };
        IsValid(_pay, m).Should().Be(valid);
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void PaymentAnomaly_Note_10To500(int len, bool valid)
        => IsValid(_pay, new ResolvePaymentAnomalyRequest { Action = "CloseAnomaly", ResolutionNote = Str(len) }).Should().Be(valid);

    // ═════════ Waste catalog ═════════
    private readonly CreateWasteCategoryValidator _wc = new();
    private static CreateWasteCategoryRequest WC() => new() { Code = "plastic_bottle", Name = "Chai nhua", StandardBinColor = "Cam" };

    [Fact] public void WasteCategory_Valid_Passes() => IsValid(_wc, WC()).Should().BeTrue();

    [Theory]
    [InlineData("plastic", false)]
    [InlineData("e_waste_2", false)]
    [InlineData("Plastic", true)]       // chữ hoa
    [InlineData("nhựa", true)]          // có dấu
    [InlineData("a-b", true)]
    [InlineData("a b", true)]
    [InlineData("", true)]
    public void WasteCategory_Code_LowercaseSnake(string code, bool expectError)
    {
        var m = WC(); m.Code = code;
        Has(_wc, m, nameof(m.Code)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(51, true)]
    public void WasteCategory_Code_Max50(int len, bool expectError)
    {
        var m = WC(); m.Code = Str(len);
        Has(_wc, m, nameof(m.Code)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    [InlineData(0, true)]
    public void WasteCategory_Name(int len, bool expectError)
    {
        var m = WC(); m.Name = Str(len);
        Has(_wc, m, nameof(m.Name)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(51, true)]
    [InlineData(0, true)]
    public void WasteCategory_BinColor(int len, bool expectError)
    {
        var m = WC(); m.StandardBinColor = Str(len);
        Has(_wc, m, nameof(m.StandardBinColor)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(500, false)]
    [InlineData(501, true)]
    public void WasteCategory_DescriptionAndIcon_Max500(int len, bool expectError)
    {
        var m = WC(); m.Description = Str(len); m.IconUrl = Str(len);
        Has(_wc, m, nameof(m.Description)).Should().Be(expectError);
        Has(_wc, m, nameof(m.IconUrl)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("Ten moi", true)]
    public void UpdateWasteCategory_Name_Required(string name, bool valid)
        => IsValid(new UpdateWasteCategoryValidator(), new UpdateWasteCategoryRequest { Name = name, StandardBinColor = "Xanh" }).Should().Be(valid);

    [Theory]
    [InlineData("Active", true)]
    [InlineData("inactive", true)]
    [InlineData(" Active ", true)]
    [InlineData("Deleted", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ToggleWasteCategory_Status(string? status, bool valid)
        => IsValid(new ToggleWasteCategoryStatusValidator(), new ToggleWasteCategoryStatusRequest { Status = status! }).Should().Be(valid);

    private readonly UpsertSortingRuleValidator _rule = new();
    private static UpsertSortingRuleRequest Rule() => new()
    { Title = "Chai nhua", PreparationSteps = "Rua sach", SortingInstruction = "Bo vao thung cam", HandlingAction = "Thu gom", ApplicableRegion = "VN" };

    [Fact] public void SortingRule_Valid_Passes() => IsValid(_rule, Rule()).Should().BeTrue();

    [Theory]
    [InlineData(150, false)]
    [InlineData(151, true)]
    [InlineData(0, true)]
    public void SortingRule_Title(int len, bool expectError)
    {
        var m = Rule(); m.Title = Str(len);
        Has(_rule, m, nameof(m.Title)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(2000, false)]
    [InlineData(2001, true)]
    public void SortingRule_PreparationAndInstruction_Max2000(int len, bool expectError)
    {
        var m = Rule(); m.PreparationSteps = Str(len); m.SortingInstruction = Str(len);
        Has(_rule, m, nameof(m.PreparationSteps)).Should().Be(expectError);
        Has(_rule, m, nameof(m.SortingInstruction)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(1000, false)]
    [InlineData(1001, true)]
    public void SortingRule_HandlingAndHazard_Max1000(int len, bool expectError)
    {
        var m = Rule(); m.HandlingAction = Str(len); m.HazardWarning = Str(len);
        Has(_rule, m, nameof(m.HandlingAction)).Should().Be(expectError);
        Has(_rule, m, nameof(m.HazardWarning)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void SortingRule_Region_Max100(int len, bool expectError)
    {
        var m = Rule(); m.ApplicableRegion = Str(len);
        Has(_rule, m, nameof(m.ApplicableRegion)).Should().Be(expectError);
    }
}
