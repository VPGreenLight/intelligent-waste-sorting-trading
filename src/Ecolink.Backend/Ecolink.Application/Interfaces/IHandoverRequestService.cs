using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

/// <summary>Handover Request Management (UC U-06, U-07, R-03, R-04, JOB-03).</summary>
public interface IHandoverRequestService
{
    // Người dân
    Task<HandoverRequestDto> CreateAsync(Guid userId, CreateHandoverRequest request, CancellationToken ct = default);
    Task<HandoverRequestDto> UpdateAsync(Guid userId, Guid requestId, UpdateHandoverRequest request, CancellationToken ct = default);
    Task<HandoverRequestDto> CancelByUserAsync(Guid userId, Guid requestId, string reason, CancellationToken ct = default);
    Task<HandoverPagedResult<HandoverRequestDto>> GetUserRequestsAsync(Guid userId, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<HandoverRequestDto> GetUserRequestAsync(Guid userId, Guid requestId, CancellationToken ct = default);

    // Recycler
    Task<HandoverPagedResult<HandoverRequestDto>> GetRecyclerRequestsAsync(Guid recyclerId, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<HandoverRequestDto> GetRecyclerRequestAsync(Guid recyclerId, Guid requestId, CancellationToken ct = default);
    Task<HandoverRequestDto> AcceptAsync(Guid recyclerId, Guid requestId, string? note, CancellationToken ct = default);
    Task<HandoverRequestDto> RejectAsync(Guid recyclerId, Guid requestId, string reason, CancellationToken ct = default);
    Task<HandoverRequestDto> CancelByRecyclerAsync(Guid recyclerId, Guid requestId, string reason, CancellationToken ct = default);

    // JOB-03
    Task<int> ExpirePendingAsync(TimeSpan timeout, CancellationToken ct = default);
}
