namespace Ecolink.Application.Dtos;

// ============================================================================
// 1. EXECUTIVE DASHBOARD, ANALYTICS, EXPORT & AUDIT LOG DTOs (A-01, A-02)
// ============================================================================

public class ExecutiveKpiQueryRequest
{
    public string Period { get; set; } = "30d"; // 7d | 30d | 90d | all
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public class ExecutiveKpiSummaryDto
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int ActiveCitizensCount { get; set; }
    public int VerifiedRecyclersCount { get; set; }
    public int CompletedHandoversCount { get; set; }
    public decimal TotalRecycledWeightKg { get; set; }
    public decimal TotalRecycledTonnage { get; set; }
    public decimal GrossTradeValueVnd { get; set; }
    public decimal VietQrServiceRevenueVnd { get; set; }
    public AiBenchmarkSummaryDto AiBenchmark { get; set; } = new();
    public OperationalBacklogDto OperationalBacklog { get; set; } = new();
}

public class AiBenchmarkSummaryDto
{
    public bool AiServiceOnline { get; set; }
    public int TotalScans { get; set; }
    public decimal OverallAccuracyPercent { get; set; }
    public decimal OodRatePercent { get; set; }
    public int FalseReportCount { get; set; }
}

public class OperationalBacklogDto
{
    public int PendingAiFeedbackCount { get; set; }
    public int PendingRecyclerKybCount { get; set; }
    public int OpenComplaintsCount { get; set; }
    public int PendingBlogPostsCount { get; set; }
}

public class AnalyticsChartsDto
{
    public IReadOnlyList<MonthlyHandoverTrendItemDto> MonthlyHandoverTrend { get; set; } = Array.Empty<MonthlyHandoverTrendItemDto>();
    public IReadOnlyList<WasteCategoryBreakdownItemDto> WasteCategoryBreakdown { get; set; } = Array.Empty<WasteCategoryBreakdownItemDto>();
    public IReadOnlyList<AiQualityGroupItemDto> AiQualityByGroup { get; set; } = Array.Empty<AiQualityGroupItemDto>();
}

public class MonthlyHandoverTrendItemDto
{
    public string Month { get; set; } = string.Empty; // yyyy-MM
    public string Label { get; set; } = string.Empty; // T6, T7...
    public int CompletedHandovers { get; set; }
    public decimal RecycledWeightKg { get; set; }
    public decimal GrossValueVnd { get; set; }
}

public class WasteCategoryBreakdownItemDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal SharePercent { get; set; }
}

public class AiQualityGroupItemDto
{
    public string GroupName { get; set; } = string.Empty;
    public int Scans { get; set; }
    public int Correct { get; set; }
    public int FalseReports { get; set; }
    public decimal AccuracyPercent { get; set; }
}

public class ReportExportQueryRequest
{
    /// <summary>accounts | handovers | vietqr_revenues | ai_feedback | audit_logs</summary>
    public string Dataset { get; set; } = "accounts";
    /// <summary>csv | xlsx</summary>
    public string Format { get; set; } = "csv";
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public class ExportedReportFileResult
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "text/csv; charset=utf-8";
    public string FileName { get; set; } = string.Empty;
    public int RowCount { get; set; }
}

