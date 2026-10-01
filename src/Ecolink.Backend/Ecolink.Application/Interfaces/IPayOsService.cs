using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

public interface IPayOsService
{
    Task<PayOsPaymentResponseDto> CreatePaymentLinkAsync(CreatePayOsPaymentRequest request, CancellationToken ct = default);
    Task<PayOsPaymentResponseDto?> GetPaymentOrderAsync(long orderCode, CancellationToken ct = default);
    Task<bool> ProcessWebhookAsync(string webhookJsonBody, CancellationToken ct = default);
    Task<string> ConfirmWebhookAsync(string webhookUrl, CancellationToken ct = default);
    Task<PayOsPayoutResponseDto> ExecutePayoutAsync(PayOsPayoutRequest request, CancellationToken ct = default);
}
