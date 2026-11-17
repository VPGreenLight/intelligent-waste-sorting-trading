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
/// Quản lý nhật ký phiên quét ảnh gốc, báo cáo phân loại sai (U-04) và hàng đợi thẩm định của Staff (S-01).
/// Quy tắc: BR-CLS-02, BR-CLS-03, BR-CLS-05, BR-CLS-06, BR-CLS-08, BR-ADM-05.
/// </summary>
public class AiFeedbackService : IAiFeedbackService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly IAiVisionClient _aiVisionClient;
    private readonly IFileStorageService _fileStorageService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AiFeedbackService> _logger;

    public AiFeedbackService(
        ApplicationDbContext db,
        IAiVisionClient aiVisionClient,
        IFileStorageService fileStorageService,
        IAuditLogService auditLogService,
        ILogger<AiFeedbackService> logger)
    {
        _db = db;
        _aiVisionClient = aiVisionClient;
        _fileStorageService = fileStorageService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<(Guid ScanId, string ImageUrl)> RecordScanHistoryAsync(
        byte[] imageBytes,
        string fileName,
        string? contentType,
        Guid? userId,
        AiScanResultDto aiResult,
        CancellationToken ct = default)
    {
        var sha256 = AiCategoryNormalizer.ComputeSha256Hex(imageBytes);
        var ext = AiCategoryNormalizer.SanitizeFileExtension(fileName);
        var folderPrefix = $"ai-scans/{DateTime.UtcNow:yyyy/MM}";
        var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType;

        string imageUrl;
        string? storageKey = null;

        try
        {
            await using var uploadStream = new MemoryStream(imageBytes, writable: false);
            var uploaded = await _fileStorageService.UploadRecyclerDocumentAsync(
                uploadStream,
                $"{sha256[..16]}{ext}",
                resolvedContentType,
                folderPrefix,
                ct);

            imageUrl = string.IsNullOrWhiteSpace(uploaded.FileUrl)
                ? $"local://{folderPrefix}/{sha256[..16]}{ext}"
                : uploaded.FileUrl;
            storageKey = uploaded.ObjectKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể tải ảnh phiên quét lên R2, sử dụng định danh cục bộ an toàn.");
            imageUrl = $"local://{folderPrefix}/{sha256[..16]}{ext}";
        }

        var predictedRaw = aiResult.TopPrediction?.Label ?? "unknown";
        var predictedNormalized = string.Equals(predictedRaw, "unknown", StringComparison.OrdinalIgnoreCase)
            ? "unknown"
            : AiCategoryNormalizer.NormalizeCode(predictedRaw);

        var confidence = aiResult.TopPrediction?.Confidence ?? 0d;
        var isOod = aiResult.IsOutOfDistribution || confidence < aiResult.ConfidenceThreshold;
        var detectedJson = JsonSerializer.Serialize(aiResult.DetectedObjects ?? new List<DetectedWasteObjectDto>(), JsonOptions);

        var scan = new AiScanHistory
        {
            UserId = userId,
            ImageUrl = imageUrl,
            ImageStorageKey = storageKey,
            ImageSha256 = sha256,
            ImageWidth = aiResult.ImageWidth,
            ImageHeight = aiResult.ImageHeight,
            ModelUsed = string.IsNullOrWhiteSpace(aiResult.ModelUsed) ? "yolov8_detect" : aiResult.ModelUsed.Trim(),
            PredictedLabel = string.IsNullOrWhiteSpace(predictedNormalized) ? "unknown" : predictedNormalized,
            ConfidenceScore = confidence,
            IsOutOfDistribution = isOod,
            DetectedObjectsJson = detectedJson,
            ScanStatus = isOod ? "LowConfidence" : "Completed",
            CreatedBy = userId?.ToString() ?? "Anonymous"
        };

        _db.AiScanHistories.Add(scan);
        await _db.SaveChangesAsync(ct);

        // BR-CLS-03: Tự động thu thập mẫu OOD / độ tin cậy thấp (< 60%) vào hàng đợi gán nhãn S-02
        if (isOod)
        {
            var existingCandidate = await _db.AiDatasetItems
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.ImageSha256 == sha256, ct);

            if (existingCandidate == null)
            {
                _db.AiDatasetItems.Add(new AiDatasetItem
                {
                    SourceType = DatasetItemSourceType.LowConfidenceAuto,
                    SourceScanId = scan.Id,
                    ImageUrl = imageUrl,
                    ImageSha256 = sha256,
                    ImageWidth = aiResult.ImageWidth,
                    ImageHeight = aiResult.ImageHeight,
                    PredictedLabel = scan.PredictedLabel,
                    PredictedConfidence = confidence,
                    Status = DatasetItemStatus.Unlabeled,
                    CreatedBy = "ActiveLearningAuto"
                });
                await _db.SaveChangesAsync(ct);
            }
        }

        return (scan.Id, imageUrl);
    }

    public async Task<SubmitAiFeedbackResponseDto> SubmitFeedbackAsync(
        Guid? userId,
        SubmitFeedbackDto request,
        CancellationToken ct = default)
    {
        var category = await ResolveActiveWasteCategoryAsync(request.UserReportedLabel, ct);

        AiScanHistory? scan = null;
        if (request.ScanId.HasValue)
        {
            var duplicate = await _db.AIFeedbackLogs
                .AsNoTracking()
                .AnyAsync(f => f.ScanId == request.ScanId.Value, ct);
            if (duplicate)
            {
                throw new ConflictException(
                    "BR-CLS-05",
                    "MSG-CLS-06",
                    "Phiên quét này đã được gửi báo cáo phân loại sai trước đó.");
            }

            scan = await _db.AiScanHistories
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.ScanId.Value, ct);
            if (scan == null)
            {
                throw new NotFoundException($"Không tìm thấy phiên quét ảnh '{request.ScanId.Value}'.");
            }
        }

        var resolvedImageUrl = !string.IsNullOrWhiteSpace(request.ImageUrl)
            ? request.ImageUrl.Trim()
            : scan?.ImageUrl ?? "local://unlinked-feedback";

        var resolvedPredicted = !string.IsNullOrWhiteSpace(request.PredictedLabel)
            ? AiCategoryNormalizer.NormalizeCode(request.PredictedLabel)
            : scan?.PredictedLabel ?? "unknown";

        var resolvedConfidence = request.ConfidenceScore > 0d
            ? request.ConfidenceScore
            : scan?.ConfidenceScore ?? 0d;

        var log = new AIFeedbackLog
        {
            UserId = userId,
            ScanId = scan?.Id,
            ImageUrl = resolvedImageUrl,
            ImageSha256 = scan?.ImageSha256,
            ModelUsed = scan?.ModelUsed ?? "yolov8_detect",
            DetectedObjectsSnapshot = scan?.DetectedObjectsJson ?? "[]",
            PredictedLabel = string.IsNullOrWhiteSpace(resolvedPredicted) ? "unknown" : resolvedPredicted,
            ConfidenceScore = resolvedConfidence,
            UserReportedLabel = category.Code,
            Status = FeedbackStatus.Pending,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };

        _db.AIFeedbackLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        var syncedToAi = false;
        try
        {
            syncedToAi = await _aiVisionClient.SubmitFeedbackAsync(new SubmitFeedbackDto
            {
                ScanId = log.ScanId,
                ImageUrl = log.ImageUrl,
                PredictedLabel = log.PredictedLabel,
                ConfidenceScore = log.ConfidenceScore,
                UserReportedLabel = log.UserReportedLabel,
                Note = log.Note
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Đồng bộ phản hồi AI sang FastAPI thất bại nhưng bản ghi CSDL {ReportId} đã an toàn.", log.Id);
        }

        return new SubmitAiFeedbackResponseDto
        {
            ReportId = log.Id,
            ScanId = log.ScanId,
            Status = AiCategoryNormalizer.FormatFeedbackStatus(log.Status),
            NormalizedLabel = category.Code,
            SyncedToAi = syncedToAi
        };
    }

    public async Task<PagedResult<MyAiFeedbackReportDto>> GetMyFeedbackReportsAsync(
        Guid userId,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // BR-CLS-08: Cô lập truy vấn báo cáo cá nhân theo UserId
        var query = _db.AIFeedbackLogs
            .AsNoTracking()
            .Where(f => f.UserId == userId);

        if (AiCategoryNormalizer.TryParseFeedbackStatus(status, out var parsedStatus))
        {
            query = query.Where(f => f.Status == parsedStatus);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<MyAiFeedbackReportDto>
        {
            Items = rows.Select(f => new MyAiFeedbackReportDto
            {
                Id = f.Id,
                ScanId = f.ScanId,
                ImageUrl = f.ImageUrl,
                ModelUsed = f.ModelUsed,
                PredictedLabel = f.PredictedLabel,
                ConfidenceScore = f.ConfidenceScore,
                UserReportedLabel = f.UserReportedLabel,
                AdminVerifiedLabel = f.AdminVerifiedLabel,
                Status = AiCategoryNormalizer.FormatFeedbackStatus(f.Status),
                ReviewDecision = f.ReviewDecision?.ToString(),
                Note = f.Note,
                ReviewNote = f.ReviewNote,
                CreatedAt = f.CreatedAt,
                ReviewedAt = f.ReviewedAt
            }).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = total
        };
    }

    public async Task<StaffAiFeedbackQueueDto> GetStaffFeedbackQueueAsync(
        StaffAiFeedbackFilterRequest filter,
        CancellationToken ct = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var pendingCount = await _db.AIFeedbackLogs.AsNoTracking().CountAsync(f => f.Status == FeedbackStatus.Pending, ct);
        var reviewedCount = await _db.AIFeedbackLogs.AsNoTracking().CountAsync(f => f.Status == FeedbackStatus.Verified, ct);
        var rejectedCount = await _db.AIFeedbackLogs.AsNoTracking().CountAsync(f => f.Status == FeedbackStatus.Rejected, ct);

        var query = _db.AIFeedbackLogs
            .AsNoTracking()
            .Include(f => f.User)
            .AsQueryable();

        if (AiCategoryNormalizer.TryParseFeedbackStatus(filter.Status, out var statusEnum))
        {
            query = query.Where(f => f.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(filter.PredictedLabel))
        {
            var normPred = AiCategoryNormalizer.NormalizeCode(filter.PredictedLabel);
            query = query.Where(f => f.PredictedLabel == normPred || f.PredictedLabel == filter.PredictedLabel.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(filter.UserReportedLabel))
        {
            var normRep = AiCategoryNormalizer.NormalizeCode(filter.UserReportedLabel);
            query = query.Where(f => f.UserReportedLabel == normRep || f.UserReportedLabel == filter.UserReportedLabel.Trim().ToLowerInvariant());
        }

        if (filter.MaxConfidence.HasValue)
        {
            query = query.Where(f => f.ConfidenceScore <= filter.MaxConfidence.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(f =>
                (f.Note != null && f.Note.ToLower().Contains(term)) ||
                f.PredictedLabel.ToLower().Contains(term) ||
                f.UserReportedLabel.ToLower().Contains(term) ||
                (f.User != null && f.User.FullName.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(f => f.Status == FeedbackStatus.Pending ? 0 : 1)
            .ThenByDescending(f => f.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var itemIds = rows.Where(r => r.DatasetItemId.HasValue).Select(r => r.DatasetItemId!.Value).Distinct().ToList();
        var verifiedDatasetIds = itemIds.Count > 0
            ? (await _db.AiDatasetItems.AsNoTracking()
                .Where(d => itemIds.Contains(d.Id) && d.Status == DatasetItemStatus.VerifiedLabeled)
                .Select(d => d.Id)
                .ToListAsync(ct)).ToHashSet()
            : new HashSet<Guid>();

        return new StaffAiFeedbackQueueDto
        {
            Items = rows.Select(f => new StaffAiFeedbackSummaryDto
            {
                Id = f.Id,
                ScanId = f.ScanId,
                ImageUrl = f.ImageUrl,
                ModelUsed = f.ModelUsed,
                PredictedLabel = f.PredictedLabel,
                ConfidenceScore = f.ConfidenceScore,
                UserReportedLabel = f.UserReportedLabel,
                AdminVerifiedLabel = f.AdminVerifiedLabel,
                Status = AiCategoryNormalizer.FormatFeedbackStatus(f.Status),
                ReviewDecision = f.ReviewDecision?.ToString(),
                ReporterName = f.User?.FullName ?? "Người dùng ẩn danh",
                Note = f.Note,
                ReviewNote = f.ReviewNote,
                DatasetItemId = f.DatasetItemId,
                InDataset = f.DatasetItemId.HasValue && verifiedDatasetIds.Contains(f.DatasetItemId.Value),
                CreatedAt = f.CreatedAt,
                ReviewedAt = f.ReviewedAt
            }).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = total,
            PendingCount = pendingCount,
            ReviewedCount = reviewedCount,
            RejectedCount = rejectedCount
        };
    }

    public async Task<StaffAiFeedbackDetailDto> GetStaffFeedbackDetailAsync(Guid reportId, CancellationToken ct = default)
    {
        var log = await _db.AIFeedbackLogs
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.Scan)
            .Include(f => f.ReviewedByStaff)
            .FirstOrDefaultAsync(f => f.Id == reportId, ct);

        if (log == null)
        {
            throw new NotFoundException($"Không tìm thấy báo cáo phân loại AI '{reportId}'.");
        }

        AiDatasetItem? datasetItem = null;
        if (log.DatasetItemId.HasValue)
        {
            datasetItem = await _db.AiDatasetItems
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == log.DatasetItemId.Value, ct);
        }

        return new StaffAiFeedbackDetailDto
        {
            Id = log.Id,
            ScanId = log.ScanId,
            ImageUrl = log.ImageUrl,
            ImageSha256 = log.ImageSha256 ?? log.Scan?.ImageSha256,
            ImageWidth = log.Scan?.ImageWidth ?? datasetItem?.ImageWidth ?? 0,
            ImageHeight = log.Scan?.ImageHeight ?? datasetItem?.ImageHeight ?? 0,
            ModelUsed = log.ModelUsed,
            PredictedLabel = log.PredictedLabel,
            ConfidenceScore = log.ConfidenceScore,
            UserReportedLabel = log.UserReportedLabel,
            AdminVerifiedLabel = log.AdminVerifiedLabel,
            Status = AiCategoryNormalizer.FormatFeedbackStatus(log.Status),
            ReviewDecision = log.ReviewDecision?.ToString(),
            Note = log.Note,
            ReviewNote = log.ReviewNote,
            ReporterName = log.User?.FullName ?? "Người dùng ẩn danh",
            ReporterRole = log.User?.Role.ToString() ?? "Anonymous",
            ReviewedByStaffName = log.ReviewedByStaff?.FullName,
            DatasetItemId = log.DatasetItemId,
            DatasetItemStatus = datasetItem?.Status.ToString(),
            DetectedObjectsSnapshot = DeserializeSafe<List<DetectedWasteObjectDto>>(log.DetectedObjectsSnapshot) ?? new List<DetectedWasteObjectDto>(),
            ExistingYoloAnnotations = datasetItem != null
                ? DeserializeSafe<List<YoloBoundingBoxDto>>(datasetItem.YoloAnnotationsJson) ?? new List<YoloBoundingBoxDto>()
                : new List<YoloBoundingBoxDto>(),
            CreatedAt = log.CreatedAt,
            ReviewedAt = log.ReviewedAt
        };
    }

    public async Task<StaffAiFeedbackDetailDto> ReviewFeedbackAsync(
        Guid staffId,
        Guid reportId,
        ReviewAiFeedbackRequest request,
        CancellationToken ct = default)
    {
        var log = await _db.AIFeedbackLogs
            .Include(f => f.Scan)
            .FirstOrDefaultAsync(f => f.Id == reportId, ct);

        if (log == null)
        {
            throw new NotFoundException($"Không tìm thấy báo cáo phân loại AI '{reportId}'.");
        }

        if (log.Status != FeedbackStatus.Pending)
        {
            throw new ConflictException(
                "BR-CLS-06",
                "MSG-CLS-07",
                "Báo cáo phân loại sai này đã được thẩm định trước đó.");
        }

        if (!Enum.TryParse<AiFeedbackReviewDecision>(request.Decision?.Trim(), true, out var decision))
        {
            throw new BusinessRuleException(
                "BR-CLS-06",
                "MSG-GEN-01",
                "Quyết định thẩm định không hợp lệ (hỗ trợ ApproveAndLabel, ApproveToQueue, Reject).");
        }

        var oldState = new
        {
            status = AiCategoryNormalizer.FormatFeedbackStatus(log.Status),
            log.AdminVerifiedLabel
        };

        var now = DateTime.UtcNow;
        var cleanNote = string.IsNullOrWhiteSpace(request.ReviewNote) ? null : request.ReviewNote.Trim();
        var itemSha = !string.IsNullOrWhiteSpace(log.ImageSha256)
            ? log.ImageSha256
            : AiCategoryNormalizer.ComputeSha256Hex($"feedback:{log.Id}");

        switch (decision)
        {
            case AiFeedbackReviewDecision.Reject:
            {
                if (string.IsNullOrWhiteSpace(cleanNote) || cleanNote.Length < 5)
                {
                    throw new BusinessRuleException(
                        "BR-CLS-06",
                        "MSG-GEN-01",
                        "Khi bác bỏ ảnh (Reject), bắt buộc nhập lý do tối thiểu 5 ký tự (BR-CLS-06).");
                }

                log.Status = FeedbackStatus.Rejected;
                log.ReviewDecision = AiFeedbackReviewDecision.Reject;
                log.ReviewedByStaffId = staffId;
                log.ReviewedAt = now;
                log.ReviewNote = cleanNote;

                // Khóa ảnh bị từ chối nếu đã từng tự động vào hàng đợi OOD (BR-CLS-06)
                var linkedAutoItem = await _db.AiDatasetItems
                    .FirstOrDefaultAsync(i => i.ImageSha256 == itemSha && i.StickySplit == null, ct);
                if (linkedAutoItem != null && linkedAutoItem.Status != DatasetItemStatus.VerifiedLabeled)
                {
                    linkedAutoItem.Status = DatasetItemStatus.Excluded;
                    linkedAutoItem.QualityNote = $"Loại trừ theo Reject feedback: {cleanNote}";
                    linkedAutoItem.UpdatedBy = staffId.ToString();
                }

                break;
            }

            case AiFeedbackReviewDecision.ApproveToQueue:
            {
                var item = await UpsertDatasetItemForFeedbackAsync(log, itemSha, staffId, ct);
                log.Status = FeedbackStatus.Verified;
                log.ReviewDecision = AiFeedbackReviewDecision.ApproveToQueue;
                log.ReviewedByStaffId = staffId;
                log.ReviewedAt = now;
                log.ReviewNote = cleanNote;
                log.DatasetItemId = item.Id;
                break;
            }

            case AiFeedbackReviewDecision.ApproveAndLabel:
            {
                if (string.IsNullOrWhiteSpace(request.VerifiedLabel))
                {
                    throw new BusinessRuleException(
                        "BR-CLS-02",
                        "MSG-GEN-01",
                        "Phải chọn nhãn danh mục chuẩn verifiedLabel khi duyệt theo chế độ ApproveAndLabel.");
                }

                var category = await ResolveActiveWasteCategoryAsync(request.VerifiedLabel, ct);
                var normalizedBoxes = await NormalizeAndValidateYoloBoxesAsync(request.YoloAnnotations, category.Code, ct);

                var item = await UpsertDatasetItemForFeedbackAsync(log, itemSha, staffId, ct);
                item.GroundTruthCategoryId = category.Id;
                item.GroundTruthLabel = category.Code;
                item.Status = DatasetItemStatus.VerifiedLabeled;
                item.YoloAnnotationsJson = JsonSerializer.Serialize(normalizedBoxes, JsonOptions);
                item.LabeledByStaffId = staffId;
                item.LabeledAt = now;
                item.QualityNote = cleanNote ?? item.QualityNote;
                item.UpdatedBy = staffId.ToString();

                log.Status = FeedbackStatus.Verified;
                log.AdminVerifiedLabel = category.Code;
                log.ReviewDecision = AiFeedbackReviewDecision.ApproveAndLabel;
                log.ReviewedByStaffId = staffId;
                log.ReviewedAt = now;
                log.ReviewNote = cleanNote;
                log.DatasetItemId = item.Id;
                break;
            }
        }

        await _db.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            action: "REVIEW_AI_FEEDBACK",
            entityType: "AIFeedbackLog",
            entityId: log.Id.ToString(),
            oldValues: oldState,
            newValues: new
            {
                decision = decision.ToString(),
                status = AiCategoryNormalizer.FormatFeedbackStatus(log.Status),
                verifiedLabel = log.AdminVerifiedLabel,
                datasetItemId = log.DatasetItemId
            },
            reason: cleanNote,
            explicitActorId: staffId,
            ct: ct);

        return await GetStaffFeedbackDetailAsync(log.Id, ct);
    }

    private async Task<AiDatasetItem> UpsertDatasetItemForFeedbackAsync(
        AIFeedbackLog log,
        string sha256,
        Guid staffId,
        CancellationToken ct)
    {
        var existing = await _db.AiDatasetItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.ImageSha256 == sha256, ct);

        if (existing != null)
        {
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
            }

            existing.SourceFeedbackId ??= log.Id;
            existing.SourceScanId ??= log.ScanId;
            return existing;
        }

        var item = new AiDatasetItem
        {
            SourceType = DatasetItemSourceType.UserFeedback,
            SourceScanId = log.ScanId,
            SourceFeedbackId = log.Id,
            ImageUrl = log.ImageUrl,
            ImageSha256 = sha256,
            ImageWidth = log.Scan?.ImageWidth ?? 0,
            ImageHeight = log.Scan?.ImageHeight ?? 0,
            PredictedLabel = log.PredictedLabel,
            PredictedConfidence = log.ConfidenceScore,
            Status = DatasetItemStatus.Unlabeled,
            CreatedBy = staffId.ToString()
        };

        _db.AiDatasetItems.Add(item);
        return item;
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

    private async Task<List<YoloBoundingBoxDto>> NormalizeAndValidateYoloBoxesAsync(
        List<YoloBoundingBoxDto>? boxes,
        string defaultCategoryCode,
        CancellationToken ct)
    {
        if (boxes == null || boxes.Count == 0)
        {
            return new List<YoloBoundingBoxDto>();
        }

        var result = new List<YoloBoundingBoxDto>(boxes.Count);
        foreach (var box in boxes)
        {
            var rawCode = string.IsNullOrWhiteSpace(box.CategoryCode) ? defaultCategoryCode : box.CategoryCode;
            var boxCategory = await ResolveActiveWasteCategoryAsync(rawCode, ct);
            result.Add(new YoloBoundingBoxDto
            {
                CategoryCode = boxCategory.Code,
                XCenter = Math.Round(box.XCenter, 6),
                YCenter = Math.Round(box.YCenter, 6),
                Width = Math.Round(box.Width, 6),
                Height = Math.Round(box.Height, 6)
            });
        }

        return result;
    }

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
