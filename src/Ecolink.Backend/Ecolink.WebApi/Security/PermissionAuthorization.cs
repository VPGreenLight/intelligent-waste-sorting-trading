using System.Security.Claims;
using Ecolink.Domain.Constants;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Ecolink.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ecolink.WebApi.Security;

/// <summary>
/// Attribute phân quyền hạt mịn theo mã quyền RBAC của Staff (UC A-04, BR-ADM-02).
/// Admin tự động có toàn quyền; Staff bắt buộc có mã quyền tương ứng trong staff_permission_assignments.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public HasPermissionAttribute(string permissionCode)
    {
        Policy = $"{PolicyPrefix}{StaffPermissions.Normalize(permissionCode)}";
    }
}

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;

public sealed record CachedUserAuthSnapshot(
    UserRole Role,
    UserStatus Status,
    DateTime? LockoutUntil,
    string? StaffStatus,
    HashSet<string> Permissions);

public static class UserAuthSnapshotLoader
{
    public static async Task<CachedUserAuthSnapshot?> LoadAsync(
        Guid userId,
        ApplicationDbContext dbContext,
        IMemoryCache cache,
        CancellationToken ct = default)
    {
        var cacheKey = $"{MemoryAuthStateCacheInvalidator.CacheKeyPrefix}{userId}";
        if (cache.TryGetValue(cacheKey, out CachedUserAuthSnapshot? cached) && cached != null)
        {
            return cached;
        }

        var row = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Role,
                u.Status,
                u.LockoutUntil,
                StaffStatus = u.StaffProfile != null ? u.StaffProfile.Status : null,
                Permissions = u.StaffProfile != null
                    ? u.StaffProfile.Permissions.Select(p => p.PermissionCode).ToList()
                    : new List<string>()
            })
            .FirstOrDefaultAsync(ct);

        if (row == null) return null;

        var snapshot = new CachedUserAuthSnapshot(
            row.Role,
            row.Status,
            row.LockoutUntil,
            row.StaffStatus,
            row.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase));

        cache.Set(cacheKey, snapshot, TimeSpan.FromMinutes(5));
        return snapshot;
    }
}

/// <summary>
/// Middleware chặn tức thời mọi request mang JWT còn hạn nhưng tài khoản vừa bị khóa / vô hiệu hóa (UC S-07, A-03 - BR-ADM-03, BR-AUTH-05).
/// </summary>
public sealed class AccountStatusGuardMiddleware
{
    private readonly RequestDelegate _next;

    public AccountStatusGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var isBypassPath = path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/api/v1/auth/logout", StringComparison.OrdinalIgnoreCase);

            if (!isBypassPath)
            {
                var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? context.User.FindFirstValue("sub");

                if (Guid.TryParse(sub, out var userId))
                {
                    var db = context.RequestServices.GetService<ApplicationDbContext>();
                    var cache = context.RequestServices.GetService<IMemoryCache>();

                    if (db != null && cache != null)
                    {
                        var snapshot = await UserAuthSnapshotLoader.LoadAsync(userId, db, cache, context.RequestAborted);
                        if (snapshot != null)
                        {
                            var isTemporarilyLocked = snapshot.Status == UserStatus.Locked &&
                                                      snapshot.LockoutUntil.HasValue &&
                                                      snapshot.LockoutUntil.Value > DateTime.UtcNow;

                            if (snapshot.Status is UserStatus.Suspended or UserStatus.Deactivated || isTemporarilyLocked)
                            {
                                throw new ForbiddenAccessException(
                                    "BR-AUTH-05",
                                    "MSG-AUTH-04",
                                    "Tài khoản của bạn đã bị tạm đình chỉ hoặc ngừng kích hoạt.");
                            }
                        }
                    }
                }
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Authorization Handler kiểm tra quyền RBAC hạt mịn cho Nhân viên vận hành (UC A-04, BR-ADM-02).
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public PermissionAuthorizationHandler(ApplicationDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? context.User.FindFirstValue("sub");

        if (!Guid.TryParse(sub, out var userId))
        {
            return;
        }

        var snapshot = await UserAuthSnapshotLoader.LoadAsync(userId, _dbContext, _cache);
        if (snapshot == null)
        {
            return;
        }

        if (snapshot.Status is UserStatus.Suspended or UserStatus.Deactivated)
        {
            throw new ForbiddenAccessException(
                "BR-AUTH-05",
                "MSG-AUTH-04",
                "Tài khoản của bạn đã bị tạm đình chỉ hoặc ngừng kích hoạt.");
        }

        // Admin có toàn quyền quản trị (BR-ADM-02)
        if (snapshot.Role == UserRole.Admin)
        {
            context.Succeed(requirement);
            return;
        }

        // Staff phải đang ACTIVE và được gán đúng mã quyền yêu cầu (BR-ADM-02)
        if (snapshot.Role == UserRole.Staff &&
            string.Equals(snapshot.StaffStatus ?? "ACTIVE", "ACTIVE", StringComparison.OrdinalIgnoreCase) &&
            snapshot.Permissions.Contains(requirement.PermissionCode))
        {
            context.Succeed(requirement);
            return;
        }

        throw new ForbiddenAccessException(
            "BR-ADM-02",
            "MSG-ADM-04",
            "Bạn không có quyền thực hiện thao tác này.");
    }
}
