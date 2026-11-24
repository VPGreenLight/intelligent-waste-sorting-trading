using System.Net;
using System.Text;
using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Application.Validators;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Services;
using Ecolink.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ecolink.UnitTests.Module02_AiClassification;

[Trait("Module", "Module02_AiWasteClassification")]
public class AiClassificationUnitTests
{
    private static AiVisionHttpClient CreateAiClient(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var config = TestDbContextFactory.CreateConfiguration();
        return new AiVisionHttpClient(httpClient, config, NullLogger<AiVisionHttpClient>.Instance);
    }

    private static WasteRuleEngineService CreateRuleEngine()
    {
        var svc = new WasteRuleEngineService(NullLogger<WasteRuleEngineService>.Instance);
        svc.InvalidateRuleCacheAsync(null).GetAwaiter().GetResult();
        return svc;
    }

    [Fact]
    public async Task UTC001_ClassifyWasteImageAsync_ValidPlasticImageConfidence085_ReturnsSuccessAndPlasticPrediction()
    {
        var payload = """
        {
          "success": true,
          "is_out_of_distribution": false,
          "model_used": "yolov8_cls",
          "inference_time_ms": 42.5,
          "top_prediction": {
            "label": "plastic",
            "display_name": "Nhựa tái chế (Plastic)",
            "confidence": 0.85
          }
        }
        """;
        var handler = MockHttpMessageHandler.FromJson(HttpStatusCode.OK, payload);
        var sut = CreateAiClient(handler);
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

        var res = await sut.ClassifyWasteImageAsync(stream, "bottle.jpg", "yolov8_cls");

        res.Success.Should().BeTrue();
        res.IsOutOfDistribution.Should().BeFalse();
        res.TopPrediction.Should().NotBeNull();
        res.TopPrediction!.Label.Should().Be("plastic");
        res.TopPrediction.Confidence.Should().Be(0.85);
        handler.CapturedRequests.Should().ContainSingle()
            .Which.RequestUri!.ToString().Should().Be("http://localhost:8000/api/v1/classify");
    }

    [Fact]
    public async Task UTC002_ClassifyWasteImageAsync_BoundaryExactConfidence040_AcceptedAsInDistribution()
    {
        var payload = """
        {
          "success": true,
          "is_out_of_distribution": false,
          "model_used": "mobilenetv2_cls",
          "inference_time_ms": 31.0,
          "top_prediction": {
            "label": "paper",
            "display_name": "Giấy báo & Tài liệu (Paper)",
            "confidence": 0.40
          }
        }
        """;
        var sut = CreateAiClient(MockHttpMessageHandler.FromJson(HttpStatusCode.OK, payload));
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var res = await sut.ClassifyWasteImageAsync(stream, "newspaper.png", "mobilenetv2_cls");

        res.Success.Should().BeTrue();
        res.IsOutOfDistribution.Should().BeFalse();
        res.TopPrediction!.Label.Should().Be("paper");
        res.TopPrediction.Confidence.Should().Be(0.40);
    }

    [Fact]
    public async Task UTC003_ClassifyWasteImageAsync_BelowClassificationThreshold0399_ReturnsOutOfDistributionUnknown()
    {
        var payload = """
        {
          "success": true,
          "is_out_of_distribution": true,
          "model_used": "yolov8_cls",
          "inference_time_ms": 28.4,
          "top_prediction": {
            "label": "unknown",
            "display_name": "Không xác định (Độ tự tin thấp)",
            "confidence": 0.399
          }
        }
        """;
        var sut = CreateAiClient(MockHttpMessageHandler.FromJson(HttpStatusCode.OK, payload));
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var res = await sut.ClassifyWasteImageAsync(stream, "blurry.webp", "yolov8_cls");

        res.Success.Should().BeTrue();
        res.IsOutOfDistribution.Should().BeTrue();
        res.TopPrediction!.Label.Should().Be("unknown");
        res.TopPrediction.Confidence.Should().BeLessThan(0.40);
    }

    [Fact]
    public async Task UTC004_ClassifyWasteImageAsync_BoundaryYoloV8DetectConfidence035_AcceptedWithBoundingBox()
    {
        var payload = """
        {
          "success": true,
          "is_out_of_distribution": false,
          "model_used": "yolov8_detect",
          "detected_count": 1,
          "top_prediction": {
            "label": "metal",
            "display_name": "Kim loại & Vỏ lon (Metal)",
            "confidence": 0.35
          },
          "detected_objects": [
            {
              "id": 1,
              "label": "metal",
              "display_name": "Kim loại & Vỏ lon (Metal)",
              "confidence": 0.35,
              "bbox": [10, 20, 110, 220]
            }
          ]
        }
        """;
        var sut = CreateAiClient(MockHttpMessageHandler.FromJson(HttpStatusCode.OK, payload));
        using var stream = new MemoryStream(new byte[] { 10, 20, 30 });

        var res = await sut.ClassifyWasteImageAsync(stream, "can.jpg", "yolov8_detect");

        res.Success.Should().BeTrue();
        res.IsOutOfDistribution.Should().BeFalse();
        res.DetectedObjects.Should().ContainSingle();
        res.DetectedObjects[0].Confidence.Should().Be(0.35);
    }

