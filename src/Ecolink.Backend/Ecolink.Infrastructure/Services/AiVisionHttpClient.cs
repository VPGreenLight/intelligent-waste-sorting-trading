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

    public async Task<AiScanResultDto> ClassifyWasteImageAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(imageStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(streamContent, "file", fileName);

            var endpoint = $"{_aiServiceBaseUrl.TrimEnd('/')}/api/v1/classify";
            _logger.LogInformation("Gửi ảnh phân tích tới AI Vision Service tại: {Endpoint}", endpoint);

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
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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
}
