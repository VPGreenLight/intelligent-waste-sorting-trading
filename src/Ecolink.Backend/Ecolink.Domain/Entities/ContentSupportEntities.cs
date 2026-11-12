namespace Ecolink.Domain.Entities;

/// <summary>
/// Thông báo trong ứng dụng (báo cáo mục 3.7.4). Khóa chính / reference_id dùng Guid theo quy ước dự án.
/// NẾU NHÓM ĐÃ CÓ entity Notification: xóa file này, cấu hình tương ứng trong BlogPostConfiguration.cs và
/// DbSet, rồi báo lại để mình đổi service sang entity của nhóm.
/// </summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string NotificationType { get; set; } = "SYSTEM";      // TRANSACTION, AI_SCAN, GAMIFICATION, SYSTEM, COMPLAINT
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Nhật ký kiểm toán bất biến (báo cáo mục 3.7.5, BR-ADM-05). Chỉ thêm, không sửa/xóa.
/// Không có ip_address / user_agent (service tầng Infrastructure không truy cập HttpContext).
/// NẾU NHÓM ĐÃ CÓ entity AuditLog: làm tương tự ghi chú ở Notification.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;           // vd BLOG_APPROVE, BLOG_REJECT
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }                       // JSONB
    public string? NewValues { get; set; }                       // JSONB
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
