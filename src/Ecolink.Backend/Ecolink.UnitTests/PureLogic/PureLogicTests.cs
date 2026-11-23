using System.IO.Compression;
using System.Reflection;
using System.Text;
using Ecolink.Application.Dtos;
using Ecolink.Domain.Constants;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Ecolink.UnitTests.PureLogic;

// LƯU Ý: ServicePackageHelpers, HandoverTradeSyncHelper, GamTime, GamErrors, TabularReportExportHelper
// và PointsService.ActionText là `internal` → cần <InternalsVisibleTo Include="Ecolink.UnitTests" />
// trong Ecolink.Infrastructure.csproj (xem README_UNIT_TESTS.md).

// ═════════════════════════════════════════════════════════════════════
[Trait("Layer", "Unit")]
[Trait("Area", "PureLogic")]
public class AiCategoryNormalizerTests
{
    [Theory]
    [InlineData("plastic", "plastic")]
    [InlineData("plastic_bottle", "plastic")]
    [InlineData("PET", "plastic")]
    [InlineData("  Nhua  ", "plastic")]
    [InlineData("hdpe", "plastic")]
    [InlineData("office-paper", "paper")]
    [InlineData("giay", "paper")]
    [InlineData("carton", "cardboard")]
    [InlineData("Corrugated", "cardboard")]
    [InlineData("aluminum", "metal")]
    [InlineData("kim_loai", "metal")]
    [InlineData("brown-glass", "glass")]
    [InlineData("thuy_tinh", "glass")]
    [InlineData("battery", "battery")]
    [InlineData("ewaste_disposal", "battery")]
    [InlineData("huu_co", "biological")]
    [InlineData("quan_ao", "clothes")]
    [InlineData("giay_dep", "shoes")]
    [InlineData("UNKNOWN_LABEL", "unknown_label")]     // không có alias → lowercase
    public void NormalizeCode_MapsAliasesToCanonical(string raw, string expected)
        => AiCategoryNormalizer.NormalizeCode(raw).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeCode_NullOrBlank_ReturnsEmpty(string? raw)
        => AiCategoryNormalizer.NormalizeCode(raw).Should().BeEmpty();

    [Fact]
    public void NormalizeCode_IsIdempotent_ForEveryYoloClass()
    {
        foreach (var c in AiCategoryNormalizer.YoloClassOrder)
            AiCategoryNormalizer.NormalizeCode(c).Should().Be(c);
    }

    [Theory]
    [InlineData("battery", 0)]
    [InlineData("biological", 1)]
    [InlineData("cardboard", 2)]
    [InlineData("clothes", 3)]
    [InlineData("glass", 4)]
    [InlineData("metal", 5)]
    [InlineData("paper", 6)]
    [InlineData("plastic", 7)]
    [InlineData("shoes", 8)]
    [InlineData("trash", 9)]
    [InlineData("pet", 7)]               // alias → plastic
    [InlineData("carton", 2)]            // alias → cardboard
    [InlineData("something_else", 9)]    // không biết → trash
    [InlineData(null, 9)]
    [InlineData("", 9)]
    public void GetYoloClassIndex_Matches_YoloOrder(string? code, int expected)
        => AiCategoryNormalizer.GetYoloClassIndex(code).Should().Be(expected);

    [Fact]
    public void YoloClassOrder_IsStable_ContractWithAiService()
        => AiCategoryNormalizer.YoloClassOrder.Should().Equal(
            "battery", "biological", "cardboard", "clothes", "glass",
            "metal", "paper", "plastic", "shoes", "trash");

    [Theory]
    [InlineData(FeedbackStatus.Pending, "Pending")]
    [InlineData(FeedbackStatus.Verified, "Reviewed")]    // Verified hiển thị là Reviewed
    [InlineData(FeedbackStatus.Rejected, "Rejected")]
    public void FormatFeedbackStatus_Rules(FeedbackStatus s, string expected)
        => AiCategoryNormalizer.FormatFeedbackStatus(s).Should().Be(expected);

