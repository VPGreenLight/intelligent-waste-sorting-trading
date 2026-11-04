using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

public class AiVisionHttpClient : IAiVisionClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiVisionHttpClient> _logger;
    private readonly string _aiServiceBaseUrl;

    public AiVisionHttpClient(HttpClient httpClient, IConfiguration configuration, ILogger<AiVisionHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _aiServiceBaseUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
    }

    public async Task<AiScanResultDto> ClassifyWasteImageAsync(Stream imageStream, string fileName, string? model = "yolov8_detect", CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(imageStream);
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            var mediaType = ext switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "image/jpeg"
            };
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            content.Add(streamContent, "file", fileName);

            if (!string.IsNullOrWhiteSpace(model))
            {
                content.Add(new StringContent(model), "model");
            }

            var endpoint = $"{_aiServiceBaseUrl.TrimEnd('/')}/api/v1/classify";
            _logger.LogInformation("Gửi ảnh phân tích tới AI Vision Service với Model '{Model}' tại: {Endpoint}", model, endpoint);

            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("AI Service trả về mã lỗi: {StatusCode}, Body: {Body}", response.StatusCode, errorBody);
                return new AiScanResultDto
                {
                    Success = false,
                    IsOutOfDistribution = false,
                    InferenceTimeMs = 0,
                    ErrorMessage = !string.IsNullOrWhiteSpace(errorBody) ? errorBody : $"AI Service trả về mã lỗi {response.StatusCode}"
                };
            }

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
            var result = JsonSerializer.Deserialize<AiScanResultDto>(jsonString, options);

            return result ?? new AiScanResultDto { Success = false, ErrorMessage = "Không thể giải mã phản hồi từ AI Service." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kết nối tới AI Vision Service");
            return new AiScanResultDto
            {
                Success = false,
                IsOutOfDistribution = false,
                InferenceTimeMs = 0,
                ErrorMessage = $"Lỗi kết nối tới AI Service: {ex.Message}",
                TopPrediction = new WastePredictionDto
                {
                    Label = "unknown",
                    DisplayName = "Không thể kết nối AI Service",
                    Confidence = 0
                }
            };
        }
    }

    public async Task<object?> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{_aiServiceBaseUrl.TrimEnd('/')}/api/v1/models";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<object>(jsonString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy danh sách mô hình từ AI Vision Service");
            return null;
        }
    }

    public async Task<bool> SubmitFeedbackAsync(SubmitFeedbackDto feedback, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{_aiServiceBaseUrl.TrimEnd('/')}/api/v1/feedback";
            var payload = new
            {
                predicted_label = string.IsNullOrWhiteSpace(feedback.PredictedLabel) ? "unknown" : feedback.PredictedLabel,
                user_reported_label = feedback.UserReportedLabel,
                confidence_score = feedback.ConfidenceScore,
                note = feedback.Note ?? string.Empty,
                image_url = feedback.ImageUrl ?? string.Empty
            };

            var response = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đồng bộ phản hồi báo sai sang AI Vision Service (sẽ lưu tại Core API log)");
            return false;
        }
    }
}
