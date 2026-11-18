using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

/// <summary>
/// Dịch vụ đánh giá 1-5 sao, tự xóa bài &amp; kiểm duyệt đánh giá vi phạm (U-10, BR-RVW-01..03).
/// </summary>
public interface IReviewService
{
    Task<TransactionReviewDto> CreateReviewAsync(
        Guid callerUserId,
        Guid requestId,
        CreateReviewRequest request,
        CancellationToken ct = default);

    Task<RecyclerReviewsSummaryDto> GetRecyclerReviewsAsync(
        Guid recyclerId,
        RecyclerReviewFilterRequest filter,
        CancellationToken ct = default);

    Task<PagedResult<TransactionReviewDto>> GetMyReviewsAsync(
        Guid callerUserId,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    Task<TransactionReviewDto> DeleteMyReviewAsync(
        Guid callerUserId,
        Guid reviewId,
        CancellationToken ct = default);

    Task<TransactionReviewDto> ReportAbusiveReviewAsync(
        Guid callerUserId,
        Guid reviewId,
        ReportReviewRequest request,
        CancellationToken ct = default);

    Task<TransactionReviewDto> ModerateReviewAsync(
        Guid staffUserId,
        Guid reviewId,
        ModerateReviewRequest request,
        CancellationToken ct = default);

    Task<List<TransactionReviewDto>> GetReportedReviewsForStaffAsync(CancellationToken ct = default);
}

/// <summary>
/// Dịch vụ khiếu nại 2 chiều (U-11) &amp; Hồ sơ Trọng tài 360 độ của Staff (S-05).
/// </summary>
public interface IComplaintService
{
    Task<RecyclerDocumentUploadResponseDto> UploadEvidenceAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default);

    Task<ComplaintDetailDto> SubmitComplaintAsync(
        Guid callerUserId,
        SubmitComplaintRequest request,
        CancellationToken ct = default);

    Task<PagedResult<ComplaintSummaryDto>> GetMyComplaintsAsync(
        Guid callerUserId,
        MyComplaintFilterRequest filter,
        CancellationToken ct = default);

    Task<StaffComplaintQueueResponseDto> GetStaffComplaintQueueAsync(
        StaffComplaintFilterRequest filter,
        CancellationToken ct = default);

    Task<StaffComplaintDossierDto> GetStaffComplaintDossierAsync(
        Guid staffUserId,
        Guid complaintId,
        bool claimTicket = true,
        CancellationToken ct = default);

    Task<ComplaintDetailDto> ResolveComplaintAsync(
        Guid staffUserId,
        Guid complaintId,
        ResolveComplaintRequest request,
        CancellationToken ct = default);
}
