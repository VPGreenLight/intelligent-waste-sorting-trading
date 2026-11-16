using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Tiện ích dùng chung cho phân hệ Gói Dịch vụ Nội bộ &amp; VietQR (M-12):
/// phân tích JSON đặc quyền, tính cấp bậc hiện tại, seed 3 gói chuẩn và kích hoạt thuê bao theo BR-PAY-04.
/// </summary>
internal static class ServicePackageHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ServicePackageFeaturesDto ParseFeatures(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ServicePackageFeaturesDto();
        }

        try
        {
            return JsonSerializer.Deserialize<ServicePackageFeaturesDto>(json, JsonOptions)
                   ?? new ServicePackageFeaturesDto();
        }
        catch
        {
            return new ServicePackageFeaturesDto();
        }
    }

    public static string SerializeFeatures(ServicePackageFeaturesDto? features)
    {
        return JsonSerializer.Serialize(features ?? new ServicePackageFeaturesDto(), JsonOptions);
    }

    /// <summary>
    /// Hợp nhất tất cả thuê bao đang hiệu lực thành tóm tắt quyền lợi và cấp bậc cao nhất (Pro &gt; Premium &gt; Basic).
    /// </summary>
    public static UserEntitlementSummaryDto AggregateEntitlements(IEnumerable<UserSubscription> activeSubscriptions)
    {
        var summary = new UserEntitlementSummaryDto();

        foreach (var sub in activeSubscriptions)
        {
            var f = ParseFeatures(sub.FeaturesSnapshot);
            if (f.AdFree) summary.IsAdFree = true;
            if (f.EcoPlusBadge) summary.HasEcoPlusBadge = true;
            if (f.VerifiedBlueBadge) summary.HasVerifiedBlueBadge = true;
            if (f.FeaturedMapMarker) summary.IsFeaturedMapMarker = true;
            if (f.DailyPointMultiplier > summary.DailyPointMultiplier)
            {
                summary.DailyPointMultiplier = f.DailyPointMultiplier;
            }
            if (f.MapPriorityBoost > summary.MapPriorityBoost)
            {
                summary.MapPriorityBoost = f.MapPriorityBoost;
            }
        }

        if (summary.HasVerifiedBlueBadge || summary.IsFeaturedMapMarker)
        {
            summary.CurrentTier = "Pro";
        }
        else if (summary.IsAdFree || summary.HasEcoPlusBadge)
        {
            summary.CurrentTier = "Premium";
        }
        else
        {
            summary.CurrentTier = "Basic";
        }

        return summary;
    }

    /// <summary>
    /// Bảo đảm 3 gói dịch vụ chuẩn BASIC - PREMIUM - PRO luôn tồn tại trong DB (hỗ trợ cả InMemory test DB chưa chạy HasData).
    /// </summary>
    public static async Task EnsureCanonicalPackagesExistAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var existingCodes = (await db.ServicePackages
                .IgnoreQueryFilters()
                .Select(p => p.PackageCode)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<ServicePackage>();

        if (!existingCodes.Contains(CanonicalServicePackages.BasicCode))
        {
            toAdd.Add(new ServicePackage
            {
                Id = CanonicalServicePackages.BasicId,
                PackageCode = CanonicalServicePackages.BasicCode,
                PackageName = "Basic (Mặc định)",
                TargetRole = ServiceTargetRole.All,
                Description = "Gói mặc định miễn phí với đầy đủ tính năng phân loại rác AI và bàn giao cơ bản.",
                IsDefaultFree = true,
                RequiresVerifiedKyb = false,
                PriceVnd = 0m,
                DurationDays = 0,
                FeaturesJson = CanonicalServicePackages.BasicFeaturesJson,
                IsActive = true,
                DisplayOrder = 10,
                CreatedAt = CanonicalServicePackages.SeedTimestampUtc,
                CreatedBy = "System",
                IsDeleted = false
            });
        }

        if (!existingCodes.Contains(CanonicalServicePackages.PremiumCode))
        {
            toAdd.Add(new ServicePackage
            {
                Id = CanonicalServicePackages.PremiumId,
                PackageCode = CanonicalServicePackages.PremiumCode,
                PackageName = "Premium (Tắt quảng cáo)",
                TargetRole = ServiceTargetRole.All,
                Description = "Tắt hoàn toàn banner quảng cáo trong 30 ngày kèm đặc quyền hội viên EcoPlus.",
                IsDefaultFree = false,
                RequiresVerifiedKyb = false,
                PriceVnd = 59_000m,
                DurationDays = 30,
                FeaturesJson = CanonicalServicePackages.PremiumFeaturesJson,
                IsActive = true,
                DisplayOrder = 20,
                CreatedAt = CanonicalServicePackages.SeedTimestampUtc,
                CreatedBy = "System",
                IsDeleted = false
            });
        }

        if (!existingCodes.Contains(CanonicalServicePackages.ProCode))
        {
            toAdd.Add(new ServicePackage
            {
                Id = CanonicalServicePackages.ProId,
                PackageCode = CanonicalServicePackages.ProCode,
                PackageName = "Pro (Tích xanh & Ưu tiên bản đồ)",
                TargetRole = ServiceTargetRole.Recycler,
                Description = "Gói thương mại 1 năm dành cho Cơ sở thu mua đã xác minh KYB: Tắt quảng cáo, Huy hiệu Tích xanh và Ghim Nổi bật ưu tiên trên bản đồ.",
                IsDefaultFree = false,
                RequiresVerifiedKyb = true,
                PriceVnd = 1_000_000m,
                DurationDays = 365,
                FeaturesJson = CanonicalServicePackages.ProFeaturesJson,
                IsActive = true,
                DisplayOrder = 30,
                CreatedAt = CanonicalServicePackages.SeedTimestampUtc,
                CreatedBy = "System",
                IsDeleted = false
            });
        }

        if (toAdd.Count > 0)
        {
            db.ServicePackages.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Kích hoạt thuê bao cho một ServiceOrder đã thanh toán thành công (lũy đẳng theo SourceOrderId - BR-PAY-02 &amp; BR-PAY-04):
    /// - Cùng mã gói: cộng dồn nối tiếp từ MAX(now, expires_at cũ).
    /// - Nâng bậc khác gói (VD PREMIUM -&gt; PRO): kích hoạt ngay tại now.
    /// </summary>
    public static async Task<UserSubscription> ActivateSubscriptionForOrderAsync(
        ApplicationDbContext db,
        ServiceOrder order,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var existingByOrder = await db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.SourceOrderId == order.Id, ct);

        if (existingByOrder != null)
        {
            return existingByOrder;
        }

        var durationDays = Math.Max(1, order.DurationDaysSnapshot);

        // Chỉ cộng dồn thời hạn đối với thuê bao đang hoạt động có CÙNG PackageCode (BR-PAY-04).
        // Nếu người dùng nâng bậc (VD từ PREMIUM lên PRO), gói PRO khởi chạy ngay từ utcNow.
        var latestSamePackageExpiry = await db.UserSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == order.UserId
                        && s.PackageCode == order.PackageCodeSnapshot
                        && s.Status == SubscriptionStatus.Active
                        && s.ExpiresAt > utcNow)
            .MaxAsync(s => (DateTime?)s.ExpiresAt, ct);

        var startsAt = latestSamePackageExpiry.HasValue && latestSamePackageExpiry.Value > utcNow
            ? latestSamePackageExpiry.Value
            : utcNow;

        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = order.UserId,
            PackageId = order.PackageId,
            SourceOrderId = order.Id,
            PackageCode = order.PackageCodeSnapshot,
            FeaturesSnapshot = string.IsNullOrWhiteSpace(order.FeaturesSnapshot) ? "{}" : order.FeaturesSnapshot,
            StartsAt = startsAt,
            ExpiresAt = startsAt.AddDays(durationDays),
            Status = SubscriptionStatus.Active,
            CreatedAt = utcNow,
            CreatedBy = order.UserId.ToString()
        };

        db.UserSubscriptions.Add(subscription);
        return subscription;
    }

    public static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
