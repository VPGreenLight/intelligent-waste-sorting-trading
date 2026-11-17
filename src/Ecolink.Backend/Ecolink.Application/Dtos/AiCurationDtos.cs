namespace Ecolink.Application.Dtos;

/// <summary>
/// Tọa độ hộp bao chuẩn hóa YOLO [categoryCode, xCenter, yCenter, width, height] nằm trong [0.0, 1.0].
/// </summary>
public class YoloBoundingBoxDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public double XCenter { get; set; }
    public double YCenter { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

/// <summary>
/// Kết quả gửi báo cáo AI nhận diện sai (STT 024 - U-04).
/// </summary>
public class SubmitAiFeedbackResponseDto
{
    public Guid ReportId { get; set; }
    public Guid? ScanId { get; set; }
    public string Status { get; set; } = "Pending";
    public string NormalizedLabel { get; set; } = string.Empty;
    public bool SyncedToAi { get; set; }
}

/// <summary>
/// Báo cáo phân loại sai của chính người dùng đã đăng nhập (STT 130 - U-04).
/// </summary>
public class MyAiFeedbackReportDto
{
    public Guid Id { get; set; }
    public Guid? ScanId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public string PredictedLabel { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string UserReportedLabel { get; set; } = string.Empty;
    public string? AdminVerifiedLabel { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ReviewDecision { get; set; }
    public string? Note { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class StaffAiFeedbackFilterRequest
{
    public string? Status { get; set; }
    public string? PredictedLabel { get; set; }
    public string? UserReportedLabel { get; set; }
    public double? MaxConfidence { get; set; }
    public string? Search { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class StaffAiFeedbackSummaryDto
{
    public Guid Id { get; set; }
    public Guid? ScanId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public string PredictedLabel { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string UserReportedLabel { get; set; } = string.Empty;
    public string? AdminVerifiedLabel { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ReviewDecision { get; set; }
    public string ReporterName { get; set; } = "Ẩn danh";
    public string? Note { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? DatasetItemId { get; set; }
    public bool InDataset { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class StaffAiFeedbackQueueDto
{
    public IReadOnlyList<StaffAiFeedbackSummaryDto> Items { get; set; } = Array.Empty<StaffAiFeedbackSummaryDto>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalRecords / PageSize) : 0;
    public int PendingCount { get; set; }
    public int ReviewedCount { get; set; }
    public int RejectedCount { get; set; }
}

public class StaffAiFeedbackDetailDto
{
    public Guid Id { get; set; }
    public Guid? ScanId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ImageSha256 { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public string PredictedLabel { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string UserReportedLabel { get; set; } = string.Empty;
    public string? AdminVerifiedLabel { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ReviewDecision { get; set; }
    public string? Note { get; set; }
    public string? ReviewNote { get; set; }
    public string ReporterName { get; set; } = "Ẩn danh";
    public string ReporterRole { get; set; } = "Citizen";
    public string? ReviewedByStaffName { get; set; }
    public Guid? DatasetItemId { get; set; }
    public string? DatasetItemStatus { get; set; }
    public IReadOnlyList<DetectedWasteObjectDto> DetectedObjectsSnapshot { get; set; } = Array.Empty<DetectedWasteObjectDto>();
    public IReadOnlyList<YoloBoundingBoxDto> ExistingYoloAnnotations { get; set; } = Array.Empty<YoloBoundingBoxDto>();
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class ReviewAiFeedbackRequest
{
    public string Decision { get; set; } = "ApproveAndLabel";
    public string? VerifiedLabel { get; set; }
    public string? ReviewNote { get; set; }
    public List<YoloBoundingBoxDto>? YoloAnnotations { get; set; }
}

public class AiLabelingQueueFilterRequest
{
    public string? Status { get; set; }
    public string? SourceType { get; set; }
    public string? GroundTruthLabel { get; set; }
    public string? PredictedLabel { get; set; }
    public bool? UnlabeledOnly { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AiDatasetItemDto
{
    public Guid Id { get; set; }
    public string SourceType { get; set; } = "UserFeedback";
    public Guid? SourceScanId { get; set; }
    public Guid? SourceFeedbackId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageSha256 { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string? PredictedLabel { get; set; }
    public double? PredictedConfidence { get; set; }
    public string? GroundTruthLabel { get; set; }
    public string Status { get; set; } = "Unlabeled";
    public string? StickySplit { get; set; }
    public string? QualityNote { get; set; }
    public string? LabeledByStaffName { get; set; }
    public DateTime? LabeledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<YoloBoundingBoxDto> YoloAnnotations { get; set; } = Array.Empty<YoloBoundingBoxDto>();
}

public class AssignGroundTruthLabelRequest
{
    public string? GroundTruthLabel { get; set; }
    public bool ExcludeFromDataset { get; set; }
    public string? QualityNote { get; set; }
    public List<YoloBoundingBoxDto>? YoloAnnotations { get; set; }
}

public class BatchAssignLabelsRequest
{
    public List<Guid> ItemIds { get; set; } = new();
    public string GroundTruthLabel { get; set; } = string.Empty;
    public string? QualityNote { get; set; }
}

public class BatchAssignLabelsResultDto
{
    public int UpdatedCount { get; set; }
    public string GroundTruthLabel { get; set; } = string.Empty;
    public IReadOnlyList<Guid> UpdatedItemIds { get; set; } = Array.Empty<Guid>();
}

public class AiDatasetSummaryMetricsDto
{
    public int TotalPoolItems { get; set; }
    public int VerifiedLabeledCount { get; set; }
    public int UnlabeledCount { get; set; }
    public int ExcludedCount { get; set; }
    public int StickyTrainCount { get; set; }
    public int StickyValidationCount { get; set; }
    public int StickyTestCount { get; set; }
    public decimal ImbalanceRatio { get; set; }
    public bool IsBalanced { get; set; }
    public string? WeakestCategory { get; set; }
    public decimal EmpiricalAccuracyPercent { get; set; }
}

public class AiDatasetCategoryStatDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int TotalVerified { get; set; }
    public int TrainCount { get; set; }
    public int ValCount { get; set; }
    public int TestCount { get; set; }
    public int UnassignedCount { get; set; }
    public int VerifiedFeedbackCount { get; set; }
    public int FalseReportCount { get; set; }
    public decimal AccuracyPercent { get; set; }
    public bool NeedsMoreSamples { get; set; }
}

public class AiDatasetVersionDto
{
    public Guid Id { get; set; }
    public string VersionCode { get; set; } = string.Empty;
    public string VersionName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TrainRatio { get; set; }
    public int ValRatio { get; set; }
    public int TestRatio { get; set; }
    public int TotalSamples { get; set; }
    public int TrainCount { get; set; }
    public int ValCount { get; set; }
    public int TestCount { get; set; }
    public decimal ImbalanceRatio { get; set; }
    public bool IsBalanced { get; set; }
    public string Status { get; set; } = "Published";
    public DateTime? SyncedToAiAt { get; set; }
    public string CreatedByStaffName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? AiSyncWarning { get; set; }
    public Dictionary<string, Dictionary<string, int>> ClassDistribution { get; set; } = new();
}

public class AiDatasetDashboardDto
{
    public AiDatasetSummaryMetricsDto Summary { get; set; } = new();
    public IReadOnlyList<AiDatasetCategoryStatDto> ByCategory { get; set; } = Array.Empty<AiDatasetCategoryStatDto>();
    public IReadOnlyList<AiDatasetVersionDto> Versions { get; set; } = Array.Empty<AiDatasetVersionDto>();
}

public class CreateDatasetVersionRequest
{
    public string VersionCode { get; set; } = string.Empty;
    public string VersionName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TrainRatio { get; set; } = 70;
    public int ValRatio { get; set; } = 20;
    public int TestRatio { get; set; } = 10;
    public bool SyncToAiService { get; set; } = true;
}

public class DatasetArchiveExportResult
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/zip";
    public int SampleCount { get; set; }
}

public class CurateDatasetSyncItemDto
{
    public Guid ItemId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageSha256 { get; set; } = string.Empty;
    public string Split { get; set; } = "train";
    public string CategoryCode { get; set; } = string.Empty;
    public List<YoloBoundingBoxDto> Annotations { get; set; } = new();
}

public class CurateDatasetSyncRequest
{
    public string VersionCode { get; set; } = string.Empty;
    public string VersionName { get; set; } = string.Empty;
    public int TrainRatio { get; set; } = 70;
    public int ValRatio { get; set; } = 20;
    public int TestRatio { get; set; } = 10;
    public List<CurateDatasetSyncItemDto> Items { get; set; } = new();
}

public class CurateDatasetSyncResponseDto
{
    public bool Success { get; set; }
    public string VersionCode { get; set; } = string.Empty;
    public string? ManifestPath { get; set; }
    public int TotalSamples { get; set; }
    public int TrainCount { get; set; }
    public int ValCount { get; set; }
    public int TestCount { get; set; }
    public bool LeakageDetected { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AiModelReloadResponseDto
{
    public bool Success { get; set; }
    public string? ReloadedAt { get; set; }
    public int ReadyCount { get; set; }
    public string? ErrorMessage { get; set; }
}
