using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Quản lý danh mục gói dịch vụ, cấu hình biểu phí Admin (A-10) và thuê bao người dùng (U-16, R-07).
/// </summary>
public class ServicePackageService : IServicePackageService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISubscriptionEntitlementService _entitlementService;
    private readonly IAuditLogService _auditLogService;

    public ServicePackageService(
        ApplicationDbContext dbContext,
        ISubscriptionEntitlementService entitlementService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _entitlementService = entitlementService;
        _auditLogService = auditLogService;
    }

    public async Task<IReadOnlyList<ServicePackageCatalogDto>> GetAvailablePackagesAsync(
        Guid currentUserId,
        UserRole currentRole,
        string? targetRoleFilter = null,
        CancellationToken ct = default)
    {
        await ServicePackageHelpers.EnsureCanonicalPackagesExistAsync(_dbContext, ct);

        var query = _dbContext.ServicePackages
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (currentRole == UserRole.User)
        {
            query = query.Where(p => p.TargetRole == ServiceTargetRole.All || p.TargetRole == ServiceTargetRole.User);
        }
        else if (!string.IsNullOrWhiteSpace(targetRoleFilter)
            && Enum.TryParse<ServiceTargetRole>(targetRoleFilter, true, out var parsedFilter)
            && parsedFilter != ServiceTargetRole.All)
        {
            query = query.Where(p => p.TargetRole == ServiceTargetRole.All || p.TargetRole == parsedFilter);
        }

        var packages = await query
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.PriceVnd)
            .ToListAsync(ct);

        var utcNow = DateTime.UtcNow;
        var validSubs = await _dbContext.UserSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == currentUserId
                        && s.Status == SubscriptionStatus.Active
                        && s.ExpiresAt > utcNow)
            .ToListAsync(ct);

        var currentEffectiveSubs = validSubs.Where(s => s.StartsAt <= utcNow).ToList();
        var entitlements = ServicePackageHelpers.AggregateEntitlements(currentEffectiveSubs);
        var currentTierRank = ServicePackageHelpers.GetTierRank(entitlements.CurrentTier);
        var recyclerProfile = currentRole == UserRole.Recycler
            ? await _dbContext.RecyclerProfiles.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == currentUserId, ct)
            : null;

        ProEligibilityReportDto? proEligibility = null;
        if (packages.Any(p => ServicePackageHelpers.IsProPackage(p.PackageCode, p.RequiresVerifiedKyb)))
        {
            proEligibility = await ServicePackageHelpers.EvaluateProEligibilityAsync(
                _dbContext,
                currentUserId,
                currentRole,
                utcNow,
                ct);
        }

        return packages.Select(pkg =>
        {
            var maxExpiry = validSubs
                .Where(s => string.Equals(s.PackageCode, pkg.PackageCode, StringComparison.OrdinalIgnoreCase))
                .Select(s => (DateTime?)s.ExpiresAt)
                .Max();

            var pkgRank = ServicePackageHelpers.GetTierRank(pkg.PackageCode, pkg.IsDefaultFree, pkg.RequiresVerifiedKyb);
            var isPro = ServicePackageHelpers.IsProPackage(pkg.PackageCode, pkg.RequiresVerifiedKyb);
            var isIncludedInHigherPlan = currentTierRank > pkgRank;
            var (eligible, reason) = isPro && proEligibility != null
                ? (proEligibility.IsEligible, proEligibility.BlockingReasons.FirstOrDefault())
                : EvaluateEligibility(pkg, currentRole, recyclerProfile);

            var isCurrentPlan = pkg.IsDefaultFree
                ? currentTierRank == 0
                : !isIncludedInHigherPlan && currentTierRank == pkgRank && maxExpiry.HasValue;

            var canRenew = !pkg.IsDefaultFree && !isIncludedInHigherPlan && !isPro && isCurrentPlan;

            return new ServicePackageCatalogDto
            {
                Id = pkg.Id,
                PackageCode = pkg.PackageCode,
                PackageName = pkg.PackageName,
                TargetRole = pkg.TargetRole.ToString(),
                Description = pkg.Description,
                IsDefaultFree = pkg.IsDefaultFree,
                RequiresVerifiedKyb = pkg.RequiresVerifiedKyb,
                PriceVnd = pkg.PriceVnd,
                DurationDays = pkg.DurationDays,
                DisplayOrder = pkg.DisplayOrder,
                Features = ServicePackageHelpers.ParseFeatures(pkg.FeaturesJson),
                IsActive = pkg.IsActive,
                IsEligible = eligible,
                IneligibleReason = reason,
                IsCurrentPlan = isCurrentPlan,
                IsIncludedInHigherPlan = isIncludedInHigherPlan,
                CanRenew = canRenew,
                CurrentActiveExpiresAt = maxExpiry,
                ProEligibility = isPro ? proEligibility : null
            };
        }).ToList();
    }

    public async Task<MySubscriptionsResponseDto> GetMySubscriptionsAsync(Guid currentUserId, CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;

        var subscriptions = await _dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Package)
            .Include(s => s.SourceOrder)
            .Where(s => s.UserId == currentUserId)
            .OrderByDescending(s => s.ExpiresAt)
            .ToListAsync(ct);

        var activeEntities = subscriptions
            .Where(s => !s.IsDeleted && s.Status == SubscriptionStatus.Active && s.ExpiresAt > utcNow)
            .OrderByDescending(s => s.ExpiresAt)
            .ToList();
        var pastEntities = subscriptions
            .Where(s => s.IsDeleted || s.Status != SubscriptionStatus.Active || s.ExpiresAt <= utcNow)
            .ToList();

        var entitlements = await _entitlementService.GetUserEntitlementsAsync(currentUserId, ct);

        return new MySubscriptionsResponseDto
        {
            CurrentTier = entitlements.CurrentTier,
            Entitlements = entitlements,
            ActiveSubscriptions = activeEntities.Select(s => MapSubscriptionDto(s, utcNow)).ToList(),
            PastSubscriptions = pastEntities.Select(s => MapSubscriptionDto(s, utcNow)).ToList()
        };
    }

    public async Task<IReadOnlyList<AdminServicePackageDto>> GetAllPackagesForAdminAsync(
        string? search = null,
        string? targetRole = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        await ServicePackageHelpers.EnsureCanonicalPackagesExistAsync(_dbContext, ct);

        var query = _dbContext.ServicePackages.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLowerInvariant();
            query = query.Where(p => p.PackageCode.ToLower().Contains(kw)
                                     || p.PackageName.ToLower().Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(targetRole)
            && Enum.TryParse<ServiceTargetRole>(targetRole, true, out var parsedRole))
        {
            query = query.Where(p => p.TargetRole == parsedRole);
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        var packages = await query
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.PriceVnd)
            .ToListAsync(ct);

        var packageIds = packages.Select(p => p.Id).ToList();
        var utcNow = DateTime.UtcNow;

        var activeCountsByPkg = await _dbContext.UserSubscriptions
            .AsNoTracking()
            .Where(s => packageIds.Contains(s.PackageId)
                        && s.Status == SubscriptionStatus.Active
                        && s.ExpiresAt > utcNow)
            .GroupBy(s => s.PackageId)
            .Select(g => new { PackageId = g.Key, ActiveUsers = g.Select(x => x.UserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.PackageId, x => x.ActiveUsers, ct);

        var paidOrderStats = await _dbContext.ServiceOrders
            .AsNoTracking()
            .Where(o => packageIds.Contains(o.PackageId) && o.OrderStatus == ServiceOrderStatus.Paid)
            .GroupBy(o => o.PackageId)
            .Select(g => new
            {
                PackageId = g.Key,
                Count = g.Count(),
                Revenue = g.Sum(x => x.ExpectedAmountVnd)
            })
            .ToDictionaryAsync(x => x.PackageId, ct);

        return packages.Select(pkg =>
        {
            paidOrderStats.TryGetValue(pkg.Id, out var stat);
            return new AdminServicePackageDto
            {
                Id = pkg.Id,
                PackageCode = pkg.PackageCode,
                PackageName = pkg.PackageName,
                TargetRole = pkg.TargetRole.ToString(),
                Description = pkg.Description,
                IsDefaultFree = pkg.IsDefaultFree,
                RequiresVerifiedKyb = pkg.RequiresVerifiedKyb,
                PriceVnd = pkg.PriceVnd,
                DurationDays = pkg.DurationDays,
                DisplayOrder = pkg.DisplayOrder,
                Features = ServicePackageHelpers.ParseFeatures(pkg.FeaturesJson),
                IsActive = pkg.IsActive,
                IsEligible = true,
                ActiveSubscribersCount = activeCountsByPkg.GetValueOrDefault(pkg.Id, 0),
                TotalPaidOrdersCount = stat?.Count ?? 0,
                TotalRevenueVnd = stat?.Revenue ?? 0m,
                CreatedAt = pkg.CreatedAt,
                UpdatedAt = pkg.UpdatedAt,
                UpdatedBy = pkg.UpdatedBy
            };
        }).ToList();
    }

    public async Task<AdminServicePackageDto> CreatePackageAsync(
        Guid adminUserId,
        UpsertServicePackageRequest request,
        CancellationToken ct = default)
    {
        await ServicePackageHelpers.EnsureCanonicalPackagesExistAsync(_dbContext, ct);

        var normalizedCode = request.PackageCode.Trim().ToUpperInvariant();
        var duplicateExists = await _dbContext.ServicePackages
            .IgnoreQueryFilters()
            .AnyAsync(p => p.PackageCode.ToUpper() == normalizedCode, ct);

        if (duplicateExists)
        {
            throw new ConflictException(
                "BR-PAY-05",
                "MSG-GEN-01",
                $"Mã gói dịch vụ '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        if (!Enum.TryParse<ServiceTargetRole>(request.TargetRole, true, out var targetRole))
        {
            throw new BusinessRuleException("BR-PAY-05", "MSG-GEN-01", "Vai trò áp dụng gói dịch vụ không hợp lệ.");
        }

        var package = new ServicePackage
        {
            Id = Guid.NewGuid(),
            PackageCode = normalizedCode,
            PackageName = request.PackageName.Trim(),
            TargetRole = targetRole,
            Description = request.Description?.Trim(),
            IsDefaultFree = false,
            RequiresVerifiedKyb = request.RequiresVerifiedKyb,
            PriceVnd = Math.Round(request.PriceVnd, 0),
            DurationDays = request.DurationDays,
            FeaturesJson = ServicePackageHelpers.SerializeFeatures(request.Features),
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = adminUserId.ToString()
        };

        _dbContext.ServicePackages.Add(package);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            action: "CREATE_SERVICE_PACKAGE",
            entityType: nameof(ServicePackage),
            entityId: package.Id.ToString(),
            oldValues: null,
            newValues: new
            {
                package.PackageCode,
                package.PackageName,
                TargetRole = package.TargetRole.ToString(),
                package.PriceVnd,
                package.DurationDays,
                package.RequiresVerifiedKyb,
                package.IsActive
            },
            reason: $"Admin khởi tạo gói dịch vụ mới '{package.PackageCode}'.",
            explicitActorId: adminUserId,
            ct: ct);

        return (await GetAllPackagesForAdminAsync(search: package.PackageCode, ct: ct))
            .First(x => x.Id == package.Id);
    }

    public async Task<AdminServicePackageDto> UpdatePackageAsync(
        Guid adminUserId,
        Guid packageId,
        UpsertServicePackageRequest request,
        CancellationToken ct = default)
    {
        await ServicePackageHelpers.EnsureCanonicalPackagesExistAsync(_dbContext, ct);

        var package = await _dbContext.ServicePackages
            .FirstOrDefaultAsync(p => p.Id == packageId, ct)
            ?? throw new NotFoundException("Không tìm thấy gói dịch vụ cần cập nhật.");

        if (!Enum.TryParse<ServiceTargetRole>(request.TargetRole, true, out var targetRole))
        {
            throw new BusinessRuleException("BR-PAY-05", "MSG-GEN-01", "Vai trò áp dụng gói dịch vụ không hợp lệ.");
        }

        var normalizedCode = request.PackageCode.Trim().ToUpperInvariant();
        if (!string.Equals(normalizedCode, package.PackageCode, StringComparison.OrdinalIgnoreCase))
        {
            var hasOrders = await _dbContext.ServiceOrders.AnyAsync(o => o.PackageId == package.Id, ct);
            if (hasOrders)
            {
                throw new BusinessRuleException(
                    "BR-PAY-05",
                    "MSG-GEN-01",
                    "Không thể đổi mã gói dịch vụ (packageCode) sau khi đã phát sinh đơn thanh toán.");
            }
            package.PackageCode = normalizedCode;
        }

        var oldSnapshot = new
        {
            package.PackageCode,
            package.PackageName,
            TargetRole = package.TargetRole.ToString(),
            package.PriceVnd,
            package.DurationDays,
            package.RequiresVerifiedKyb,
            package.FeaturesJson,
            package.IsActive
        };

        if (package.IsDefaultFree)
        {
            // Bảo vệ gói nền BASIC luôn 0đ và áp dụng cho All (BR-PAY-05)
            package.PackageName = request.PackageName.Trim();
            package.Description = request.Description?.Trim();
            package.DisplayOrder = request.DisplayOrder;
        }
        else
        {
            package.PackageName = request.PackageName.Trim();
            package.TargetRole = targetRole;
            package.Description = request.Description?.Trim();
            package.RequiresVerifiedKyb = request.RequiresVerifiedKyb;
            package.PriceVnd = Math.Round(request.PriceVnd, 0);
            package.DurationDays = request.DurationDays;
            package.FeaturesJson = ServicePackageHelpers.SerializeFeatures(request.Features);
            package.IsActive = request.IsActive;
            package.DisplayOrder = request.DisplayOrder;
        }

        package.UpdatedAt = DateTime.UtcNow;
        package.UpdatedBy = adminUserId.ToString();

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            action: "UPDATE_SERVICE_PACKAGE",
            entityType: nameof(ServicePackage),
            entityId: package.Id.ToString(),
            oldValues: oldSnapshot,
            newValues: new
            {
                package.PackageCode,
                package.PackageName,
                TargetRole = package.TargetRole.ToString(),
                package.PriceVnd,
                package.DurationDays,
                package.RequiresVerifiedKyb,
                package.FeaturesJson,
                package.IsActive
            },
            reason: $"Admin cập nhật biểu phí/cấu hình gói '{package.PackageCode}' (áp dụng cho lượt mua mới theo BR-PAY-05).",
            explicitActorId: adminUserId,
            ct: ct);

        return (await GetAllPackagesForAdminAsync(search: package.PackageCode, ct: ct))
            .First(x => x.Id == package.Id);
    }

    public async Task<AdminServicePackageDto> TogglePackageStatusAsync(
        Guid adminUserId,
        Guid packageId,
        ToggleServicePackageStatusRequest request,
        CancellationToken ct = default)
    {
        await ServicePackageHelpers.EnsureCanonicalPackagesExistAsync(_dbContext, ct);

        var package = await _dbContext.ServicePackages
            .FirstOrDefaultAsync(p => p.Id == packageId, ct)
            ?? throw new NotFoundException("Không tìm thấy gói dịch vụ cần thay đổi trạng thái.");

        if (package.IsDefaultFree && !request.IsActive)
        {
            throw new BusinessRuleException(
                "BR-PAY-05",
                "MSG-GEN-01",
                "Không thể tạm ngừng gói mặc định miễn phí BASIC của hệ thống.");
        }

        var oldActive = package.IsActive;
        package.IsActive = request.IsActive;
        package.UpdatedAt = DateTime.UtcNow;
        package.UpdatedBy = adminUserId.ToString();

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            action: "TOGGLE_SERVICE_PACKAGE_STATUS",
            entityType: nameof(ServicePackage),
            entityId: package.Id.ToString(),
            oldValues: new { IsActive = oldActive },
            newValues: new { IsActive = package.IsActive },
            reason: string.IsNullOrWhiteSpace(request.Reason)
                ? $"Admin chuyển trạng thái mở bán gói '{package.PackageCode}' sang {(package.IsActive ? "ACTIVE" : "INACTIVE")}."
                : request.Reason.Trim(),
            explicitActorId: adminUserId,
            ct: ct);

        return (await GetAllPackagesForAdminAsync(search: package.PackageCode, ct: ct))
            .First(x => x.Id == package.Id);
    }

    private static (bool IsEligible, string? Reason) EvaluateEligibility(
        ServicePackage pkg,
        UserRole currentRole,
        RecyclerProfile? recyclerProfile)
    {
        if (pkg.IsDefaultFree)
        {
            return (true, null);
        }

        if (pkg.TargetRole == ServiceTargetRole.Recycler && currentRole != UserRole.Recycler)
        {
            return (false, "Gói thương mại Pro chỉ dành riêng cho tài khoản Cơ sở thu mua (Recycler).");
        }

        if (pkg.TargetRole == ServiceTargetRole.User && currentRole == UserRole.Recycler)
        {
            return (false, "Gói này chỉ dành cho tài khoản Người dân (Citizen).");
        }

        if (pkg.RequiresVerifiedKyb)
        {
            if (currentRole != UserRole.Recycler)
            {
                return (false, "Gói này yêu cầu tài khoản Cơ sở thu mua đã xác minh KYB.");
            }

            if (recyclerProfile == null || recyclerProfile.VerificationStatus != VerificationStatus.Verified)
            {
                return (false, "Cơ sở thu mua cần hoàn tất xác minh hồ sơ KYB trước khi đăng ký gói Pro Tích xanh.");
            }
        }

        return (true, null);
    }

    private static ActiveSubscriptionDto MapSubscriptionDto(UserSubscription s, DateTime utcNow)
    {
        var remaining = s.ExpiresAt > utcNow
            ? (int)Math.Ceiling((s.ExpiresAt - utcNow).TotalDays)
            : 0;

        string statusLabel = s.Status == SubscriptionStatus.Revoked
            ? SubscriptionStatus.Revoked.ToString()
            : s.ExpiresAt <= utcNow
                ? SubscriptionStatus.Expired.ToString()
                : s.StartsAt > utcNow
                    ? "Scheduled"
                    : SubscriptionStatus.Active.ToString();

        return new ActiveSubscriptionDto
        {
            SubscriptionId = s.Id,
            PackageId = s.PackageId,
            PackageCode = s.PackageCode,
            PackageName = s.Package?.PackageName ?? s.PackageCode,
            SourceOrderCode = s.SourceOrder?.OrderCode ?? string.Empty,
            StartsAt = s.StartsAt,
            ExpiresAt = s.ExpiresAt,
            RemainingDays = Math.Max(0, remaining),
            Status = statusLabel,
            Features = ServicePackageHelpers.ParseFeatures(s.FeaturesSnapshot)
        };
    }
}
