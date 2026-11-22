using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

[ApiController]
[Route("api/v1/waste-scan")]
public class WasteScanController : ControllerBase
{
    private readonly IAiVisionClient _aiClient;
    private readonly IWasteRuleEngineService _ruleEngine;
    private readonly ILogger<WasteScanController> _logger;
    private readonly IAiFeedbackService? _feedbackService;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IValidator<SubmitFeedbackDto>? _feedbackValidator;
    private readonly IValidator<UpsertSortingRuleRequest>? _upsertRuleValidator;

    public WasteScanController(
        IAiVisionClient aiClient,
        IWasteRuleEngineService ruleEngine,
        ILogger<WasteScanController> logger,
        IAiFeedbackService? feedbackService = null,
        ICurrentUserService? currentUserService = null,
        IValidator<SubmitFeedbackDto>? feedbackValidator = null,
        IValidator<UpsertSortingRuleRequest>? upsertRuleValidator = null)
    {
        _aiClient = aiClient;
        _ruleEngine = ruleEngine;
        _logger = logger;
        _feedbackService = feedbackService;
        _currentUserService = currentUserService;
        _feedbackValidator = feedbackValidator;
        _upsertRuleValidator = upsertRuleValidator;
    }

    /// <summary>
    /// [STT 021] Nhận diện rác thải qua ảnh, lưu vết lịch sử quét SHA-256 và ánh xạ quy tắc xử lý (U-01, U-04).
    /// </summary>
    [HttpPost("classify")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<WasteScanResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClassifyImage(
        IFormFile? file,
        [FromForm] string? model = "yolo26_detect",
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Vui lòng tải lên file ảnh hợp lệ.", "MSG-CLS-04"));
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
        {
            return BadRequest(ApiResponse<object>.Fail("Tập tin tải lên phải là hình ảnh hợp lệ (.jpg, .jpeg, .png, .webp).", "MSG-CLS-05"));
        }

        var validModels = new[]
        {
            "yolo26_detect",
            "yolo26s_cls",
            "yolo26n_cls",
            "yolo26s",
            "yolo26n",
            "yolo26",
            "yolov8_detect",
            "mobilenet_v3",
            "efficientnet_b0",
            "yolov8n"
        };
        if (!string.IsNullOrWhiteSpace(model) && !validModels.Contains(model.Trim().ToLowerInvariant()))
        {
            return BadRequest(ApiResponse<object>.Fail($"Mô hình '{model}' không tồn tại. Danh sách mô hình hỗ trợ: {string.Join(", ", validModels)}.", "MSG-CLS-06"));
        }

        _logger.LogInformation("Tiếp nhận yêu cầu phân loại ảnh: {FileName} ({Length} bytes) với mô hình: {Model}",
            file.FileName, file.Length, model);

        byte[] imageBytes;
        await using (var copyBuffer = new MemoryStream())
        {
            await file.CopyToAsync(copyBuffer, ct);
            imageBytes = copyBuffer.ToArray();
        }

        await using var aiStream = new MemoryStream(imageBytes, writable: false);
        var aiResult = await _aiClient.ClassifyWasteImageAsync(aiStream, file.FileName, model, ct);

        if (!aiResult.Success)
        {
            var errorMsg = !string.IsNullOrWhiteSpace(aiResult.ErrorMessage)
                ? aiResult.ErrorMessage
                : "Không thể phân tích hình ảnh từ AI Vision Service.";
            return BadRequest(ApiResponse<object>.Fail(errorMsg, "MSG-CLS-03"));
        }

        var callerId = TryResolveCurrentUserId();
        Guid? scanId = null;
        string? imageUrl = null;
        if (_feedbackService != null)
        {
            var recorded = await _feedbackService.RecordScanHistoryAsync(
                imageBytes,
                file.FileName,
                file.ContentType,
                callerId,
                aiResult,
                ct);
            scanId = recorded.ScanId;
            imageUrl = recorded.ImageUrl;
        }

        // Gắn hướng dẫn phân loại (Rule Engine) cho từng vật thể đóng khung để FE chọn xem tức thì
        var guideCache = new Dictionary<string, WasteSortingGuideDto?>(StringComparer.OrdinalIgnoreCase);
        if (aiResult.DetectedObjects is { Count: > 0 })
        {
            foreach (var obj in aiResult.DetectedObjects)
            {
                if (string.IsNullOrWhiteSpace(obj.Label)) continue;
                if (!guideCache.TryGetValue(obj.Label, out var objGuide))
                {
                    objGuide = await _ruleEngine.GetSortingGuideAsync(obj.Label, ct);
                    guideCache[obj.Label] = objGuide;
                }
                obj.SortingGuide = objGuide;
            }
        }

        var response = new WasteScanResponseDto
        {
            ScanId = scanId,
            ImageUrl = imageUrl,
            AiResult = aiResult
        };

        // Kiểm tra Out-of-Distribution (ảnh hợp lệ nhưng rác lạ hoặc chưa đủ ngưỡng khẳng định)
        if (aiResult.IsOutOfDistribution || aiResult.TopPrediction == null || aiResult.TopPrediction.Confidence < aiResult.ConfidenceThreshold)
        {
            response.Message = "Hình ảnh chưa đủ rõ hoặc vật thể không thuộc nhóm rác hỗ trợ nên hệ thống chưa đưa ra kết luận. Vui lòng chụp lại ở góc rõ nét hơn.";
            return Ok(ApiResponse<WasteScanResponseDto>.Ok(response, response.Message, "MSG-CLS-02"));
        }

        // Mapping với cơ sở dữ liệu quy tắc tĩnh (Rule Engine) cho vật thể chính (#1)
        var categoryCode = aiResult.TopPrediction.Label;
        if (!guideCache.TryGetValue(categoryCode, out var ruleGuide))
        {
            ruleGuide = await _ruleEngine.GetSortingGuideAsync(categoryCode, ct);
        }

        response.SortingGuide = ruleGuide;
        response.Message = aiResult.DetectedCount > 1
            ? $"Đã nhận diện {aiResult.DetectedCount} vật rác trong ảnh — Vật thể chính (#1): {aiResult.TopPrediction.DisplayName}."
            : $"Kết quả nhận diện: {aiResult.TopPrediction.DisplayName}.";

        return Ok(ApiResponse<WasteScanResponseDto>.Ok(response, response.Message, "MSG-CLS-01"));
    }

    /// <summary>
    /// [STT 022] Lấy danh sách các mô hình AI có sẵn để phân loại rác (yolo26_detect, yolo26n_cls, efficientnet_b0, mobilenet_v3).
    /// </summary>
    [HttpGet("models")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableModels(CancellationToken ct)
    {
        var models = await _aiClient.GetAvailableModelsAsync(ct);
        return Ok(ApiResponse<object>.Ok(models ?? new
        {
            default_model = "yolo26_detect",
            models = new[]
            {
                new { id = "yolo26_detect", name = "YOLO26 Waste Object Detection (End-to-End NMS-Free)", is_ready = true },
                new { id = "yolo26s_cls", name = "YOLO26 Small Classification", is_ready = true },
                new { id = "yolo26n_cls", name = "YOLO26 Nano Classification", is_ready = true },
                new { id = "efficientnet_b0", name = "EfficientNet-B0 (Legacy Benchmark)", is_ready = true },
                new { id = "mobilenet_v3", name = "MobileNetV3-Small (Legacy Benchmark)", is_ready = true }
            }
        }));
    }

    /// <summary>
    /// [STT 023] Tra cứu toàn bộ danh mục quy tắc phân loại rác (Admin có thể truyền detailed=true để lấy kèm ruleId &amp; bản DRAFT).
    /// </summary>
    [HttpGet("rules")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WasteSortingGuideDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllRules(
        [FromQuery] bool detailed = false,
        [FromQuery] bool includeDraft = false,
        CancellationToken ct = default)
    {
        if (detailed && (User.IsInRole("Admin") || User.IsInRole("Staff")))
        {
            var details = await _ruleEngine.GetAllRuleDetailsAsync(includeDraft, ct);
            return Ok(ApiResponse<IReadOnlyList<SortingRuleDetailDto>>.Ok(
                details,
                message: "Tải danh sách cấu hình quy tắc phân loại thành công.",
                messageCode: "MSG-SRT-RULE-LIST-OK"));
        }

        var rules = await _ruleEngine.GetAllRulesAsync(ct);
        return Ok(ApiResponse<IEnumerable<WasteSortingGuideDto>>.Ok(
            rules,
            message: "Tải quy tắc phân loại rác thành công.",
            messageCode: "MSG-SRT-RULE-LIST-OK"));
    }

    /// <summary>
    /// [STT 026] Người dân xem lại lịch sử các lần chụp/quét ảnh rác AI của cá nhân kèm hướng dẫn đã nhận (U-01, U-03, BR-CLS-08).
    /// </summary>
    [HttpGet("history")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MyWasteScanHistoryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyScanHistory(
        [FromQuery] WasteScanHistoryFilterRequest filter,
        CancellationToken ct = default)
    {
        if (_feedbackService == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Dịch vụ lưu trữ lịch sử quét AI chưa sẵn sàng.", "MSG-SYS-503"));
        }

        var userId = TryResolveCurrentUserId()
            ?? throw new UnauthorizedAccessException("Không xác định được danh tính người dùng từ phiên đăng nhập.");

        var data = await _feedbackService.GetMyScanHistoryAsync(userId, filter, ct);
        return Ok(ApiResponse<PagedResult<MyWasteScanHistoryItemDto>>.Ok(
            data,
            message: "Tải lịch sử quét ảnh AI cá nhân thành công.",
            messageCode: "MSG-CLS-HISTORY-OK"));
    }

    /// <summary>
    /// [STT 030] Admin tạo hoặc cập nhật chính sách hướng dẫn phân loại, sơ chế sạch &amp; cảnh báo an toàn theo danh mục (A-06, BR-SRT-01).
    /// </summary>
    [HttpPut("rules/{categoryId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<SortingRuleDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertSortingRule(
        Guid categoryId,
        [FromBody] UpsertSortingRuleRequest request,
        CancellationToken ct = default)
    {
        if (_upsertRuleValidator != null)
        {
            var validation = await _upsertRuleValidator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    "Nội dung quy tắc phân loại chưa hợp lệ.",
                    "MSG-GEN-01",
                    validation.Errors.Select(e => e.ErrorMessage).ToList()));
            }
        }

        var adminId = TryResolveCurrentUserId()
            ?? throw new UnauthorizedAccessException("Không xác định được danh tính quản trị viên.");

        var dto = await _ruleEngine.UpsertSortingRuleAsync(adminId, categoryId, request, ct);
        return Ok(ApiResponse<SortingRuleDetailDto>.Ok(
            dto,
            message: $"Đã lưu quy tắc phân loại cho danh mục '{dto.CategoryCode}' ở trạng thái DRAFT.",
            messageCode: "MSG-SRT-RULE-SAVED"));
    }

    /// <summary>
    /// [STT 031] Phát hành phiên bản luật phân loại mới (ACTIVE), làm mới cache Rule Engine &amp; đồng bộ sang AI Service (A-06, BR-SRT-03).
    /// </summary>
    [HttpPatch("rules/{ruleId:guid}/publish")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<PublishSortingRuleResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishSortingRule(Guid ruleId, CancellationToken ct = default)
    {
        var adminId = TryResolveCurrentUserId()
            ?? throw new UnauthorizedAccessException("Không xác định được danh tính quản trị viên.");

        var result = await _ruleEngine.PublishSortingRuleAsync(adminId, ruleId, ct);
        return Ok(ApiResponse<PublishSortingRuleResultDto>.Ok(
            result,
            message: $"Đã phát hành chính thức quy tắc phân loại '{result.Rule.CategoryCode}'.",
            messageCode: "MSG-SRT-RULE-PUBLISHED"));
    }

    /// <summary>
    /// [STT 024] [U-04] Báo cáo AI nhận diện sai, lưu bền vững PostgreSQL kèm ScanId &amp; đồng bộ vòng lặp retrain (BR-CLS-02, BR-CLS-05).
    /// </summary>
    [HttpPost("feedback")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SubmitAiFeedbackResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitFeedback([FromBody] SubmitFeedbackDto feedback, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(feedback.UserReportedLabel))
        {
            return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn nhãn loại rác đúng.", "MSG-GEN-01"));
        }

        if (_feedbackValidator != null)
        {
            var validation = await _feedbackValidator.ValidateAsync(feedback, ct);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
                return BadRequest(ApiResponse<object>.Fail("Dữ liệu báo cáo phân loại sai không hợp lệ.", "MSG-GEN-01", errors));
            }
        }

        _logger.LogInformation("Tiếp nhận phản hồi báo sai: Dự đoán [{Pred}] -> Người dùng chọn [{UserLabel}] (ScanId: {ScanId})",
            feedback.PredictedLabel, feedback.UserReportedLabel, feedback.ScanId);

        if (_feedbackService == null)
        {
            var syncedToAi = await _aiClient.SubmitFeedbackAsync(feedback, ct);
            return Ok(ApiResponse<object>.Ok(
                new { syncedToAi },
                message: "Cảm ơn bạn đã phản hồi! Dữ liệu đã được chuyển tới Nhân viên vận hành để kiểm chứng và cải thiện mô hình AI.",
                messageCode: "MSG-CLS-06"));
        }

        var result = await _feedbackService.SubmitFeedbackAsync(TryResolveCurrentUserId(), feedback, ct);
        return Ok(ApiResponse<SubmitAiFeedbackResponseDto>.Ok(
            result,
            message: "Cảm ơn bạn đã phản hồi! Dữ liệu đã được chuyển tới Nhân viên vận hành để kiểm chứng và cải thiện mô hình AI.",
            messageCode: "MSG-CLS-06"));
    }

    /// <summary>
    /// [STT 130] [U-04] Người dùng đã đăng nhập theo dõi lịch sử &amp; kết quả thẩm định báo cáo AI sai của chính mình (BR-CLS-08).
    /// </summary>
    [HttpGet("feedback/me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MyAiFeedbackReportDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<MyAiFeedbackReportDto>>>> GetMyFeedbackReports(
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (_feedbackService == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Dịch vụ lưu trữ phản hồi AI chưa sẵn sàng.", "MSG-SYS-503"));
        }

        var userId = TryResolveCurrentUserId()
            ?? throw new UnauthorizedAccessException("Không xác định được danh tính người dùng từ phiên đăng nhập.");

        var paged = await _feedbackService.GetMyFeedbackReportsAsync(userId, status, pageNumber, pageSize, ct);
        return Ok(ApiResponse<PagedResult<MyAiFeedbackReportDto>>.Ok(
            paged,
            "Tải lịch sử báo cáo AI nhận diện sai thành công.",
            "MSG-GEN-01"));
    }

    private Guid? TryResolveCurrentUserId()
    {
        if (_currentUserService?.UserId is { } uid && uid != Guid.Empty)
        {
            return uid;
        }

        var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }
}