public class AuditLogFilterRequest
{
    public string? Keyword { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AuditLogItemDto
{
    public Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }
    public string? ActorRole { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================================================
// 2. USER & RECYCLER ACCOUNT MODERATION DTOs (S-06, S-07)
// ============================================================================

public class AccountGovernanceFilterRequest
{
    public string? Keyword { get; set; }
    public string? Role { get; set; } // User | Recycler
    public string? Status { get; set; } // Active | Suspended | Locked | Deactivated
    public int? MinWarnings { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ManagedAccountSummaryDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? VerificationStatus { get; set; }
    public int CompletedDealsCount { get; set; }
    public int WarningCount { get; set; }
    public string? ModerationReason { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ManagedAccountActivityStatsDto
{
    public int TotalHandoverRequests { get; set; }
    public int CompletedDealsCount { get; set; }
    public int CancelledLateCount { get; set; }
    public decimal TotalRecycledKg { get; set; }
    public int GreenPoints { get; set; }
    public int TreeLevel { get; set; }
}

public class AccountModerationLogDto
{
    public Guid Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid? RelatedComplaintId { get; set; }
    public int RevokedTokensCount { get; set; }
    public Guid PerformedBy { get; set; }
    public string PerformedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ManagedAccountDetailDto : ManagedAccountSummaryDto
{
    public ManagedAccountActivityStatsDto ActivityStats { get; set; } = new();
    public IReadOnlyList<AccountModerationLogDto> ModerationHistory { get; set; } = Array.Empty<AccountModerationLogDto>();
}

public class LockOrUnlockAccountRequest
{
    public bool IsLocked { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IncrementWarning { get; set; } = true;
    public Guid? RelatedComplaintId { get; set; }
}

public class DeactivateAccountRequest
{
    public string Reason { get; set; } = string.Empty;
}

// ============================================================================
// 3. STAFF MANAGEMENT & RBAC PERMISSIONS DTOs (A-03, A-04)
// ============================================================================

public class StaffAccountFilterRequest
{
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? Department { get; set; }
    public string? PermissionCode { get; set; }
}

public class StaffAccountDto
{
    public Guid StaffId { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string UserStatus { get; set; } = string.Empty;
    public string StaffStatus { get; set; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
}

public class CreateStaffAccountRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? PhoneNumber { get; set; }
    public string? InitialPassword { get; set; }
    public string? StaffCode { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public List<string>? InitialPermissions { get; set; }
}

public class UpdateStaffAccountRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? ResetTemporaryPassword { get; set; }
}

public class UpdateStaffStatusRequest
{
    /// <summary>Active | Suspended | Deactivated</summary>
    public string Status { get; set; } = "Suspended";
    public string Reason { get; set; } = string.Empty;
}

public class PermissionCatalogItemDto
{
    public string Code { get; set; } = string.Empty;
    public string LegacyAlias { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class StaffPermissionAssignmentItemDto
{
    public string PermissionCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public Guid GrantedBy { get; set; }
    public DateTime GrantedAt { get; set; }
}

public class StaffPermissionsDetailDto
{
    public Guid StaffId { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<StaffPermissionAssignmentItemDto> AssignedPermissions { get; set; } = Array.Empty<StaffPermissionAssignmentItemDto>();
}

public class UpdateStaffPermissionsRequest
{
    public List<string> Permissions { get; set; } = new();
    public string? Reason { get; set; }
}

// ============================================================================
// 4. STAFF DAILY CHECK-IN & ATTENDANCE DTOs (S-11)
// ============================================================================

public class StaffCheckInRequest
{
    public string? ShiftNote { get; set; }
}

public class StaffCheckOutRequest
{
    public string? ShiftNote { get; set; }
}

public class StaffAttendanceLogDto
{
    public Guid AttendanceId { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTime CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public int? WorkedMinutes { get; set; }
    public int StreakDay { get; set; }
    public int WorkPointsEarned { get; set; }
    public int TasksCompletedSnapshot { get; set; }
    public string? ShiftNote { get; set; }
}

public class StaffAttendanceStatusDto
{
    public Guid? AttendanceId { get; set; }
    public DateOnly WorkDate { get; set; }
    public bool CheckedInToday { get; set; }
    public bool CheckedOutToday { get; set; }
    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public int? WorkedMinutesToday { get; set; }
    public int CurrentStreakDay { get; set; }
    public int PointsEarnedToday { get; set; }
    public int MonthlyWorkPoints { get; set; }
    public int MonthlyTasksCompleted { get; set; }
    public string? TodayShiftNote { get; set; }
    public IReadOnlyList<StaffAttendanceLogDto> RecentLogs { get; set; } = Array.Empty<StaffAttendanceLogDto>();
}

public class AdminStaffAttendanceFilterRequest
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public Guid? StaffId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AdminStaffAttendanceItemDto
{
    public Guid AttendanceId { get; set; }
    public Guid StaffId { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTime CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public int? WorkedMinutes { get; set; }
    public int StreakDay { get; set; }
    public int WorkPointsEarned { get; set; }
    public int TasksCompletedSnapshot { get; set; }
    public string? ShiftNote { get; set; }
}