    [Fact]
    public async Task UTC005_ClassifyWasteImageAsync_YoloV8DetectBelow035NoBoxes_ReturnsOutOfDistributionUnknown()
    {
        var payload = """
        {
          "success": true,
          "is_out_of_distribution": true,
          "model_used": "yolov8_detect",
          "detected_count": 0,
          "top_prediction": {
            "label": "unknown",
            "display_name": "Không phát hiện vật thể đủ ngưỡng 0.35",
            "confidence": 0.34
          },
          "detected_objects": []
        }
        """;
        var sut = CreateAiClient(MockHttpMessageHandler.FromJson(HttpStatusCode.OK, payload));
        using var stream = new MemoryStream(new byte[] { 10, 20 });

        var res = await sut.ClassifyWasteImageAsync(stream, "empty_table.jpg", "yolov8_detect");

        res.IsOutOfDistribution.Should().BeTrue();
        res.TopPrediction!.Label.Should().Be("unknown");
        res.DetectedObjects.Should().BeEmpty();
    }

    [Fact]
    public async Task UTC006_ClassifyWasteImageAsync_UnsupportedFileOrExceeds10Mb422_ReturnsSuccessFalseWithErrorMessage()
    {
        var errorBody = """{"detail":"MSG-CLS-01: Định dạng tệp không hợp lệ hoặc vượt quá 10MB."}""";
        var sut = CreateAiClient(MockHttpMessageHandler.FromJson(HttpStatusCode.UnprocessableEntity, errorBody));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake-pdf-content"));

        var res = await sut.ClassifyWasteImageAsync(stream, "malware.pdf", "yolov8_detect");

        res.Success.Should().BeFalse();
        res.ErrorMessage.Should().Contain("MSG-CLS-01");
    }

    [Fact]
    public async Task UTC007_ClassifyWasteImageAsync_CorruptedImageBytes400_ReturnsFailureGracefully()
    {
        var errorBody = "Cannot identify image file (corrupted stream)";
        var sut = CreateAiClient(MockHttpMessageHandler.FromJson(HttpStatusCode.BadRequest, errorBody));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not-an-image-stream"));

        var res = await sut.ClassifyWasteImageAsync(stream, "corrupted.jpg");

        res.Success.Should().BeFalse();
        res.ErrorMessage.Should().Contain("Cannot identify image file");
    }

    [Fact]
    public async Task UTC008_ClassifyWasteImageAsync_AiServiceOfflineThrowsHttpRequestException_ReturnsFallbackUnknownWithoutCrash()
    {
        var handler = MockHttpMessageHandler.FromException(new HttpRequestException("Connection refused (localhost:8000)"));
        var sut = CreateAiClient(handler);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var res = await sut.ClassifyWasteImageAsync(stream, "bottle.jpg");

        res.Success.Should().BeFalse();
        res.ErrorMessage.Should().Contain("Lỗi kết nối tới AI Service");
        res.TopPrediction.Should().NotBeNull();
        res.TopPrediction!.Label.Should().Be("unknown");
    }

    [Fact]
    public async Task UTC009_ClassifyWasteImageAsync_MultiObjectDetection_ParsesObjectCountAndClassCountsCorrectly()
    {
        var payload = """
        {
          "success": true,
          "is_out_of_distribution": false,
          "model_used": "yolov8_detect",
          "detected_count": 3,
          "category_summary": {
            "plastic": 2,
            "metal": 1
          },
          "top_prediction": {
            "label": "plastic",
            "display_name": "Nhựa tái chế (Plastic)",
            "confidence": 0.91
          },
          "detected_objects": [
            { "id": 1, "label": "plastic", "display_name": "Nhựa", "confidence": 0.91 },
            { "id": 2, "label": "plastic", "display_name": "Nhựa", "confidence": 0.84 },
            { "id": 3, "label": "metal", "display_name": "Kim loại", "confidence": 0.78 }
          ]
        }
        """;
        var sut = CreateAiClient(MockHttpMessageHandler.FromJson(HttpStatusCode.OK, payload));
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var res = await sut.ClassifyWasteImageAsync(stream, "mixed_waste.jpg", "yolov8_detect");

        res.Success.Should().BeTrue();
        res.DetectedCount.Should().Be(3);
        res.DetectedObjects.Should().HaveCount(3);
        res.CategorySummary.Should().NotBeNull();
        res.CategorySummary["plastic"].Should().Be(2);
        res.CategorySummary["metal"].Should().Be(1);
    }

