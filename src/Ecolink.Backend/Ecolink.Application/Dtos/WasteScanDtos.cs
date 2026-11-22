namespace Ecolink.Application.Dtos;

public class WastePredictionDto
{
    public string Label { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public class DetectedWasteObjectDto
{
    public int Id { get; set; }
    public string RawClass { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public double CenterScore { get; set; }
    public bool IsPrimary { get; set; }
    public string? Source { get; set; }
    public List<double> Bbox { get; set; } = new();
    public WasteSortingGuideDto? SortingGuide { get; set; }
}

public class AiScanResultDto
{
    public bool Success { get; set; }
    public bool IsOutOfDistribution { get; set; }
    public double ConfidenceThreshold { get; set; } = 0.60;
    public string? ModelUsed { get; set; }
    public string? ModelName { get; set; }
    public string? ModelFamily { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public WastePredictionDto? TopPrediction { get; set; }
    public List<WastePredictionDto> Predictions { get; set; } = new();
    public List<WastePredictionDto> AllProbabilities { get; set; } = new();
    public int DetectedCount { get; set; }
    public Dictionary<string, int> CategorySummary { get; set; } = new();
    public List<DetectedWasteObjectDto> DetectedObjects { get; set; } = new();
    public string? Pipeline { get; set; }
    public List<double>? CroppedBbox { get; set; }
    public double InferenceTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

public class WasteSortingGuideDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string StandardBinColor { get; set; } = string.Empty;
    public string PreparationSteps { get; set; } = string.Empty;
    public string HandlingAction { get; set; } = string.Empty;
    public string? HazardWarning { get; set; }
}

public class WasteScanResponseDto
{
    public Guid? ScanId { get; set; }
    public string? ImageUrl { get; set; }
    public AiScanResultDto AiResult { get; set; } = new();
    public WasteSortingGuideDto? SortingGuide { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SubmitFeedbackDto
{
    public Guid? ScanId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string PredictedLabel { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string UserReportedLabel { get; set; } = string.Empty;
    public string? Note { get; set; }
}

// ---------------------------------------------------------------------------
// Module M-03 (FE-03): Scan History, Master Categories & Rule Engine (STT 026-031)
// ---------------------------------------------------------------------------

public class WasteScanHistoryFilterRequest
{
    public string? CategoryCode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class MyWasteScanHistoryItemDto
{
    public Guid ScanId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string PredictedLabel { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string PipelineUsed { get; set; } = string.Empty;
    public WasteSortingGuideDto? SortingGuide { get; set; }
    public DateTime ScannedAt { get; set; }
}

public class CreateWasteCategoryRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StandardBinColor { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public bool IsRecyclable { get; set; } = true;
}

public class UpdateWasteCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StandardBinColor { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public bool IsRecyclable { get; set; } = true;
}

public class ToggleWasteCategoryStatusRequest
{
    public string Status { get; set; } = "Active";
}

public class UpsertSortingRuleRequest
{
    public string Title { get; set; } = string.Empty;
    public string PreparationSteps { get; set; } = string.Empty;
    public string SortingInstruction { get; set; } = string.Empty;
    public string HandlingAction { get; set; } = string.Empty;
    public string? HazardWarning { get; set; }
    public string ApplicableRegion { get; set; } = "VN_NATIONAL";
}

public class SortingRuleDetailDto
{
    public Guid RuleId { get; set; }
    public Guid WasteCategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryStatus { get; set; } = "Active";
    public string StandardBinColor { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PreparationSteps { get; set; } = string.Empty;
    public string SortingInstruction { get; set; } = string.Empty;
    public string HandlingAction { get; set; } = string.Empty;
    public string? HazardWarning { get; set; }
    public string ApplicableRegion { get; set; } = "VN_NATIONAL";
    public string Status { get; set; } = "DRAFT";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PublishSortingRuleResultDto
{
    public SortingRuleDetailDto Rule { get; set; } = new();
    public bool AiServiceSynced { get; set; }
    public string? AiSyncMessage { get; set; }
}

public class AiRuleSyncResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double? ConfidenceThreshold { get; set; }
}
