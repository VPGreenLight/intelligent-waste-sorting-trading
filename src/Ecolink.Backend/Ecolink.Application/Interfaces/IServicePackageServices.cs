using Ecolink.Application.Dtos;
using Ecolink.Domain.Enums;

namespace Ecolink.Application.Interfaces;

/// <summary>
/// Quản trị danh mục gói dịch vụ &amp; thuê bao quyền lợi người dùng (U-16, R-07, A-10 - STT 120, 121, 123–126).
/// </summary>
public interface IServicePackageService
{
    Task<IReadOnlyList<ServicePackageCatalogDto>> GetAvailablePackagesAsync(
        Guid currentUserId,
        UserRole currentRole,
        string? targetRoleFilter = null,
        CancellationToken ct = default);

    Task<MySubscriptionsResponseDto> GetMySubscriptionsAsync(
        Guid currentUserId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AdminServicePackageDto>> GetAllPackagesForAdminAsync(
        string? search = null,
        string? targetRole = null,
        bool? isActive = null,
        CancellationToken ct = default);

    Task<AdminServicePackageDto> CreatePackageAsync(
        Guid adminUserId,
        UpsertServicePackageRequest request,
        CancellationToken ct = default);

    Task<AdminServicePackageDto> UpdatePackageAsync(
        Guid adminUserId,
        Guid packageId,
        UpsertServicePackageRequest request,
        CancellationToken ct = default);

    Task<AdminServicePackageDto> TogglePackageStatusAsync(
        Guid adminUserId,
        Guid packageId,
        ToggleServicePackageStatusRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Truy vấn &amp; tổng hợp quyền lợi thuê bao có bộ nhớ đệm IMemoryCache (U-16, R-07, U-05).
/// </summary>
public interface ISubscriptionEntitlementService
{
    Task<UserEntitlementSummaryDto> GetUserEntitlementsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, UserEntitlementSummaryDto>> GetBatchEntitlementsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken ct = default);

    void InvalidateUserEntitlements(Guid userId);
}

/// <summary>
/// Sổ cái đối soát dòng tiền VietQR, xử lý bất thường &amp; xuất bảng kê (A-11 - STT 127–129).
/// </summary>
public interface IAdminReconciliationService
{
    Task<ReconciliationLedgerResponseDto> GetPaymentLedgerAsync(
        ReconciliationLedgerQuery query,
        CancellationToken ct = default);

    Task<ReconciliationLedgerItemDto> ResolvePaymentAnomalyAsync(
        Guid adminUserId,
        string code,
        ResolvePaymentAnomalyRequest request,
        CancellationToken ct = default);

    Task<ExportedReportFileResult> ExportReconciliationReportAsync(
        Guid adminUserId,
        ReconciliationExportQuery query,
        CancellationToken ct = default);
}
