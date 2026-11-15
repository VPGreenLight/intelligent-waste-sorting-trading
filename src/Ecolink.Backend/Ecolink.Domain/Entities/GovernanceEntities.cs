using Ecolink.Domain.Common;

namespace Ecolink.Domain.Entities;

/// <summary>
/// Nhật ký kiểm toán hệ thống bất biến (Append-Only Audit Trail - UC A-02, BR-ADM-05).
/// Không kế thừa BaseAuditableEntity để cấm Soft-Delete và Update.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
    public User? ActorUser { get; set; }
    public string? ActorEmail { get; set; }
    public string? ActorRole { get; set; }

    /// <summary>Mã hành động chuẩn: LOCK_ACCOUNT, UNLOCK_ACCOUNT, DEACTIVATE_ACCOUNT, CREATE_STAFF, UPDATE_STAFF_PERMISSIONS, EXPORT_REPORT...</summary>
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }

    /// <summary>Chuỗi JSON ánh xạ vào cột jsonb của PostgreSQL.</summary>
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Reason { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lịch sử xử lý vi phạm tài khoản Citizen / Recycler / Staff (UC S-06, S-07, A-03 - BR-ADM-03, BR-ADM-04).
/// </summary>
public class AccountModerationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TargetUserId { get; set; }
    public User TargetUser { get; set; } = null!;

    public Guid PerformedBy { get; set; }
    public User PerformedByUser { get; set; } = null!;

    public string ActionType { get; set; } = string.Empty; // LOCK, UNLOCK, DEACTIVATE, WARN
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid? RelatedComplaintId { get; set; }
    public int RevokedTokensCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Bảng chấm công ca trực hàng ngày của Nhân viên vận hành (UC S-11 - BR-GAM-09).
/// Hoàn toàn độc lập với điểm thưởng Green Sprout của Người dân (BR-GAM-08).
/// </summary>
public class StaffAttendance : BaseAuditableEntity
{
    public Guid StaffId { get; set; }
    public StaffProfile StaffProfile { get; set; } = null!;

    public DateOnly WorkDate { get; set; }
    public DateTime CheckInAt { get; set; } = DateTime.UtcNow;
    public DateTime? CheckOutAt { get; set; }
    public int? WorkedMinutes { get; set; }

    public int StreakDay { get; set; } = 1;
    public int WorkPointsEarned { get; set; } = 10;
    public int TasksCompletedSnapshot { get; set; } = 0;
    public string? ShiftNote { get; set; }
}
