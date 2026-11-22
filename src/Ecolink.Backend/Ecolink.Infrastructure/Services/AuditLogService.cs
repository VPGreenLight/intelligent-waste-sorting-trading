using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Bộ xóa cache trạng thái xác thực &amp; quyền RBAC tức thời trong IMemoryCache.
/// </summary>
public sealed class MemoryAuthStateCacheInvalidator : IAuthStateCacheInvalidator
{
    public const string CacheKeyPrefix = "auth_snapshot:";
    private readonly IMemoryCache _cache;

    public MemoryAuthStateCacheInvalidator(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void InvalidateUserAuthState(Guid userId)
    {
        _cache.Remove($"{CacheKeyPrefix}{userId}");
    }
}

/// <summary>
/// Tiện ích che dấu thông tin định danh cá nhân (PII Masking) theo quy tắc BR-ADM-06.
/// </summary>
public static class PiiMaskingHelper
{
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "***";
        var trimmed = email.Trim();
        var atIdx = trimmed.IndexOf('@');
        if (atIdx <= 0 || atIdx == trimmed.Length - 1) return "***";

        var local = trimmed[..atIdx];
        var domain = trimmed[(atIdx + 1)..];
        var visiblePrefix = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visiblePrefix}***@{domain}";
    }

    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "***";
        var trimmed = phone.Trim();
        if (trimmed.Length < 7) return "***";
        return $"{trimmed[..3]}****{trimmed[^3..]}";
    }
}

/// <summary>
/// Dịch vụ ghi và truy vấn Nhật ký kiểm toán hệ thống bất biến (UC A-02, BR-ADM-05).
/// </summary>
public class AuditLogService : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public AuditLogService(ApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task LogAsync(
        string action,
        string entityType,
        string? entityId,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null,
        Guid? explicitActorId = null,
        CancellationToken ct = default)
    {
        var actorId = explicitActorId ?? _currentUserService.UserId;
        var actorEmail = _currentUserService.Email;
        var actorRole = _currentUserService.Role;

        if (actorId.HasValue && (string.IsNullOrWhiteSpace(actorEmail) || string.IsNullOrWhiteSpace(actorRole)))
        {
            var actor = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == actorId.Value)
                .Select(u => new { u.Email, Role = u.Role.ToString() })
                .FirstOrDefaultAsync(ct);

            if (actor != null)
            {
                actorEmail ??= actor.Email;
                actorRole ??= actor.Role;
            }
        }

        var entry = new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            ActorEmail = actorEmail,
            ActorRole = actorRole,
            Action = action.Trim().ToUpperInvariant(),
            EntityType = entityType.Trim(),
            EntityId = entityId,
            OldValues = SerializeOrNull(oldValues),
            NewValues = SerializeOrNull(newValues),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            IpAddress = Truncate(_currentUserService.IpAddress, 45),
            UserAgent = Truncate(_currentUserService.UserAgent, 512),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AuditLogs.Add(entry);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditLogItemDto>> GetAuditLogsAsync(
        AuditLogFilterRequest filter,
        CancellationToken ct = default)
    {
        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var act = filter.Action.Trim().ToUpperInvariant();
            query = query.Where(a => a.Action == act);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            var entityType = filter.EntityType.Trim().ToLower();
            query = query.Where(a => a.EntityType.ToLower() == entityType);
        }

        if (filter.ActorUserId.HasValue)
        {
            query = query.Where(a => a.ActorUserId == filter.ActorUserId.Value);
        }

        if (filter.FromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(filter.FromDate.Value, DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAt >= fromUtc);
        }

        if (filter.ToDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(filter.ToDate.Value, DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAt <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(a =>
                (a.ActorEmail != null && a.ActorEmail.ToLower().Contains(kw)) ||
                (a.EntityId != null && a.EntityId.ToLower().Contains(kw)) ||
                (a.Reason != null && a.Reason.ToLower().Contains(kw)) ||
                a.Action.ToLower().Contains(kw));
        }

        var totalRecords = await query.CountAsync(ct);
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogItemDto
            {
                Id = a.Id,
                ActorUserId = a.ActorUserId,
                ActorEmail = a.ActorEmail,
                ActorRole = a.ActorRole,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                Reason = a.Reason,
                IpAddress = a.IpAddress,
                UserAgent = a.UserAgent,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<AuditLogItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords
        };
    }

    private static string? SerializeOrNull(object? value)
    {
        if (value == null) return null;
        if (value is string rawJson) return rawJson;
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static string? Truncate(string? input, int maxLength)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input.Length <= maxLength ? input : input[..maxLength];
    }
}
