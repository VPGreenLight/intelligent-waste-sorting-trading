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
    private static readonly Guid DemoAdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DemoStaffId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly SemaphoreSlim DefaultSeedLock = new(1, 1);
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly Regex TagRegex = new(@"^[\p{L}\p{N}][\p{L}\p{N} \-]{0,28}[\p{L}\p{N}]$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ILogger<BlogPostService> _logger;
    private readonly IAuditLogService? _auditLog;
    private readonly INotificationRealtimeNotifier? _realtimeNotifier;
    private bool _defaultSeedChecked;

    public BlogPostService(
        ApplicationDbContext db,
        ILogger<BlogPostService> logger,
        IAuditLogService? auditLog = null,
        INotificationRealtimeNotifier? realtimeNotifier = null)
    {
        _db = db;
        _logger = logger;
        _auditLog = auditLog;
        _realtimeNotifier = realtimeNotifier;
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
        if (_db.Database.IsRelational())
        {
            var affected = await _db.BlogPosts
                .Where(p => p.Id == postId && p.AuthorId == authorId && p.Status == BlogPostStatus.PendingApproval)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, BlogPostStatus.Draft)
                    .SetProperty(p => p.SubmittedAt, (DateTime?)null)
                    .SetProperty(p => p.UpdatedAt, now), ct);
            if (affected == 0)
                throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01", "Bài viết không ở trạng thái chờ duyệt nên không thể thu hồi.");
        }
        else
        {
            var tracked = await _db.BlogPosts
                .FirstOrDefaultAsync(p => p.Id == postId && p.AuthorId == authorId && p.Status == BlogPostStatus.PendingApproval, ct)
                ?? throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01", "Bài viết không ở trạng thái chờ duyệt nên không thể thu hồi.");
            tracked.Status = BlogPostStatus.Draft;
            tracked.SubmittedAt = null;
            tracked.UpdatedAt = now;
        }

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

        if (_db.Database.IsRelational())
        {
            var affected = await _db.BlogPosts
                .Where(p => p.Id == postId && p.AuthorId == authorId && p.Status == BlogPostStatus.Draft)
                .ExecuteDeleteAsync(ct);
            if (affected == 0)
                throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01", "Chỉ có thể xóa bài viết ở trạng thái nháp.");
            return;
        }

        var tracked = await _db.BlogPosts
            .FirstOrDefaultAsync(p => p.Id == postId && p.AuthorId == authorId && p.Status == BlogPostStatus.Draft, ct)
            ?? throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01", "Chỉ có thể xóa bài viết ở trạng thái nháp.");
        _db.BlogPosts.Remove(tracked);
        await _db.SaveChangesAsync(ct);
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
        await EnsureDefaultBlogPostsSeededAsync(ct);

        var query = _db.BlogPosts.Where(p => p.AuthorId == authorId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = ParseStatus(status);
            query = query.Where(p => p.Status == parsed);
        }

        var rows = Rows(query);
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (_db.Database.IsRelational())
            {
                var pattern = Like(search);
                rows = rows.Where(r => EF.Functions.ILike(r.Post.Title, pattern));
            }
            else
            {
                var kw = search.Trim();
                rows = rows.Where(r => r.Post.Title.Contains(kw, StringComparison.OrdinalIgnoreCase));
            }
        }

        return await PageAsync(rows.OrderByDescending(r => r.Post.UpdatedAt).ThenBy(r => r.Post.Id), page, pageSize, ToSummary, ct);
    }

    // ============================================================
    // Admin (A-09)
    // ============================================================
    public async Task<BlogPagedResult<BlogPostSummaryDto>> ListQueueAsync(string? search, string? sort, int page, int pageSize,
        CancellationToken ct = default)
    {
        await EnsureDefaultBlogPostsSeededAsync(ct);

        var rows = Rows(_db.BlogPosts.Where(p => p.Status == BlogPostStatus.PendingApproval));
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (_db.Database.IsRelational())
            {
                var pattern = Like(search);
                rows = rows.Where(r => EF.Functions.ILike(r.Post.Title, pattern)
                                       || (r.AuthorName != null && EF.Functions.ILike(r.AuthorName, pattern)));
            }
            else
            {
                var kw = search.Trim();
                rows = rows.Where(r => r.Post.Title.Contains(kw, StringComparison.OrdinalIgnoreCase)
                                       || (r.AuthorName != null && r.AuthorName.Contains(kw, StringComparison.OrdinalIgnoreCase)));
            }
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
        if (_db.Database.IsRelational())
        {
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
        }
        else
        {
            var tracked = await _db.BlogPosts
                .FirstOrDefaultAsync(p => p.Id == postId && p.Status == BlogPostStatus.PendingApproval, ct)
                ?? throw StaleDecision();
            tracked.Status = BlogPostStatus.Approved;
            tracked.ApprovedByAdminId = adminId;
            tracked.ApprovedAt = now;
            tracked.PublishedAt = now;
            tracked.RejectionReason = null;
            tracked.UpdatedAt = now;
        }

        var notif = new Notification
        {
            UserId = post.AuthorId,
            Title = "Bài viết đã được duyệt",
            Message = $"Bài viết '{post.Title}' đã được duyệt và xuất bản.",
            NotificationType = "SYSTEM",
            ReferenceType = "BlogPost",
            ReferenceId = postId
        };
        _db.Notifications.Add(notif);
        if (_auditLog != null)
        {
            await _auditLog.LogAsync(
                action: "BLOG_APPROVE",
                entityType: "BlogPost",
                entityId: postId.ToString(),
                oldValues: new { status = "Pending_Approval" },
                newValues: new { status = "Approved", publishedAt = now },
                explicitActorId: adminId,
                ct: ct);
        }
        else
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = adminId,
                Action = "BLOG_APPROVE",
                EntityType = "BlogPost",
                EntityId = postId.ToString(),
                OldValues = JsonSerializer.Serialize(new { status = "Pending_Approval" }, JsonOptions),
                NewValues = JsonSerializer.Serialize(new { status = "Approved", publishedAt = now }, JsonOptions)
            });
            await _db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);

        await TryPushRealtimeNotificationAsync(notif, ct);

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
        if (_db.Database.IsRelational())
        {
            var affected = await _db.BlogPosts
                .Where(p => p.Id == postId && p.Status == BlogPostStatus.PendingApproval)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, BlogPostStatus.Draft)
                    .SetProperty(p => p.RejectionReason, feedback)
                    .SetProperty(p => p.SubmittedAt, (DateTime?)null)
                    .SetProperty(p => p.UpdatedAt, now), ct);
            if (affected == 0)
                throw StaleDecision();
        }
        else
        {
            var tracked = await _db.BlogPosts
                .FirstOrDefaultAsync(p => p.Id == postId && p.Status == BlogPostStatus.PendingApproval, ct)
                ?? throw StaleDecision();
            tracked.Status = BlogPostStatus.Draft;
            tracked.RejectionReason = feedback;
            tracked.SubmittedAt = null;
            tracked.UpdatedAt = now;
        }

        var rejectNotif = new Notification
        {
            UserId = post.AuthorId,
            Title = "Bài viết cần chỉnh sửa",
            Message = $"Bài viết '{post.Title}' chưa được duyệt. Góp ý của Admin: {feedback}",
            NotificationType = "SYSTEM",
            ReferenceType = "BlogPost",
            ReferenceId = postId
        };
        _db.Notifications.Add(rejectNotif);
        if (_auditLog != null)
        {
            await _auditLog.LogAsync(
                action: "BLOG_REJECT",
                entityType: "BlogPost",
                entityId: postId.ToString(),
                oldValues: new { status = "Pending_Approval" },
                newValues: new { status = "Draft", rejectionReason = feedback },
                reason: feedback,
                explicitActorId: adminId,
                ct: ct);
        }
        else
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = adminId,
                Action = "BLOG_REJECT",
                EntityType = "BlogPost",
                EntityId = postId.ToString(),
                OldValues = JsonSerializer.Serialize(new { status = "Pending_Approval" }, JsonOptions),
                NewValues = JsonSerializer.Serialize(new { status = "Draft", rejectionReason = feedback }, JsonOptions)
            });
            await _db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);

        await TryPushRealtimeNotificationAsync(rejectNotif, ct);

        _logger.LogInformation("Admin {AdminId} đã từ chối bài viết {PostId}", adminId, postId);
        return await LoadDetailAsync(postId, ct);
    }

    // ============================================================
    // Công khai
    // ============================================================
    public async Task<BlogPagedResult<BlogPostSummaryDto>> ListPublishedAsync(string? search, string? tag, int page, int pageSize,
        CancellationToken ct = default)
    {
        await EnsureDefaultBlogPostsSeededAsync(ct);

        var now = DateTime.UtcNow;
        var rows = Rows(_db.BlogPosts.Where(p => p.Status == BlogPostStatus.Approved && p.PublishedAt != null && p.PublishedAt <= now));

        if (!string.IsNullOrWhiteSpace(search))
        {
            if (_db.Database.IsRelational())
            {
                var pattern = Like(search);
                rows = rows.Where(r => EF.Functions.ILike(r.Post.Title, pattern)
                                       || (r.Post.Summary != null && EF.Functions.ILike(r.Post.Summary, pattern)));
            }
            else
            {
                var kw = search.Trim();
                rows = rows.Where(r => r.Post.Title.Contains(kw, StringComparison.OrdinalIgnoreCase)
                                       || (r.Post.Summary != null && r.Post.Summary.Contains(kw, StringComparison.OrdinalIgnoreCase)));
            }
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            if (_db.Database.IsRelational())
            {
                // Tags lưu dạng mảng JSON ["a","b"] nên khớp nguyên thẻ bằng cách bao trong dấu nháy kép
                var tagPattern = "%\"" + EscapeLike(tag.Trim()) + "\"%";
                rows = rows.Where(r => EF.Functions.ILike(r.Post.Tags, tagPattern));
            }
            else
            {
                var quotedTag = "\"" + tag.Trim() + "\"";
                rows = rows.Where(r => r.Post.Tags.Contains(quotedTag, StringComparison.OrdinalIgnoreCase));
            }
        }

        return await PageAsync(rows.OrderByDescending(r => r.Post.PublishedAt).ThenBy(r => r.Post.Id), page, pageSize, ToSummary, ct);
    }

    public async Task<PublicBlogPostDto> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        await EnsureDefaultBlogPostsSeededAsync(ct);

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
            if (cover.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
            {
                var relExt = Path.GetExtension(cover.Split('?')[0]).ToLowerInvariant();
                if (relExt.Length == 0 || !AllowedImageExtensions.Contains(relExt))
                    throw new BusinessRuleException("BR-ADM-08", "MSG-CLS-01", "Định dạng ảnh bìa không được hỗ trợ (chỉ JPG, PNG, WEBP).");
            }
            else
            {
                if (!Uri.TryCreate(cover, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    throw new BusinessRuleException("BR-ADM-08", "MSG-AUTH-02", "URL ảnh bìa không hợp lệ.");
                var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
                if (ext.Length > 0 && !AllowedImageExtensions.Contains(ext))
                    throw new BusinessRuleException("BR-ADM-08", "MSG-CLS-01", "Định dạng ảnh bìa không được hỗ trợ (chỉ JPG, PNG, WEBP).");
            }
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
        if (_db.Database.IsRelational())
        {
            var affected = await _db.BlogPosts
                .Where(p => p.Id == postId && p.AuthorId == authorId && p.Status == BlogPostStatus.Draft)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, BlogPostStatus.PendingApproval)
                    .SetProperty(p => p.SubmittedAt, (DateTime?)now)
                    .SetProperty(p => p.RejectionReason, (string?)null)
                    .SetProperty(p => p.UpdatedAt, now), ct);
            if (affected == 0)
                throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01", "Bài viết không còn ở trạng thái nháp nên không thể nộp duyệt.");
        }
        else
        {
            var tracked = await _db.BlogPosts
                .FirstOrDefaultAsync(p => p.Id == postId && p.AuthorId == authorId && p.Status == BlogPostStatus.Draft, ct)
                ?? throw new BusinessRuleException("BR-ADM-08", "MSG-GEN-01", "Bài viết không còn ở trạng thái nháp nên không thể nộp duyệt.");
            tracked.Status = BlogPostStatus.PendingApproval;
            tracked.SubmittedAt = now;
            tracked.RejectionReason = null;
            tracked.UpdatedAt = now;
        }

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
            var n = new Notification
            {
                UserId = id,
                Title = title,
                Message = message,
                NotificationType = "SYSTEM",
                ReferenceType = "BlogPost",
                ReferenceId = postId
            };
            _db.Notifications.Add(n);
            await TryPushRealtimeNotificationAsync(n, ct);
        }
    }

    private async Task TryPushRealtimeNotificationAsync(Notification n, CancellationToken ct)
    {
        if (_realtimeNotifier == null) return;
        try
        {
            var unreadCount = await _db.Notifications.AsNoTracking()
                .CountAsync(x => x.UserId == n.UserId && !x.IsRead, ct);
            await _realtimeNotifier.PushNotificationAsync(
                n.UserId,
                new NotificationDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    NotificationType = n.NotificationType,
                    ReferenceType = n.ReferenceType,
                    ReferenceId = n.ReferenceId,
                    IsRead = n.IsRead,
                    ReadAt = n.ReadAt,
                    CreatedAt = n.CreatedAt
                },
                unreadCount,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đẩy SignalR notification cho bài viết {PostId}.", n.ReferenceId);
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
        if (string.Equals(key, "Published", StringComparison.OrdinalIgnoreCase)) return BlogPostStatus.Approved;
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

    // ============================================================
    // Khởi tạo dữ liệu mẫu idempotent
    // ============================================================
    private sealed record DefaultBlogSeed(
        Guid Id,
        string Title,
        string Summary,
        string Content,
        string CoverImageUrl,
        string[] Tags,
        BlogPostStatus Status,
        TimeSpan AgeOffset);

    private static readonly DefaultBlogSeed[] DefaultBlogSeeds =
    {
        new(
            Guid.Parse("b1090000-0000-4000-8000-000000000001"),
            "Khủng hoảng rác thải nhựa đại dương & Bước chuyển của chuỗi giá trị",
            "Các chuỗi giá trị Đông Nam Á đang tái định hình hạ tầng thu gom với hạn ngạch EPR khắt khe, kết nối trực tiếp người dân vào mạng lưới tái chế quy chuẩn.",
            """
            <p>Mỗi năm, hơn 350 triệu tấn nhựa được sản xuất trên toàn cầu, nhưng tỷ lệ hoàn lưu vào chu trình tái sinh thực chất mới chỉ đạt khoảng 9%. Phần lớn bao bì giá trị thấp bị thất thoát ra kênh rạch và đại dương do khâu phân loại tại nguồn chưa được chuẩn hóa và chuỗi thu gom truyền thống thiếu minh bạch về giá.</p>
            <h2>Nút thắt nằm ở chất lượng phế liệu đầu vào</h2>
            <p>Khi chai nhựa PET và can HDPE bị trộn lẫn cùng rác hữu cơ hoặc còn sót cặn dầu mỡ, chi phí tẩy rửa tại nhà máy tái chế tăng vọt, kéo giá thu mua tại điểm tập kết xuống thấp. Ngược lại, phế liệu được làm sạch cơ bản và phân loại đúng chuẩn ngay từ hộ gia đình giúp giảm tới 30% hao hụt xử lý.</p>
            <ul>
              <li><strong>Tráng sạch và để ráo:</strong> Loại bỏ tạp chất thực phẩm khỏi chai nước giải khát, hộp nhựa PP trước khi lưu trữ.</li>
              <li><strong>Tháo nắp và gỡ nhãn co:</strong> Tách riêng màng co PVC giúp hạt nhựa tái sinh đạt độ tinh khiết cao.</li>
              <li><strong>Kết nối điểm thu gom xác minh:</strong> Sử dụng bản đồ EcoLink để so sánh đơn giá và đặt lịch thu gom tận nơi.</li>
            </ul>
            <blockquote>Minh bạch hóa từng kilogram phế liệu qua biên nhận điện tử chính là cầu nối giúp hạn ngạch EPR đi vào thực chất và mang lại thu nhập công bằng cho người phân loại.</blockquote>
            """,
            "/assets/insights/36f2d.png",
            new[] { "Kinh tế tuần hoàn", "Nhựa đại dương", "Chính sách EPR" },
            BlogPostStatus.Approved,
            TimeSpan.FromDays(1)),
        new(
            Guid.Parse("b1090000-0000-4000-8000-000000000002"),
            "Mỏ vàng từ bo mạch cũ: Đòn bẩy kinh tế cho ngành tái chế thiết bị viễn thông",
            "Phục hồi kim loại quý từ 1 triệu điện thoại tương đương khai thác 35 tấn quặng mà không gây tổn hại địa chất sinh thái.",
            """
            <p>Rác thải điện tử đang là dòng chất thải tăng trưởng nhanh nhất tại các đô thị lớn. Bên trong mỗi chiếc điện thoại, bộ định tuyến hay bo mạch cũ đều chứa hàm lượng đồng, vàng, bạc và paladi cao hơn nhiều lần so với quặng tự nhiên.</p>
            <h2>Khai thác mỏ đô thị thay thế khai khoáng truyền thống</h2>
            <p>Theo thống kê của UNEP, thu hồi kim loại quý từ 1 triệu thiết bị di động có thể tái chế tương đương 35 tấn quặng nguyên khai, đồng thời cắt giảm đáng kể lượng phát thải khí nhà kính và nước thải axit từ hoạt động luyện kim sơ cấp.</p>
            <ul>
              <li><strong>Không tháo dỡ tự phát:</strong> Bo mạch và tụ điện có thể chứa chì hoặc chất chống cháy brom hóa cần được xử lý tại cơ sở đủ năng lực.</li>
              <li><strong>Tách riêng pin trước khi bàn giao:</strong> Tránh chèn ép gây chập mạch nhiệt trong quá trình vận chuyển.</li>
            </ul>
            <p>EcoLink giúp người dân phân biệt nhanh nhóm thiết bị điện tử và chuyển đúng tới các cơ sở thu mua được cấp phép trong bán kính gần nhất.</p>
            """,
            "/assets/insights/c4ba2.png",
            new[] { "Rác điện tử", "Kim loại quý", "Kinh tế tuần hoàn" },
            BlogPostStatus.Approved,
            TimeSpan.FromDays(3)),
        new(
            Guid.Parse("b1090000-0000-4000-8000-000000000003"),
            "Khung pháp lý Trách nhiệm Mở rộng của Nhà sản xuất: Hạn ngạch bắt đầu hiệu lực",
            "Doanh nghiệp F&B và bao bì tiêu dùng bắt buộc đạt tỷ lệ tái chế thực tế theo quy chuẩn mới nhằm hướng tới Net-Zero.",
            """
            <p>Cơ chế Trách nhiệm Mở rộng của Nhà sản xuất (EPR) theo Luật Bảo vệ Môi trường buộc các doanh nghiệp sản xuất, nhập khẩu bao bì nhựa, nhôm, giấy carton và thiết bị điện tử phải tổ chức tái chế theo tỷ lệ bắt buộc hoặc đóng góp tài chính vào Quỹ Bảo vệ Môi trường.</p>
            <h2>Số hóa dữ liệu thu gom — yêu cầu sống còn để chứng minh tuân thủ</h2>
            <p>Để được công nhận hoàn thành hạn ngạch EPR, mỗi lô phế liệu từ hộ gia đình đến cơ sở thu mua và nhà máy tái chế đều cần chuỗi chứng từ minh bạch về khối lượng cân thực tế, chủng loại vật liệu và thời điểm bàn giao.</p>
            <ul>
              <li><strong>Chuẩn hóa danh mục thu gom:</strong> Đồng bộ mã loại rác từ khâu quét AI đến bảng giá niêm yết của từng vựa.</li>
              <li><strong>Biên nhận điện tử có kiểm chứng:</strong> Ghi nhận ảnh đồng hồ cân, khối lượng và lịch sử giao dịch chống gian lận sản lượng.</li>
            </ul>
            <p>Khi dữ liệu thu gom được số hóa trên EcoLink, cả doanh nghiệp sản xuất lẫn mạng lưới vựa thu mua địa phương đều hưởng lợi từ chuỗi cung ứng nguyên liệu thứ cấp ổn định.</p>
            """,
            "/assets/insights/fc414.png",
            new[] { "Chính sách EPR", "Phân loại tại nguồn", "Kinh tế tuần hoàn" },
            BlogPostStatus.Approved,
            TimeSpan.FromDays(6)),
        new(
            Guid.Parse("b1090000-0000-4000-8000-000000000004"),
            "Tín dụng xanh: Nền tảng cho chuỗi thu gom tuần hoàn tại Đồng bằng sông Cửu Long",
            "Chuyển đổi phụ phẩm nông nghiệp và phế liệu tái chế thành nguồn nguyên liệu tuần hoàn nhờ dòng vốn tín dụng ưu đãi cho cơ sở thu gom địa phương.",
            """
            <p>Tại khu vực Đồng bằng sông Cửu Long, lượng bao bì nông nghiệp, thùng carton xuất khẩu và phụ phẩm sinh khối phát sinh quanh năm nhưng đa phần các điểm thu gom vừa và nhỏ gặp khó khi tiếp cận vốn để nâng cấp mặt bằng và thiết bị ép kiện.</p>
            <h2>Dòng vốn xanh gắn liền với tiêu chuẩn vận hành</h2>
            <p>Các chương trình tín dụng xanh ưu tiên giải ngân cho cơ sở thu mua có hồ sơ pháp lý rõ ràng, minh bạch nhật ký cân đo điện tử và cam kết chuyển giao phế liệu tới đơn vị tái chế đạt chuẩn môi trường.</p>
            <ul>
              <li>Đầu tư máy ép kiện giúp giảm 40% chi phí vận chuyển đường thủy và đường bộ.</li>
              <li>Chuẩn hóa phân loại rác sạch ngay tại trạm trung chuyển, gia tăng giá trị đầu ra cho bao bì giấy và nhựa HDPE.</li>
            </ul>
            <p>Nhờ dữ liệu giao dịch minh bạch trên nền tảng số, các cơ sở thu gom địa phương có đủ cơ sở tín nhiệm để mở rộng quy mô bền vững.</p>
            """,
            "/assets/insights/01ee5.png",
            new[] { "Tín dụng xanh", "Sinh khối", "Kinh tế tuần hoàn" },
            BlogPostStatus.Approved,
            TimeSpan.FromDays(10)),
        new(
            Guid.Parse("b1090000-0000-4000-8000-000000000005"),
            "Vì sao bìa carton nhà bạn thường bị thu gom giá thấp hơn kỳ vọng",
            "Giá thu mua giấy và bìa carton phụ thuộc lớn vào độ khô ráo, tỷ lệ tạp chất băng keo và lớp màng phủ chống thấm chứ không chỉ tính theo khối lượng.",
            "Nhiều hộ gia đình thường thắc mắc vì sao cùng là thùng carton giao nhận hàng nhưng mức giá thu mua giữa các lần bàn giao lại chênh lệch rõ rệt.\n\nNguyên nhân đầu tiên là băng keo nhựa, màng xốp khí và nhãn dán ni-lông còn bám trên bề mặt thùng. Khi đưa vào bể nghiền bột giấy, các tạp chất này làm giảm hiệu suất thu hồi sợi cellulose nên cơ sở thu mua phải trừ hao tỷ lệ tạp chất.\n\nNguyên nhân thứ hai là độ ẩm và vết dầu mỡ. Carton bị dính nước mưa hoặc dầu thức ăn sẽ mất độ bền sợi, rất dễ chuyển sang nhóm rác thông thường.\n\nĐể đạt mức giá niêm yết tốt nhất trên EcoLink, bạn hãy rạch bỏ băng keo, gập phẳng thùng carton khô sạch và trao đổi trực tiếp điều kiện thu mua trong phòng chat trước khi chốt lịch hẹn.",
            "/assets/insights/28077.png",
            new[] { "Bìa carton", "Phân loại tại nguồn" },
            BlogPostStatus.PendingApproval,
            TimeSpan.FromHours(8)),
        new(
            Guid.Parse("b1090000-0000-4000-8000-000000000006"),
            "Hướng dẫn xử lý pin Li-ion và thiết bị điện tử cũ an toàn tại nhà",
            "Tuyệt đối không bỏ pin sạc hoặc pin phồng vào thùng rác sinh hoạt để phòng ngừa nguy cơ chập cháy trong xe ép rác.",
            "Pin Li-ion từ điện thoại, sạc dự phòng, laptop hay đồ chơi điện tử vẫn còn tích trữ năng lượng ngay cả khi thiết bị đã ngừng hoạt động.\n\nNếu bị nén ép trong thùng rác sinh hoạt, lớp màng ngăn bên trong cell pin rất dễ rách và gây đoản mạch sinh nhiệt, dẫn tới cháy nổ âm ỉ tại bãi tập kết rác.\n\nHãy dán băng keo cách điện hai đầu cực của pin cũ, bảo quản trong hộp nhựa khô ráo tách biệt khỏi kim loại sắc nhọn và tra cứu điểm tiếp nhận rác nguy hại trên bản đồ EcoLink.",
            "/assets/insights/e1b25.png",
            new[] { "Pin Li-ion", "An toàn thu gom", "Rác điện tử" },
            BlogPostStatus.Draft,
            TimeSpan.FromHours(3))
    };

    private async Task EnsureDefaultBlogPostsSeededAsync(CancellationToken ct)
    {
        if (_defaultSeedChecked || !_db.Database.IsRelational())
        {
            return;
        }

        if (await _db.BlogPosts.AsNoTracking().AnyAsync(ct))
        {
            _defaultSeedChecked = true;
            return;
        }

        await DefaultSeedLock.WaitAsync(ct);
        try
        {
            if (!_defaultSeedChecked && !await _db.BlogPosts.AsNoTracking().AnyAsync(ct))
            {
                await SeedDefaultBlogPostsCoreAsync(ct);
            }
            _defaultSeedChecked = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể tự động khởi tạo bài viết Blog mẫu (bỏ qua để không chặn truy vấn).");
        }
        finally
        {
            DefaultSeedLock.Release();
        }
    }

    internal async Task SeedDefaultBlogPostsCoreAsync(CancellationToken ct = default)
    {
        if (await _db.BlogPosts.AsNoTracking().AnyAsync(ct))
        {
            return;
        }

        var staffUser = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == DemoStaffId || u.Username == "staff", ct);
        if (staffUser == null)
        {
            staffUser = new User
            {
                Id = DemoStaffId,
                Username = "staff",
                Email = "staff@ecolink.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("staff123"),
                FullName = "Lê Thu Hà",
                PhoneNumber = "0909887766",
                Role = UserRole.Staff,
                Status = UserStatus.Active
            };
            _db.Users.Add(staffUser);
        }

        var adminUser = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == DemoAdminId || u.Username == "admin", ct);
        if (adminUser == null)
        {
            adminUser = new User
            {
                Id = DemoAdminId,
                Username = "admin",
                Email = "admin@ecolink.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                FullName = "Quản trị viên EcoLink",
                PhoneNumber = "0912345678",
                Role = UserRole.Admin,
                Status = UserStatus.Active
            };
            _db.Users.Add(adminUser);
        }

        if (_db.ChangeTracker.HasChanges())
        {
            await _db.SaveChangesAsync(ct);
        }

        var now = DateTime.UtcNow;
        foreach (var seed in DefaultBlogSeeds)
        {
            var timestamp = now.Subtract(seed.AgeOffset);
            var isApproved = seed.Status == BlogPostStatus.Approved;
            var isPending = seed.Status == BlogPostStatus.PendingApproval;

            _db.BlogPosts.Add(new BlogPost
            {
                Id = seed.Id,
                AuthorId = staffUser.Id,
                Title = seed.Title,
                Slug = Slugify(seed.Title),
                Summary = seed.Summary,
                Content = seed.Content.Trim(),
                CoverImageUrl = seed.CoverImageUrl,
                Tags = JsonSerializer.Serialize(seed.Tags, JsonOptions),
                Status = seed.Status,
                ApprovedByAdminId = isApproved ? adminUser.Id : null,
                SubmittedAt = isApproved || isPending ? timestamp.AddHours(-2) : null,
                ApprovedAt = isApproved ? timestamp : null,
                PublishedAt = isApproved ? timestamp : null,
                CreatedAt = timestamp.AddHours(-4),
                UpdatedAt = timestamp
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Đã khởi tạo {Count} bài viết Blog mẫu mặc định cho EcoLink.", DefaultBlogSeeds.Length);
    }
}
