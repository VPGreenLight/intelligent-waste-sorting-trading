using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

/// <summary>
/// Xóa cache trạng thái xác thực &amp; quyền RBAC tức thời khi khóa tài khoản hoặc đổi quyền (BR-ADM-02, BR-ADM-03).
/// </summary>
public interface IAuthStateCacheInvalidator
{
    void InvalidateUserAuthState(Guid userId);
}

/// <summary>
/// Dịch vụ ghi và truy vấn Nhật ký kiểm toán bất biến (Append-Only Audit Trail - UC A-02, BR-ADM-05).
/// </summary>
public interface IAuditLogService
{
    Task LogAsync(
        string action,
        string entityType,
        string? entityId,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null,
        Guid? explicitActorId = null,
        CancellationToken ct = default);

    Task<PagedResult<AuditLogItemDto>> GetAuditLogsAsync(
        AuditLogFilterRequest filter,
        CancellationToken ct = default);
}

/// <summary>
/// Dịch vụ kiểm duyệt tài khoản Citizen &amp; Recycler (UC S-06, S-07).
/// </summary>
public interface IAccountGovernanceService
{
    Task<PagedResult<ManagedAccountSummaryDto>> GetAccountsAsync(
        AccountGovernanceFilterRequest filter,
        CancellationToken ct = default);

    Task<ManagedAccountDetailDto> GetAccountDetailAsync(
        Guid targetUserId,
        CancellationToken ct = default);

    Task<ManagedAccountSummaryDto> LockOrUnlockAccountAsync(
        Guid actorUserId,
        Guid targetUserId,
        LockOrUnlockAccountRequest request,
        CancellationToken ct = default);

    Task<ManagedAccountSummaryDto> DeactivateAccountAsync(
        Guid actorUserId,
        Guid targetUserId,
        DeactivateAccountRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Dịch vụ quản lý nhân sự vận hành &amp; ma trận quyền RBAC (UC A-03, A-04).
/// </summary>
public interface IStaffManagementService
{
    Task<IReadOnlyList<StaffAccountDto>> GetStaffAccountsAsync(
        StaffAccountFilterRequest filter,
        CancellationToken ct = default);

    Task<StaffAccountDto> CreateStaffAccountAsync(
        Guid adminUserId,
        CreateStaffAccountRequest request,
        CancellationToken ct = default);

    Task<StaffAccountDto> UpdateStaffAccountAsync(
        Guid adminUserId,
        Guid staffId,
        UpdateStaffAccountRequest request,
        CancellationToken ct = default);

    Task<StaffAccountDto> UpdateStaffStatusAsync(
        Guid adminUserId,
        Guid staffId,
        UpdateStaffStatusRequest request,
        CancellationToken ct = default);

    IReadOnlyList<PermissionCatalogItemDto> GetPermissionCatalog();

    Task<StaffPermissionsDetailDto> GetStaffPermissionsAsync(
        Guid staffId,
        CancellationToken ct = default);

    Task<StaffAccountDto> UpdateStaffPermissionsAsync(
        Guid adminUserId,
        Guid staffId,
        UpdateStaffPermissionsRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Dịch vụ chấm công ca trực hàng ngày của Nhân viên vận hành (UC S-11 - BR-GAM-09).
/// </summary>
public interface IStaffAttendanceService
{
    Task<StaffAttendanceStatusDto> CheckInAsync(
        Guid staffUserId,
        StaffCheckInRequest request,
        CancellationToken ct = default);

    Task<StaffAttendanceStatusDto> CheckOutAsync(
        Guid staffUserId,
        StaffCheckOutRequest request,
        CancellationToken ct = default);

    Task<StaffAttendanceStatusDto> GetMyAttendanceAsync(
        Guid staffUserId,
        int? month = null,
        int? year = null,
        CancellationToken ct = default);

    Task<PagedResult<AdminStaffAttendanceItemDto>> GetAllStaffAttendanceAsync(
        AdminStaffAttendanceFilterRequest filter,
        CancellationToken ct = default);
}

/// <summary>
/// Dịch vụ tổng hợp KPI điều hành, biểu đồ phân tích &amp; trích xuất báo cáo ẩn danh PII (UC A-01, A-02).
/// </summary>
public interface IAdminAnalyticsService
{
    Task<ExecutiveKpiSummaryDto> GetExecutiveKpiSummaryAsync(
        ExecutiveKpiQueryRequest query,
        CancellationToken ct = default);

    Task<AnalyticsChartsDto> GetAnalyticsChartsAsync(
        int months = 6,
        CancellationToken ct = default);

    Task<ExportedReportFileResult> ExportSystemReportAsync(
        Guid adminUserId,
        ReportExportQueryRequest query,
        CancellationToken ct = default);
}