    [Theory]
    [InlineData("Reviewed", true, FeedbackStatus.Verified)]
    [InlineData("verified", true, FeedbackStatus.Verified)]
    [InlineData("  REVIEWED ", true, FeedbackStatus.Verified)]
    [InlineData("Pending", true, FeedbackStatus.Pending)]
    [InlineData("rejected", true, FeedbackStatus.Rejected)]
    [InlineData("garbage", false, FeedbackStatus.Pending)]
    [InlineData("", false, FeedbackStatus.Pending)]
    [InlineData(null, false, FeedbackStatus.Pending)]
    public void TryParseFeedbackStatus_Rules(string? raw, bool ok, FeedbackStatus expected)
    {
        AiCategoryNormalizer.TryParseFeedbackStatus(raw, out var status).Should().Be(ok);
        if (ok) status.Should().Be(expected);   // khi parse thất bại, giá trị out không được đảm bảo
    }

    [Fact]
    public void ComputeSha256Hex_KnownVectors()
    {
        AiCategoryNormalizer.ComputeSha256Hex("abc")
            .Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        AiCategoryNormalizer.ComputeSha256Hex(Array.Empty<byte>())
            .Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public void ComputeSha256Hex_StringAndBytesOverloads_Agree()
        => AiCategoryNormalizer.ComputeSha256Hex("xin chào")
            .Should().Be(AiCategoryNormalizer.ComputeSha256Hex(Encoding.UTF8.GetBytes("xin chào")));

    [Theory]
    [InlineData(null, ".jpg")]
    [InlineData("", ".jpg")]
    [InlineData("photo.PNG", ".png")]
    [InlineData("photo.jpeg", ".jpeg")]
    [InlineData("a.webp", ".webp")]
    [InlineData("a.bmp", ".bmp")]
    [InlineData("https://cdn.x/a/b.webp?token=1&x=2", ".webp")]
    [InlineData("virus.exe", ".jpg")]
    [InlineData("noextension", ".jpg")]
    [InlineData("a.gif", ".jpg")]
    public void SanitizeFileExtension_OnlyAllowsImageExtensions(string? name, string expected)
        => AiCategoryNormalizer.SanitizeFileExtension(name).Should().Be(expected);
}

// ═════════════════════════════════════════════════════════════════════
[Trait("Layer", "Unit")]
[Trait("Area", "PureLogic")]
public class ServicePackageHelpersTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("null")]
    public void ParseFeatures_InvalidOrEmpty_ReturnsBasicDefaults(string? json)
    {
        var f = ServicePackageHelpers.ParseFeatures(json);
        f.TierName.Should().Be("Basic");
        f.AdFree.Should().BeFalse();
        f.DailyPointMultiplier.Should().Be(1.0m);
        f.MapPriorityBoost.Should().Be(0);
    }

    [Fact]
    public void ParseFeatures_ProSeedJson_ParsesAllFlags()
    {
        var f = ServicePackageHelpers.ParseFeatures(CanonicalServicePackages.ProFeaturesJson);
        f.TierName.Should().Be("Pro");
        f.AdFree.Should().BeTrue();
        f.VerifiedBlueBadge.Should().BeTrue();
        f.FeaturedMapMarker.Should().BeTrue();
        f.EcoPlusBadge.Should().BeTrue();
        f.DailyPointMultiplier.Should().Be(1.2m);
        f.MapPriorityBoost.Should().Be(100);
    }

    [Fact]
    public void ParseFeatures_IsCaseInsensitive()
        => ServicePackageHelpers.ParseFeatures("{\"ADFREE\":true,\"TierName\":\"X\"}").AdFree.Should().BeTrue();

    [Fact]
    public void Serialize_Then_Parse_RoundTrips()
    {
        var src = new ServicePackageFeaturesDto
        {
            TierName = "Custom", AdFree = true, DailyPointMultiplier = 1.5m, EcoPlusBadge = true,
            VerifiedBlueBadge = false, FeaturedMapMarker = true, MapPriorityBoost = 7
        };
        var back = ServicePackageHelpers.ParseFeatures(ServicePackageHelpers.SerializeFeatures(src));
        back.Should().BeEquivalentTo(src);
    }

    [Fact]
    public void SerializeFeatures_Null_WritesCamelCaseBasicDefaults()
    {
        var json = ServicePackageHelpers.SerializeFeatures(null);
        json.Should().Contain("\"tierName\":\"Basic\"").And.Contain("\"adFree\":false");
    }

    private static UserSubscription Sub(string featuresJson) => new() { FeaturesSnapshot = featuresJson };

    [Fact]
    public void Aggregate_NoSubscriptions_IsBasic()
    {
        var s = ServicePackageHelpers.AggregateEntitlements(Array.Empty<UserSubscription>());
        s.CurrentTier.Should().Be("Basic");
        s.IsAdFree.Should().BeFalse();
        s.DailyPointMultiplier.Should().Be(1.0m);
    }

    [Fact]
    public void Aggregate_Premium_IsPremium()
    {
        var s = ServicePackageHelpers.AggregateEntitlements(new[] { Sub(CanonicalServicePackages.PremiumFeaturesJson) });
        s.CurrentTier.Should().Be("Premium");
        s.IsAdFree.Should().BeTrue();
        s.HasEcoPlusBadge.Should().BeTrue();
        s.DailyPointMultiplier.Should().Be(1.2m);
        s.HasVerifiedBlueBadge.Should().BeFalse();
    }

    [Fact]
    public void Aggregate_Pro_IsPro_EvenWithPremiumToo()
    {
        var s = ServicePackageHelpers.AggregateEntitlements(new[]
        {
            Sub(CanonicalServicePackages.PremiumFeaturesJson),
            Sub(CanonicalServicePackages.ProFeaturesJson)
        });
        s.CurrentTier.Should().Be("Pro");
        s.MapPriorityBoost.Should().Be(100);
        s.IsFeaturedMapMarker.Should().BeTrue();
    }

    [Fact]
    public void Aggregate_TakesMaxMultiplierAndBoost_NotSum()
    {
        var s = ServicePackageHelpers.AggregateEntitlements(new[]
        {
            Sub("{\"dailyPointMultiplier\":1.5,\"mapPriorityBoost\":10}"),
            Sub("{\"dailyPointMultiplier\":1.2,\"mapPriorityBoost\":50}")
        });
        s.DailyPointMultiplier.Should().Be(1.5m);
        s.MapPriorityBoost.Should().Be(50);
    }

    [Fact]
    public void Aggregate_CorruptSnapshot_IsIgnored()
    {
        var s = ServicePackageHelpers.AggregateEntitlements(new[] { Sub("{{{"), Sub(CanonicalServicePackages.PremiumFeaturesJson) });
        s.CurrentTier.Should().Be("Premium");
    }

    [Theory]
    [InlineData("PRO", false, true)]
    [InlineData(" pro ", false, true)]
    [InlineData("PREMIUM", false, false)]
    [InlineData("BASIC", false, false)]
    [InlineData(null, false, false)]
    [InlineData("PREMIUM", true, true)]     // yêu cầu KYB → coi như Pro
    [InlineData(null, true, true)]
    public void IsProPackage_Rules(string? code, bool kyb, bool expected)
        => ServicePackageHelpers.IsProPackage(code, kyb).Should().Be(expected);

    [Theory]
    [InlineData("PRO", false, false, 2)]
    [InlineData("Pro", false, false, 2)]
    [InlineData("PREMIUM", false, true, 2)] // requiresKyb thắng
    [InlineData("BASIC", false, false, 0)]
    [InlineData(null, false, false, 0)]
    [InlineData("", false, false, 0)]
    [InlineData("PREMIUM", false, false, 1)]
    [InlineData("Premium", false, false, 1)]
    [InlineData("PREMIUM", true, false, 0)]  // isDefaultFree thắng nhánh còn lại
    public void GetTierRank_Rules(string? tier, bool defaultFree, bool kyb, int expected)
        => ServicePackageHelpers.GetTierRank(tier, defaultFree, kyb).Should().Be(expected);

    [Fact]
    public void GetTierRank_Ordering_Basic_LessThan_Premium_LessThan_Pro()
    {
        ServicePackageHelpers.GetTierRank("BASIC").Should().BeLessThan(ServicePackageHelpers.GetTierRank("PREMIUM"));
        ServicePackageHelpers.GetTierRank("PREMIUM").Should().BeLessThan(ServicePackageHelpers.GetTierRank("PRO"));
    }

    [Fact]
    public void ComputeSha256Hex_IsUpperCaseHex()
        => ServicePackageHelpers.ComputeSha256Hex("abc")
            .Should().Be("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD");
}

