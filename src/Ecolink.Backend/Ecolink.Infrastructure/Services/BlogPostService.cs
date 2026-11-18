using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Hiện thực Blog Content Management (UC S-08 Staff soạn/nộp bài, A-09 Admin duyệt/từ chối, xem blog công khai).
/// Quy tắc: BR-ADM-08 (bài của Staff chỉ ở Draft/Pending_Approval, chỉ công khai sau khi Admin duyệt),
/// BR-ADM-05 (mọi quyết định của Admin ghi nhật ký kiểm toán bất biến), MSG-ADM-05/06, MSG-AUTH-02.
/// Các chuyển trạng thái dùng ExecuteUpdate có điều kiện trạng thái để tránh hai thao tác đua nhau.
/// </summary>
public class BlogPostService : IBlogPostService
{
    private const int MaxTags = 5;
    private const int MaxPageSize = 50;
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly Regex TagRegex = new(@"^[\p{L}\p{N}][\p{L}\p{N} \-]{0,28}[\p{L}\p{N}]$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ILogger<BlogPostService> _logger;

    public BlogPostService(ApplicationDbContext db, ILogger<BlogPostService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private sealed class PostRow
    {
        public BlogPost Post { get; set; } = null!;
        public string? AuthorName { get; set; }
    }

    private IQueryable<PostRow> Rows(IQueryable<BlogPost> posts) =>
        from p in posts.AsNoTracking()
        join u in _db.Users.AsNoTracking() on p.AuthorId equals u.Id into g
        from u in g.DefaultIfEmpty()
        select new PostRow { Post = p, AuthorName = u != null ? u.FullName : null };

    // ============================================================
    // Staff (S-08)
    // ============================================================
    public async Task<BlogPostDto> CreateAsync(Guid authorId, SaveBlogPostRequest request, CancellationToken ct = default)
    {
        var input = Normalize(request);
        if (request.SubmitForApproval) EnsureSubmittable(input);

        var post = new BlogPost
        {
            AuthorId = authorId,
            Title = input.Title,
            Summary = input.Summary,
            Content = input.Content,
            CoverImageUrl = input.CoverImageUrl,
            Tags = JsonSerializer.Serialize(input.Tags, JsonOptions),
            Status = BlogPostStatus.Draft
        };
        var baseSlug = Slugify(input.Title);
        post.Slug = await UniqueSlugAsync(baseSlug, post.Id, ct);

        _db.BlogPosts.Add(post);
        await SaveWithSlugRetryAsync(post, baseSlug, ct);

        if (request.SubmitForApproval)
            await SubmitCoreAsync(authorId, post.Id, ct);

        return await LoadDetailAsync(post.Id, ct);
    }

    public async Task<BlogPostDto> UpdateAsync(Guid authorId, Guid postId, SaveBlogPostRequest request, CancellationToken ct = default)
    {
        var post = await _db.BlogPosts.FirstOrDefaultAsync(p => p.Id == postId && p.AuthorId == authorId, ct)
                   ?? throw new NotFoundException("Không tìm thấy bài viết.");
        EnsureEditable(post.Status);

        var input = Normalize(request);
        if (request.SubmitForApproval) EnsureSubmittable(input);

        var titleChanged = !string.Equals(post.Title, input.Title, StringComparison.Ordinal);
        post.Title = input.Title;
        post.Summary = input.Summary;
        post.Content = input.Content;
        post.CoverImageUrl = input.CoverImageUrl;
        post.Tags = JsonSerializer.Serialize(input.Tags, JsonOptions);
        post.UpdatedAt = DateTime.UtcNow;

        var baseSlug = Slugify(input.Title);
        if (titleChanged) // bài Draft chưa từng công khai nên đổi slug theo tiêu đề mới là an toàn
            post.Slug = await UniqueSlugAsync(baseSlug, post.Id, ct);

        await SaveWithSlugRetryAsync(post, baseSlug, ct);

        if (request.SubmitForApproval)
            await SubmitCoreAsync(authorId, post.Id, ct);

        return await LoadDetailAsync(post.Id, ct);
    }

    public async Task<BlogPostDto> SubmitAsync(Guid authorId, Guid postId, CancellationToken ct = default)
    {
        var post = await _db.BlogPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId && p.AuthorId == authorId, ct)
                   ?? throw new NotFoundException("Không tìm thấy bài viết.");
        EnsureEditable(post.Status);

        EnsureSubmittable(new BlogInput(post.Title, post.Summary, post.Content, post.CoverImageUrl, new List<string>()));
        await SubmitCoreAsync(authorId, postId, ct);
        return await LoadDetailAsync(postId, ct);
    }

    public async Task<BlogPostDto> RecallAsync(Guid authorId, Guid postId, CancellationToken ct = default)
    {
        var post = await _db.BlogPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId && p.AuthorId == authorId, ct)
                   ?? throw new NotFoundException("Không tìm thấy bài viết.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var affected = await _db.BlogPosts
            .Where(p => p.Id == postId && p.AuthorId == authorId && p.Status == BlogPostStatus.PendingApproval)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, BlogPostStatus.Draft)
                .SetProperty(p => p.SubmittedAt, (DateTime?)null)
                .SetProperty(p => p.UpdatedAt, now), ct);
        if (affected == 0)
            throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01", "Bài viết không ở trạng thái chờ duyệt nên không thể thu hồi.");

