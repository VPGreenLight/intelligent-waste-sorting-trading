using Ecolink.Domain.Enums;

namespace Ecolink.Domain.Entities;

/// <summary>
/// Bài viết blog/tin tức do Staff soạn, Admin duyệt (UC S-08, A-09). Ánh xạ bảng BlogPost (báo cáo mục 3.6.2).
/// Khác báo cáo (tối thiểu): thêm <see cref="Tags"/> (màn hình SCR-STF-06 có chọn thẻ phân loại) và
/// <see cref="SubmittedAt"/> (hàng đợi duyệt hiển thị ngày nộp). Khóa chính là Guid theo quy ước chung của dự án.
/// </summary>
public class BlogPost
{
    public Guid Id { get; set; } = Guid.NewGuid();                 // post_id
    public Guid AuthorId { get; set; }                            // author_staff_id = user_id của Staff soạn bài

    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;           // Markdown / HTML
    public string? CoverImageUrl { get; set; }

    /// <summary>Mảng JSON các thẻ phân loại (tối đa 5), ví dụ ["tai-che","nhua"]. Cột thêm ngoài báo cáo.</summary>
    public string Tags { get; set; } = "[]";

    public BlogPostStatus Status { get; set; } = BlogPostStatus.Draft;

    public Guid? ApprovedByAdminId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? SubmittedAt { get; set; }                    // cột thêm ngoài báo cáo
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