// ═════════════════════════════════════════════════════════════════════
[Trait("Layer", "Unit")]
[Trait("Area", "PureLogic")]
public class HandoverTradeSyncHelperTests
{
    [Fact]
    public void StatusMapping_RoundTrips_ForEveryHandoverStatus()
    {
        foreach (var s in Enum.GetValues<HandoverStatus>())
        {
            var order = HandoverTradeSyncHelper.ToOrderStatus(s);
            order.ToString().Should().Be(s.ToString(), "tên trạng thái hai enum phải tương ứng");
            HandoverTradeSyncHelper.ToHandoverStatus(order).Should().Be(s);
        }
    }

    [Fact]
    public void StatusMapping_RoundTrips_ForEveryOrderStatus()
    {
        foreach (var o in Enum.GetValues<OrderStatus>())
        {
            var h = HandoverTradeSyncHelper.ToHandoverStatus(o);
            HandoverTradeSyncHelper.ToOrderStatus(h).Should().Be(o);
        }
    }

    [Theory]
    [InlineData(2026, 12, 1, 1, 0, "2026-12-01 (08:00 - 10:00)")]     // UTC 01:00 = ICT 08:00
    [InlineData(2026, 12, 1, 17, 30, "2026-12-02 (00:30 - 02:30)")]   // qua nửa đêm ICT → sang ngày hôm sau
    [InlineData(2026, 12, 1, 15, 30, "2026-12-01 (22:30 - 23:30)")]   // giờ kết thúc bị chặn ở 23
    public void FormatSlotText_ConvertsUtcToIct_AndCapsEndHour(int y, int mo, int d, int h, int mi, string expected)
        => HandoverTradeSyncHelper.FormatSlotText(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc)).Should().Be(expected);

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 15)]
    [InlineData(8, 45)]
    [InlineData(16, 0)]      // ICT 23:00
    public void FormatSlotText_Then_ParseSlotToUtc_RoundTrips(int hourUtc, int minute)
    {
        var utc = new DateTime(2026, 12, 1, hourUtc, minute, 0, DateTimeKind.Utc);
        var fallback = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        HandoverTradeSyncHelper.ParseSlotToUtc(HandoverTradeSyncHelper.FormatSlotText(utc), fallback).Should().Be(utc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sáng mai")]          // không có ngày/giờ nhận dạng được
    public void TryParseAppointmentSlot_Unparseable_ReturnsFalse_AndFallback(string? text)
    {
        var fallback = new DateTime(2026, 12, 1, 3, 0, 0, DateTimeKind.Utc);
        HandoverTradeSyncHelper.TryParseAppointmentSlotUtc(text, fallback, out var utc, out var hasDate, out var hasTime)
            .Should().BeFalse();
        utc.Should().Be(fallback);
        hasDate.Should().BeFalse();
        hasTime.Should().BeFalse();
    }

    [Fact]
    public void ParseSlotToUtc_Unparseable_ReturnsFallbackAsUtc()
    {
        var fallback = new DateTime(2026, 12, 1, 3, 0, 0, DateTimeKind.Unspecified);
        var r = HandoverTradeSyncHelper.ParseSlotToUtc("không rõ", fallback);
        r.Kind.Should().Be(DateTimeKind.Utc);
        r.Should().Be(fallback);
    }

    // ───── ValidateAppointmentWindowUtc (phụ thuộc DateTime.UtcNow nên chỉ dùng mốc cách xa biên) ─────
    [Fact]
    public void ValidateWindow_Tomorrow_IsAllowed()
    {
        var act = () => HandoverTradeSyncHelper.ValidateAppointmentWindowUtc(DateTime.UtcNow.AddDays(1));
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateWindow_MaxAheadDays_IsAllowed()
    {
        var act = () => HandoverTradeSyncHelper.ValidateAppointmentWindowUtc(
            DateTime.UtcNow.AddDays(HandoverTradeSyncHelper.MaxAppointmentAheadDays));
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateWindow_BeyondMaxAhead_Throws_BR_NEG_04()
    {
        var act = () => HandoverTradeSyncHelper.ValidateAppointmentWindowUtc(
            DateTime.UtcNow.AddDays(HandoverTradeSyncHelper.MaxAppointmentAheadDays + 1));
        act.Should().Throw<BusinessRuleException>().Which.RuleCode.Should().Be("BR-NEG-04");
    }

    [Theory]
    [InlineData(-2, true)]
    [InlineData(-2, false)]
    [InlineData(-30, true)]
    public void ValidateWindow_PastDate_Throws_RegardlessOfCheckTime(int days, bool checkTime)
    {
        var act = () => HandoverTradeSyncHelper.ValidateAppointmentWindowUtc(DateTime.UtcNow.AddDays(days), checkTime);
        act.Should().Throw<BusinessRuleException>().Which.RuleCode.Should().Be("BR-NEG-04");
    }

    [Fact]
    public void ValidateWindow_FewMinutesAgo_Throws_WhenCheckingTime()
    {
        var act = () => HandoverTradeSyncHelper.ValidateAppointmentWindowUtc(DateTime.UtcNow.AddMinutes(-10), checkTimeToo: true);
        act.Should().Throw<BusinessRuleException>();
    }
}

// ═════════════════════════════════════════════════════════════════════
[Trait("Layer", "Unit")]
[Trait("Area", "PureLogic")]
public class GamificationCommonTests
{
    [Fact]
    public void GamTime_Ict_IsPlus7()
        => GamTime.Ict.Should().Be(TimeSpan.FromHours(7));

    [Theory]
    [InlineData(2026, 1, 1, 2025, 12, 31, 17)]
    [InlineData(2026, 3, 15, 2026, 3, 14, 17)]
    public void GamTime_StartOfDayUtc_IsMidnightIct_AsUtcKind(int y, int m, int d, int ey, int em, int ed, int eh)
    {
        var r = GamTime.StartOfDayUtc(new DateOnly(y, m, d));
        r.Should().Be(new DateTime(ey, em, ed, eh, 0, 0, DateTimeKind.Utc));
        r.Kind.Should().Be(DateTimeKind.Utc, "Npgsql từ chối Kind=Unspecified với timestamptz");
    }

    [Fact]
    public void GamTime_Today_IsWithinOneDayOfUtcDate()
    {
        var utc = DateOnly.FromDateTime(DateTime.UtcNow);
        (GamTime.Today().DayNumber - utc.DayNumber).Should().BeInRange(0, 1);
    }

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("50%", "50\\%")]
    [InlineData("a_b", "a\\_b")]
    [InlineData("a\\b", "a\\\\b")]
    [InlineData("%_\\", "\\%\\_\\\\")]
    [InlineData("", "")]
    public void EscapeLike_EscapesWildcards(string input, string expected)
        => GamErrors.EscapeLike(input).Should().Be(expected);

    [Theory]
    [InlineData("  abc  ", "%abc%")]
    [InlineData("50%", "%50\\%%")]
    public void Like_TrimsAndWraps(string input, string expected)
        => GamErrors.Like(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CleanImageUrl_Blank_ReturnsNull(string? url)
        => GamErrors.CleanImageUrl(url, "R").Should().BeNull();

    [Theory]
    [InlineData("https://cdn.x/a.jpg")]
    [InlineData("http://cdn.x/a.PNG")]
    [InlineData("https://cdn.x/a.webp?v=1")]
    [InlineData("https://cdn.x/noext")]          // không có đuôi → cho qua
    [InlineData("/assets/img/a.jpeg")]
    public void CleanImageUrl_Valid_ReturnsTrimmedUrl(string url)
        => GamErrors.CleanImageUrl("  " + url + "  ", "R").Should().Be(url);

    [Theory]
    [InlineData("https://cdn.x/a.gif", "MSG-CLS-01")]
    [InlineData("https://cdn.x/a.svg", "MSG-CLS-01")]
    [InlineData("/assets/a.gif", "MSG-CLS-01")]
    [InlineData("/assets/noext", "MSG-CLS-01")]
    [InlineData("ftp://cdn.x/a.jpg", "MSG-AUTH-02")]
    [InlineData("javascript:alert(1)", "MSG-AUTH-02")]
    [InlineData("not a url", "MSG-AUTH-02")]
    public void CleanImageUrl_Invalid_Throws_WithMessageCode(string url, string code)
    {
        var act = () => GamErrors.CleanImageUrl(url, "BR-X");
        var ex = act.Should().Throw<BusinessRuleException>().Which;
        ex.MessageCode.Should().Be(code);
        ex.RuleCode.Should().Be("BR-X");
    }

    [Fact]
    public void CleanImageUrl_TooLong_Throws_MSG_NTF_03()
    {
        var act = () => GamErrors.CleanImageUrl("https://x/" + new string('a', 1000) + ".jpg", "R");
        act.Should().Throw<BusinessRuleException>().Which.MessageCode.Should().Be("MSG-NTF-03");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void CleanHttpUrl_Blank_ReturnsNull(string? url)
        => GamErrors.CleanHttpUrl(url, "R", "Link").Should().BeNull();

    [Fact]
    public void CleanHttpUrl_Valid_ReturnsTrimmed()
        => GamErrors.CleanHttpUrl(" https://a.b/c ", "R", "Link").Should().Be("https://a.b/c");

    [Theory]
    [InlineData("ftp://a.b")]
    [InlineData("a.b/c")]
    public void CleanHttpUrl_Invalid_Throws_AndMentionsLabel(string url)
    {
        var act = () => GamErrors.CleanHttpUrl(url, "R", "Nguồn");
        var ex = act.Should().Throw<BusinessRuleException>().Which;
        ex.MessageCode.Should().Be("MSG-AUTH-02");
        ex.Message.Should().Contain("Nguồn");
    }

    [Fact]
    public void CleanHttpUrl_TooLong_Throws()
    {
        var act = () => GamErrors.CleanHttpUrl("https://x/" + new string('a', 1000), "R", "Link");
        act.Should().Throw<BusinessRuleException>().Which.MessageCode.Should().Be("MSG-NTF-03");
    }

    [Fact]
    public void Rule_BuildsBusinessRuleException()
    {
        var ex = GamErrors.Rule("BR-1", "MSG-1", "m");
        ex.RuleCode.Should().Be("BR-1");
        ex.MessageCode.Should().Be("MSG-1");
        ex.Message.Should().Be("m");
    }

    [Theory]
    [InlineData(PointActionType.HandoverReward, "Handover_Reward")]
    [InlineData(PointActionType.DailyCheckin, "Daily_Checkin")]
    [InlineData(PointActionType.RedeemGift, "Redeem_Gift")]
    [InlineData(PointActionType.TreeCare, "Tree_Care")]
    [InlineData(PointActionType.LeaderboardBonus, "Leaderboard_Bonus")]
    [InlineData(PointActionType.Adjustment, "Adjustment")]
    [InlineData((PointActionType)999, "Adjustment")]
    public void PointsService_ActionText(PointActionType a, string expected)
        => PointsService.ActionText(a).Should().Be(expected);
}

// ═════════════════════════════════════════════════════════════════════
[Trait("Layer", "Unit")]
[Trait("Area", "PureLogic")]
public class TabularReportExportHelperTests
{
    private static string CsvText(byte[] bytes) => Encoding.UTF8.GetString(bytes).TrimStart('﻿');

    [Fact]
    public void Csv_StartsWithUtf8Bom()
    {
        var b = TabularReportExportHelper.BuildUtf8BomCsv(new[] { "A" }, new List<string[]>());
        b.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF });
    }

    [Fact]
    public void Csv_HeaderOnly_WhenNoRows()
        => CsvText(TabularReportExportHelper.BuildUtf8BomCsv(new[] { "Mã", "Tên" }, new List<string[]>()))
            .Should().Be("Mã,Tên\r\n");

    [Fact]
    public void Csv_PlainRows_AreCommaSeparated_CrLf()
        => CsvText(TabularReportExportHelper.BuildUtf8BomCsv(new[] { "A", "B" },
                new List<string[]> { new[] { "1", "2" }, new[] { "3", "4" } }))
            .Should().Be("A,B\r\n1,2\r\n3,4\r\n");

    [Theory]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("line1\r\nline2", "\"line1\r\nline2\"")]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    public void Csv_EscapesSpecialCharacters(string cell, string expectedCell)
        => CsvText(TabularReportExportHelper.BuildUtf8BomCsv(new[] { "H" }, new List<string[]> { new[] { cell } }))
            .Should().Be("H\r\n" + expectedCell + "\r\n");

    [Fact]
    public void Csv_EscapesHeaders_TooAndKeepsVietnamese()
        => CsvText(TabularReportExportHelper.BuildUtf8BomCsv(new[] { "Tên, đầy đủ" }, new List<string[]>()))
            .Should().Be("\"Tên, đầy đủ\"\r\n");

    // ───── xlsx ─────
    private static Dictionary<string, string> Unzip(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var r = new StreamReader(e.Open(), Encoding.UTF8);
            return r.ReadToEnd();
        });
    }

    [Fact]
    public void Xlsx_ContainsRequiredOpenXmlParts()
    {
        var files = Unzip(TabularReportExportHelper.BuildMinimalOpenXmlWorkbook("Báo cáo", new[] { "A" }, new List<string[]>()));
        files.Keys.Should().Contain(new[]
        {
            "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/worksheets/sheet1.xml"
        });
    }

    [Fact]
    public void Xlsx_SheetContainsHeaderAndRowValues_WithCellReferences()
    {
        var files = Unzip(TabularReportExportHelper.BuildMinimalOpenXmlWorkbook("KPI", new[] { "Tên", "SL" },
            new List<string[]> { new[] { "Nguyễn A", "5" } }));
        var sheet = files["xl/worksheets/sheet1.xml"];
        sheet.Should().Contain("r=\"A1\"").And.Contain("r=\"B1\"").And.Contain("r=\"A2\"").And.Contain("r=\"B2\"");
        sheet.Should().Contain("Tên").And.Contain("Nguyễn A").And.Contain(">5<");
    }

    [Fact]
    public void Xlsx_ColumnNames_RollOverAfterZ()
    {
        var headers = Enumerable.Range(1, 28).Select(i => "H" + i).ToArray();
        var sheet = Unzip(TabularReportExportHelper.BuildMinimalOpenXmlWorkbook("T", headers, new List<string[]>()))["xl/worksheets/sheet1.xml"];
        sheet.Should().Contain("r=\"Z1\"").And.Contain("r=\"AA1\"").And.Contain("r=\"AB1\"");
    }

    [Theory]
    [InlineData("Báo cáo 2026!", "Báocáo2026")]
    [InlineData("!!!", "Report")]
    [InlineData("", "Report")]
    public void Xlsx_SheetName_IsSanitized(string title, string expected)
    {
        var wb = Unzip(TabularReportExportHelper.BuildMinimalOpenXmlWorkbook(title, new[] { "A" }, new List<string[]>()))["xl/workbook.xml"];
        wb.Should().Contain($"name=\"{expected}\"");
    }

    [Fact]
    public void Xlsx_SheetName_IsTruncatedTo25Chars()
    {
        var wb = Unzip(TabularReportExportHelper.BuildMinimalOpenXmlWorkbook(new string('a', 40), new[] { "A" }, new List<string[]>()))["xl/workbook.xml"];
        wb.Should().Contain($"name=\"{new string('a', 25)}\"").And.NotContain(new string('a', 26));
    }

    [Fact]
    public void Xlsx_XmlSpecialCharacters_AreEscaped_SoFileStaysWellFormed()
    {
        var sheet = Unzip(TabularReportExportHelper.BuildMinimalOpenXmlWorkbook("T", new[] { "A" },
            new List<string[]> { new[] { "<b>&\"x\"</b>" } }))["xl/worksheets/sheet1.xml"];
        var act = () => System.Xml.Linq.XDocument.Parse(sheet);
        act.Should().NotThrow();
        sheet.Should().Contain("&lt;b&gt;&amp;");
    }
}

