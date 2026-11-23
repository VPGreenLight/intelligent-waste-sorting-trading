using Ecolink.Application.Dtos;
using Ecolink.Application.Validators;
using FluentAssertions;
using Xunit;
using static Ecolink.UnitTests.Validators.ValidatorAssert;

namespace Ecolink.UnitTests.Validators;

[Trait("Layer", "Unit")]
[Trait("Area", "Validators")]
public class AiBlogGamificationValidatorTests
{
    // ═════════ AI feedback / curation ═════════
    private readonly SubmitFeedbackDtoValidator _fb = new();

    [Theory]
    [InlineData("", true)]
    [InlineData("plastic", false)]
    public void SubmitFeedback_UserLabel_Required(string label, bool expectError)
    {
        var m = new SubmitFeedbackDto { UserReportedLabel = label };
        Has(_fb, m, nameof(m.UserReportedLabel)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(51, true)]
    public void SubmitFeedback_UserLabel_MaxLength(int len, bool expectError)
    {
        var m = new SubmitFeedbackDto { UserReportedLabel = Str(len) };
        Has(_fb, m, nameof(m.UserReportedLabel)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0d, false)]      // 0 = bỏ qua rule
    [InlineData(0.5d, false)]
    [InlineData(1d, false)]
    [InlineData(1.01d, true)]
    [InlineData(-0.1d, true)]
    public void SubmitFeedback_Confidence_Range(double score, bool expectError)
    {
        var m = new SubmitFeedbackDto { UserReportedLabel = "metal", ConfidenceScore = score };
        Has(_fb, m, nameof(m.ConfidenceScore)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(500, false)]
    [InlineData(501, true)]
    public void SubmitFeedback_Note_MaxLength(int len, bool expectError)
    {
        var m = new SubmitFeedbackDto { UserReportedLabel = "metal", Note = Str(len) };
        Has(_fb, m, nameof(m.Note)).Should().Be(expectError);
    }

    // ───── YoloBoundingBoxDtoValidator ─────
    private readonly YoloBoundingBoxDtoValidator _yolo = new();
    private static YoloBoundingBoxDto Box() => new() { CategoryCode = "plastic", XCenter = .5, YCenter = .5, Width = .2, Height = .2 };

    [Fact] public void Yolo_Valid_Passes() => IsValid(_yolo, Box()).Should().BeTrue();

    [Theory]
    [InlineData(0d, false)]
    [InlineData(1d, false)]
    [InlineData(-0.001, true)]
    [InlineData(1.001, true)]
    public void Yolo_Center_Range(double v, bool expectError)
    {
        var x = Box(); x.XCenter = v;
        var y = Box(); y.YCenter = v;
        Has(_yolo, x, nameof(x.XCenter)).Should().Be(expectError);
        Has(_yolo, y, nameof(y.YCenter)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0d, true)]          // phải > 0
    [InlineData(0.0001, false)]
    [InlineData(1d, false)]
    [InlineData(1.0001, true)]
    [InlineData(-0.5, true)]
    public void Yolo_Size_Range(double v, bool expectError)
    {
        var w = Box(); w.Width = v;
        var h = Box(); h.Height = v;
        Has(_yolo, w, nameof(w.Width)).Should().Be(expectError);
        Has(_yolo, h, nameof(h.Height)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("glass", false)]
    public void Yolo_CategoryCode_Required(string code, bool expectError)
    {
        var b = Box(); b.CategoryCode = code;
        Has(_yolo, b, nameof(b.CategoryCode)).Should().Be(expectError);
    }

    // ───── ReviewAiFeedbackRequestValidator ─────
    private readonly ReviewAiFeedbackRequestValidator _review = new();

    [Theory]
    [InlineData("ApproveAndLabel", false)]
    [InlineData("approveandlabel", false)]
    [InlineData("  ApproveToQueue ", false)]
    [InlineData("Reject", false)]
    [InlineData("Delete", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void ReviewAi_Decision_MustBeEnumName(string? decision, bool expectError)
    {
        var m = new ReviewAiFeedbackRequest { Decision = decision!, VerifiedLabel = "plastic", ReviewNote = "ghi chu dai" };
        Has(_review, m, nameof(m.Decision)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("plastic", false)]
    public void ReviewAi_ApproveAndLabel_RequiresVerifiedLabel(string? label, bool expectError)
    {
        var m = new ReviewAiFeedbackRequest { Decision = "ApproveAndLabel", VerifiedLabel = label };
        Has(_review, m, nameof(m.VerifiedLabel)).Should().Be(expectError);
    }

    [Fact]
    public void ReviewAi_ApproveToQueue_DoesNotRequireVerifiedLabel()
    {
        var m = new ReviewAiFeedbackRequest { Decision = "ApproveToQueue" };
        Has(_review, m, nameof(m.VerifiedLabel)).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("   ", true)]
    [InlineData("abcd", true)]       // 4 ký tự
    [InlineData("abcde", false)]     // 5 ký tự
    [InlineData("  abcde  ", false)]
    public void ReviewAi_Reject_RequiresNoteMin5(string? note, bool expectError)
    {
        var m = new ReviewAiFeedbackRequest { Decision = "Reject", ReviewNote = note };
        Has(_review, m, nameof(m.ReviewNote)).Should().Be(expectError);
    }

    [Fact]
    public void ReviewAi_InvalidYoloBox_IsReportedWithIndex()
    {
        var m = new ReviewAiFeedbackRequest
        {
            Decision = "ApproveToQueue",
            YoloAnnotations = new List<YoloBoundingBoxDto> { Box(), new() { CategoryCode = "glass", XCenter = 2, YCenter = .5, Width = .1, Height = .1 } }
        };
        var result = _review.Validate(m);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "YoloAnnotations[1].XCenter");
    }

    // ───── AssignGroundTruthLabelRequestValidator ─────
    private readonly AssignGroundTruthLabelRequestValidator _assign = new();

    [Theory]
    [InlineData(false, null, true)]
    [InlineData(false, "", true)]
    [InlineData(false, "metal", false)]
    [InlineData(true, null, false)]      // loại khỏi dataset → không cần nhãn
    public void AssignLabel_Label_RequiredUnlessExcluded(bool exclude, string? label, bool expectError)
    {
        var m = new AssignGroundTruthLabelRequest { ExcludeFromDataset = exclude, GroundTruthLabel = label };
        Has(_assign, m, nameof(m.GroundTruthLabel)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(500, false)]
    [InlineData(501, true)]
    public void AssignLabel_QualityNote_MaxLength(int len, bool expectError)
    {
        var m = new AssignGroundTruthLabelRequest { GroundTruthLabel = "metal", QualityNote = Str(len) };
        Has(_assign, m, nameof(m.QualityNote)).Should().Be(expectError);
    }

    // ───── BatchAssignLabelsRequestValidator ─────
    private readonly BatchAssignLabelsRequestValidator _batch = new();

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(200, false)]
    [InlineData(201, true)]
    public void BatchAssign_ItemCount_Boundary(int count, bool expectError)
    {
        var m = new BatchAssignLabelsRequest
        {
            ItemIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList(),
            GroundTruthLabel = "paper"
        };
        Has(_batch, m, nameof(m.ItemIds)).Should().Be(expectError);
    }

    [Fact]
    public void BatchAssign_EmptyLabel_Fails()
    {
        var m = new BatchAssignLabelsRequest { ItemIds = new() { Guid.NewGuid() }, GroundTruthLabel = "" };
        Has(_batch, m, nameof(m.GroundTruthLabel)).Should().BeTrue();
    }

    [Fact]
    public void BatchAssign_NullItemIds_ReturnsValidationError_NotException()
    {
        var m = new BatchAssignLabelsRequest { ItemIds = null!, GroundTruthLabel = "paper" };
        var act = () => _batch.Validate(m);
        act.Should().NotThrow();
        act().IsValid.Should().BeFalse();
    }

    // ───── CreateDatasetVersionRequestValidator ─────
    private readonly CreateDatasetVersionRequestValidator _ds = new();
    private static CreateDatasetVersionRequest Ver() => new() { VersionCode = "v1.0.0", VersionName = "Ban dau", TrainRatio = 70, ValRatio = 20, TestRatio = 10 };

    [Fact] public void Dataset_Valid_Passes() => IsValid(_ds, Ver()).Should().BeTrue();

    [Theory]
    [InlineData("v1.0.0", false)]
    [InlineData("v10.20.30", false)]
    [InlineData(" v1.0.0 ", false)]
    [InlineData("1.0.0", true)]
    [InlineData("v1.0", true)]
    [InlineData("v1.0.0.1", true)]
    [InlineData("V1.0.0", true)]
    [InlineData("v1.a.0", true)]
    [InlineData("", true)]
    public void Dataset_VersionCode_MustBeSemVer(string code, bool expectError)
    {
        var m = Ver(); m.VersionCode = code;
        Has(_ds, m, nameof(m.VersionCode)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(70, 20, 10, true)]
    [InlineData(100, 0, 0, true)]
    [InlineData(60, 20, 10, false)]      // tổng 90
    [InlineData(80, 20, 10, false)]      // tổng 110
    [InlineData(0, 50, 50, false)]       // Train phải > 0
    [InlineData(110, -5, -5, false)]     // Val/Test âm
    public void Dataset_Ratios_MustSumTo100(int train, int val, int test, bool valid)
    {
        var m = Ver(); m.TrainRatio = train; m.ValRatio = val; m.TestRatio = test;
        IsValid(_ds, m).Should().Be(valid);
    }

    [Theory]
    [InlineData(150, false)]
    [InlineData(151, true)]
    public void Dataset_VersionName_MaxLength(int len, bool expectError)
    {
        var m = Ver(); m.VersionName = Str(len);
        Has(_ds, m, nameof(m.VersionName)).Should().Be(expectError);
    }

    // ═════════ Blog ═════════
    private readonly SaveBlogPostRequestValidator _blog = new();
    private static SaveBlogPostRequest Post() => new() { Title = "Tieu de bai viet hop le" };

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(150, false)]
    [InlineData(151, true)]
    public void Blog_Title_Length10To150(int len, bool expectError)
    {
        var m = Post(); m.Title = Str(len);
        Has(_blog, m, nameof(m.Title)).Should().Be(expectError);
    }

    [Fact]
    public void Blog_Title_Whitespace_Fails()
    {
        var m = Post(); m.Title = "            ";
        Has(_blog, m, nameof(m.Title)).Should().BeTrue();
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void Blog_Tags_Max5(int count, bool expectError)
    {
        var m = Post(); m.Tags = Enumerable.Range(0, count).Select(i => "t" + i).ToList();
        Has(_blog, m, nameof(m.Tags)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(500, false)]
    [InlineData(501, true)]
    public void Blog_Summary_MaxLength(int len, bool expectError)
    {
        var m = Post(); m.Summary = Str(len);
        Has(_blog, m, nameof(m.Summary)).Should().Be(expectError);
    }

    [Fact]
    public void Blog_Draft_DoesNotRequireCoverOrContent() => IsValid(_blog, Post()).Should().BeTrue();

    [Fact]
    public void Blog_Submit_RequiresCoverAndContent()
    {
        var m = Post(); m.SubmitForApproval = true;
        Has(_blog, m, nameof(m.CoverImageUrl)).Should().BeTrue();
        Has(_blog, m, nameof(m.Content)).Should().BeTrue();
    }

    [Fact]
    public void Blog_Submit_WithCoverAndContent_Passes()
    {
        var m = Post(); m.SubmitForApproval = true; m.CoverImageUrl = "https://x/y.jpg"; m.Content = "<p>noi dung</p>";
        IsValid(_blog, m).Should().BeTrue();
    }

    // ───── RejectBlogPostRequestValidator ─────
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Can sua lai", true)]
    public void RejectBlog_Feedback_Required(string fb, bool valid)
        => IsValid(new RejectBlogPostRequestValidator(), new RejectBlogPostRequest { Feedback = fb }).Should().Be(valid);

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void RejectBlog_Feedback_MaxLength(int len, bool valid)
        => IsValid(new RejectBlogPostRequestValidator(), new RejectBlogPostRequest { Feedback = Str(len) }).Should().Be(valid);

    // ───── ReviewBlogPostRequestValidator ─────
    private readonly ReviewBlogPostRequestValidator _rb = new();

    [Theory]
    [InlineData("approve", null, null, true)]
    [InlineData("APPROVED", null, null, true)]
    [InlineData("publish", null, null, true)]
    [InlineData("published", null, null, true)]
    [InlineData("reject", null, "Sai nội dung", true)]
    [InlineData("rejected", null, "Sai nội dung", true)]
    [InlineData("reject", null, null, false)]        // reject phải có feedback
    [InlineData("hide", null, null, false)]          // action lạ
    [InlineData(null, true, null, true)]             // Approve=true, không Action
    [InlineData(null, false, "Lý do", true)]
    [InlineData(null, false, null, false)]           // Approve=false cần feedback
    [InlineData(null, null, null, false)]            // không có quyết định
    public void ReviewBlog_Decision_Matrix(string? action, bool? approve, string? feedback, bool valid)
    {
        var m = new ReviewBlogPostRequest { Action = action, Approve = approve, Feedback = feedback };
        IsValid(_rb, m).Should().Be(valid);
    }

    // ═════════ Gift / Redeem / EcoFact ═════════
    private readonly SaveGiftRequestValidator _gift = new();
    private static SaveGiftRequest Gift() => new() { Name = "Voucher 50k", Type = "Voucher", PointsRequired = 100, StockQuantity = 10 };

    [Fact] public void Gift_Valid_Passes() => IsValid(_gift, Gift()).Should().BeTrue();

    [Theory]
    [InlineData("Physical", false)]
    [InlineData("voucher", false)]
    [InlineData(" Voucher ", false)]
    [InlineData("Coupon", true)]
    [InlineData("", true)]
    public void Gift_Type_Rules(string type, bool expectError)
    {
        var m = Gift(); m.Type = type;
        Has(_gift, m, nameof(m.Type)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(150, false)]
    [InlineData(151, true)]
    [InlineData(0, true)]
    public void Gift_Name_Length(int len, bool expectError)
    {
        var m = Gift(); m.Name = Str(len);
        Has(_gift, m, nameof(m.Name)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(-1, true)]
    public void Gift_Points_NonNegative(long pts, bool expectError)
    {
        var m = Gift(); m.PointsRequired = pts;
        Has(_gift, m, nameof(m.PointsRequired)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1_000_000, false)]
    [InlineData(1_000_001, true)]
    [InlineData(-1, true)]
    public void Gift_Stock_Range(int stock, bool expectError)
    {
        var m = Gift(); m.StockQuantity = stock;
        Has(_gift, m, nameof(m.StockQuantity)).Should().Be(expectError);
    }

    private readonly RedeemGiftRequestValidator _redeem = new();

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(10, false)]
    [InlineData(11, true)]
    public void Redeem_Quantity_1To10(int qty, bool expectError)
    {
        var m = new RedeemGiftRequest { Quantity = qty };
        Has(_redeem, m, nameof(m.Quantity)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(500, false)]
    [InlineData(501, true)]
    public void Redeem_Address_MaxLength(int len, bool expectError)
    {
        var m = new RedeemGiftRequest { Quantity = 1, DeliveryAddress = Str(len) };
        Has(_redeem, m, nameof(m.DeliveryAddress)).Should().Be(expectError);
    }

    [Fact]
    public void CreateRedemption_EmptyGiftId_Fails_AndInheritsQuantityRule()
    {
        var v = new CreateRedemptionRequestValidator();
        var m = new CreateRedemptionRequest { GiftId = Guid.Empty, Quantity = 11 };
        Has(v, m, nameof(m.GiftId)).Should().BeTrue();
        Has(v, m, nameof(m.Quantity)).Should().BeTrue();
        IsValid(v, new CreateRedemptionRequest { GiftId = Guid.NewGuid(), Quantity = 2 }).Should().BeTrue();
    }

    private readonly SaveEcoFactRequestValidator _fact = new();

    [Theory]
    [InlineData(200, false)]
    [InlineData(201, true)]
    [InlineData(0, true)]
    public void EcoFact_Title_Length(int len, bool expectError)
    {
        var m = new SaveEcoFactRequest { Title = Str(len), Content = "ok", UnlockTreeLevel = 1 };
        Has(_fact, m, nameof(m.Title)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(300, false)]
    [InlineData(301, true)]
    [InlineData(0, true)]
    public void EcoFact_Content_Length(int len, bool expectError)
    {
        var m = new SaveEcoFactRequest { Title = "t", Content = Str(len), UnlockTreeLevel = 1 };
        Has(_fact, m, nameof(m.Content)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(10, false)]
    public void EcoFact_UnlockLevel_Min1(int level, bool expectError)
    {
        var m = new SaveEcoFactRequest { Title = "t", Content = "c", UnlockTreeLevel = level };
        Has(_fact, m, nameof(m.UnlockTreeLevel)).Should().Be(expectError);
    }

    // ═════════ GamificationConfigDtoValidator ═════════
    private readonly GamificationConfigDtoValidator _cfg = new();

    private static GamificationConfigDto Cfg() => new()
    {
        DailyTreeCareLimit = 5, VoucherValidityDays = 30,
        TreeLevels = new List<TreeLevelConfigDto>
        {
            new() { Level = 1, Title = "Mầm", RequiredPoints = 0 },
            new() { Level = 2, Title = "Cây non", RequiredPoints = 100 },
            new() { Level = 3, Title = "Cây lớn", RequiredPoints = 500 }
        }
    };

    [Fact] public void GamConfig_Valid_Passes() => IsValid(_cfg, Cfg()).Should().BeTrue();

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void GamConfig_DailyTreeCareLimit_1To100(int v, bool expectError)
    {
        var m = Cfg(); m.DailyTreeCareLimit = v;
        Has(_cfg, m, nameof(m.DailyTreeCareLimit)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(3650, false)]
    [InlineData(3651, true)]
    public void GamConfig_VoucherValidityDays_1To3650(int v, bool expectError)
    {
        var m = Cfg(); m.VoucherValidityDays = v;
        Has(_cfg, m, nameof(m.VoucherValidityDays)).Should().Be(expectError);
    }

    [Fact]
    public void GamConfig_NegativeCap_Fails()
    {
        var m = Cfg(); m.DailyNonTransactionCap = -1;
        Has(_cfg, m, nameof(m.DailyNonTransactionCap)).Should().BeTrue();
    }

    [Fact]
    public void GamConfig_EmptyTreeLevels_Fails()
    {
        var m = Cfg(); m.TreeLevels = new();
        Has(_cfg, m, nameof(m.TreeLevels)).Should().BeTrue();
    }

    [Fact]
    public void GamConfig_MoreThan10Levels_Fails()
    {
        var m = Cfg();
        m.TreeLevels = Enumerable.Range(1, 11).Select(i => new TreeLevelConfigDto { Level = i, Title = "L" + i, RequiredPoints = (i - 1) * 10 }).ToList();
        Has(_cfg, m, nameof(m.TreeLevels)).Should().BeTrue();
    }

    [Fact]
    public void GamConfig_Exactly10Levels_Passes()
    {
        var m = Cfg();
        m.TreeLevels = Enumerable.Range(1, 10).Select(i => new TreeLevelConfigDto { Level = i, Title = "L" + i, RequiredPoints = (i - 1) * 10 }).ToList();
        IsValid(_cfg, m).Should().BeTrue();
    }

    [Fact]
    public void GamConfig_Level1_MustStartAtZeroPoints()
    {
        var m = Cfg(); m.TreeLevels[0].RequiredPoints = 5;
        Has(_cfg, m, nameof(m.TreeLevels)).Should().BeTrue();
    }

    [Fact]
    public void GamConfig_LevelsMustBeContiguous()
    {
        var m = Cfg(); m.TreeLevels[2].Level = 4;       // 1,2,4 → thủng cấp 3
        Has(_cfg, m, nameof(m.TreeLevels)).Should().BeTrue();
    }

    [Fact]
    public void GamConfig_PointsMustStrictlyIncrease()
    {
        var m = Cfg(); m.TreeLevels[2].RequiredPoints = 100;   // bằng cấp 2
        Has(_cfg, m, nameof(m.TreeLevels)).Should().BeTrue();
    }

    [Fact]
    public void GamConfig_LevelsOutOfOrder_AreSortedBeforeCheck()
    {
        var m = Cfg();
        m.TreeLevels = m.TreeLevels.OrderByDescending(l => l.Level).ToList();   // 3,2,1
        IsValid(_cfg, m).Should().BeTrue();
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void GamConfig_LevelTitle_MaxLength(int len, bool expectError)
    {
        var m = Cfg(); m.TreeLevels[1].Title = Str(len);
        Has(_cfg, m, nameof(m.TreeLevels)).Should().Be(expectError);
    }

    // ═════════ SaveLeaderboardCycleRequestValidator ═════════
    private readonly SaveLeaderboardCycleRequestValidator _lb = new();
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static SaveLeaderboardCycleRequest Cycle() => new()
    { Name = "Tuần 1", PeriodType = "Weekly", StartsAt = T0, EndsAt = T0.AddDays(7), Top1BonusPoints = 300, Top2BonusPoints = 200, Top3BonusPoints = 100 };

    [Fact] public void Leaderboard_Valid_Passes() => IsValid(_lb, Cycle()).Should().BeTrue();

    [Theory]
    [InlineData("Weekly", false)]
    [InlineData("monthly", false)]
    [InlineData("Quarterly", false)]
    [InlineData("week", false)]
    [InlineData("quarter", false)]
    [InlineData("Daily", true)]
    [InlineData("", true)]
    public void Leaderboard_PeriodType_Rules(string p, bool expectError)
    {
        var m = Cycle(); m.PeriodType = p;
        Has(_lb, m, nameof(m.PeriodType)).Should().Be(expectError);
    }

    [Fact]
    public void Leaderboard_EndMustBeAfterStart()
    {
        var m = Cycle(); m.EndsAt = m.StartsAt;
        Has(_lb, m, nameof(m.EndsAt)).Should().BeTrue();
        m.EndsAt = m.StartsAt.AddSeconds(1);
        Has(_lb, m, nameof(m.EndsAt)).Should().BeFalse();
    }

    [Theory]
    [InlineData(300, 200, 100, true)]
    [InlineData(100, 100, 100, true)]     // bằng nhau vẫn hợp lệ
    [InlineData(0, 0, 0, true)]
    [InlineData(100, 200, 50, false)]     // Top1 < Top2
    [InlineData(300, 100, 200, false)]    // Top2 < Top3
    [InlineData(300, 200, -1, false)]     // âm
    public void Leaderboard_Bonus_NonIncreasing(long t1, long t2, long t3, bool valid)
    {
        var m = Cycle(); m.Top1BonusPoints = t1; m.Top2BonusPoints = t2; m.Top3BonusPoints = t3;
        IsValid(_lb, m).Should().Be(valid);
    }

    [Theory]
    [InlineData(150, false)]
    [InlineData(151, true)]
    [InlineData(0, true)]
    public void Leaderboard_Name_Length(int len, bool expectError)
    {
        var m = Cycle(); m.Name = Str(len);
        Has(_lb, m, nameof(m.Name)).Should().Be(expectError);
    }
}
