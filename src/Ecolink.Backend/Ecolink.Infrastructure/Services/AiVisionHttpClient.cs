using System.Net.Http.Headers;
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
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
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
                    IsOutOfDistribution = true,
                    InferenceTimeMs = 0
                };
            }

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
            var result = JsonSerializer.Deserialize<AiScanResultDto>(jsonString, options);

            return result ?? new AiScanResultDto { Success = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kết nối tới AI Vision Service");
            return new AiScanResultDto
            {
                Success = false,
                IsOutOfDistribution = true,
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
}