// ═════════════════════════════════════════════════════════════════════
[Trait("Layer", "Unit")]
[Trait("Area", "PureLogic")]
public class StaffPermissionsTests
{
    [Fact]
    public void Catalog_Has7UniqueCodes_AndUniqueAliases()
    {
        StaffPermissions.Catalog.Should().HaveCount(7);
        StaffPermissions.AllCodes.Should().OnlyHaveUniqueItems();
        StaffPermissions.Catalog.Select(c => c.LegacyAlias).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("REVIEW_AI", "REVIEW_AI")]
    [InlineData("review_ai", "REVIEW_AI")]
    [InlineData("ai.review", "REVIEW_AI")]
    [InlineData("  BLOG.WRITE  ", "MANAGE_BLOG")]
    [InlineData("gift.manage", "MANAGE_INVENTORY")]
    [InlineData("account.manage", "MANAGE_ACCOUNTS")]
    public void Normalize_MapsCodesAndAliases(string raw, string expected)
        => StaffPermissions.Normalize(raw).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_Blank_ReturnsEmpty(string? raw)
        => StaffPermissions.Normalize(raw).Should().BeEmpty();

    [Theory]
    [InlineData("unknown_perm", "UNKNOWN_PERM")]
    public void Normalize_Unknown_ReturnsUpperTrimmed(string raw, string expected)
        => StaffPermissions.Normalize(raw).Should().Be(expected);