    [Fact]
    public async Task UTC010_GetSortingGuideAsync_ValidPlasticCode_ReturnsOrangeBinAndPreparationSteps()
    {
        var sut = CreateRuleEngine();

        var guide = await sut.GetSortingGuideAsync("plastic");

        guide.Should().NotBeNull();
        guide!.CategoryCode.Should().Be("plastic");
        guide.CategoryName.Should().Be("Nhựa tái chế (Plastic)");
        guide.StandardBinColor.Should().Be("Cam");
        guide.PreparationSteps.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UTC011_GetSortingGuideAsync_UppercaseWithWhitespaceCardboard_NormalizesAndReturnsGuide()
    {
        var sut = CreateRuleEngine();

        var guide = await sut.GetSortingGuideAsync("  CARDBOARD  ");

        guide.Should().NotBeNull();
        guide!.CategoryCode.Should().Be("cardboard");
        guide.StandardBinColor.Should().Be("Nâu Vàng");
    }

    [Fact]
    public async Task UTC012_GetSortingGuideAsync_TrashNonRecyclableCategory_ReturnsBlackBinDisposalGuide()
    {
        var sut = CreateRuleEngine();

        var guide = await sut.GetSortingGuideAsync("trash");

        guide.Should().NotBeNull();
        guide!.CategoryCode.Should().Be("trash");
        guide.StandardBinColor.Should().Be("Đen");
        guide.HandlingAction.Should().Contain("Đen");
    }

    [Fact]
    public async Task UTC013_GetSortingGuideAsync_UnknownCategoryCode_ReturnsNullWithoutThrowing()
    {
        var sut = CreateRuleEngine();

        var guide = await sut.GetSortingGuideAsync("unknown_xyz");

        guide.Should().BeNull();
    }

    [Fact]
    public async Task UTC014_GetAllRulesAsync_DefaultInitialization_ReturnsAll10StandardCategories()
    {
        var sut = CreateRuleEngine();

        var rules = (await sut.GetAllRulesAsync()).ToList();

        rules.Should().HaveCount(10);
        rules.Select(r => r.CategoryCode).Should().BeEquivalentTo(
            new[] { "plastic", "paper", "glass", "metal", "cardboard", "battery", "biological", "clothes", "shoes", "trash" });
    }

    [Fact]
    public async Task UTC015_InvalidateRuleCacheAsync_SpecificCategoryPlastic_RemovesOnlyPlasticFromCache()
    {
        var sut = CreateRuleEngine();

        await sut.InvalidateRuleCacheAsync("plastic");

        var plastic = await sut.GetSortingGuideAsync("plastic");
        var paper = await sut.GetSortingGuideAsync("paper");
        var allRemaining = (await sut.GetAllRulesAsync()).ToList();

        plastic.Should().BeNull();
        paper.Should().NotBeNull();
        allRemaining.Should().HaveCount(9);

        // Khôi phục lại cache mặc định sau test
        await sut.InvalidateRuleCacheAsync(null);
    }

    [Fact]
    public async Task UTC016_InvalidateRuleCacheAsync_NullCategory_ResetsAndReloadsAll10DefaultRules()
    {
        var sut = CreateRuleEngine();
        await sut.InvalidateRuleCacheAsync("plastic");
        await sut.InvalidateRuleCacheAsync("metal");

        await sut.InvalidateRuleCacheAsync(null);

        var rules = (await sut.GetAllRulesAsync()).ToList();
        rules.Should().HaveCount(10);
        (await sut.GetSortingGuideAsync("plastic")).Should().NotBeNull();
        (await sut.GetSortingGuideAsync("metal")).Should().NotBeNull();
    }

    [Fact]
    public async Task UTC017_SubmitFeedbackAsync_ValidFeedbackAndBlankPredictedLabel_PostsSnakeCasePayloadAndReturnsTrue()
    {
        var handler = MockHttpMessageHandler.FromJson(HttpStatusCode.OK, """{"status":"saved"}""");
        var sut = CreateAiClient(handler);

        var ok = await sut.SubmitFeedbackAsync(new SubmitFeedbackDto
        {
            PredictedLabel = "   ",
            UserReportedLabel = "glass",
            ConfidenceScore = 0.62,
            Note = "Đây là chai thủy tinh trong suốt"
        });

        ok.Should().BeTrue();
        handler.CapturedBodies.Should().ContainSingle();
        using var doc = JsonDocument.Parse(handler.CapturedBodies[0]!);
        doc.RootElement.GetProperty("predicted_label").GetString().Should().Be("unknown");
        doc.RootElement.GetProperty("user_reported_label").GetString().Should().Be("glass");
    }

    [Fact]
    public async Task UTC018_SubmitFeedbackAsync_AiServiceThrowsException_CatchesAndReturnsFalse_BrCls04()
    {
        var handler = MockHttpMessageHandler.FromException(new HttpRequestException("AI Service timeout"));
        var sut = CreateAiClient(handler);

        var ok = await sut.SubmitFeedbackAsync(new SubmitFeedbackDto
        {
            PredictedLabel = "plastic",
            UserReportedLabel = "metal",
            ConfidenceScore = 0.51
        });

        ok.Should().BeFalse();
    }

    private static ICurrentUserService CreateAdminUserMock(Guid userId)
    {
        var mock = Substitute.For<ICurrentUserService>();
        mock.UserId.Returns((Guid?)userId);
        mock.Role.Returns("Admin");
        mock.IsAuthenticated.Returns(true);
        mock.IpAddress.Returns("127.0.0.1");
        mock.UserAgent.Returns("EcoLink-UnitTest/1.0");
        return mock;
    }

    [Fact]
    public async Task UTC019_GetMyScanHistoryAsync_IsolatesByUserIdAndHydratesSortingGuide_BrCls08()
    {
        using var db = TestDbContextFactory.Create();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.AiScanHistories.AddRange(
            new AiScanHistory
            {
                Id = Guid.NewGuid(),
                UserId = userA,
                ImageUrl = "https://cdn.ecolink.vn/scans/a1.jpg",
                ImageSha256 = new string('a', 64),
                ModelUsed = "yolov8_detect",
                PredictedLabel = "plastic",
                ConfidenceScore = 0.93,
                CreatedAt = now.AddMinutes(-10)
            },
            new AiScanHistory
            {
                Id = Guid.NewGuid(),
                UserId = userB,
                ImageUrl = "https://cdn.ecolink.vn/scans/b1.jpg",
                ImageSha256 = new string('b', 64),
                ModelUsed = "yolov8_detect",
                PredictedLabel = "glass",
                ConfidenceScore = 0.88,
                CreatedAt = now
            });
        await db.SaveChangesAsync();

        var ruleEngine = new WasteRuleEngineService(NullLogger<WasteRuleEngineService>.Instance, db);
        var auditSvc = new AuditLogService(db, CreateAdminUserMock(userA));
        var feedbackSvc = new AiFeedbackService(
            db,
            Substitute.For<IAiVisionClient>(),
            Substitute.For<IFileStorageService>(),
            auditSvc,
            NullLogger<AiFeedbackService>.Instance,
            ruleEngine);

        var page = await feedbackSvc.GetMyScanHistoryAsync(userA, new WasteScanHistoryFilterRequest());

        page.TotalRecords.Should().Be(1);
        page.Items.Should().ContainSingle();
        page.Items[0].PredictedLabel.Should().Be("plastic");
        page.Items[0].SortingGuide.Should().NotBeNull();
        page.Items[0].SortingGuide!.StandardBinColor.Should().Be("Cam");
    }

    [Fact]
    public async Task UTC020_GetMyScanHistoryAsync_FiltersByCategoryCodeAndDateRange()
    {
        using var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

        db.AiScanHistories.AddRange(
            new AiScanHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ImageUrl = "https://cdn.ecolink.vn/scans/1.jpg",
                ImageSha256 = new string('1', 64),
                ModelUsed = "yolov8_detect",
                PredictedLabel = "plastic",
                ConfidenceScore = 0.91,
                CreatedAt = baseTime.AddDays(-5)
            },
            new AiScanHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ImageUrl = "https://cdn.ecolink.vn/scans/2.jpg",
                ImageSha256 = new string('2', 64),
                ModelUsed = "yolov8_detect",
                PredictedLabel = "plastic",
                ConfidenceScore = 0.94,
                CreatedAt = baseTime
            },
            new AiScanHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ImageUrl = "https://cdn.ecolink.vn/scans/3.jpg",
                ImageSha256 = new string('3', 64),
                ModelUsed = "yolov8_detect",
                PredictedLabel = "paper",
                ConfidenceScore = 0.87,
                CreatedAt = baseTime
            });
        await db.SaveChangesAsync();

        var feedbackSvc = new AiFeedbackService(
            db,
            Substitute.For<IAiVisionClient>(),
            Substitute.For<IFileStorageService>(),
            new AuditLogService(db, CreateAdminUserMock(userId)),
            NullLogger<AiFeedbackService>.Instance);

        var filtered = await feedbackSvc.GetMyScanHistoryAsync(userId, new WasteScanHistoryFilterRequest
        {
            CategoryCode = "  PLASTIC ",
            FromDate = baseTime.AddHours(-1),
            ToDate = baseTime.AddHours(1)
        });

        filtered.TotalRecords.Should().Be(1);
        filtered.Items[0].ImageUrl.Should().EndWith("/2.jpg");
    }

    [Fact]
    public async Task UTC021_CreateWasteCategoryAsync_ValidCategory_CreatesActiveAndLogsAudit_BrSrt02()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var auditSvc = new AuditLogService(db, CreateAdminUserMock(adminId));
        var sut = new RecyclerManagementService(db, NullLogger<RecyclerManagementService>.Instance, auditSvc);

        var created = await sut.CreateWasteCategoryAsync(adminId, new CreateWasteCategoryRequest
        {
            Code = " E_Waste ",
            Name = "Pin & Thiết bị điện tử",
            Description = "Rác thải điện tử thu hồi riêng",
            StandardBinColor = "Đỏ",
            IsRecyclable = false
        });

        created.Code.Should().Be("e_waste");
        created.Status.Should().Be("Active");
        created.IsRecyclable.Should().BeFalse();
        created.HasSortingRule.Should().BeTrue();
        created.RuleStatus.Should().Be("DRAFT");

        var audit = await db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.Action == "CREATE_WASTE_CATEGORY");
        audit.Should().NotBeNull();
        audit!.ActorUserId.Should().Be(adminId);
    }

    [Fact]
    public async Task UTC022_CreateWasteCategoryAsync_DuplicateCode_ThrowsConflictException_MsgSrt02()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        db.WasteCategories.Add(new WasteCategory
        {
            Code = "electronic",
            Name = "Điện tử",
            StandardBinColor = "Đỏ",
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var sut = new RecyclerManagementService(db, NullLogger<RecyclerManagementService>.Instance);

        var act = () => sut.CreateWasteCategoryAsync(adminId, new CreateWasteCategoryRequest
        {
            Code = "Electronic",
            Name = "Rác điện tử mới",
            StandardBinColor = "Tím"
        });

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.RuleCode.Should().Be("BR-SRT-02");
        ex.Which.MessageCode.Should().Be("MSG-SRT-02");
    }

    [Fact]
    public async Task UTC023_UpdateWasteCategoryAsync_UpdatesMetadataInvalidatesRuleCacheAndLogsAudit()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var cat = new WasteCategory
        {
            Code = "plastic",
            Name = "Nhựa cũ",
            StandardBinColor = "Cam",
            Status = "Active",
            IsRecyclable = true
        };
        db.WasteCategories.Add(cat);
        await db.SaveChangesAsync();

        var ruleEngineMock = Substitute.For<IWasteRuleEngineService>();
        var auditSvc = new AuditLogService(db, CreateAdminUserMock(adminId));
        var sut = new RecyclerManagementService(db, NullLogger<RecyclerManagementService>.Instance, auditSvc, ruleEngineMock);

        var updated = await sut.UpdateWasteCategoryAsync(adminId, cat.Id, new UpdateWasteCategoryRequest
        {
            Name = "Nhựa PET/HDPE Chuẩn",
            Description = "Nhựa sạch tái chế cao",
            StandardBinColor = "Vàng Cam",
            IsRecyclable = true
        });

        updated.Code.Should().Be("plastic");
        updated.Name.Should().Be("Nhựa PET/HDPE Chuẩn");
        updated.StandardBinColor.Should().Be("Vàng Cam");
        await ruleEngineMock.Received(1).InvalidateRuleCacheAsync("plastic", Arg.Any<CancellationToken>());
        (await db.AuditLogs.AnyAsync(a => a.Action == "UPDATE_WASTE_CATEGORY")).Should().BeTrue();
    }

    [Fact]
    public async Task UTC024_ToggleWasteCategoryStatusAsync_SoftDeactivatePreservesDbRowAndHidesActiveQueries_BrSrt04()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var auditSvc = new AuditLogService(db, CreateAdminUserMock(adminId));
        var ruleEngine = new WasteRuleEngineService(NullLogger<WasteRuleEngineService>.Instance, db, auditSvc);
        var recyclerSvc = new RecyclerManagementService(db, NullLogger<RecyclerManagementService>.Instance, auditSvc, ruleEngine);

        // Seed danh mục & quy tắc mặc định
        var initialCategories = await recyclerSvc.GetWasteCategoriesAsync();
        var plastic = initialCategories.First(c => c.Code == "plastic");

        var toggled = await recyclerSvc.ToggleWasteCategoryStatusAsync(adminId, plastic.Id, new ToggleWasteCategoryStatusRequest
        {
            Status = "Inactive"
        });

        toggled.Status.Should().Be("Inactive");

        // Hàng trong DB vẫn được bảo toàn (không xóa cứng BR-SRT-04)
        var dbRow = await db.WasteCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == plastic.Id);
        dbRow.Should().NotBeNull();
        dbRow!.Status.Should().Be("Inactive");

        // Danh mục Inactive không xuất hiện trong dropdown Active hay Rule Guide đang bật
        var activeOnly = await recyclerSvc.GetWasteCategoriesAsync();
        activeOnly.Any(c => c.Code == "plastic").Should().BeFalse();
        (await ruleEngine.GetSortingGuideAsync("plastic")).Should().BeNull();
    }

    [Fact]
    public async Task UTC025_UpsertSortingRuleAsync_CreatesAndUpdatesDraftRuleWithAudit_BrSrt01()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var cat = new WasteCategory
        {
            Code = "organic_compost",
            Name = "Rác hữu cơ",
            StandardBinColor = "Xanh Lá",
            IsRecyclable = true,
            Status = "Active"
        };
        db.WasteCategories.Add(cat);
        await db.SaveChangesAsync();

        var auditSvc = new AuditLogService(db, CreateAdminUserMock(adminId));
        var sut = new WasteRuleEngineService(NullLogger<WasteRuleEngineService>.Instance, db, auditSvc);

        var created = await sut.UpsertSortingRuleAsync(adminId, cat.Id, new UpsertSortingRuleRequest
        {
            Title = "Quy chuẩn phân loại rác hữu cơ v1",
            PreparationSteps = "1. Để ráo nước thừa.",
            SortingInstruction = "Tách riêng thức ăn thừa khỏi túi nilon.",
            HandlingAction = "Đưa vào thùng ủ phân vi sinh màu Xanh Lá.",
            ApplicableRegion = "HCM"
        });

        created.Status.Should().Be("DRAFT");
        created.CategoryCode.Should().Be("organic_compost");

        // Cập nhật đè trên đúng 1 bản ghi SortingRule (quan hệ 1-1)
        var updated = await sut.UpsertSortingRuleAsync(adminId, cat.Id, new UpsertSortingRuleRequest
        {
            Title = "Quy chuẩn phân loại rác hữu cơ v2",
            PreparationSteps = "1. Ráo nước.\n2. Cắt nhỏ gốc rau.",
            SortingInstruction = "Không lẫn xương cứng hoặc vỏ sò.",
            HandlingAction = "Bỏ vào thùng ủ compost.",
            ApplicableRegion = "VN_NATIONAL"
        });

        updated.RuleId.Should().Be(created.RuleId);
        updated.Title.Should().Be("Quy chuẩn phân loại rác hữu cơ v2");
        (await db.SortingRules.CountAsync(r => r.WasteCategoryId == cat.Id)).Should().Be(1);
    }

    [Fact]
    public async Task UTC026_PublishSortingRuleAsync_ActivatesRuleWarmsCacheAndSyncsToAiService_BrSrt03()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var cat = new WasteCategory
        {
            Code = "tetrapak",
            Name = "Vỏ hộp sữa TetraPak",
            StandardBinColor = "Xanh Dương",
            IsRecyclable = true,
            Status = "Active"
        };
        db.WasteCategories.Add(cat);
        await db.SaveChangesAsync();

        var aiClient = Substitute.For<IAiVisionClient>();
        aiClient.SyncSortingRuleAsync("tetrapak", "Xanh Dương", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiRuleSyncResponseDto { Success = true, Message = "Synced" }));

        var auditSvc = new AuditLogService(db, CreateAdminUserMock(adminId));
        var sut = new WasteRuleEngineService(NullLogger<WasteRuleEngineService>.Instance, db, auditSvc, aiClient);

        var draft = await sut.UpsertSortingRuleAsync(adminId, cat.Id, new UpsertSortingRuleRequest
        {
            Title = "Hướng dẫn vỏ hộp sữa giấy",
            PreparationSteps = "1. Xả sạch bên trong.\n2. Ép dẹp hộp.",
            SortingInstruction = "Gắn gọn ống hút vào khe hộp.",
            HandlingAction = "Bỏ thùng tái chế vỏ hộp giấy.",
            HazardWarning = "Không để sữa lên men gây mùi."
        });

        // Chưa publish thì chưa phục vụ ở GetSortingGuideAsync
        (await sut.GetSortingGuideAsync("tetrapak")).Should().BeNull();

        var published = await sut.PublishSortingRuleAsync(adminId, draft.RuleId);

        published.Rule.Status.Should().Be("ACTIVE");
        published.AiServiceSynced.Should().BeTrue();
        await aiClient.Received(1).SyncSortingRuleAsync("tetrapak", "Xanh Dương", Arg.Any<string>(), Arg.Any<CancellationToken>());

        var activeGuide = await sut.GetSortingGuideAsync("tetrapak");
        activeGuide.Should().NotBeNull();
        activeGuide!.StandardBinColor.Should().Be("Xanh Dương");
        (await db.AuditLogs.AnyAsync(a => a.Action == "PUBLISH_SORTING_RULE")).Should().BeTrue();
    }

    [Fact]
    public async Task UTC027_PublishSortingRuleAsync_UnknownRuleOrBlankContent_ThrowsExpectedException()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var cat = new WasteCategory { Code = "foam", Name = "Xốp", StandardBinColor = "Đen", Status = "Active" };
        db.WasteCategories.Add(cat);
        var badRule = new SortingRule
        {
            WasteCategoryId = cat.Id,
            Title = "Incomplete",
            PreparationSteps = "   ",
            HandlingAction = "",
            Status = "DRAFT"
        };
        db.SortingRules.Add(badRule);
        await db.SaveChangesAsync();

        var sut = new WasteRuleEngineService(NullLogger<WasteRuleEngineService>.Instance, db);

        await FluentActions.Invoking(() => sut.PublishSortingRuleAsync(adminId, Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();

        var ex = await FluentActions.Invoking(() => sut.PublishSortingRuleAsync(adminId, badRule.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-SRT-03");
        ex.Which.MessageCode.Should().Be("MSG-SRT-03");
    }

    [Fact]
    public async Task UTC028_WasteCatalogValidators_EnforceCodePatternStatusAndFieldLengthBoundaries()
    {
        var createVal = new CreateWasteCategoryValidator();
        (await createVal.ValidateAsync(new CreateWasteCategoryRequest { Code = "Invalid-Code!", Name = "A", StandardBinColor = "Cam" })).IsValid.Should().BeFalse();
        (await createVal.ValidateAsync(new CreateWasteCategoryRequest { Code = new string('a', 51), Name = "A", StandardBinColor = "Cam" })).IsValid.Should().BeFalse();
        (await createVal.ValidateAsync(new CreateWasteCategoryRequest { Code = "pet_clear_1", Name = new string('B', 100), StandardBinColor = "Cam" })).IsValid.Should().BeTrue();

        var updateVal = new UpdateWasteCategoryValidator();
        (await updateVal.ValidateAsync(new UpdateWasteCategoryRequest { Name = "", StandardBinColor = "Cam" })).IsValid.Should().BeFalse();
        (await updateVal.ValidateAsync(new UpdateWasteCategoryRequest { Name = new string('B', 101), StandardBinColor = "Cam" })).IsValid.Should().BeFalse();

        var statusVal = new ToggleWasteCategoryStatusValidator();
        (await statusVal.ValidateAsync(new ToggleWasteCategoryStatusRequest { Status = "Deleted" })).IsValid.Should().BeFalse();
        (await statusVal.ValidateAsync(new ToggleWasteCategoryStatusRequest { Status = "Inactive" })).IsValid.Should().BeTrue();

        var ruleVal = new UpsertSortingRuleValidator();
        (await ruleVal.ValidateAsync(new UpsertSortingRuleRequest
        {
            Title = new string('T', 151),
            PreparationSteps = "Bước 1",
            SortingInstruction = "Phân loại",
            HandlingAction = "Xử lý"
        })).IsValid.Should().BeFalse();
        (await ruleVal.ValidateAsync(new UpsertSortingRuleRequest
        {
            Title = new string('T', 150),
            PreparationSteps = "Bước 1",
            SortingInstruction = "Phân loại",
            HandlingAction = "Xử lý",
            ApplicableRegion = new string('R', 100)
        })).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UTC029_GetAllRuleDetailsAsync_IncludesCreatedAndLegacyCategoriesWithoutSortingRule()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var auditSvc = new AuditLogService(db, CreateAdminUserMock(adminId));
        var ruleEngine = new WasteRuleEngineService(NullLogger<WasteRuleEngineService>.Instance, db, auditSvc);
        var categorySvc = new RecyclerManagementService(db, NullLogger<RecyclerManagementService>.Instance, auditSvc, ruleEngine);

        // 1. Tạo danh mục mới thông qua service -> tự sinh SortingRule DRAFT
        var created = await categorySvc.CreateWasteCategoryAsync(adminId, new CreateWasteCategoryRequest
        {
            Code = "pvc_pipe",
            Name = "Ống nhựa PVC",
            Description = "Rửa sạch bùn đất trước khi thu gom",
            StandardBinColor = "Cam",
            IsRecyclable = true
        });

        // 2. Giả lập danh mục cũ tạo trực tiếp trong DB nhưng chưa có SortingRule
        var legacyCat = new WasteCategory
        {
            Code = "used_oil",
            Name = "Dầu ăn đã qua sử dụng",
            StandardBinColor = "Tím",
            IsRecyclable = false,
            Status = "Active"
        };
        db.WasteCategories.Add(legacyCat);
        await db.SaveChangesAsync();

        var allDetails = await ruleEngine.GetAllRuleDetailsAsync(includeDraft: true);
        var pvcRule = allDetails.FirstOrDefault(r => r.WasteCategoryId == created.Id);
        pvcRule.Should().NotBeNull();
        pvcRule!.RuleId.Should().NotBe(Guid.Empty);
        pvcRule.Status.Should().Be("DRAFT");

        var legacyRule = allDetails.FirstOrDefault(r => r.WasteCategoryId == legacyCat.Id);
        legacyRule.Should().NotBeNull();
        legacyRule!.RuleId.Should().Be(Guid.Empty);
        legacyRule.Status.Should().Be("DRAFT");
        legacyRule.CategoryCode.Should().Be("used_oil");

        var activeOnly = await ruleEngine.GetAllRuleDetailsAsync(includeDraft: false);
        activeOnly.Any(r => r.WasteCategoryId == created.Id).Should().BeFalse();
        activeOnly.Any(r => r.WasteCategoryId == legacyCat.Id).Should().BeFalse();
    }

    [Fact]
    public async Task UTC030_PlatformPolicyService_SeedsDefaultsSupportsCrudAndProtectsLastActiveSection()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var auditSvc = new AuditLogService(db, CreateAdminUserMock(adminId));
        var sut = new PlatformPolicyService(db, auditSvc);

        // Tự động seed 5 mục mặc định
        var publicList = await sut.GetPublicPoliciesAsync();
        publicList.Should().HaveCount(5);

        // Lọc theo nhóm PRIVACY
        var privacyOnly = await sut.GetPublicPoliciesAsync("PRIVACY");
        privacyOnly.Should().ContainSingle(x => x.Code == "PRIV-01");

        // Tạo mới với mã tự sinh
        var created = await sut.CreatePolicyAsync(adminId, new SavePlatformPolicyRequest
        {
            Category = "TERMS",
            Title = "6. Giải quyết tranh chấp và khiếu nại",
            Content = "Mọi khiếu nại cân đo sẽ được nhân viên vận hành kiểm tra đối chiếu trong vòng 24 giờ làm việc.",
            DisplayOrder = 6,
            Status = "ACTIVE"
        });
        created.Code.Should().Be("TERM-04");
        created.Status.Should().Be("ACTIVE");

        // Trùng mã ném ConflictException
        var dupAct = () => sut.CreatePolicyAsync(adminId, new SavePlatformPolicyRequest
        {
            Code = "TERM-04",
            Category = "TERMS",
            Title = "Trùng mã",
            Content = "Nội dung kiểm thử trùng mã điều khoản hệ thống.",
            DisplayOrder = 7,
            Status = "DRAFT"
        });
        var conflict = await dupAct.Should().ThrowAsync<ConflictException>();
        conflict.Which.RuleCode.Should().Be("BR-POL-02");

        // Cập nhật nội dung & chuyển trạng thái DRAFT
        var updated = await sut.UpdatePolicyAsync(adminId, created.Id, new SavePlatformPolicyRequest
        {
            Code = "TERM-04",
            Category = "ENVIRONMENT",
            Title = "6. Quy trình xử lý khiếu nại môi trường",
            Content = "Khiếu nại được xác minh dựa trên biên nhận điện tử và lịch sử trò chuyện giữa hai bên.",
            DisplayOrder = 6,
            Status = "DRAFT"
        });
        updated.Category.Should().Be("ENVIRONMENT");
        updated.Status.Should().Be("DRAFT");
        (await sut.GetPublicPoliciesAsync()).Should().HaveCount(5);

        // Chuyển 4 mục trong 5 mục ACTIVE sang INACTIVE -> thành công; mục ACTIVE cuối cùng bị chặn (BR-POL-03)
        var activeIds = (await sut.GetPublicPoliciesAsync()).Select(x => x.Id).ToList();
        foreach (var id in activeIds.Take(activeIds.Count - 1))
        {
            await sut.SetPolicyStatusAsync(adminId, id, "INACTIVE");
        }

        var lastActiveId = activeIds.Last();
        var hideLastAct = () => sut.SetPolicyStatusAsync(adminId, lastActiveId, "INACTIVE");
        var minEx = await hideLastAct.Should().ThrowAsync<BusinessRuleException>();
        minEx.Which.RuleCode.Should().Be("BR-POL-03");

        var deleteLastAct = () => sut.DeletePolicyAsync(adminId, lastActiveId);
        await deleteLastAct.Should().ThrowAsync<BusinessRuleException>();

        // Xóa mục DRAFT thành công & ghi AuditLog
        await sut.DeletePolicyAsync(adminId, created.Id);
        (await sut.GetAdminPoliciesAsync()).Any(x => x.Id == created.Id).Should().BeFalse();
        (await db.AuditLogs.AnyAsync(a => a.Action == "CREATE_PLATFORM_POLICY")).Should().BeTrue();
        (await db.AuditLogs.AnyAsync(a => a.Action == "DELETE_PLATFORM_POLICY")).Should().BeTrue();
    }

    [Fact]
    public async Task UTC031_ClassifyWasteImageAsync_DefaultModelUsesYolo26Detect_AndRecordsHistoryModelUsed()
    {
        var payload = """
        {
          "success": true,
          "is_out_of_distribution": false,
          "model_used": "yolo26_detect",
          "pipeline": "yolo26_unified_two_stage",
          "inference_time_ms": 185.0,
          "top_prediction": {
            "label": "plastic",
            "display_name": "Nhựa tái chế (Plastic)",
            "confidence": 0.93
          }
        }
        """;
        var handler = MockHttpMessageHandler.FromJson(HttpStatusCode.OK, payload);
        var sut = CreateAiClient(handler);
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 });

        var res = await sut.ClassifyWasteImageAsync(stream, "pet.png");

        res.Success.Should().BeTrue();
        res.ModelUsed.Should().Be("yolo26_detect");
        handler.CapturedBodies.Should().ContainSingle()
            .Which.Should().Contain("yolo26_detect");

        using var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var feedbackSvc = new AiFeedbackService(
            db,
            sut,
            Substitute.For<IFileStorageService>(),
            new AuditLogService(db, CreateAdminUserMock(userId)),
            NullLogger<AiFeedbackService>.Instance);

        var (scanId, _) = await feedbackSvc.RecordScanHistoryAsync(
            new byte[] { 10, 20, 30, 40 },
            "pet.png",
            "image/png",
            userId,
            new AiScanResultDto
            {
                Success = true,
                ConfidenceThreshold = 0.60,
                ModelUsed = null,
                TopPrediction = new WastePredictionDto { Label = "plastic", Confidence = 0.91 }
            });

        var saved = await db.AiScanHistories.AsNoTracking().FirstAsync(x => x.Id == scanId);
        saved.ModelUsed.Should().Be("yolo26_detect");
    }

    [Fact]
    public async Task UTC032_ExpandedNonTradableCategories_HaveActiveRulesAndAreBlockedFromRecyclerPurchasing()
    {
        AiCategoryNormalizer.NormalizeCode("white-glass").Should().Be("glass");
        AiCategoryNormalizer.NormalizeCode("green-glass").Should().Be("glass");
        AiCategoryNormalizer.NormalizeCode("brown-glass").Should().Be("glass");
        AiCategoryNormalizer.NormalizeCode("battery").Should().Be("battery");
        AiCategoryNormalizer.NormalizeCode("biological").Should().Be("biological");
        AiCategoryNormalizer.NormalizeCode("clothes").Should().Be("clothes");
        AiCategoryNormalizer.NormalizeCode("shoes").Should().Be("shoes");

        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var auditSvc = new AuditLogService(db, CreateAdminUserMock(adminId));
        var ruleEngine = new WasteRuleEngineService(NullLogger<WasteRuleEngineService>.Instance, db, auditSvc);
        var recyclerSvc = new RecyclerManagementService(db, NullLogger<RecyclerManagementService>.Instance, auditSvc, ruleEngine);

        var batteryGuide = await ruleEngine.GetSortingGuideAsync("battery");
        batteryGuide.Should().NotBeNull();
        batteryGuide!.HazardWarning.Should().NotBeNullOrWhiteSpace();

        var tradableOnly = await recyclerSvc.GetWasteCategoriesAsync();
        tradableOnly.Should().HaveCount(5);
        tradableOnly.Select(c => c.Code).Should().BeEquivalentTo(new[] { "plastic", "paper", "glass", "metal", "cardboard" });
        tradableOnly.Should().NotContain(c => c.Code == "battery" || c.Code == "biological" || c.Code == "clothes" || c.Code == "shoes" || c.Code == "trash");

        var allIncludingNonTradable = await recyclerSvc.GetAllWasteCategoriesAsync(includeInactive: true, includeNonRecyclable: true);
        allIncludingNonTradable.Should().HaveCount(10);
        allIncludingNonTradable.Where(c => !c.IsRecyclable).Select(c => c.Code).Should().BeEquivalentTo(
            new[] { "battery", "biological", "clothes", "shoes", "trash" });
    }

    [Fact]
    public async Task UTC033_PublishSortingRuleAsync_InactiveCategory_ThrowsBusinessRuleBrSrt04_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var cat = new WasteCategory
        {
            Code = "inactive_plastic",
            Name = "Nhựa tạm ngưng",
            StandardBinColor = "Cam",
            IsRecyclable = true,
            Status = "Inactive"
        };
        db.WasteCategories.Add(cat);
        var rule = new SortingRule
        {
            WasteCategoryId = cat.Id,
            Title = "Rule Nhựa tạm ngưng",
            PreparationSteps = "1. Rửa sạch.",
            SortingInstruction = "Để riêng.",
            HandlingAction = "Bỏ vào thùng.",
            Status = "DRAFT"
        };
        db.SortingRules.Add(rule);
        await db.SaveChangesAsync();

        var sut = new WasteRuleEngineService(NullLogger<WasteRuleEngineService>.Instance, db);

        var ex = await FluentActions.Invoking(() => sut.PublishSortingRuleAsync(adminId, rule.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-SRT-04");
        ex.Which.MessageCode.Should().Be("MSG-SRT-04");
    }

    [Fact]
    public async Task UTC034_ToggleWasteCategoryStatusAsync_ToInactive_ClearsGuideAndSyncsAiVisionClient()
    {
        using var db = TestDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var aiClient = Substitute.For<IAiVisionClient>();
        aiClient.SyncCategoryRuleStatusAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiRuleSyncResponseDto { Success = true, Message = "OK" }));

        var auditSvc = new AuditLogService(db, CreateAdminUserMock(adminId));
        var ruleEngine = new WasteRuleEngineService(NullLogger<WasteRuleEngineService>.Instance, db, auditSvc, aiClient);
        var recyclerSvc = new RecyclerManagementService(db, NullLogger<RecyclerManagementService>.Instance, auditSvc, ruleEngine);

        // Warm guide and get seeded plastic category
        var activePlastic = await ruleEngine.GetSortingGuideAsync("plastic");
        activePlastic.Should().NotBeNull();
        var plasticCat = await db.WasteCategories.FirstAsync(c => c.Code == "plastic");

        // Pause -> Guide becomes null & AI service notified isActive = false
        await recyclerSvc.ToggleWasteCategoryStatusAsync(adminId, plasticCat.Id, new ToggleWasteCategoryStatusRequest { Status = "Inactive" });
        (await ruleEngine.GetSortingGuideAsync("plastic")).Should().BeNull();
        await aiClient.Received(1).SyncCategoryRuleStatusAsync("plastic", false, Arg.Any<CancellationToken>());

        // Re-activate -> Guide returns & AI service notified isActive = true
        await recyclerSvc.ToggleWasteCategoryStatusAsync(adminId, plasticCat.Id, new ToggleWasteCategoryStatusRequest { Status = "Active" });
        (await ruleEngine.GetSortingGuideAsync("plastic")).Should().NotBeNull();
        await aiClient.Received(1).SyncCategoryRuleStatusAsync("plastic", true, Arg.Any<CancellationToken>());
    }
}
