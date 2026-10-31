using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Dịch vụ lưu trữ chứng từ pháp lý (Giấy phép kinh doanh) và ảnh cơ sở thu mua lên Cloudflare R2
/// Sử dụng giao thức chuẩn AWS S3 Signature Version 4 (Zero external SDK dependency).
/// </summary>
public class CloudflareR2StorageService : IFileStorageService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

    private readonly HttpClient _httpClient;
    private readonly string _accountId;
    private readonly string _accessKeyId;
    private readonly string _secretAccessKey;
    private readonly string _bucketName;
    private readonly string _publicUrl;
    private readonly ILogger<CloudflareR2StorageService> _logger;

    public CloudflareR2StorageService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CloudflareR2StorageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _accountId = configuration["CloudflareR2:AccountId"]?.Trim() ?? string.Empty;
        _accessKeyId = configuration["CloudflareR2:AccessKeyId"]?.Trim() ?? string.Empty;
        _secretAccessKey = configuration["CloudflareR2:SecretAccessKey"]?.Trim() ?? string.Empty;
        _bucketName = configuration["CloudflareR2:BucketName"]?.Trim() ?? "ecolink";
        _publicUrl = (configuration["CloudflareR2:PublicUrl"]
                      ?? configuration["CloudflareR2:PublicDomainUrl"]
                      ?? string.Empty).Trim().TrimEnd('/');
    }

    public async Task<RecyclerDocumentUploadResponseDto> UploadRecyclerDocumentAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        string folderPrefix = "recycler-kyb",
        CancellationToken ct = default)
    {
        if (fileStream == null)
        {
            throw new BusinessRuleException("BR-MTH-01", "MSG-GEN-01", "Vui lòng chọn tệp chứng từ hoặc hình ảnh hợp lệ.");
        }

        var extension = Path.GetExtension(originalFileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new BusinessRuleException("BR-MTH-01", "MSG-GEN-01",
                $"Định dạng tệp '{extension}' không được hỗ trợ. Chỉ chấp nhận: {string.Join(", ", AllowedExtensions)}.");
        }

        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct);
        var fileBytes = ms.ToArray();

        if (fileBytes.Length == 0)
        {
            throw new BusinessRuleException("BR-MTH-01", "MSG-GEN-01", "Tệp tải lên không được rỗng (0 byte).");
        }

        if (fileBytes.Length > MaxFileSizeBytes)
        {
            throw new BusinessRuleException("BR-MTH-01", "MSG-GEN-01", "Dung lượng tệp vượt quá giới hạn tối đa 10 MB.");
        }

        var cleanPrefix = string.IsNullOrWhiteSpace(folderPrefix) ? "recycler-kyb" : folderPrefix.Trim('/');
        var objectKey = $"{cleanPrefix}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
        var resolvedContentType = ResolveContentType(contentType, extension);

        // Nếu chưa cấu hình R2 thật hoặc đang ở chế độ mock key
        if (string.IsNullOrWhiteSpace(_accountId) ||
            string.IsNullOrWhiteSpace(_accessKeyId) ||
            string.IsNullOrWhiteSpace(_secretAccessKey) ||
            _accountId.Contains("{R2_") ||
            _accessKeyId.Contains("local-dev"))
        {
            _logger.LogWarning("[Cloudflare R2 Fallback] Chưa cấu hình R2 Key thật, trả về URL mô phỏng cho {ObjectKey}", objectKey);
            var fallbackBase = !string.IsNullOrWhiteSpace(_publicUrl) && !_publicUrl.Contains("{R2_")
                ? _publicUrl
                : "https://pub-71848fa50a564bf9b27f3607254f44a0.r2.dev";

            return new RecyclerDocumentUploadResponseDto
            {
                FileUrl = $"{fallbackBase}/{objectKey}",
                ObjectKey = objectKey,
                ContentType = resolvedContentType,
                SizeBytes = fileBytes.Length,
                UploadedAt = DateTime.UtcNow
            };
        }

        // Ký request chuẩn AWS Signature Version 4 gửi lên Cloudflare R2 S3 Endpoint
        var host = $"{_accountId}.r2.cloudflarestorage.com";
        var endpointUri = new Uri($"https://{host}/{_bucketName}/{objectKey}");

        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");
        var payloadHash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();

        var canonicalUri = $"/{_bucketName}/{objectKey}";
        var canonicalHeaders = $"host:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";

        var canonicalRequest = $"PUT\n{canonicalUri}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var canonicalRequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))).ToLowerInvariant();

        var credentialScope = $"{dateStamp}/auto/s3/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";

        var signingKey = GetSignatureKey(_secretAccessKey, dateStamp, "auto", "s3");
        var signature = Convert.ToHexString(HmacSha256(signingKey, stringToSign)).ToLowerInvariant();

        var authorizationHeader =
            $"AWS4-HMAC-SHA256 Credential={_accessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Put, endpointUri);
        requestMessage.Headers.Host = host;
        requestMessage.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        requestMessage.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        requestMessage.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);

        var byteContent = new ByteArrayContent(fileBytes);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue(resolvedContentType);
        requestMessage.Content = byteContent;

        var response = await _httpClient.SendAsync(requestMessage, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Lỗi khi tải tệp lên Cloudflare R2 ({StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
            throw new BusinessRuleException("BR-MTH-01", "MSG-SYS-500",
                $"Không thể tải chứng từ lên Cloudflare R2 (Mã lỗi: {(int)response.StatusCode}). Vui lòng kiểm tra cấu hình R2 Bucket.");
        }

        var publicFileUrl = !string.IsNullOrWhiteSpace(_publicUrl)
            ? $"{_publicUrl}/{objectKey}"
            : endpointUri.ToString();

        _logger.LogInformation("Đã tải chứng từ lên Cloudflare R2 thành công: {FileUrl} ({Size} bytes)", publicFileUrl, fileBytes.Length);

        return new RecyclerDocumentUploadResponseDto
        {
            FileUrl = publicFileUrl,
            ObjectKey = objectKey,
            ContentType = resolvedContentType,
            SizeBytes = fileBytes.Length,
            UploadedAt = DateTime.UtcNow
        };
    }

    private static byte[] HmacSha256(byte[] key, string data) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + key);
        var kDate = HmacSha256(kSecret, dateStamp);
        var kRegion = HmacSha256(kDate, regionName);
        var kService = HmacSha256(kRegion, serviceName);
        return HmacSha256(kService, "aws4_request");
    }

    private static string ResolveContentType(string? providedContentType, string extension)
    {
        if (!string.IsNullOrWhiteSpace(providedContentType) && providedContentType != "application/octet-stream")
        {
            return providedContentType;
        }

        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}

