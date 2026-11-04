namespace Ecolink.Application.Dtos;

public class WastePredictionDto
{
    public string Label { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public class AiScanResultDto
{
    public bool Success { get; set; }
    public bool IsOutOfDistribution { get; set; }
    public double ConfidenceThreshold { get; set; } = 0.60;
    public string? ModelUsed { get; set; }
    public string? ModelName { get; set; }
    public string? ModelFamily { get; set; }
    public WastePredictionDto? TopPrediction { get; set; }
    public List<WastePredictionDto> Predictions { get; set; } = new();
    public List<WastePredictionDto> AllProbabilities { get; set; } = new();
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
    public AiScanResultDto AiResult { get; set; } = new();
    public WasteSortingGuideDto? SortingGuide { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SubmitFeedbackDto
{
    public string ImageUrl { get; set; } = string.Empty;
    public string PredictedLabel { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string UserReportedLabel { get; set; } = string.Empty;
    public string? Note { get; set; }
}
