using System.Net.Http.Headers;
using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>Test cho nhóm WasteScan.</summary>
[Trait("Category", "WasteScan")]
public class WasteScanTests
{
    private static Task<string> CitizenToken() => Tokens.GetAsync("citizen");

    private static MultipartFormDataContent Multipart(
        byte[]? file, string fileName = "scan.png", string mime = "image/png", string? model = null)
    {
        var content = new MultipartFormDataContent();
        if (file != null)
        {
            var fileContent = new ByteArrayContent(file);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
            content.Add(fileContent, "file", fileName);
        }
        if (model != null)
            content.Add(new StringContent(model), "model");
        return content;
    }

    private static async Task<ApiResult> Classify(
        byte[]? file, string fileName = "scan.png", string mime = "image/png", string? model = null)
    {
        var token = await CitizenToken();
        return await Api.SendAsync(HttpMethod.Post, Routes.ScanClassify,
            Multipart(file, fileName, mime, model), token);
    }

    private static void AssertEnvelope(JsonElement body)
    {
        Assert.True(body.GetProperty("success").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.True(body.TryGetProperty("timestamp", out _), "Missing 'timestamp' field in response envelope");
    }

    // ======================= models, rules =======================
    [Fact]
    public async Task Models_ReturnsNonEmptyList()
    {
        var token = await CitizenToken();
        var result = (await Api.GetAsync(Routes.ScanModels, token)).ShouldBe(200);

        AssertEnvelope(result.Body);
        Assert.True(result.Body.GetProperty("success").GetBoolean());
        Assert.True(TestData.IsNonEmpty(result.Body.GetProperty("data")), "Model list is empty");
    }

    [Fact]
    public async Task Rules_ReturnsNonEmptyJson()
    {
        var token = await CitizenToken();
        var result = (await Api.GetAsync(Routes.ScanRules, token)).ShouldBe(200);
        Assert.True(TestData.IsNonEmpty(result.Body), "Sorting rules list is empty");
    }

    // ======================= classify =======================
    [Fact]
    public async Task Classify_WithDefaultModel_ReturnsExpectedShape()
    {
        var result = (await Classify(TestImages.Png())).ShouldBe(200);

        AssertEnvelope(result.Body);
        Assert.True(result.Body.GetProperty("success").GetBoolean());

        var ai = result.Body.GetProperty("data").GetProperty("aiResult");
        Assert.True(ai.GetProperty("success").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.True(ai.GetProperty("isOutOfDistribution").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.Equal(JsonValueKind.Number, ai.GetProperty("confidenceThreshold").ValueKind);
        Assert.True(ai.GetProperty("inferenceTimeMs").GetDouble() >= 0);

        if (ai.TryGetProperty("predictions", out var predictions) && predictions.ValueKind != JsonValueKind.Null)
            Assert.Equal(JsonValueKind.Array, predictions.ValueKind);
    }

    [Fact]
    public async Task Classify_WithExplicitModel_Succeeds()
    {
        var result = (await Classify(TestImages.Png(), model: "yolov8_detect")).ShouldBe(200);
        Assert.True(result.Body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Classify_PredictionConfidence_IsBetween0And1()
    {
        var result = (await Classify(TestImages.Png())).ShouldBe(200);
        var ai = result.Body.GetProperty("data").GetProperty("aiResult");

        // Ảnh tự vẽ có thể không cho dự đoán nào -> không coi là lỗi
        if (ai.TryGetProperty("topPrediction", out var top) && top.ValueKind == JsonValueKind.Object)
        {
            Assert.False(string.IsNullOrEmpty(top.GetProperty("label").GetString()));
            Assert.InRange(top.GetProperty("confidence").GetDouble(), 0.0, 1.0);
        }

        if (ai.TryGetProperty("predictions", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var p in list.EnumerateArray())
                Assert.InRange(p.GetProperty("confidence").GetDouble(), 0.0, 1.0);
    }

    [Fact]
    public async Task Classify_WithoutFile_Returns400()
    {
        var token = await CitizenToken();
        // multipart chỉ có field model, không có file
        var content = Multipart(null, model: "yolov8_detect");
        (await Api.SendAsync(HttpMethod.Post, Routes.ScanClassify, content, token)).ShouldBe(400);
    }

    [Fact]
    public async Task Classify_WithEmptyFile_Returns400() =>
        (await Classify(Array.Empty<byte>(), "empty.png")).ShouldBe(400);

    [Fact]
    public async Task Classify_WithNonImageFile_ReturnsClientError() =>
        (await Classify("this is not an image"u8.ToArray(), "note.txt", "text/plain")).ShouldBeClientError();

    [Fact]
    public async Task Classify_WithCorruptedImage_DoesNotCauseServerError() =>
        (await Classify(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0 }, "corrupted.png"))
            .ShouldNotBeServerError();

    [Fact]
    public async Task Classify_WithUnknownModel_ReturnsClientError() =>
        (await Classify(TestImages.Png(), model: "nonexistent_model")).ShouldBeClientError();

    [Fact]
    public async Task Classify_WithJsonInsteadOfMultipart_Returns400Or415()
    {
        var token = await CitizenToken();
        (await Api.PostJsonAsync(Routes.ScanClassify, new { model = "yolov8_detect" }, token))
            .ShouldBeIn(400, 415);
    }

    // ======================= feedback =======================
    [Fact]
    public async Task Feedback_WithValidData_ReturnsOk()
    {
        var token = await CitizenToken();
        var payload = new
        {
            imageUrl = "https://example.com/test.jpg",
            predictedLabel = "plastic_bottle",
            confidenceScore = 0.72,
            userReportedLabel = "glass_bottle",
            note = "Automated test: AI misclassified the item",
        };
        (await Api.PostJsonAsync(Routes.ScanFeedback, payload, token)).ShouldBe(200);
    }

    [Fact]
    public async Task Feedback_WithSingleField_ReturnsOk()
    {
        // Mọi field của SubmitFeedbackDto đều optional
        var token = await CitizenToken();
        (await Api.PostJsonAsync(Routes.ScanFeedback, new { userReportedLabel = "paper" }, token)).ShouldBe(200);
    }

    [Fact]
    public async Task Feedback_WithWrongTypeForConfidence_Returns400()
    {
        var token = await CitizenToken();
        (await Api.PostRawJsonAsync(Routes.ScanFeedback, "{\"confidenceScore\":\"not-a-number\"}", token))
            .ShouldBe(400);
    }

    [Fact]
    public async Task Feedback_WithoutBody_Returns400Or415()
    {
        var token = await CitizenToken();
        (await Api.PostEmptyAsync(Routes.ScanFeedback, token)).ShouldBeIn(400, 415);
    }

    [Fact]
    public async Task Feedback_WithMalformedJson_Returns400()
    {
        var token = await CitizenToken();
        (await Api.PostRawJsonAsync(Routes.ScanFeedback, "{not valid json", token)).ShouldBe(400);
    }

    [Fact]
    public async Task ClassifyThenFeedback_EndToEndFlow()
    {
        var result = (await Classify(TestImages.Png())).ShouldBe(200);
        var ai = result.Body.GetProperty("data").GetProperty("aiResult");

        if (!ai.TryGetProperty("topPrediction", out var top) || top.ValueKind != JsonValueKind.Object)
            return; // không có dự đoán để gửi phản hồi

        var token = await CitizenToken();
        var label = top.GetProperty("label").GetString();
        (await Api.PostJsonAsync(Routes.ScanFeedback, new
        {
            predictedLabel = label,
            confidenceScore = top.GetProperty("confidence").GetDouble(),
            userReportedLabel = label,
            note = "Automated test: classify then feedback flow",
        }, token)).ShouldBe(200);
    }

    // ======================= Truy cập ẩn danh =======================
    // Thực tế các endpoint WasteScan cho phép gọi KHÔNG cần token (kể cả token sai vẫn trả 200).
    // Nếu sau này team bật xác thực, đổi các test này về kỳ vọng 401.
    private static Task<ApiResult> Call(string endpoint, string? token) => endpoint switch
    {
        "models" => Api.GetAsync(Routes.ScanModels, token),
        "rules" => Api.GetAsync(Routes.ScanRules, token),
        "classify" => Api.SendAsync(HttpMethod.Post, Routes.ScanClassify, Multipart(TestImages.Png()), token),
        "feedback" => Api.PostJsonAsync(Routes.ScanFeedback, new { note = "anonymous request" }, token),
        _ => throw new ArgumentException(endpoint),
    };

    [Theory]
    [InlineData("models")]
    [InlineData("rules")]
    [InlineData("classify")]
    [InlineData("feedback")]
    public async Task Endpoint_WithoutToken_IsAccessible(string endpoint) =>
        (await Call(endpoint, null)).ShouldBe(200);

    [Theory]
    [InlineData("models")]
    [InlineData("rules")]
    [InlineData("classify")]
    [InlineData("feedback")]
    public async Task Endpoint_WithInvalidToken_DoesNotCauseServerError(string endpoint) =>
        (await Call(endpoint, "invalid.fake.token")).ShouldNotBeServerError();
}