    [Theory]
    [InlineData("REVIEW_AI", true)]
    [InlineData("ai.review", true)]
    [InlineData("manage_blog", true)]
    [InlineData("HACK", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_Rules(string? raw, bool expected)
        => StaffPermissions.IsValid(raw).Should().Be(expected);

    [Fact]
    public void EveryCatalogCode_And_Alias_IsValid()
    {
        foreach (var (code, alias, _, _) in StaffPermissions.Catalog)
        {
            StaffPermissions.IsValid(code).Should().BeTrue();
            StaffPermissions.IsValid(alias).Should().BeTrue();
            StaffPermissions.Normalize(alias).Should().Be(code);
        }
    }
}

// ═════════════════════════════════════════════════════════════════════
[Trait("Layer", "Unit")]
[Trait("Area", "PureLogic")]
public class GeoDistanceTests
{
    // CalculateDistanceKm là private static → gọi qua reflection (công thức Haversine, R = 6371 km).
    private static double Dist(double lat1, double lon1, double lat2, double lon2)
    {
        var mi = typeof(GeoMatchingService).GetMethod("CalculateDistanceKm", BindingFlags.NonPublic | BindingFlags.Static);
        mi.Should().NotBeNull("GeoMatchingService.CalculateDistanceKm đã bị đổi tên/chữ ký – cập nhật test");
        return (double)mi!.Invoke(null, new object[] { lat1, lon1, lat2, lon2 })!;
    }

    [Fact]
    public void SamePoint_IsZero() => Dist(10.77, 106.69, 10.77, 106.69).Should().BeApproximately(0, 1e-9);

    [Fact]
    public void IsSymmetric()
        => Dist(21.0285, 105.8542, 10.8231, 106.6297).Should().BeApproximately(Dist(10.8231, 106.6297, 21.0285, 105.8542), 1e-9);

    [Fact]
    public void OneDegreeOfLongitudeAtEquator_Is111Km()
        => Dist(0, 0, 0, 1).Should().BeApproximately(111.19, 0.05);

    [Fact]
    public void OneDegreeOfLatitude_Is111Km_AnywhereOnTheMeridian()
    {
        Dist(0, 100, 1, 100).Should().BeApproximately(111.19, 0.05);
        Dist(60, 100, 61, 100).Should().BeApproximately(111.19, 0.05);
    }

    [Fact]
    public void LongitudeDegreeShrinksWithLatitude()
        => Dist(60, 0, 60, 1).Should().BeApproximately(111.19 * Math.Cos(60 * Math.PI / 180), 0.1);

    [Fact]
    public void HanoiToHoChiMinh_IsAbout1138Km()
        => Dist(21.0285, 105.8542, 10.8231, 106.6297).Should().BeInRange(1130, 1146);

    [Fact]
    public void Antipodes_AreHalfTheEarthCircumference()
        => Dist(0, 0, 0, 180).Should().BeApproximately(Math.PI * 6371.0, 1.0);

    [Fact]
    public void ShortDistance_100Metres_IsAccurate()
        => Dist(10.0, 106.0, 10.0009, 106.0).Should().BeApproximately(0.1, 0.005);

    [Fact]
    public void CrossingTheDateLine_UsesShortestArc()
        => Dist(0, 179.5, 0, -179.5).Should().BeApproximately(111.19, 0.05);
}
