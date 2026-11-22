using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Enums;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Dịch vụ đọc và cache quyền lợi thuê bao theo tài khoản (U-16, R-07, U-05).
/// </summary>
public class SubscriptionEntitlementService : ISubscriptionEntitlementService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;

    private static string BuildCacheKey(Guid userId) => $"sub_entitlements:{userId:N}";

    public SubscriptionEntitlementService(ApplicationDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<UserEntitlementSummaryDto> GetUserEntitlementsAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = BuildCacheKey(userId);
        if (_cache.TryGetValue(cacheKey, out UserEntitlementSummaryDto? cached) && cached != null)
        {
            return cached;
        }

        var utcNow = DateTime.UtcNow;
        var activeSubs = await _dbContext.UserSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId
                        && s.Status == SubscriptionStatus.Active
                        && s.StartsAt <= utcNow
                        && s.ExpiresAt > utcNow)
            .ToListAsync(ct);

        var summary = ServicePackageHelpers.AggregateEntitlements(activeSubs);
        _cache.Set(cacheKey, summary, TimeSpan.FromMinutes(2));
        return summary;
    }

    public async Task<IReadOnlyDictionary<Guid, UserEntitlementSummaryDto>> GetBatchEntitlementsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken ct = default)
    {
        var distinctIds = userIds.Distinct().ToList();
        var result = new Dictionary<Guid, UserEntitlementSummaryDto>(distinctIds.Count);
        if (distinctIds.Count == 0)
        {
            return result;
        }

        var missingIds = new List<Guid>();
        foreach (var id in distinctIds)
        {
            if (_cache.TryGetValue(BuildCacheKey(id), out UserEntitlementSummaryDto? cached) && cached != null)
            {
                result[id] = cached;
            }
            else
            {
                missingIds.Add(id);
            }
        }

        if (missingIds.Count > 0)
        {
            var utcNow = DateTime.UtcNow;
            var activeSubs = await _dbContext.UserSubscriptions
                .AsNoTracking()
                .Where(s => missingIds.Contains(s.UserId)
                            && s.Status == SubscriptionStatus.Active
                            && s.StartsAt <= utcNow
                            && s.ExpiresAt > utcNow)
                .ToListAsync(ct);

            var grouped = activeSubs.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var id in missingIds)
            {
                var subsForUser = grouped.TryGetValue(id, out var list)
                    ? list
                    : new List<Domain.Entities.UserSubscription>();
                var summary = ServicePackageHelpers.AggregateEntitlements(subsForUser);
                _cache.Set(BuildCacheKey(id), summary, TimeSpan.FromMinutes(2));
                result[id] = summary;
            }
        }

        return result;
    }

    public void InvalidateUserEntitlements(Guid userId)
    {
        _cache.Remove(BuildCacheKey(userId));
    }
}
