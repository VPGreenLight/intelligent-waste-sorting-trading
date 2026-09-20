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
    /// Nhận diện rác thải qua ảnh và ánh xạ quy tắc xử lý (UC1, UC2, UC3)
    /// </summary>
    [HttpPost("classify")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(WasteScanResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClassifyImage(IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng tải lên file ảnh hợp lệ." });
        }

        _logger.LogInformation("Tiếp nhận yêu cầu phân loại ảnh: {FileName}, Kích thước: {Length} bytes", file.FileName, file.Length);

        using var stream = file.OpenReadStream();
        var aiResult = await _aiClient.ClassifyWasteImageAsync(stream, file.FileName, ct);

        var response = new WasteScanResponseDto
        {
            AiResult = aiResult
        };

        // Kiểm tra Out-of-Distribution
        if (aiResult.IsOutOfDistribution || aiResult.TopPrediction == null || aiResult.TopPrediction.Confidence < aiResult.ConfidenceThreshold)
        {
            response.Message = "Hệ thống không nhận diện chắc chắn (> 60%) hoặc vật thể không thuộc nhóm rác hỗ trợ. Vui lòng chụp lại hình ảnh ở góc rõ hơn.";
            return Ok(response);
        }

        // Mapping với cơ sở dữ liệu quy tắc tĩnh (Rule Engine)
        var categoryCode = aiResult.TopPrediction.Label;
        var ruleGuide = await _ruleEngine.GetSortingGuideAsync(categoryCode, ct);

        response.SortingGuide = ruleGuide;
        response.Message = $"Nhận diện thành công loại rác: {aiResult.TopPrediction.DisplayName} với độ tin cậy {Math.Round(aiResult.TopPrediction.Confidence * 100, 1)}%.";

        return Ok(response);
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
