using System.Net;
using System.Text;
using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Infrastructure.Services;
using Ecolink.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task UTC014_GetAllRulesAsync_DefaultInitialization_ReturnsAll6StandardCategories()
    {
        var sut = CreateRuleEngine();

        var rules = (await sut.GetAllRulesAsync()).ToList();

        rules.Should().HaveCount(6);
        rules.Select(r => r.CategoryCode).Should().BeEquivalentTo(
            new[] { "plastic", "paper", "glass", "metal", "cardboard", "trash" });
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
        allRemaining.Should().HaveCount(5);

        // Khôi phục lại cache mặc định sau test
        await sut.InvalidateRuleCacheAsync(null);
    }

    [Fact]
    public async Task UTC016_InvalidateRuleCacheAsync_NullCategory_ResetsAndReloadsAll6DefaultRules()
    {
        var sut = CreateRuleEngine();
        await sut.InvalidateRuleCacheAsync("plastic");
        await sut.InvalidateRuleCacheAsync("metal");

        await sut.InvalidateRuleCacheAsync(null);

        var rules = (await sut.GetAllRulesAsync()).ToList();
        rules.Should().HaveCount(6);
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
}
