using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

/// <summary>Blog Content Management (UC S-08, A-09, xem blog công khai; BR-ADM-05, BR-ADM-08).</summary>
public interface IBlogPostService
{
    // Staff (S-08)
    Task<BlogPostDto> CreateAsync(Guid authorId, SaveBlogPostRequest request, CancellationToken ct = default);
    Task<BlogPostDto> UpdateAsync(Guid authorId, Guid postId, SaveBlogPostRequest request, CancellationToken ct = default);
    Task<BlogPostDto> SubmitAsync(Guid authorId, Guid postId, CancellationToken ct = default);
    Task<BlogPostDto> RecallAsync(Guid authorId, Guid postId, CancellationToken ct = default);
    Task DeleteAsync(Guid authorId, Guid postId, CancellationToken ct = default);
    Task<BlogPostDto> GetMineAsync(Guid authorId, Guid postId, CancellationToken ct = default);
    Task<BlogPagedResult<BlogPostSummaryDto>> ListMineAsync(Guid authorId, string? status, string? search, int page, int pageSize, CancellationToken ct = default);

    // Admin (A-09)
    Task<BlogPagedResult<BlogPostSummaryDto>> ListQueueAsync(string? search, string? sort, int page, int pageSize, CancellationToken ct = default);
    Task<BlogPostDto> GetForReviewAsync(Guid postId, CancellationToken ct = default);
    Task<BlogPostDto> ApproveAsync(Guid adminId, Guid postId, CancellationToken ct = default);
    Task<BlogPostDto> RejectAsync(Guid adminId, Guid postId, string feedback, CancellationToken ct = default);

    // Công khai
    Task<BlogPagedResult<BlogPostSummaryDto>> ListPublishedAsync(string? search, string? tag, int page, int pageSize, CancellationToken ct = default);
    Task<PublicBlogPostDto> GetPublishedBySlugAsync(string slug, CancellationToken ct = default);
}
