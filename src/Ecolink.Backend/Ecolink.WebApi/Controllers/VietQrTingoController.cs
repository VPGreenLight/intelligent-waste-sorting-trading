using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Controller chuyên trách tương thích chuẩn kết nối VietQR Tingo / TingoPay Portal
/// URL Path: /vqr
/// </summary>
[ApiController]
[Route("vqr")]
[Produces("application/json")]
public class VietQrTingoController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<VietQrTingoController> _logger;

    public VietQrTingoController(IPaymentService paymentService, ILogger<VietQrTingoController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// API cấp Token cho VietQR Tingo theo chuẩn Basic Authentication
    /// Phục vụ nút 'Test Get Token' trên trang quản trị VietQR
    /// Endpoint: /vqr/api/token_generate
    /// </summary>
    [HttpPost("api/token_generate")]
    [HttpGet("api/token_generate")]
    public IActionResult GenerateToken()
    {
        string? authHeader = Request.Headers.Authorization;
        _logger.LogInformation("==> [VietQR Tingo] Nhận yêu cầu Get Token. Authorization Header: {Auth}", authHeader);

        // Sinh token ngẫu nhiên có hiệu lực 24h
        string accessToken = $"vqr_{Guid.NewGuid():N}";

        return Ok(new
        {
            error = 0,
            message = "Thành công",
            access_token = accessToken,
            token_type = "bearer",
            expires_in = 86400,
            data = new
            {
                token = accessToken
            }
        });
    }

    /// <summary>
    /// Webhook tiếp nhận biến động số dư / giao dịch chuyển khoản trên môi trường Test (UAT)
    /// Endpoint: /vqr/bank/api/test/transaction-callback
    /// </summary>
    [HttpPost("bank/api/test/transaction-callback")]
    public async Task<IActionResult> TestTransactionCallback([FromBody] JsonElement rawPayload, CancellationToken ct)
    {
        _logger.LogInformation("==> [VietQR Tingo UAT Callback]: {Json}", rawPayload.ToString());
        return await HandleCallbackInternal(rawPayload, ct);
    }

    /// <summary>
    /// Webhook tiếp nhận biến động số dư / giao dịch chuyển khoản trên môi trường Thật (Production)
    /// Endpoint: /vqr/bank/api/transaction-callback
    /// </summary>
    [HttpPost("bank/api/transaction-callback")]
    public async Task<IActionResult> ProductionTransactionCallback([FromBody] JsonElement rawPayload, CancellationToken ct)
    {
        _logger.LogInformation("==> [VietQR Tingo Production Callback]: {Json}", rawPayload.ToString());
        return await HandleCallbackInternal(rawPayload, ct);
    }

    private async Task<IActionResult> HandleCallbackInternal(JsonElement rawPayload, CancellationToken ct)
    {
        try
        {
            // Trích xuất các trường thông tin từ payload VietQR Tingo
            string? content = null;
            string? transId = null;
            decimal amount = 0;

            if (rawPayload.TryGetProperty("data", out var dataProp))
            {
                if (dataProp.TryGetProperty("content", out var cProp)) content = cProp.GetString();
                if (dataProp.TryGetProperty("transId", out var tProp)) transId = tProp.GetString();
                if (dataProp.TryGetProperty("amount", out var aProp))
                {
                    if (aProp.ValueKind == JsonValueKind.Number) amount = aProp.GetDecimal();
                    else if (decimal.TryParse(aProp.GetString(), out var d)) amount = d;
                }
            }
            else
            {
                if (rawPayload.TryGetProperty("content", out var cProp)) content = cProp.GetString();
                if (rawPayload.TryGetProperty("transId", out var tProp)) transId = tProp.GetString();
                if (rawPayload.TryGetProperty("amount", out var aProp))
                {
                    if (aProp.ValueKind == JsonValueKind.Number) amount = aProp.GetDecimal();
                    else if (decimal.TryParse(aProp.GetString(), out var d)) amount = d;
                }
            }

            var webhookPayload = new VietQrWebhookPayload
            {
                Description = content,
                Reference = transId,
                Amount = amount
            };

            var processResult = await _paymentService.ProcessWebhookAsync(webhookPayload, ct);

            return Ok(new
            {
                error = 0,
                message = processResult.Success ? "Xử lý giao dịch thành công" : processResult.Message,
                data = new
                {
                    orderId = processResult.OrderCode,
                    status = processResult.Status
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xử lý Callback từ VietQR Tingo");
            return Ok(new
            {
                error = 1,
                message = ex.Message
            });
        }
    }
}
