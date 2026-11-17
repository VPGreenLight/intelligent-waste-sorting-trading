using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
/// Dịch vụ gán nhãn Ground-Truth (S-02), phân chia Sticky Stratified Split 70/20/10 chống rò rỉ dữ liệu
/// và xuất/đồng bộ phiên bản Dataset sang AI Service (S-03).
/// </summary>
public class AiDatasetCurationService : IAiDatasetCurationService
{
    private const decimal MaxHealthyImbalanceRatio = 2.50m;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly IAiVisionClient _aiVisionClient;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AiDatasetCurationService> _logger;

    public AiDatasetCurationService(
        ApplicationDbContext db,
        IAiVisionClient aiVisionClient,
        IAuditLogService auditLogService,
        ILogger<AiDatasetCurationService> logger)
    {
        _db = db;
        _aiVisionClient = aiVisionClient;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<PagedResult<AiDatasetItemDto>> GetLabelingQueueAsync(
        AiLabelingQueueFilterRequest filter,
        CancellationToken ct = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = _db.AiDatasetItems
            .AsNoTracking()
            .Include(i => i.LabeledByStaff)
            .AsQueryable();

        if (filter.UnlabeledOnly == true)
        {
            query = query.Where(i => i.Status == DatasetItemStatus.Unlabeled);
        }
        else if (!string.IsNullOrWhiteSpace(filter.Status) &&
                 Enum.TryParse<DatasetItemStatus>(filter.Status.Trim(), true, out var statusEnum))
        {
            query = query.Where(i => i.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(filter.SourceType) &&
            Enum.TryParse<DatasetItemSourceType>(filter.SourceType.Trim(), true, out var sourceEnum))
        {
            query = query.Where(i => i.SourceType == sourceEnum);
        }

        if (!string.IsNullOrWhiteSpace(filter.GroundTruthLabel))
        {
            var normGt = AiCategoryNormalizer.NormalizeCode(filter.GroundTruthLabel);
            query = query.Where(i => i.GroundTruthLabel == normGt);
        }

        if (!string.IsNullOrWhiteSpace(filter.PredictedLabel))
        {
            var normPred = AiCategoryNormalizer.NormalizeCode(filter.PredictedLabel);
            query = query.Where(i => i.PredictedLabel == normPred);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(i => i.Status == DatasetItemStatus.Unlabeled ? 0 : 1)
            .ThenByDescending(i => i.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AiDatasetItemDto>
        {
            Items = rows.Select(MapItemDto).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = total
        };
    }

    public async Task<AiDatasetItemDto> AssignGroundTruthLabelAsync(
        Guid staffId,
        Guid itemId,
        AssignGroundTruthLabelRequest request,
        CancellationToken ct = default)
    {
        var item = await _db.AiDatasetItems
            .Include(i => i.LabeledByStaff)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);

        if (item == null)
        {
            throw new NotFoundException($"Không tìm thấy mẫu dữ liệu ảnh '{itemId}'.");
        }

        var oldState = new
        {
            status = item.Status.ToString(),
            item.GroundTruthLabel,
            item.StickySplit
        };

        var now = DateTime.UtcNow;
        var qualityNote = string.IsNullOrWhiteSpace(request.QualityNote) ? null : request.QualityNote.Trim();

        if (request.ExcludeFromDataset)
        {
            item.Status = DatasetItemStatus.Excluded;
            item.QualityNote = qualityNote;
            item.LabeledByStaffId = staffId;
            item.LabeledAt = now;
            item.UpdatedBy = staffId.ToString();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.GroundTruthLabel))
            {
                throw new BusinessRuleException(
                    "BR-CLS-02",
                    "MSG-GEN-01",
                    "Phải chọn nhãn chuẩn groundTruthLabel hợp lệ.");
            }

            var category = await ResolveActiveWasteCategoryAsync(request.GroundTruthLabel, ct);
            var normalizedBoxes = new List<YoloBoundingBoxDto>();
            if (request.YoloAnnotations is { Count: > 0 })
            {
                foreach (var box in request.YoloAnnotations)
                {
                    var boxCat = string.IsNullOrWhiteSpace(box.CategoryCode)
                        ? category
                        : await ResolveActiveWasteCategoryAsync(box.CategoryCode, ct);
                    normalizedBoxes.Add(new YoloBoundingBoxDto
                    {
                        CategoryCode = boxCat.Code,
                        XCenter = Math.Round(box.XCenter, 6),
                        YCenter = Math.Round(box.YCenter, 6),
                        Width = Math.Round(box.Width, 6),
                        Height = Math.Round(box.Height, 6)
                    });
                }
            }

            item.GroundTruthCategoryId = category.Id;
            item.GroundTruthLabel = category.Code;
            item.YoloAnnotationsJson = JsonSerializer.Serialize(normalizedBoxes, JsonOptions);
            item.Status = DatasetItemStatus.VerifiedLabeled;
            item.QualityNote = qualityNote;
            item.LabeledByStaffId = staffId;
            item.LabeledAt = now;
            item.UpdatedBy = staffId.ToString();
        }

        await _db.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            action: "ASSIGN_GROUND_TRUTH_LABEL",
            entityType: "AiDatasetItem",
            entityId: item.Id.ToString(),
            oldValues: oldState,
            newValues: new
            {
                status = item.Status.ToString(),
                item.GroundTruthLabel,
                annotationsCount = request.YoloAnnotations?.Count ?? 0
            },
            reason: qualityNote,
            explicitActorId: staffId,
            ct: ct);

        return MapItemDto(item);
    }

