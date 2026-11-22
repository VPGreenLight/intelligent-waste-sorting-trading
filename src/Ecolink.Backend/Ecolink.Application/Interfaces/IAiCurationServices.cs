using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

/// <summary>
/// Dịch vụ ghi vết phiên quét AI, tiếp nhận phản hồi sai (U-04) và hàng đợi thẩm định của Staff (S-01).
/// </summary>
public interface IAiFeedbackService
{
    Task<(Guid ScanId, string ImageUrl)> RecordScanHistoryAsync(
        byte[] imageBytes,
        string fileName,
        string? contentType,
        Guid? userId,
        AiScanResultDto aiResult,
        CancellationToken ct = default);

    Task<SubmitAiFeedbackResponseDto> SubmitFeedbackAsync(
        Guid? userId,
        SubmitFeedbackDto request,
        CancellationToken ct = default);

    Task<PagedResult<MyAiFeedbackReportDto>> GetMyFeedbackReportsAsync(
        Guid userId,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<PagedResult<MyWasteScanHistoryItemDto>> GetMyScanHistoryAsync(
        Guid userId,
        WasteScanHistoryFilterRequest filter,
        CancellationToken ct = default);

    Task<StaffAiFeedbackQueueDto> GetStaffFeedbackQueueAsync(
        StaffAiFeedbackFilterRequest filter,
        CancellationToken ct = default);

    Task<StaffAiFeedbackDetailDto> GetStaffFeedbackDetailAsync(
        Guid reportId,
        CancellationToken ct = default);

    Task<StaffAiFeedbackDetailDto> ReviewFeedbackAsync(
        Guid staffId,
        Guid reportId,
        ReviewAiFeedbackRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Dịch vụ gán nhãn Ground-Truth (S-02), đóng gói phiên bản Dataset Sticky Stratified Split 70/20/10 &amp; xuất ZIP (S-03).
/// </summary>
public interface IAiDatasetCurationService
{
    Task<PagedResult<AiDatasetItemDto>> GetLabelingQueueAsync(
        AiLabelingQueueFilterRequest filter,
        CancellationToken ct = default);

    Task<AiDatasetItemDto> AssignGroundTruthLabelAsync(
        Guid staffId,
        Guid itemId,
        AssignGroundTruthLabelRequest request,
        CancellationToken ct = default);

    Task<BatchAssignLabelsResultDto> BatchAssignLabelsAsync(
        Guid staffId,
        BatchAssignLabelsRequest request,
        CancellationToken ct = default);

    Task<AiDatasetDashboardDto> GetDatasetOverviewAsync(
        CancellationToken ct = default);

    Task<AiDatasetVersionDto> CreateDatasetVersionAsync(
        Guid staffId,
        CreateDatasetVersionRequest request,
        CancellationToken ct = default);

    Task<DatasetArchiveExportResult> ExportDatasetVersionAsync(
        Guid staffId,
        Guid versionId,
        string format = "yolo",
        CancellationToken ct = default);
}
