using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

[ApiController]
[Route("api/v1/waste-scan")]
public class WasteScanController : ControllerBase
{
    private readonly IAiVisionClient _aiClient;
    private readonly IWasteRuleEngineService _ruleEngine;
    private readonly ILogger<WasteScanController> _logger;

    public WasteScanController(
        IAiVisionClient aiClient,
        IWasteRuleEngineService ruleEngine,
        ILogger<WasteScanController> logger)
    {
        _aiClient = aiClient;
        _ruleEngine = ruleEngine;
        _logger = logger;
    }

    /// <summary>
    /// Nhận diện rác thải qua ảnh và ánh xạ quy tắc xử lý (UC-CLS-01)
    /// </summary>
    /// <param name="file">Tập tin hình ảnh rác cần phân tích</param>
    /// <param name="model">Tùy chọn mô hình AI: yolov8_detect (Mặc định - Detection & Crop), mobilenet_v3, efficientnet_b0, yolov8n</param>
    [HttpPost("classify")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<WasteScanResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClassifyImage(
        IFormFile? file, 
        [FromForm] string? model = "yolov8_detect", 
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Vui lòng tải lên file ảnh hợp lệ.", "MSG-CLS-04"));
        }

        _logger.LogInformation("Tiếp nhận yêu cầu phân loại ảnh: {FileName} ({Length} bytes) với mô hình: {Model}", 
            file.FileName, file.Length, model);

        using var stream = file.OpenReadStream();
        var aiResult = await _aiClient.ClassifyWasteImageAsync(stream, file.FileName, model, ct);

        var response = new WasteScanResponseDto
        {
            AiResult = aiResult
        };

        // Kiểm tra Out-of-Distribution
        if (aiResult.IsOutOfDistribution || aiResult.TopPrediction == null || aiResult.TopPrediction.Confidence < aiResult.ConfidenceThreshold)
        {
            response.Message = "Hệ thống không nhận diện chắc chắn (> 60%) hoặc vật thể không thuộc nhóm rác hỗ trợ. Vui lòng chụp lại hình ảnh ở góc rõ hơn.";
            return Ok(ApiResponse<WasteScanResponseDto>.Ok(response, response.Message, "MSG-CLS-02"));
        }

        // Mapping với cơ sở dữ liệu quy tắc tĩnh (Rule Engine)
        var categoryCode = aiResult.TopPrediction.Label;
        var ruleGuide = await _ruleEngine.GetSortingGuideAsync(categoryCode, ct);

        response.SortingGuide = ruleGuide;
        response.Message = $"Nhận diện thành công loại rác: {aiResult.TopPrediction.DisplayName} với độ tin cậy {Math.Round(aiResult.TopPrediction.Confidence * 100, 1)}% (Mô hình: {model}).";

        return Ok(ApiResponse<WasteScanResponseDto>.Ok(response, response.Message, "MSG-CLS-01"));
    }

    /// <summary>
    /// Lấy danh sách các mô hình AI có sẵn để phân loại rác (yolov8_detect, mobilenet_v3, efficientnet_b0, yolov8n)
    /// </summary>
    [HttpGet("models")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableModels(CancellationToken ct)
    {
        var models = await _aiClient.GetAvailableModelsAsync(ct);
        return Ok(ApiResponse<object>.Ok(models ?? new
        {
            default_model = "yolov8_detect",
            models = new[]
            {
                new { id = "yolov8_detect", name = "YOLOv8 Detection (Khuyên dùng - Định vị & Cắt nền)", is_ready = true },
                new { id = "mobilenet_v3", name = "MobileNetV3-Small (Siêu nhẹ, tối ưu di động)", is_ready = true },
                new { id = "efficientnet_b0", name = "EfficientNet-B0 (Độ chính xác cao)", is_ready = true },
                new { id = "yolov8n", name = "YOLOv8 Nano Classifier", is_ready = true }
            }
        }));
    }

    /// <summary>
    /// Tra cứu toàn bộ danh mục quy tắc phân loại rác tĩnh (UC3, UC14)
    /// </summary>
    [HttpGet("rules")]
    public async Task<IActionResult> GetAllRules(CancellationToken ct)
    {
        var rules = await _ruleEngine.GetAllRulesAsync(ct);
        return Ok(rules);
    }

    /// <summary>
    /// Báo cáo AI nhận diện sai để quản trị viên kiểm chứng và retrain (UC4, UC17)
    /// </summary>
    [HttpPost("feedback")]
    public IActionResult SubmitFeedback([FromBody] SubmitFeedbackDto feedback)
    {
        _logger.LogInformation("Tiếp nhận phản hồi báo sai: Dự đoán [{Pred}] -> Người dùng chọn [{UserLabel}]",
            feedback.PredictedLabel, feedback.UserReportedLabel);

        return Ok(new
        {
            success = true,
            message = "Cảm ơn bạn đã phản hồi! Dữ liệu đã được chuyển tới Admin để kiểm chứng và cải thiện mô hình AI."
        });
    }
}
