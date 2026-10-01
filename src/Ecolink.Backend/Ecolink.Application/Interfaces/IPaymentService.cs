using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

public interface IPaymentService
{
    Task<PayInOrderDto> CreatePayInOrderAsync(CreatePayInRequest request, CancellationToken cancellationToken = default);
    Task<PayInOrderDto?> GetOrderAsync(string orderCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<PayInOrderDto>> GetAllOrdersAsync(CancellationToken cancellationToken = default);
    Task<WebhookResponseDto> ProcessWebhookAsync(VietQrWebhookPayload payload, CancellationToken cancellationToken = default);
    Task<PayInOrderDto> SimulateTransferSuccessAsync(string orderCode, CancellationToken cancellationToken = default);
}