        await NotifyAdminsAsync("Bài viết đã được thu hồi",
            $"Bài viết '{post.Title}' đã được tác giả thu hồi về bản nháp và không còn chờ duyệt.", postId, ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await LoadDetailAsync(postId, ct);
    }

    public async Task DeleteAsync(Guid authorId, Guid postId, CancellationToken ct = default)
    {
        var post = await _db.BlogPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId && p.AuthorId == authorId, ct)
                   ?? throw new NotFoundException("Không tìm thấy bài viết.");
        EnsureEditable(post.Status);

        var affected = await _db.BlogPosts
            .Where(p => p.Id == postId && p.AuthorId == authorId && p.Status == BlogPostStatus.Draft)
            .ExecuteDeleteAsync(ct);
        if (affected == 0)
            throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01", "Chỉ có thể xóa bài viết ở trạng thái nháp.");
    }

    public async Task<BlogPostDto> GetMineAsync(Guid authorId, Guid postId, CancellationToken ct = default)
    {
        var row = await Rows(_db.BlogPosts.Where(p => p.Id == postId && p.AuthorId == authorId)).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Không tìm thấy bài viết.");
        return ToDetail(row);
    }

    public async Task<BlogPagedResult<BlogPostSummaryDto>> ListMineAsync(Guid authorId, string? status, string? search,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.BlogPosts.Where(p => p.AuthorId == authorId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = ParseStatus(status);
            query = query.Where(p => p.Status == parsed);
        }

        var rows = Rows(query);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = Like(search);
            rows = rows.Where(r => EF.Functions.ILike(r.Post.Title, pattern));
        }

        return await PageAsync(rows.OrderByDescending(r => r.Post.UpdatedAt).ThenBy(r => r.Post.Id), page, pageSize, ToSummary, ct);
    }

    // ============================================================
    // Admin (A-09)
    // ============================================================
    public async Task<BlogPagedResult<BlogPostSummaryDto>> ListQueueAsync(string? search, string? sort, int page, int pageSize,
        CancellationToken ct = default)
    {
        var rows = Rows(_db.BlogPosts.Where(p => p.Status == BlogPostStatus.PendingApproval));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = Like(search);
            rows = rows.Where(r => EF.Functions.ILike(r.Post.Title, pattern)
                                   || (r.AuthorName != null && EF.Functions.ILike(r.AuthorName, pattern)));
        }

        var newestFirst = string.Equals(sort, "newest", StringComparison.OrdinalIgnoreCase);
        var ordered = newestFirst
            ? rows.OrderByDescending(r => r.Post.SubmittedAt).ThenBy(r => r.Post.Id)
            : rows.OrderBy(r => r.Post.SubmittedAt).ThenBy(r => r.Post.Id);

        return await PageAsync(ordered, page, pageSize, ToSummary, ct);
    }

    public async Task<BlogPostDto> GetForReviewAsync(Guid postId, CancellationToken ct = default)
    {
        var row = await Rows(_db.BlogPosts.Where(p => p.Id == postId)).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Không tìm thấy bài viết.");
        return ToDetail(row);
    }

    public async Task<BlogPostDto> ApproveAsync(Guid adminId, Guid postId, CancellationToken ct = default)
    {
        var post = await _db.BlogPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId, ct)
                   ?? throw new NotFoundException("Không tìm thấy bài viết.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var affected = await _db.BlogPosts
            .Where(p => p.Id == postId && p.Status == BlogPostStatus.PendingApproval)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, BlogPostStatus.Approved)
                .SetProperty(p => p.ApprovedByAdminId, (Guid?)adminId)
                .SetProperty(p => p.ApprovedAt, (DateTime?)now)
                .SetProperty(p => p.PublishedAt, (DateTime?)now)
                .SetProperty(p => p.RejectionReason, (string?)null)
                .SetProperty(p => p.UpdatedAt, now), ct);
        if (affected == 0)
            throw StaleDecision();

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = adminId,
            Action = "BLOG_APPROVE",
            EntityType = "BlogPost",
            EntityId = postId.ToString(),
            OldValues = JsonSerializer.Serialize(new { status = "Pending_Approval" }, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { status = "Approved", publishedAt = now }, JsonOptions)
        });
        _db.Notifications.Add(new Notification
        {
            UserId = post.AuthorId,
            Title = "Bài viết đã được duyệt",
            Message = $"Bài viết '{post.Title}' đã được duyệt và xuất bản.",
            NotificationType = "SYSTEM",
            ReferenceType = "BlogPost",
            ReferenceId = postId
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Admin {AdminId} đã duyệt bài viết {PostId}", adminId, postId);
        return await LoadDetailAsync(postId, ct);
    }

    public async Task<BlogPostDto> RejectAsync(Guid adminId, Guid postId, string feedback, CancellationToken ct = default)
    {
        feedback = (feedback ?? string.Empty).Trim();
        if (feedback.Length == 0)
            throw new BusinessRuleException("BR-ADM-08", "MSG-AUTH-02", "Vui lòng nhập nội dung góp ý chỉnh sửa.");

        var post = await _db.BlogPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId, ct)
                   ?? throw new NotFoundException("Không tìm thấy bài viết.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var affected = await _db.BlogPosts
            .Where(p => p.Id == postId && p.Status == BlogPostStatus.PendingApproval)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, BlogPostStatus.Draft)
                .SetProperty(p => p.RejectionReason, feedback)
                .SetProperty(p => p.SubmittedAt, (DateTime?)null)
                .SetProperty(p => p.UpdatedAt, now), ct);
        if (affected == 0)
            throw StaleDecision();

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = adminId,
            Action = "BLOG_REJECT",
            EntityType = "BlogPost",
            EntityId = postId.ToString(),
            OldValues = JsonSerializer.Serialize(new { status = "Pending_Approval" }, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { status = "Draft", rejectionReason = feedback }, JsonOptions)
        });
        _db.Notifications.Add(new Notification
        {
            UserId = post.AuthorId,
            Title = "Bài viết cần chỉnh sửa",
            Message = $"Bài viết '{post.Title}' chưa được duyệt. Góp ý của Admin: {feedback}",
            NotificationType = "SYSTEM",
            ReferenceType = "BlogPost",
            ReferenceId = postId
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Admin {AdminId} đã từ chối bài viết {PostId}", adminId, postId);
        return await LoadDetailAsync(postId, ct);
    }

    // ============================================================
    // Công khai
    // ============================================================
    public async Task<BlogPagedResult<BlogPostSummaryDto>> ListPublishedAsync(string? search, string? tag, int page, int pageSize,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = Rows(_db.BlogPosts.Where(p => p.Status == BlogPostStatus.Approved && p.PublishedAt != null && p.PublishedAt <= now));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = Like(search);
            rows = rows.Where(r => EF.Functions.ILike(r.Post.Title, pattern)
                                   || (r.Post.Summary != null && EF.Functions.ILike(r.Post.Summary, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            // Tags lưu dạng mảng JSON ["a","b"] nên khớp nguyên thẻ bằng cách bao trong dấu nháy kép
            var tagPattern = "%\"" + EscapeLike(tag.Trim()) + "\"%";
            rows = rows.Where(r => EF.Functions.ILike(r.Post.Tags, tagPattern));
        }

        return await PageAsync(rows.OrderByDescending(r => r.Post.PublishedAt).ThenBy(r => r.Post.Id), page, pageSize, ToSummary, ct);
    }

    public async Task<PublicBlogPostDto> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        slug = (slug ?? string.Empty).Trim().ToLowerInvariant();
        var row = await Rows(_db.BlogPosts.Where(p => p.Slug == slug && p.Status == BlogPostStatus.Approved
                                                       && p.PublishedAt != null && p.PublishedAt <= now))
                      .FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Không tìm thấy bài viết.");

        var p = row.Post;
        return new PublicBlogPostDto
        {
            Id = p.Id,
            Title = p.Title,
            Slug = p.Slug,
            Summary = p.Summary,
            Content = p.Content,
            CoverImageUrl = p.CoverImageUrl,
            Tags = ParseTags(p.Tags),
            AuthorName = row.AuthorName ?? "EcoLink",
            PublishedAt = p.PublishedAt!.Value
        };
    }

    // ============================================================
    // Hỗ trợ
    // ============================================================
    private sealed record BlogInput(string Title, string? Summary, string Content, string? CoverImageUrl, List<string> Tags);

    private static BlogInput Normalize(SaveBlogPostRequest r)
    {
        var title = (r.Title ?? string.Empty).Trim();
        if (title.Length is < 10 or > 150)
            throw new BusinessRuleException("BR-ADM-08", "MSG-AUTH-02", "Tiêu đề phải từ 10 đến 150 ký tự.");

        var summary = string.IsNullOrWhiteSpace(r.Summary) ? null : r.Summary.Trim();

        string? cover = null;
        if (!string.IsNullOrWhiteSpace(r.CoverImageUrl))
        {
            cover = r.CoverImageUrl.Trim();
            if (!Uri.TryCreate(cover, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new BusinessRuleException("BR-ADM-08", "MSG-AUTH-02", "URL ảnh bìa không hợp lệ.");
            var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (ext.Length > 0 && !AllowedImageExtensions.Contains(ext))
                throw new BusinessRuleException("BR-ADM-08", "MSG-CLS-01", "Định dạng ảnh bìa không được hỗ trợ (chỉ JPG, PNG, WEBP).");
        }

        var tags = new List<string>();
        foreach (var raw in r.Tags ?? new List<string>())
        {
            var t = (raw ?? string.Empty).Trim();
            if (!TagRegex.IsMatch(t))
                throw new BusinessRuleException("BR-ADM-08", "MSG-AUTH-02",
                    "Thẻ phân loại phải từ 2 đến 30 ký tự, chỉ gồm chữ, số, khoảng trắng và gạch ngang.");
            if (!tags.Contains(t, StringComparer.OrdinalIgnoreCase)) tags.Add(t);
        }
        if (tags.Count > MaxTags)
            throw new BusinessRuleException("BR-ADM-08", "MSG-AUTH-02", $"Tối đa {MaxTags} thẻ phân loại.");

        return new BlogInput(title, summary, r.Content?.Trim() ?? string.Empty, cover, tags);
    }

    /// <summary>Nộp duyệt cần có tiêu đề, ảnh bìa và nội dung (EF-01 của S-08).</summary>
    private static void EnsureSubmittable(BlogInput input)
    {
        if (string.IsNullOrWhiteSpace(input.CoverImageUrl))
            throw new BusinessRuleException("BR-ADM-08", "MSG-AUTH-02", "Vui lòng tải lên ảnh bìa trước khi nộp duyệt.");
        if (string.IsNullOrWhiteSpace(input.Content))
            throw new BusinessRuleException("BR-ADM-08", "MSG-AUTH-02", "Vui lòng nhập nội dung bài viết trước khi nộp duyệt.");
    }

    private static void EnsureEditable(BlogPostStatus status)
    {
        if (status == BlogPostStatus.PendingApproval)
            throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01",
                "Bài viết đang chờ duyệt. Hãy thu hồi về bản nháp trước khi chỉnh sửa.");
        if (status == BlogPostStatus.Approved)
            throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01", "Bài viết đã được xuất bản nên không thể chỉnh sửa.");
    }

    private static BusinessRuleException StaleDecision() =>
        new("BR-ADM-08", "MSG-GEN-01",
            "Bài viết không còn ở trạng thái chờ duyệt (có thể đã được thu hồi hoặc xử lý). Vui lòng tải lại hàng đợi.");

    private async Task SubmitCoreAsync(Guid authorId, Guid postId, CancellationToken ct)
    {
        var post = await _db.BlogPosts.AsNoTracking().FirstAsync(p => p.Id == postId, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var affected = await _db.BlogPosts
            .Where(p => p.Id == postId && p.AuthorId == authorId && p.Status == BlogPostStatus.Draft)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, BlogPostStatus.PendingApproval)
                .SetProperty(p => p.SubmittedAt, (DateTime?)now)
                .SetProperty(p => p.RejectionReason, (string?)null)
                .SetProperty(p => p.UpdatedAt, now), ct);
        if (affected == 0)
            throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01", "Bài viết không còn ở trạng thái nháp nên không thể nộp duyệt.");

        var authorName = await _db.Users.AsNoTracking().Where(u => u.Id == authorId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        await NotifyAdminsAsync("Có bài viết chờ duyệt",
            $"{authorName ?? "Một nhân viên"} vừa nộp bài viết '{post.Title}' để chờ duyệt.", postId, ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task NotifyAdminsAsync(string title, string message, Guid postId, CancellationToken ct)
    {
        var adminIds = await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Admin && u.Status == UserStatus.Active)
            .Select(u => u.Id).ToListAsync(ct);
        foreach (var id in adminIds)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = id,
                Title = title,
                Message = message,
                NotificationType = "SYSTEM",
                ReferenceType = "BlogPost",
                ReferenceId = postId
            });
        }
    }

    private async Task<BlogPostDto> LoadDetailAsync(Guid postId, CancellationToken ct)
    {
        var row = await Rows(_db.BlogPosts.Where(p => p.Id == postId)).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Không tìm thấy bài viết.");
        return ToDetail(row);
    }

    private async Task<string> UniqueSlugAsync(string baseSlug, Guid excludeId, CancellationToken ct)
    {
        var slug = baseSlug;
        for (var i = 2; i <= 50; i++)
        {
            if (!await _db.BlogPosts.AnyAsync(p => p.Slug == slug && p.Id != excludeId, ct)) return slug;
            slug = $"{baseSlug}-{i}";
        }
        return $"{baseSlug}-{Guid.NewGuid():N}"[..(baseSlug.Length + 7)];
    }

    /// <summary>Lưu bài; nếu hai người cùng tạo một slug (đụng unique index) thì thêm hậu tố ngẫu nhiên rồi thử lại.</summary>
    private async Task SaveWithSlugRetryAsync(BlogPost post, string baseSlug, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (attempt < 3 && IsSlugConflict(ex))
            {
                post.Slug = $"{baseSlug}-{Guid.NewGuid():N}"[..(baseSlug.Length + 7)];
            }
        }
    }

    private static bool IsSlugConflict(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg && pg.ConstraintName == "ux_blog_posts_slug";

    internal static string Slugify(string title)
    {
        var lower = title.Trim().ToLowerInvariant().Replace('đ', 'd');
        var sb = new StringBuilder();
        var lastDash = true;
        foreach (var ch in lower.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(ch);
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }
        var s = sb.ToString().Trim('-');
        if (s.Length > 200) s = s[..200].Trim('-');
        return s.Length == 0 ? "bai-viet" : s;
    }

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string Like(string s) => "%" + EscapeLike(s.Trim()) + "%";

    private static BlogPostStatus ParseStatus(string status)
    {
        var key = status.Trim().Replace("_", string.Empty);
        if (Enum.TryParse<BlogPostStatus>(key, true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01", "Trạng thái lọc không hợp lệ.");
    }

    private static string StatusText(BlogPostStatus s) => s == BlogPostStatus.PendingApproval ? "Pending_Approval" : s.ToString();

    private static List<string> ParseTags(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>(); }
        catch (JsonException) { return new List<string>(); }
    }

    private static async Task<BlogPagedResult<T>> PageAsync<T>(IQueryable<PostRow> ordered, int page, int pageSize,
        Func<PostRow, T> map, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var total = await ordered.CountAsync(ct);
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new BlogPagedResult<T> { Items = rows.Select(map).ToList(), Page = page, PageSize = pageSize, TotalCount = total };
    }

    private static BlogPostSummaryDto ToSummary(PostRow r)
    {
        var dto = new BlogPostSummaryDto();
        FillSummary(dto, r);
        return dto;
    }

    private static void FillSummary(BlogPostSummaryDto dto, PostRow r)
    {
        var p = r.Post;
        dto.Id = p.Id;
        dto.Title = p.Title;
        dto.Slug = p.Slug;
        dto.Summary = p.Summary;
        dto.CoverImageUrl = p.CoverImageUrl;
        dto.Tags = ParseTags(p.Tags);
        dto.Status = StatusText(p.Status);
        dto.Author = new BlogAuthorDto { Id = p.AuthorId, FullName = r.AuthorName ?? string.Empty };
        dto.SubmittedAt = p.SubmittedAt;
        dto.PublishedAt = p.PublishedAt;
        dto.CreatedAt = p.CreatedAt;
        dto.UpdatedAt = p.UpdatedAt;
    }

    private static BlogPostDto ToDetail(PostRow r)
    {
        var dto = new BlogPostDto();
        FillSummary(dto, r);
        var p = r.Post;
        dto.Content = p.Content;
        dto.RejectionReason = p.RejectionReason;
        dto.ApprovedAt = p.ApprovedAt;
        dto.CanEdit = p.Status == BlogPostStatus.Draft;
        dto.CanSubmit = p.Status == BlogPostStatus.Draft;
        dto.CanRecall = p.Status == BlogPostStatus.PendingApproval;
        dto.CanDelete = p.Status == BlogPostStatus.Draft;
        return dto;
    }
}
