using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

public interface IPlatformPolicyService
{
    Task<IReadOnlyList<PlatformPolicySectionDto>> GetPublicPoliciesAsync(
        string? category = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<PlatformPolicySectionDto>> GetAdminPoliciesAsync(
        string? category = null,
        string? status = null,
        string? search = null,
        CancellationToken ct = default);

    Task<PlatformPolicySectionDto> CreatePolicyAsync(
        Guid adminUserId,
        SavePlatformPolicyRequest request,
        CancellationToken ct = default);

    Task<PlatformPolicySectionDto> UpdatePolicyAsync(
        Guid adminUserId,
        Guid id,
        SavePlatformPolicyRequest request,
        CancellationToken ct = default);

    Task<PlatformPolicySectionDto> SetPolicyStatusAsync(
        Guid adminUserId,
        Guid id,
        string status,
        CancellationToken ct = default);

    Task DeletePolicyAsync(
        Guid adminUserId,
        Guid id,
        CancellationToken ct = default);
}