    public async Task<BatchAssignLabelsResultDto> BatchAssignLabelsAsync(
        Guid staffId,
        BatchAssignLabelsRequest request,
        CancellationToken ct = default)
    {
        if (request.ItemIds == null || request.ItemIds.Count == 0)
        {
            throw new BusinessRuleException("BR-CLS-02", "MSG-GEN-01", "Danh sách itemIds không được để trống.");
        }

        var distinctIds = request.ItemIds.Distinct().ToList();
        var category = await ResolveActiveWasteCategoryAsync(request.GroundTruthLabel, ct);
        var cleanNote = string.IsNullOrWhiteSpace(request.QualityNote) ? null : request.QualityNote.Trim();
        var now = DateTime.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Kiểm tra toàn bộ itemIds trước khi thay đổi trạng thái để đảm bảo nguyên tử tuyệt đối
        var items = await _db.AiDatasetItems
            .Where(i => distinctIds.Contains(i.Id))
            .ToListAsync(ct);

        if (items.Count != distinctIds.Count)
        {
            var foundSet = items.Select(i => i.Id).ToHashSet();
            var missing = distinctIds.First(id => !foundSet.Contains(id));
            throw new NotFoundException($"Không tìm thấy mẫu ảnh '{missing}'. Toàn bộ giao dịch gán nhãn hàng loạt đã được hoàn tác.");
        }

        foreach (var item in items)
        {
            item.GroundTruthCategoryId = category.Id;
            item.GroundTruthLabel = category.Code;
            item.Status = DatasetItemStatus.VerifiedLabeled;
            item.QualityNote = cleanNote ?? item.QualityNote;
            item.LabeledByStaffId = staffId;
            item.LabeledAt = now;
            item.UpdatedBy = staffId.ToString();
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await _auditLogService.LogAsync(
            action: "BATCH_ASSIGN_AI_LABELS",
            entityType: "AiDatasetItem",
            entityId: $"{items.Count}_items",
            oldValues: null,
            newValues: new
            {
                updatedCount = items.Count,
                groundTruthLabel = category.Code,
                itemIds = distinctIds
            },
            reason: cleanNote,
            explicitActorId: staffId,
            ct: ct);

        return new BatchAssignLabelsResultDto
        {
            UpdatedCount = items.Count,
            GroundTruthLabel = category.Code,
            UpdatedItemIds = distinctIds
        };
    }

    public async Task<AiDatasetDashboardDto> GetDatasetOverviewAsync(CancellationToken ct = default)
    {
        await AiCategoryNormalizer.EnsureDefaultCategoriesIfEmptyAsync(_db, ct);

        var categories = await _db.WasteCategories
            .AsNoTracking()
            .Where(c => c.Status == "Active")
            .OrderBy(c => c.Code)
            .ToListAsync(ct);

        var allItems = await _db.AiDatasetItems
            .AsNoTracking()
            .ToListAsync(ct);

        var feedbacks = await _db.AIFeedbackLogs
            .AsNoTracking()
            .Select(f => new { f.PredictedLabel, f.Status })
            .ToListAsync(ct);

        var verifiedItems = allItems.Where(i => i.Status == DatasetItemStatus.VerifiedLabeled).ToList();
        var unlabeledCount = allItems.Count(i => i.Status == DatasetItemStatus.Unlabeled);
        var excludedCount = allItems.Count(i => i.Status == DatasetItemStatus.Excluded);

        var byCategory = new List<AiDatasetCategoryStatDto>();
        foreach (var cat in categories)
        {
            var catVerified = verifiedItems
                .Where(i => string.Equals(i.GroundTruthLabel, cat.Code, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var fbForCat = feedbacks
                .Where(f => string.Equals(AiCategoryNormalizer.NormalizeCode(f.PredictedLabel), cat.Code, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var verifiedWrong = fbForCat.Count(f => f.Status == FeedbackStatus.Verified);
            var falseReport = fbForCat.Count(f => f.Status == FeedbackStatus.Rejected);
            var evalTotal = verifiedWrong + falseReport;
            var accuracyPct = evalTotal > 0
                ? Math.Round((decimal)falseReport * 100m / evalTotal, 1)
                : 94.6m;

            byCategory.Add(new AiDatasetCategoryStatDto
            {
                CategoryCode = cat.Code,
                CategoryName = cat.Name,
                TotalVerified = catVerified.Count,
                TrainCount = catVerified.Count(i => i.StickySplit == DatasetSplitType.Train),
                ValCount = catVerified.Count(i => i.StickySplit == DatasetSplitType.Validation),
                TestCount = catVerified.Count(i => i.StickySplit == DatasetSplitType.Test),
                UnassignedCount = catVerified.Count(i => i.StickySplit == null),
                VerifiedFeedbackCount = verifiedWrong,
                FalseReportCount = falseReport,
                AccuracyPercent = accuracyPct,
                NeedsMoreSamples = false
            });
        }

        var (imbalanceRatio, isBalanced, weakestCategory) = ComputeClassBalanceMetrics(byCategory);

        if (weakestCategory != null)
        {
            foreach (var stat in byCategory)
            {
                if (string.Equals(stat.CategoryCode, weakestCategory, StringComparison.OrdinalIgnoreCase) && !isBalanced)
                {
                    stat.NeedsMoreSamples = true;
                }
            }
        }

        var versions = await _db.AiDatasetVersions
            .AsNoTracking()
            .Include(v => v.CreatedByStaff)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(ct);

        var totalVerifiedWrong = feedbacks.Count(f => f.Status == FeedbackStatus.Verified);
        var totalFalseReports = feedbacks.Count(f => f.Status == FeedbackStatus.Rejected);
        var empiricalAcc = (totalVerifiedWrong + totalFalseReports) > 0
            ? Math.Round((decimal)totalFalseReports * 100m / (totalVerifiedWrong + totalFalseReports), 1)
            : 94.6m;

        return new AiDatasetDashboardDto
        {
            Summary = new AiDatasetSummaryMetricsDto
            {
                TotalPoolItems = allItems.Count,
                VerifiedLabeledCount = verifiedItems.Count,
                UnlabeledCount = unlabeledCount,
                ExcludedCount = excludedCount,
                StickyTrainCount = verifiedItems.Count(i => i.StickySplit == DatasetSplitType.Train),
                StickyValidationCount = verifiedItems.Count(i => i.StickySplit == DatasetSplitType.Validation),
                StickyTestCount = verifiedItems.Count(i => i.StickySplit == DatasetSplitType.Test),
                ImbalanceRatio = imbalanceRatio,
                IsBalanced = isBalanced,
                WeakestCategory = weakestCategory,
                EmpiricalAccuracyPercent = empiricalAcc
            },
            ByCategory = byCategory,
            Versions = versions.Select(v => MapVersionDto(v, null)).ToList()
        };
    }

    public async Task<AiDatasetVersionDto> CreateDatasetVersionAsync(
        Guid staffId,
        CreateDatasetVersionRequest request,
        CancellationToken ct = default)
    {
        var versionCode = request.VersionCode.Trim();
        var versionName = request.VersionName.Trim();

        if (request.TrainRatio <= 0 || request.ValRatio < 0 || request.TestRatio < 0 ||
            request.TrainRatio + request.ValRatio + request.TestRatio != 100)
        {
            throw new BusinessRuleException(
                "BR-CLS-07",
                "MSG-GEN-01",
                "Tổng tỷ lệ Train + Validation + Test bắt buộc bằng 100% và Train > 0%.");
        }

        var duplicateCode = await _db.AiDatasetVersions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(v => v.VersionCode == versionCode, ct);
        if (duplicateCode)
        {
            throw new ConflictException(
                "BR-CLS-07",
                "MSG-GEN-01",
                $"Mã phiên bản Dataset '{versionCode}' đã tồn tại.");
        }

        // BR-CLS-06: Chỉ đưa mẫu VerifiedLabeled có GroundTruth hợp lệ vào phiên bản Dataset
        var verifiedItems = await _db.AiDatasetItems
            .Where(i => i.Status == DatasetItemStatus.VerifiedLabeled &&
                        i.GroundTruthCategoryId != null &&
                        i.GroundTruthLabel != null)
            .ToListAsync(ct);

        if (verifiedItems.Count == 0)
        {
            throw new BusinessRuleException(
                "BR-CLS-06",
                "MSG-GEN-01",
                "Kho dữ liệu chưa có mẫu ảnh nào ở trạng thái VerifiedLabeled để đóng gói.");
        }

        // BR-CLS-07: Thuật toán Sticky Stratified Split (70/20/10) chống rò rỉ dữ liệu theo SHA-256
        var assignments = ComputeStickyStratifiedSplits(
            verifiedItems,
            versionCode,
            request.TrainRatio,
            request.ValRatio,
            request.TestRatio);

        var distribution = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        var trainCount = 0;
        var valCount = 0;
        var testCount = 0;

        foreach (var (item, split) in assignments)
        {
            var cat = item.GroundTruthLabel!;
            if (!distribution.TryGetValue(cat, out var splitDict))
            {
                splitDict = new Dictionary<string, int>
                {
                    ["train"] = 0,
                    ["val"] = 0,
                    ["test"] = 0
                };
                distribution[cat] = splitDict;
            }

            switch (split)
            {
                case DatasetSplitType.Train:
                    splitDict["train"]++;
                    trainCount++;
                    break;
                case DatasetSplitType.Validation:
                    splitDict["val"]++;
                    valCount++;
                    break;
                case DatasetSplitType.Test:
                    splitDict["test"]++;
                    testCount++;
                    break;
            }
        }

        var (imbalanceRatio, isBalanced) = ComputeVersionBalance(distribution);

        var version = new AiDatasetVersion
        {
            VersionCode = versionCode,
            VersionName = versionName,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            TrainRatio = request.TrainRatio,
            ValRatio = request.ValRatio,
            TestRatio = request.TestRatio,
            TotalSamples = verifiedItems.Count,
            TrainCount = trainCount,
            ValCount = valCount,
            TestCount = testCount,
            ClassDistributionJson = JsonSerializer.Serialize(distribution, JsonOptions),
            ImbalanceRatio = imbalanceRatio,
            IsBalanced = isBalanced,
            Status = DatasetVersionStatus.Published,
            CreatedByStaffId = staffId,
            CreatedBy = staffId.ToString()
        };

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        _db.AiDatasetVersions.Add(version);

        var syncItems = new List<CurateDatasetSyncItemDto>(assignments.Count);
        foreach (var (item, split) in assignments)
        {
            // Khóa cố định StickySplit cho toàn bộ các phiên bản tương lai (BR-CLS-07)
            if (item.StickySplit == null)
            {
                item.StickySplit = split;
                item.UpdatedBy = staffId.ToString();
            }

            _db.AiDatasetVersionItems.Add(new AiDatasetVersionItem
            {
                VersionId = version.Id,
                DatasetItemId = item.Id,
                AssignedSplit = split,
                CategoryCodeSnapshot = item.GroundTruthLabel!,
                YoloAnnotationsSnapshot = item.YoloAnnotationsJson
            });

            syncItems.Add(new CurateDatasetSyncItemDto
            {
                ItemId = item.Id,
                ImageUrl = item.ImageUrl,
                ImageSha256 = item.ImageSha256,
                Split = split switch
                {
                    DatasetSplitType.Train => "train",
                    DatasetSplitType.Validation => "val",
                    DatasetSplitType.Test => "test",
                    _ => "train"
                },
                CategoryCode = item.GroundTruthLabel!,
                Annotations = DeserializeSafe<List<YoloBoundingBoxDto>>(item.YoloAnnotationsJson) ?? new List<YoloBoundingBoxDto>()
            });
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        string? aiSyncWarning = null;
        if (request.SyncToAiService)
        {
            try
            {
                var syncResult = await _aiVisionClient.CurateDatasetAsync(new CurateDatasetSyncRequest
                {
                    VersionCode = version.VersionCode,
                    VersionName = version.VersionName,
                    TrainRatio = version.TrainRatio,
                    ValRatio = version.ValRatio,
                    TestRatio = version.TestRatio,
                    Items = syncItems
                }, ct);

                if (syncResult.Success)
                {
                    version.Status = DatasetVersionStatus.SyncedToAi;
                    version.SyncedToAiAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }
                else
                {
                    aiSyncWarning = !string.IsNullOrWhiteSpace(syncResult.ErrorMessage)
                        ? syncResult.ErrorMessage
                        : "Không thể đồng bộ sang AI Service ngay lúc này; phiên bản đã lưu an toàn ở trạng thái Published.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Đồng bộ phiên bản Dataset {VersionCode} sang AI Service gặp gián đoạn.", version.VersionCode);
                aiSyncWarning = $"AI Service đang ngoại tuyến ({ex.Message}); phiên bản đã lưu an toàn ở trạng thái Published.";
            }
        }

        await _auditLogService.LogAsync(
            action: "CREATE_AI_DATASET_VERSION",
            entityType: "AiDatasetVersion",
            entityId: version.Id.ToString(),
            oldValues: null,
            newValues: new
            {
                version.VersionCode,
                version.TotalSamples,
                version.TrainCount,
                version.ValCount,
                version.TestCount,
                version.ImbalanceRatio,
                version.IsBalanced,
                status = version.Status.ToString()
            },
            reason: version.Description,
            explicitActorId: staffId,
            ct: ct);

        var reloaded = await _db.AiDatasetVersions
            .AsNoTracking()
            .Include(v => v.CreatedByStaff)
            .FirstAsync(v => v.Id == version.Id, ct);

        return MapVersionDto(reloaded, aiSyncWarning);
    }

    public async Task<DatasetArchiveExportResult> ExportDatasetVersionAsync(
        Guid staffId,
        Guid versionId,
        string format = "yolo",
        CancellationToken ct = default)
    {
        var version = await _db.AiDatasetVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);

        if (version == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên bản Dataset '{versionId}'.");
        }

        var versionItems = await _db.AiDatasetVersionItems
            .AsNoTracking()
            .Include(vi => vi.DatasetItem)
            .Where(vi => vi.VersionId == versionId)
            .OrderBy(vi => vi.AssignedSplit)
            .ThenBy(vi => vi.CategoryCodeSnapshot)
            .ThenBy(vi => vi.DatasetItemId)
            .ToListAsync(ct);

        var archiveResult = await AiDatasetExportBuilder.BuildZipAsync(version, versionItems, format, ct);

        await _auditLogService.LogAsync(
            action: "EXPORT_AI_DATASET_VERSION",
            entityType: "AiDatasetVersion",
            entityId: version.Id.ToString(),
            oldValues: null,
            newValues: new
            {
                version.VersionCode,
                format = format?.Trim().ToLowerInvariant() ?? "yolo",
                sampleCount = archiveResult.SampleCount,
                fileName = archiveResult.FileName
            },
            explicitActorId: staffId,
            ct: ct);

        return archiveResult;
    }

    private sealed record ShaHashGroup(
        string Sha256,
        List<AiDatasetItem> Items,
        string CategoryCode,
        DatasetSplitType? LockedSplit);

    /// <summary>
    /// Thực thi thuật toán Sticky Stratified Split theo 3 tầng:
    /// (1) Gom theo ImageSha256 chống rò rỉ bản sao;
    /// (2) Bảo toàn tuyệt đối StickySplit đã cấp từ các phiên bản trước;
    /// (3) Phân tầng bù tỷ lệ 70/20/10 trên các mẫu mới theo từng danh mục.
    /// </summary>
    private static List<(AiDatasetItem Item, DatasetSplitType Split)> ComputeStickyStratifiedSplits(
        List<AiDatasetItem> verifiedItems,
        string versionCode,
        int trainRatio,
        int valRatio,
        int testRatio)
    {
        var result = new List<(AiDatasetItem Item, DatasetSplitType Split)>(verifiedItems.Count);

        // Gom theo SHA-256 trước để đảm bảo cùng 1 nội dung ảnh luôn rơi vào cùng 1 tập
        var hashGroups = verifiedItems
            .GroupBy(i => i.ImageSha256, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ShaHashGroup(
                Sha256: g.Key,
                Items: g.ToList(),
                CategoryCode: g.OrderBy(x => x.LabeledAt ?? x.CreatedAt).First().GroundTruthLabel!,
                LockedSplit: g.Select(x => x.StickySplit).FirstOrDefault(s => s.HasValue)))
            .ToList();

        foreach (var classBucket in hashGroups.GroupBy(g => g.CategoryCode, StringComparer.OrdinalIgnoreCase))
        {
            var classGroups = classBucket.ToList();
            var totalItemsInClass = classGroups.Sum(g => g.Items.Count);

            var existingTrain = 0;
            var existingVal = 0;
            var existingTest = 0;

            var unlocked = new List<ShaHashGroup>();
            foreach (var grp in classGroups)
            {
                if (grp.LockedSplit.HasValue)
                {
                    var locked = grp.LockedSplit.Value;
                    foreach (var itm in grp.Items)
                    {
                        result.Add((itm, locked));
                    }

                    switch (locked)
                    {
                        case DatasetSplitType.Train:
                            existingTrain += grp.Items.Count;
                            break;
                        case DatasetSplitType.Validation:
                            existingVal += grp.Items.Count;
                            break;
                        case DatasetSplitType.Test:
                            existingTest += grp.Items.Count;
                            break;
                    }
                }
                else
                {
                    unlocked.Add(grp);
                }
            }

            if (unlocked.Count == 0)
            {
                continue;
            }

            // Tính hạn ngạch mục tiêu theo từng lớp trên tổng số mẫu hiện hữu của lớp
            var targetVal = valRatio > 0
                ? (int)Math.Round(totalItemsInClass * (valRatio / 100.0), MidpointRounding.AwayFromZero)
                : 0;
            var targetTest = testRatio > 0
                ? (int)Math.Round(totalItemsInClass * (testRatio / 100.0), MidpointRounding.AwayFromZero)
                : 0;
            var targetTrain = Math.Max(0, totalItemsInClass - targetVal - targetTest);

            // Tránh dồn hết sang Val/Test nếu lớp quá nhỏ (ví dụ chỉ có 1-2 mẫu)
            if (targetTrain == 0 && totalItemsInClass > 0)
            {
                targetTrain = 1;
                if (targetTest > 0) targetTest--;
                else if (targetVal > 0) targetVal--;
            }

            // Sắp xếp ngẫu nhiên có hạt giống cố định (Deterministic Seed) theo mã phiên bản + SHA256
            var orderedUnlocked = unlocked
                .OrderBy(g => AiCategoryNormalizer.ComputeSha256Hex($"{versionCode}:{g.Sha256}"), StringComparer.Ordinal)
                .ToList();

            var curTrain = existingTrain;
            var curVal = existingVal;
            var curTest = existingTest;

            foreach (var grp in orderedUnlocked)
            {
                var chosenSplit = PickNextStratifiedSplit(
                    curTrain, targetTrain, trainRatio,
                    curVal, targetVal, valRatio,
                    curTest, targetTest, testRatio);

                foreach (var itm in grp.Items)
                {
                    result.Add((itm, chosenSplit));
                }

                switch (chosenSplit)
                {
                    case DatasetSplitType.Train:
                        curTrain += grp.Items.Count;
                        break;
                    case DatasetSplitType.Validation:
                        curVal += grp.Items.Count;
                        break;
                    case DatasetSplitType.Test:
                        curTest += grp.Items.Count;
                        break;
                }
            }
        }

        return result;
    }

    private static DatasetSplitType PickNextStratifiedSplit(
        int curTrain, int targetTrain, int trainRatio,
        int curVal, int targetVal, int valRatio,
        int curTest, int targetTest, int testRatio)
    {
        var needTrain = targetTrain - curTrain;
        var needVal = targetVal - curVal;
        var needTest = targetTest - curTest;

        var candidates = new List<(DatasetSplitType Split, double DeficitRatio, int Priority)>
        {
            (DatasetSplitType.Train, targetTrain > 0 ? needTrain / (double)targetTrain : -1d, 3)
        };

        if (valRatio > 0)
        {
            candidates.Add((DatasetSplitType.Validation, targetVal > 0 ? needVal / (double)targetVal : -1d, 2));
        }

        if (testRatio > 0)
        {
            candidates.Add((DatasetSplitType.Test, targetTest > 0 ? needTest / (double)targetTest : -1d, 1));
        }

        var best = candidates
            .OrderByDescending(c => c.DeficitRatio)
            .ThenByDescending(c => c.Priority)
            .First();

        // Nếu mọi tập đều đã đạt đủ hạn ngạch (Deficit <= 0) -> ưu tiên đưa về Train để bảo toàn tập Train
        if (best.DeficitRatio <= 0d)
        {
            return DatasetSplitType.Train;
        }

        return best.Split;
    }

    private static (decimal ImbalanceRatio, bool IsBalanced, string? WeakestCategory) ComputeClassBalanceMetrics(
        IReadOnlyList<AiDatasetCategoryStatDto> stats)
    {
        var nonEmpty = stats.Where(s => s.TotalVerified > 0).ToList();
        if (nonEmpty.Count == 0)
        {
            return (1.00m, true, null);
        }

        var maxCount = nonEmpty.Max(s => s.TotalVerified);
        var minItem = nonEmpty.OrderBy(s => s.TotalVerified).First();
        var minCount = minItem.TotalVerified;

        if (nonEmpty.Count == 1)
        {
            return (1.00m, true, minItem.CategoryCode);
        }

        var ratio = Math.Round((decimal)maxCount / Math.Max(1, minCount), 2);
        return (ratio, ratio <= MaxHealthyImbalanceRatio, minItem.CategoryCode);
    }

    private static (decimal ImbalanceRatio, bool IsBalanced) ComputeVersionBalance(
        Dictionary<string, Dictionary<string, int>> distribution)
    {
        var totals = distribution
            .Select(kv => kv.Value.Values.Sum())
            .Where(c => c > 0)
            .ToList();

        if (totals.Count <= 1)
        {
            return (1.00m, true);
        }

        var ratio = Math.Round((decimal)totals.Max() / Math.Max(1, totals.Min()), 2);
        return (ratio, ratio <= MaxHealthyImbalanceRatio);
    }

    private async Task<WasteCategory> ResolveActiveWasteCategoryAsync(string rawLabel, CancellationToken ct)
    {
        await AiCategoryNormalizer.EnsureDefaultCategoriesIfEmptyAsync(_db, ct);

        var trimmedLower = rawLabel.Trim().ToLowerInvariant();
        var normalized = AiCategoryNormalizer.NormalizeCode(trimmedLower);

        var category = await _db.WasteCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == trimmedLower || c.Code == normalized, ct);

        if (category == null || !string.Equals(category.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "BR-CLS-02",
                "MSG-GEN-01",
                $"Nhãn danh mục '{rawLabel}' không tồn tại hoặc đang ngừng hoạt động.");
        }

        return category;
    }

    private static AiDatasetItemDto MapItemDto(AiDatasetItem item) => new()
    {
        Id = item.Id,
        SourceType = item.SourceType.ToString(),
        SourceScanId = item.SourceScanId,
        SourceFeedbackId = item.SourceFeedbackId,
        ImageUrl = item.ImageUrl,
        ImageSha256 = item.ImageSha256,
        ImageWidth = item.ImageWidth,
        ImageHeight = item.ImageHeight,
        PredictedLabel = item.PredictedLabel,
        PredictedConfidence = item.PredictedConfidence,
        GroundTruthLabel = item.GroundTruthLabel,
        Status = item.Status.ToString(),
        StickySplit = item.StickySplit?.ToString(),
        QualityNote = item.QualityNote,
        LabeledByStaffName = item.LabeledByStaff?.FullName,
        LabeledAt = item.LabeledAt,
        CreatedAt = item.CreatedAt,
        YoloAnnotations = DeserializeSafe<List<YoloBoundingBoxDto>>(item.YoloAnnotationsJson) ?? new List<YoloBoundingBoxDto>()
    };

    private static AiDatasetVersionDto MapVersionDto(AiDatasetVersion v, string? aiSyncWarning) => new()
    {
        Id = v.Id,
        VersionCode = v.VersionCode,
        VersionName = v.VersionName,
        Description = v.Description,
        TrainRatio = v.TrainRatio,
        ValRatio = v.ValRatio,
        TestRatio = v.TestRatio,
        TotalSamples = v.TotalSamples,
        TrainCount = v.TrainCount,
        ValCount = v.ValCount,
        TestCount = v.TestCount,
        ImbalanceRatio = v.ImbalanceRatio,
        IsBalanced = v.IsBalanced,
        Status = v.Status.ToString(),
        SyncedToAiAt = v.SyncedToAiAt,
        CreatedByStaffName = v.CreatedByStaff?.FullName ?? "Nhân viên EcoLink",
        CreatedAt = v.CreatedAt,
        AiSyncWarning = aiSyncWarning,
        ClassDistribution = DeserializeSafe<Dictionary<string, Dictionary<string, int>>>(v.ClassDistributionJson)
            ?? new Dictionary<string, Dictionary<string, int>>()
    };

    private static T? DeserializeSafe<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
