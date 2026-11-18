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
/// Dịch vụ Đánh giá 1-5 sao, Tự xóa đánh giá &amp; Kiểm duyệt cộng đồng (U-10, BR-RVW-01..03, BR-ADM-05).
/// </summary>
public class ReviewService : IReviewService
{
    public const int ReviewWindowDays = 14;

    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        ApplicationDbContext db,
        IAuditLogService auditLogService,
        ILogger<ReviewService> logger)
    {
        _db = db;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<TransactionReviewDto> CreateReviewAsync(
        Guid callerUserId,
        Guid requestId,
        CreateReviewRequest request,
        CancellationToken ct = default)
    {
        var handover = await _db.HandoverRequests
            .FirstOrDefaultAsync(h => h.Id == requestId, ct)
            ?? throw new NotFoundException("Không tìm thấy yêu cầu bàn giao.");

        if (handover.UserId != callerUserId)
        {
            throw new ForbiddenAccessException(
                "BR-RVW-01",
                "MSG-ADM-04",
                "Chỉ người dân sở hữu đơn bàn giao mới có quyền gửi đánh giá cho giao dịch này.");
        }

        if (handover.Status != HandoverStatus.Completed)
        {
            throw new BusinessRuleException(
                "BR-RVW-01",
                "MSG-RVW-02",
                "Bạn chỉ có thể đánh giá mỗi đơn giao dịch hoàn tất một lần trong thời hạn quy định.");
        }

        var receipt = await _db.HandoverReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct);

        var completedAt = receipt?.ConfirmedByUserAt ?? handover.ActualHandoverAt ?? handover.UpdatedAt;
        if ((DateTime.UtcNow - completedAt) > TimeSpan.FromDays(ReviewWindowDays))
        {
            throw new BusinessRuleException(
                "BR-RVW-01",
                "MSG-RVW-02",
                "Bạn chỉ có thể đánh giá mỗi đơn giao dịch hoàn tất một lần trong thời hạn 14 ngày (MSG-RVW-02).");
        }

        var alreadyExists = await _db.TransactionReviews
            .IgnoreQueryFilters()
            .AnyAsync(r => r.RequestId == requestId && r.ReviewerId == callerUserId, ct);

        if (alreadyExists)
        {
            throw new ConflictException(
                "BR-RVW-01",
                "MSG-RVW-02",
                "Bạn chỉ có thể đánh giá mỗi đơn giao dịch hoàn tất một lần trong thời hạn quy định.");
        }

        var review = new TransactionReview
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            ReviewerId = callerUserId,
            RecyclerId = handover.RecyclerId,
            RatingStars = request.RatingStars,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            Status = ReviewStatus.Visible,
            ReportCount = 0
        };

        _db.TransactionReviews.Add(review);
        await _db.SaveChangesAsync(ct);

        var (avg, total) = await RecalculateRecyclerRatingAsync(handover.RecyclerId, ct);

        await _auditLogService.LogAsync(
            "CREATE_REVIEW",
            "TransactionReview",
            review.Id.ToString(),
            oldValues: null,
            newValues: new { review.RequestId, review.RecyclerId, review.RatingStars },
            reason: review.Comment,
            explicitActorId: callerUserId,
            ct: ct);

        _logger.LogInformation(
            "Citizen {UserId} đã đánh giá {Stars} sao cho Recycler {RecyclerId} (đơn {RequestId}). Điểm TB mới: {Avg} ({Total} lượt).",
            callerUserId, review.RatingStars, handover.RecyclerId, requestId, avg, total);

        var loaded = await LoadReviewDtoAsync(review.Id, ct);
        loaded.UpdatedRecyclerAverageRating = avg;
        loaded.UpdatedRecyclerTotalReviews = total;
        return loaded;
    }

    public async Task<RecyclerReviewsSummaryDto> GetRecyclerReviewsAsync(
        Guid recyclerId,
        RecyclerReviewFilterRequest filter,
        CancellationToken ct = default)
    {
        var recyclerProfile = await _db.RecyclerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == recyclerId, ct)
            ?? throw new NotFoundException("Không tìm thấy cơ sở thu mua.");

        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);

        var visibleBaseQuery = _db.TransactionReviews
            .AsNoTracking()
            .Where(r => r.RecyclerId == recyclerId &&
                        (r.Status == ReviewStatus.Visible || r.Status == ReviewStatus.Reported));

        var allRatings = await visibleBaseQuery
            .Select(r => r.RatingStars)
            .ToListAsync(ct);

        var distribution = new StarDistributionDto
        {
            FiveStars = allRatings.Count(s => s == 5),
            FourStars = allRatings.Count(s => s == 4),
            ThreeStars = allRatings.Count(s => s == 3),
            TwoStars = allRatings.Count(s => s == 2),
            OneStar = allRatings.Count(s => s == 1)
        };

        var filteredQuery = visibleBaseQuery;
        if (filter.RatingStars is >= 1 and <= 5)
        {
            filteredQuery = filteredQuery.Where(r => r.RatingStars == filter.RatingStars.Value);
        }

        var totalRecords = await filteredQuery.CountAsync(ct);

        var rows = await filteredQuery
            .Include(r => r.Reviewer)
            .Include(r => r.Recycler)
            .Include(r => r.Request)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var averageRating = allRatings.Count == 0
            ? 0m
            : Math.Round((decimal)allRatings.Average(), 2, MidpointRounding.AwayFromZero);

        return new RecyclerReviewsSummaryDto
        {
            RecyclerId = recyclerId,
            FacilityName = recyclerProfile.FacilityName,
            AverageRating = recyclerProfile.TotalReviews == allRatings.Count ? recyclerProfile.AverageRating : averageRating,
            TotalReviews = allRatings.Count,
            StarDistribution = distribution,
            Reviews = new PagedResult<TransactionReviewDto>
            {
                Items = rows.Select(r => MapToDto(r, maskAuthor: true)).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = totalRecords
            }
        };
    }

    public async Task<PagedResult<TransactionReviewDto>> GetMyReviewsAsync(
        Guid callerUserId,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.TransactionReviews
            .AsNoTracking()
            .Where(r => r.ReviewerId == callerUserId && r.Status != ReviewStatus.Deleted);

        var totalRecords = await query.CountAsync(ct);

        var rows = await query
            .Include(r => r.Reviewer)
            .Include(r => r.Recycler)
            .Include(r => r.Request)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<TransactionReviewDto>
        {
            Items = rows.Select(r => MapToDto(r, maskAuthor: false)).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords
        };
    }

    public async Task<TransactionReviewDto> DeleteMyReviewAsync(
        Guid callerUserId,
        Guid reviewId,
        CancellationToken ct = default)
    {
        var review = await _db.TransactionReviews
            .Include(r => r.Reviewer)
            .Include(r => r.Recycler)
            .Include(r => r.Request)
            .FirstOrDefaultAsync(r => r.Id == reviewId, ct)
            ?? throw new NotFoundException("Không tìm thấy đánh giá.");

        if (review.Status == ReviewStatus.Deleted)
        {
            throw new NotFoundException("Đánh giá này đã bị xóa trước đó.");
        }

        if (review.ReviewerId != callerUserId)
        {
            throw new ForbiddenAccessException(
                "BR-RVW-03",
                "MSG-ADM-04",
                "Chỉ chính tác giả mới có quyền xóa vĩnh viễn bài đánh giá của mình.");
        }

        review.Status = ReviewStatus.Deleted;
        review.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var (avg, total) = await RecalculateRecyclerRatingAsync(review.RecyclerId, ct);

        await _auditLogService.LogAsync(
            "DELETE_OWN_REVIEW",
            "TransactionReview",
            review.Id.ToString(),
            oldValues: new { Status = ReviewStatus.Visible.ToString(), review.RatingStars },
            newValues: new { Status = ReviewStatus.Deleted.ToString() },
            reason: "Tác giả tự xóa vĩnh viễn bài đánh giá (BR-RVW-03).",
            explicitActorId: callerUserId,
            ct: ct);

        var dto = MapToDto(review, maskAuthor: false);
        dto.UpdatedRecyclerAverageRating = avg;
        dto.UpdatedRecyclerTotalReviews = total;
        return dto;
    }

    public async Task<TransactionReviewDto> ReportAbusiveReviewAsync(
        Guid callerUserId,
        Guid reviewId,
        ReportReviewRequest request,
        CancellationToken ct = default)
    {
        var review = await _db.TransactionReviews
            .Include(r => r.Reviewer)
            .Include(r => r.Recycler)
            .Include(r => r.Request)
            .FirstOrDefaultAsync(r => r.Id == reviewId, ct)
            ?? throw new NotFoundException("Không tìm thấy đánh giá.");

        if (review.Status is ReviewStatus.Deleted or ReviewStatus.Hidden)
        {
            throw new NotFoundException("Đánh giá này không còn hiển thị công khai.");
        }

        if (review.ReviewerId == callerUserId)
        {
            throw new BusinessRuleException(
                "BR-RVW-03",
                "MSG-GEN-01",
                "Bạn không thể tự báo cáo bài đánh giá của chính mình.");
        }

        review.ReportCount += 1;
        review.LastReportReason = request.Reason.Trim();
        review.ReportedByUserId = callerUserId;
        review.Status = ReviewStatus.Reported;
        review.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Người dùng {UserId} đã báo cáo vi phạm đánh giá {ReviewId} (tổng {Count} lần).", callerUserId, reviewId, review.ReportCount);
        return MapToDto(review, maskAuthor: false);
    }

    public async Task<TransactionReviewDto> ModerateReviewAsync(
        Guid staffUserId,
        Guid reviewId,
        ModerateReviewRequest request,
        CancellationToken ct = default)
    {
        var review = await _db.TransactionReviews
            .Include(r => r.Reviewer)
            .Include(r => r.Recycler)
            .Include(r => r.Request)
            .FirstOrDefaultAsync(r => r.Id == reviewId, ct)
            ?? throw new NotFoundException("Không tìm thấy đánh giá.");

        if (review.Status == ReviewStatus.Deleted)
        {
            throw new ConflictException("BR-RVW-03", "MSG-RVW-03", "Đánh giá đã bị tác giả xóa vĩnh viễn và không thể kiểm duyệt.");
        }

        var oldStatus = review.Status.ToString();
        var isHide = string.Equals(request.Action.Trim(), "Hide", StringComparison.OrdinalIgnoreCase);

        review.Status = isHide ? ReviewStatus.Hidden : ReviewStatus.Visible;
        review.ModeratedByStaffId = staffUserId;
        review.ModeratedAt = DateTime.UtcNow;
        review.ModerationReason = request.ModerationReason.Trim();
        review.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var (avg, total) = await RecalculateRecyclerRatingAsync(review.RecyclerId, ct);

        await _auditLogService.LogAsync(
            "MODERATE_REVIEW",
            "TransactionReview",
            review.Id.ToString(),
            oldValues: new { Status = oldStatus },
            newValues: new { Status = review.Status.ToString(), RecyclerId = review.RecyclerId, AverageRating = avg, TotalReviews = total },
            reason: review.ModerationReason,
            explicitActorId: staffUserId,
            ct: ct);

        var dto = MapToDto(review, maskAuthor: false);
        dto.UpdatedRecyclerAverageRating = avg;
        dto.UpdatedRecyclerTotalReviews = total;
        return dto;
    }

    public async Task<List<TransactionReviewDto>> GetReportedReviewsForStaffAsync(CancellationToken ct = default)
    {
        var rows = await _db.TransactionReviews
            .AsNoTracking()
            .Include(r => r.Reviewer)
            .Include(r => r.Recycler)
            .Include(r => r.Request)
            .Where(r => r.Status == ReviewStatus.Reported || (r.Status == ReviewStatus.Hidden && r.ReportCount > 0))
            .OrderByDescending(r => r.Status == ReviewStatus.Reported)
            .ThenByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return rows.Select(r => MapToDto(r, maskAuthor: false)).ToList();
    }

    public async Task<(decimal AverageRating, int TotalReviews)> RecalculateRecyclerRatingAsync(
        Guid recyclerId,
        CancellationToken ct = default)
    {
        var activeStars = await _db.TransactionReviews
            .AsNoTracking()
            .Where(r => r.RecyclerId == recyclerId &&
                        (r.Status == ReviewStatus.Visible || r.Status == ReviewStatus.Reported))
            .Select(r => r.RatingStars)
            .ToListAsync(ct);

        var total = activeStars.Count;
        var average = total == 0
            ? 0m
            : Math.Round((decimal)activeStars.Average(), 2, MidpointRounding.AwayFromZero);

        var profile = await _db.RecyclerProfiles.FirstOrDefaultAsync(p => p.UserId == recyclerId, ct);
        if (profile != null)
        {
            profile.AverageRating = average;
            profile.TotalReviews = total;
            profile.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return (average, total);
    }

    private async Task<TransactionReviewDto> LoadReviewDtoAsync(Guid reviewId, CancellationToken ct)
    {
        var entity = await _db.TransactionReviews
            .AsNoTracking()
            .Include(r => r.Reviewer)
            .Include(r => r.Recycler)
            .Include(r => r.Request)
            .FirstAsync(r => r.Id == reviewId, ct);

        return MapToDto(entity, maskAuthor: false);
    }

    private static TransactionReviewDto MapToDto(TransactionReview review, bool maskAuthor)
    {
        var fullName = review.Reviewer?.FullName;
        var displayName = maskAuthor
            ? MaskAuthorName(fullName)
            : (string.IsNullOrWhiteSpace(fullName) ? "Thành viên EcoLink" : fullName);

        return new TransactionReviewDto
        {
            ReviewId = review.Id,
            RequestId = review.RequestId,
            RequestCode = review.Request?.RequestCode,
            ReviewerId = review.ReviewerId,
            ReviewerDisplayName = displayName,
            RecyclerId = review.RecyclerId,
            FacilityName = review.Recycler?.FacilityName ?? "Cơ sở thu mua EcoLink",
            RatingStars = review.RatingStars,
            Comment = review.Comment,
            Status = review.Status.ToString(),
            ReportCount = review.ReportCount,
            LastReportReason = review.LastReportReason,
            ModerationReason = review.ModerationReason,
            ModeratedAt = review.ModeratedAt,
            CreatedAt = DateTime.SpecifyKind(review.CreatedAt, DateTimeKind.Utc)
        };
    }

    private static string MaskAuthorName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "Người dân EcoLink";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1) return parts[0];
        var last = parts[^1];
        return $"{parts[0]} {new string('*', Math.Max(2, parts.Length > 2 ? 3 : 2))} {last}";
    }
}
