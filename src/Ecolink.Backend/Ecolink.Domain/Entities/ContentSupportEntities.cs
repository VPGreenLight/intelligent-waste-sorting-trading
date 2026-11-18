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
