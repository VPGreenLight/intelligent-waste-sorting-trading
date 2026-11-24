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

    public static bool IsProPackage(string? packageCode, bool requiresVerifiedKyb = false)
    {
        return requiresVerifiedKyb
               || string.Equals(packageCode?.Trim(), CanonicalServicePackages.ProCode, StringComparison.OrdinalIgnoreCase);
    }

    public static int GetTierRank(string? tierOrCode, bool isDefaultFree = false, bool requiresVerifiedKyb = false)
    {
        if (IsProPackage(tierOrCode, requiresVerifiedKyb)
            || string.Equals(tierOrCode?.Trim(), "Pro", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (isDefaultFree
            || string.IsNullOrWhiteSpace(tierOrCode)
            || string.Equals(tierOrCode.Trim(), CanonicalServicePackages.BasicCode, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return 1;
    }

    /// <summary>
    /// Đánh giá 4 tiêu chí điều kiện đăng ký / tái đăng ký gói Tích xanh PRO cho Cơ sở thu mua (R-07):
    /// 1. KYB = Verified &amp; hồ sơ cơ sở đầy đủ (+ cập nhật lại nếu từng có cảnh cáo ở kỳ Pro trước).
    /// 2. Số lần cảnh cáo không vượt quá 5 (WarningCount &lt;= 5; bỏ qua điều kiện khiếu nại).
    /// 3. Điểm đánh giá tích cực: tối thiểu 50 lượt đánh giá (TotalReviews &gt;= 50) &amp; điểm trung bình &gt;= 4.0 sao ngay từ lần đăng ký đầu tiên lẫn tái đăng ký.
    /// 4. Không có gói Pro đang còn hiệu lực (không gia hạn cộng dồn trước hạn).
    /// </summary>
    public static async Task<ProEligibilityReportDto> EvaluateProEligibilityAsync(
        ApplicationDbContext db,
        Guid userId,
        UserRole currentRole,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        const int MaxAllowedWarnings = 5;
        const decimal MinRequiredRating = 4.0m;
        const int MinRequiredReviews = 50;

        var report = new ProEligibilityReportDto
        {
            MaxAllowedWarnings = MaxAllowedWarnings,
            MinRequiredRating = MinRequiredRating,
            MinRequiredReviews = MinRequiredReviews,
            MinRequiredReviewsForRenewal = MinRequiredReviews
        };

        if (userId == Guid.Empty || currentRole != UserRole.Recycler)
        {
            report.VerificationStatus = "NotApplicable";
            report.BlockingReasons.Add("Chỉ tài khoản Cơ sở thu mua (Recycler) đã xác minh pháp lý (KYB = Verified) mới đủ điều kiện đăng ký gói Tích xanh Pro.");
            return report;
        }

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        var profile = await db.RecyclerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        var proSubs = await db.UserSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId
                        && (s.PackageCode.ToUpper() == CanonicalServicePackages.ProCode
                            || (s.Package != null && s.Package.RequiresVerifiedKyb)))
            .ToListAsync(ct);

        var activePro = proSubs
            .Where(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > utcNow)
            .OrderByDescending(s => s.ExpiresAt)
            .FirstOrDefault();

        var latestPastPro = proSubs
            .Where(s => s.ExpiresAt <= utcNow || s.Status != SubscriptionStatus.Active)
            .OrderByDescending(s => s.ExpiresAt)
            .FirstOrDefault();

        bool isReRegistration = latestPastPro != null;
        report.IsReRegistration = isReRegistration;
        report.VerificationStatus = profile?.VerificationStatus.ToString() ?? "Missing";
        report.CurrentWarningCount = user?.WarningCount ?? 0;
        report.AverageRating = profile?.AverageRating ?? 0m;
        report.TotalReviews = profile?.TotalReviews ?? 0;
        report.ActiveProExpiresAt = activePro?.ExpiresAt;

        // 1. KYB & Hồ sơ cơ sở (+ xác thực lại nếu phát sinh cảnh cáo sau kỳ Pro cũ)
        bool hasValidProfile = user != null
                               && user.Status == UserStatus.Active
                               && profile != null
                               && profile.VerificationStatus == VerificationStatus.Verified
                               && !string.IsNullOrWhiteSpace(profile.FacilityName)
                               && !string.IsNullOrWhiteSpace(profile.BusinessAddress);

        bool requiresReverify = isReRegistration
                                && latestPastPro != null
                                && user?.ModeratedAt != null
                                && user.ModeratedAt.Value > latestPastPro.StartsAt
                                && (profile == null || !profile.UpdatedAt.HasValue || profile.UpdatedAt.Value <= user.ModeratedAt.Value);

        report.RequiresProfileReverification = requiresReverify;
        report.IsKybAndProfileValid = hasValidProfile && !requiresReverify;

        if (!hasValidProfile)
        {
            report.BlockingReasons.Add("Cơ sở thu mua cần hoàn tất xác minh hồ sơ KYB (Verified) và cập nhật đầy đủ tên cơ sở, địa chỉ kho bãi trước khi đăng ký gói Pro Tích xanh.");
        }
        else if (requiresReverify)
        {
            report.BlockingReasons.Add("Tài khoản từng phát sinh cảnh cáo trong chu kỳ Pro trước. Vui lòng cập nhật và xác thực lại hồ sơ cơ sở trước khi đăng ký lại gói Pro.");
        }

        // 2. Giới hạn cảnh cáo <= 5 lần (bỏ qua điều kiện khiếu nại theo yêu cầu nghiệp vụ)
        report.IsWarningLimitOk = report.CurrentWarningCount <= MaxAllowedWarnings;
        if (!report.IsWarningLimitOk)
        {
            report.BlockingReasons.Add($"Tài khoản đang có {report.CurrentWarningCount} lần cảnh cáo (vượt quá giới hạn tối đa {MaxAllowedWarnings} lần cảnh cáo cho phép để đăng ký gói Tích xanh Pro).");
        }

        // 3. Mức đánh giá tích cực (tối thiểu 50 lượt đánh giá & điểm trung bình >= 4.0 sao ngay từ lần đăng ký đầu tiên)
        if (report.TotalReviews < MinRequiredReviews)
        {
            report.IsPositiveFeedbackMet = false;
            report.BlockingReasons.Add(
                $"Cơ sở thu mua hiện có {report.TotalReviews}/{MinRequiredReviews} lượt đánh giá (yêu cầu tối thiểu {MinRequiredReviews} lượt đánh giá thực tế và điểm trung bình từ {MinRequiredRating:0.0}/5.0 sao trở lên để đăng ký gói Tích xanh Pro).");
        }
        else if (report.AverageRating < MinRequiredRating)
        {
            report.IsPositiveFeedbackMet = false;
            report.BlockingReasons.Add(
                $"Điểm đánh giá trung bình hiện tại là {report.AverageRating:0.0}/5.0 sao ({report.TotalReviews} lượt) — yêu cầu tối thiểu {MinRequiredRating:0.0}/5.0 sao (kèm từ {MinRequiredReviews} lượt đánh giá) để đăng ký gói Tích xanh Pro.");
        }
        else
        {
            report.IsPositiveFeedbackMet = true;
        }

        // 4. Không gia hạn cộng dồn khi gói Pro đang hiệu lực
        report.IsNotCurrentlyActivePro = activePro == null;
        if (!report.IsNotCurrentlyActivePro)
        {
            report.BlockingReasons.Add($"Gói Tích xanh Pro hiện đang còn hiệu lực đến {activePro!.ExpiresAt:dd/MM/yyyy} và không áp dụng gia hạn cộng dồn trước hạn.");
        }

        report.IsEligible = report.IsKybAndProfileValid
                            && report.IsWarningLimitOk
                            && report.IsPositiveFeedbackMet
                            && report.IsNotCurrentlyActivePro;

        return report;
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

        // Chỉ cộng dồn thời hạn đối với gói hỗ trợ gia hạn (PREMIUM).
        // Gói thương mại PRO không gia hạn cộng dồn trước hạn và luôn kích hoạt từ utcNow.
        bool isPro = IsProPackage(order.PackageCodeSnapshot);
        DateTime startsAt = utcNow;
        if (!isPro)
        {
            var latestSamePackageExpiry = await db.UserSubscriptions
                .AsNoTracking()
                .Where(s => s.UserId == order.UserId
                            && s.PackageCode == order.PackageCodeSnapshot
                            && s.Status == SubscriptionStatus.Active
                            && s.ExpiresAt > utcNow)
                .MaxAsync(s => (DateTime?)s.ExpiresAt, ct);

            if (latestSamePackageExpiry.HasValue && latestSamePackageExpiry.Value > utcNow)
            {
                startsAt = latestSamePackageExpiry.Value;
            }
        }

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
