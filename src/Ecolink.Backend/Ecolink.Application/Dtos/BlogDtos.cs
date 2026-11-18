using System.ComponentModel.DataAnnotations;

namespace Ecolink.Application.Dtos;

// ============================================================
// Request
// ============================================================
/// <summary>Soạn / sửa bài viết (UC S-08). Tiêu đề 10–150 ký tự (SCR-STF-06).</summary>
public class SaveBlogPostRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề bài viết.")]
    [StringLength(150, MinimumLength = 10, ErrorMessage = "Tiêu đề phải từ 10 đến 150 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Tóm tắt tối đa 500 ký tự.")]
    public string? Summary { get; set; }

    /// <summary>Nội dung Markdown/HTML. Có thể để trống khi lưu nháp, bắt buộc khi nộp duyệt.</summary>
    [MaxLength(200000, ErrorMessage = "Nội dung quá dài.")]
    public string? Content { get; set; }

    /// <summary>URL ảnh bìa (đã upload lên Cloudflare R2). Bắt buộc khi nộp duyệt.</summary>
    [MaxLength(1000, ErrorMessage = "URL ảnh bìa quá dài.")]
    public string? CoverImageUrl { get; set; }

    /// <summary>Thẻ phân loại, tối đa 5 thẻ, mỗi thẻ 2–30 ký tự (chữ, số, khoảng trắng, gạch ngang).</summary>
    public List<string>? Tags { get; set; }

    /// <summary>true = nộp duyệt ngay (Pending_Approval); false = lưu nháp.</summary>
    public bool SubmitForApproval { get; set; }
}

public class RejectBlogPostRequest
{
    // Không dùng [Required]: để service kiểm tra và trả đúng MSG-AUTH-02 (EF-01 của A-09) thay vì mã lỗi chung.
    [StringLength(2000, ErrorMessage = "Góp ý tối đa 2000 ký tự.")]
    public string Feedback { get; set; } = string.Empty;
}

// ============================================================
// Response
// ============================================================
public class BlogAuthorDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
}

/// <summary>Dòng trong danh sách (không kèm nội dung).</summary>
public class BlogPostSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? CoverImageUrl { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public BlogAuthorDto Author { get; set; } = new();
    public DateTime? SubmittedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Chi tiết đầy đủ (Staff/Admin).</summary>
public class BlogPostDto : BlogPostSummaryDto
{
    public string Content { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanRecall { get; set; }
    public bool CanDelete { get; set; }
}

/// <summary>Bài viết công khai (Guest / User xem).</summary>
public class PublicBlogPostDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public List<string> Tags { get; set; } = new();
    public string AuthorName { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
}

public class BlogPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}