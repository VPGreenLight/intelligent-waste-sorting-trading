using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

/// <summary>Handover Completion &amp; Transaction Receipt (UC R-06, U-09, U-12, JOB-01).</summary>
public interface IHandoverReceiptService
{
    // Recycler
    Task<ReceiptDto> PreviewAsync(Guid recyclerId, Guid requestId, CreateReceiptRequest request, CancellationToken ct = default);
    Task<ReceiptDto> CreateAsync(Guid recyclerId, Guid requestId, CreateReceiptRequest request, CancellationToken ct = default);
    Task<ReceiptDto> GetForRecyclerAsync(Guid recyclerId, Guid requestId, CancellationToken ct = default);
    Task<TransactionHistoryDto> GetRecyclerHistoryAsync(Guid recyclerId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);

    // Người dân
    Task<ReceiptDto> GetForUserAsync(Guid userId, Guid requestId, CancellationToken ct = default);
    Task<ReceiptDto> ConfirmAsync(Guid userId, Guid requestId, CancellationToken ct = default);
    Task<TransactionHistoryDto> GetUserHistoryAsync(Guid userId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);

    // JOB-01
    Task<int> AutoCompleteExpiredAsync(TimeSpan timeout, CancellationToken ct = default);
}
