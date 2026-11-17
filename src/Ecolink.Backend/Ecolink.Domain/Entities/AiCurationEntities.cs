using Ecolink.Domain.Common;
using Ecolink.Domain.Enums;

namespace Ecolink.Domain.Entities;

/// <summary>
/// Nhật ký phiên quét phân loại rác bằng AI (U-01, U-04, BR-CLS-03, BR-CLS-05).
/// </summary>
public class AiScanHistory : BaseAuditableEntity
{
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string ImageUrl { get; set; } = string.Empty;
    public string? ImageStorageKey { get; set; }
    public string ImageSha256 { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string ModelUsed { get; set; } = "yolov8_detect";
    public string PredictedLabel { get; set; } = "unknown";
    public double ConfidenceScore { get; set; }
    public bool IsOutOfDistribution { get; set; }
    public string DetectedObjectsJson { get; set; } = "[]";
    public string ScanStatus { get; set; } = "Completed";
}

/// <summary>
/// Mẫu ảnh trong kho gán nhãn Ground-Truth &amp; Active Learning (S-01, S-02, BR-CLS-06, BR-CLS-07).
/// </summary>
public class AiDatasetItem : BaseAuditableEntity
{
    public DatasetItemSourceType SourceType { get; set; } = DatasetItemSourceType.UserFeedback;
    public Guid? SourceScanId { get; set; }
    public AiScanHistory? SourceScan { get; set; }

    public Guid? SourceFeedbackId { get; set; }
    public AIFeedbackLog? SourceFeedback { get; set; }

    public string ImageUrl { get; set; } = string.Empty;
    public string ImageSha256 { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string? PredictedLabel { get; set; }
    public double? PredictedConfidence { get; set; }

    public Guid? GroundTruthCategoryId { get; set; }
    public WasteCategory? GroundTruthCategory { get; set; }

    public string? GroundTruthLabel { get; set; }
    public string YoloAnnotationsJson { get; set; } = "[]";
    public DatasetItemStatus Status { get; set; } = DatasetItemStatus.Unlabeled;
    public DatasetSplitType? StickySplit { get; set; }

    public Guid? LabeledByStaffId { get; set; }
    public User? LabeledByStaff { get; set; }
    public DateTime? LabeledAt { get; set; }
    public string? QualityNote { get; set; }

    public ICollection<AiDatasetVersionItem> VersionItems { get; set; } = new List<AiDatasetVersionItem>();
}

/// <summary>
/// Phiên bản tập dữ liệu huấn luyện &amp; kiểm chứng chính thức Train/Val/Test 70/20/10 (S-03, BR-CLS-07).
/// </summary>
public class AiDatasetVersion : BaseAuditableEntity
{
    public string VersionCode { get; set; } = string.Empty;
    public string VersionName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int TrainRatio { get; set; } = 70;
    public int ValRatio { get; set; } = 20;
    public int TestRatio { get; set; } = 10;

    public int TotalSamples { get; set; }
    public int TrainCount { get; set; }
    public int ValCount { get; set; }
    public int TestCount { get; set; }

    public string ClassDistributionJson { get; set; } = "{}";
    public decimal ImbalanceRatio { get; set; } = 1.00m;
    public bool IsBalanced { get; set; } = true;
    public DatasetVersionStatus Status { get; set; } = DatasetVersionStatus.Published;
    public DateTime? SyncedToAiAt { get; set; }

    public Guid CreatedByStaffId { get; set; }
    public User? CreatedByStaff { get; set; }

    public ICollection<AiDatasetVersionItem> Items { get; set; } = new List<AiDatasetVersionItem>();
}

/// <summary>
/// Bản ghi chụp nhanh từng mẫu ảnh thuộc một phiên bản Dataset cố định (S-03).
/// </summary>
public class AiDatasetVersionItem : BaseEntity
{
    public Guid VersionId { get; set; }
    public AiDatasetVersion? Version { get; set; }

    public Guid DatasetItemId { get; set; }
    public AiDatasetItem? DatasetItem { get; set; }

    public DatasetSplitType AssignedSplit { get; set; }
    public string CategoryCodeSnapshot { get; set; } = string.Empty;
    public string YoloAnnotationsSnapshot { get; set; } = "[]";
}
