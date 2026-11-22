using Ecolink.Application.Dtos;
using Ecolink.Domain.Enums;

namespace Ecolink.Application.Interfaces;

public interface IPaymentService
{
    // Giữ chữ ký tương thích ngược cho các module/test hiện hữu
    Task<PayInOrderDto> CreatePayInOrderAsync(CreatePayInRequest request, CancellationToken cancellationToken = default);
    Task<PayInOrderDto?> GetOrderAsync(string orderCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<PayInOrderDto>> GetAllOrdersAsync(CancellationToken cancellationToken = default);
    Task<WebhookResponseDto> ProcessWebhookAsync(VietQrWebhookPayload payload, CancellationToken cancellationToken = default);
    Task<PayInOrderDto> SimulateTransferSuccessAsync(string orderCode, CancellationToken cancellationToken = default);

    // Chữ ký chuẩn bảo mật phân hệ M-12 (STT 115, 116, 117, 118, 119, 122)
    Task<PayInOrderDto> CreatePayInOrderAsync(Guid currentUserId, UserRole currentRole, CreatePayInRequest request, CancellationToken cancellationToken = default);
    Task<PayInOrderDto> GetOrderForUserAsync(Guid currentUserId, bool isStaffOrAdmin, string orderCode, CancellationToken cancellationToken = default);
    Task<PagedResult<PayInOrderDto>> GetUserOrdersAsync(Guid currentUserId, string? status, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<PayInOrderDto> CancelPendingOrderAsync(Guid currentUserId, string orderCode, string? reason = null, CancellationToken cancellationToken = default);
    Task<WebhookResponseDto> ProcessWebhookAsync(VietQrWebhookPayload payload, string? headerSecret, CancellationToken cancellationToken = default);
    Task<PayInOrderDto> SimulateTransferAsync(string orderCode, SimulateTransferRequest? request = null, CancellationToken cancellationToken = default);
}
