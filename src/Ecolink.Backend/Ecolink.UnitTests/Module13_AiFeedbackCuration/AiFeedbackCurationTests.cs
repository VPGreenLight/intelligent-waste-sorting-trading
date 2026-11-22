using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Application.Validators;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Ecolink.Infrastructure.Services;
using Ecolink.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ecolink.UnitTests.Module13_AiFeedbackCuration;

/// <summary>
/// Bộ kiểm thử đơn vị toàn diện cho Module M-13 / FE-15 (Module 11 trong bộ Report-5.1):
/// AI Misclassification Feedback &amp; Dataset Curation (U-04, S-01, S-02, S-03).
/// Chuẩn hóa 22 test cases (UTC001 -&gt; UTC022: 10 Normal, 8 Abnormal, 4 Boundary)
/// đối chiếu trực tiếp với tài liệu đặc tả 11_unit_test_ai_misclassification_feedback_dataset_curation.md.
/// </summary>
[Trait("Module", "Module13_AiFeedbackCuration")]
public class AiFeedbackCurationTests
{
    private static (
        AiFeedbackService FeedbackSvc,
        AiDatasetCurationService CurationSvc,
        IAiVisionClient AiMock,
        IFileStorageService StorageMock)
        CreateServices(ApplicationDbContext db, Guid actorUserId, string role = "Staff")
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns((Guid?)actorUserId);
        currentUser.Role.Returns(role);
        currentUser.IsAuthenticated.Returns(true);
        currentUser.IpAddress.Returns("127.0.0.1");
        currentUser.UserAgent.Returns("EcoLink-UnitTest/1.0");

        var aiMock = Substitute.For<IAiVisionClient>();
        aiMock.SubmitFeedbackAsync(Arg.Any<SubmitFeedbackDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        aiMock.CurateDatasetAsync(Arg.Any<CurateDatasetSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var req = ci.Arg<CurateDatasetSyncRequest>();
                return Task.FromResult(new CurateDatasetSyncResponseDto
                {
                    Success = true,
                    VersionCode = req.VersionCode,
                    TotalSamples = req.Items.Count,
                    TrainCount = req.Items.Count(x => x.Split == "train"),
                    ValCount = req.Items.Count(x => x.Split == "val"),
                    TestCount = req.Items.Count(x => x.Split == "test"),
                    LeakageDetected = false
                });
            });

        var storageMock = Substitute.For<IFileStorageService>();
        storageMock.UploadRecyclerDocumentAsync(
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new RecyclerDocumentUploadResponseDto
            {
                FileUrl = $"https://cdn.ecolink.test/{ci.ArgAt<string>(3)}/{ci.ArgAt<string>(1)}",
                ObjectKey = $"{ci.ArgAt<string>(3)}/{ci.ArgAt<string>(1)}",
                ContentType = ci.ArgAt<string>(2),
                SizeBytes = 128
            }));

        var auditSvc = new AuditLogService(db, currentUser);
        var feedbackSvc = new AiFeedbackService(
            db,
            aiMock,
            storageMock,
            auditSvc,
            NullLogger<AiFeedbackService>.Instance);

        var curationSvc = new AiDatasetCurationService(
            db,
            aiMock,
            auditSvc,
            NullLogger<AiDatasetCurationService>.Instance);

        return (feedbackSvc, curationSvc, aiMock, storageMock);
    }

    private static async Task<(User Citizen, User Staff)> SeedUsersAndCategoriesAsync(ApplicationDbContext db)
    {
        var citizen = new User
        {
            Username = "citizen_u04",
            Email = "citizen_u04@ecolink.vn",
            FullName = "Nguyễn Văn Dân",
            Role = UserRole.User,
            Status = UserStatus.Active
        };
        var staff = new User
        {
            Username = "staff_ai",
            Email = "staff_ai@ecolink.vn",
            FullName = "Trần Đình Khoa",
            Role = UserRole.Staff,
            Status = UserStatus.Active
        };
        db.Users.AddRange(citizen, staff);
        await db.SaveChangesAsync();
        await AiCategoryNormalizer.EnsureDefaultCategoriesIfEmptyAsync(db, CancellationToken.None);
        return (citizen, staff);
    }

    private static async Task<AiDatasetItem> SeedVerifiedItemAsync(
        ApplicationDbContext db,
        WasteCategory category,
        string uniqueKey,
        DatasetSplitType? stickySplit = null)
    {
        var item = new AiDatasetItem
        {
            SourceType = DatasetItemSourceType.UserFeedback,
            ImageUrl = $"https://cdn.ecolink.test/ai-scans/{uniqueKey}.jpg",
            ImageSha256 = AiCategoryNormalizer.ComputeSha256Hex($"img-{uniqueKey}"),
            ImageWidth = 640,
            ImageHeight = 640,
            PredictedLabel = category.Code,
            PredictedConfidence = 0.85,
            GroundTruthCategoryId = category.Id,
            GroundTruthLabel = category.Code,
            YoloAnnotationsJson = JsonSerializer.Serialize(new[]
            {
                new YoloBoundingBoxDto
                {
                    CategoryCode = category.Code,
                    XCenter = 0.5,
                    YCenter = 0.5,
                    Width = 0.4,
                    Height = 0.6
                }
            }),
            Status = DatasetItemStatus.VerifiedLabeled,
            StickySplit = stickySplit,
            LabeledAt = DateTime.UtcNow
        };
        db.AiDatasetItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    // =========================================================================
    // NORMAL TEST CASES (UTC001 -> UTC010) — 10 TCs
    // =========================================================================

    [Fact]
    public async Task UTC001_RecordScanHistoryAsync_HighConfidenceScan_PersistsHistoryWithSha256WithoutAutoOodItem_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var (citizen, _) = await SeedUsersAndCategoriesAsync(db);
        var (feedbackSvc, _, _, _) = CreateServices(db, citizen.Id, "User");

        var imgBytes = Encoding.UTF8.GetBytes("high-confidence-pet-bottle-bytes");
        var aiResult = new AiScanResultDto
        {
            Success = true,
            IsOutOfDistribution = false,
            ConfidenceThreshold = 0.60,
            ModelUsed = "yolov8_detect",
            ImageWidth = 1280,
            ImageHeight = 720,
            TopPrediction = new WastePredictionDto { Label = "plastic", Confidence = 0.91, DisplayName = "Nhựa" }
        };

        var (scanId, imageUrl) = await feedbackSvc.RecordScanHistoryAsync(imgBytes, "pet.jpg", "image/jpeg", citizen.Id, aiResult);

        scanId.Should().NotBeEmpty();
        imageUrl.Should().StartWith("https://cdn.ecolink.test/ai-scans/");

        var history = await db.AiScanHistories.FirstAsync(s => s.Id == scanId);
        history.ImageSha256.Should().HaveLength(64);
        history.ScanStatus.Should().Be("Completed");
        history.IsOutOfDistribution.Should().BeFalse();
        (await db.AiDatasetItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UTC002_RecordScanHistoryAsync_LowConfidenceOodScan_AutoQueuesLowConfidenceItemAndDeduplicatesSha256_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var (citizen, _) = await SeedUsersAndCategoriesAsync(db);
        var (feedbackSvc, _, _, _) = CreateServices(db, citizen.Id, "User");

        var imgBytes = Encoding.UTF8.GetBytes("ambiguous-ood-photo-002");
        var aiResult = new AiScanResultDto
        {
            Success = true,
            IsOutOfDistribution = true,
            ConfidenceThreshold = 0.60,
            ModelUsed = "efficientnet_b0",
            ImageWidth = 640,
            ImageHeight = 640,
            TopPrediction = new WastePredictionDto { Label = "glass", Confidence = 0.42 }
        };

        await feedbackSvc.RecordScanHistoryAsync(imgBytes, "ood1.jpg", "image/jpeg", citizen.Id, aiResult);
        await feedbackSvc.RecordScanHistoryAsync(imgBytes, "ood2.jpg", "image/jpeg", citizen.Id, aiResult);

        (await db.AiScanHistories.CountAsync()).Should().Be(2);
        var items = await db.AiDatasetItems.ToListAsync();
        items.Should().ContainSingle();
        items[0].SourceType.Should().Be(DatasetItemSourceType.LowConfidenceAuto);
        items[0].Status.Should().Be(DatasetItemStatus.Unlabeled);
    }

    [Fact]
    public async Task UTC003_SubmitFeedbackAsync_ValidScanWithAliasLabel_SavesPendingLogAndSyncsAi_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var (citizen, _) = await SeedUsersAndCategoriesAsync(db);
        var (feedbackSvc, _, aiMock, _) = CreateServices(db, citizen.Id, "User");

        var (scanId, _) = await feedbackSvc.RecordScanHistoryAsync(
            Encoding.UTF8.GetBytes("scan-glass-vs-plastic"),
            "bottle.png",
            "image/png",
            citizen.Id,
            new AiScanResultDto
            {
                Success = true,
                TopPrediction = new WastePredictionDto { Label = "glass", Confidence = 0.67 }
            });

        var res = await feedbackSvc.SubmitFeedbackAsync(citizen.Id, new SubmitFeedbackDto
        {
            ScanId = scanId,
            UserReportedLabel = "plastic_bottle",
            Note = "Chai nhựa trong suốt bị nhầm thành thủy tinh."
        });

        res.NormalizedLabel.Should().Be("plastic");
        res.Status.Should().Be("Pending");
        res.SyncedToAi.Should().BeTrue();
        await aiMock.Received(1).SubmitFeedbackAsync(Arg.Any<SubmitFeedbackDto>(), Arg.Any<CancellationToken>());

        var log = await db.AIFeedbackLogs.FirstAsync(f => f.Id == res.ReportId);
        log.ScanId.Should().Be(scanId);
        log.UserId.Should().Be(citizen.Id);
        log.UserReportedLabel.Should().Be("plastic");
    }

    [Fact]
    public async Task UTC004_GetMyFeedbackReportsAsync_IsolatesReportsByOwnerUserId_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var (citizenA, staff) = await SeedUsersAndCategoriesAsync(db);
        var citizenB = new User
        {
            Username = "citizen_b",
            Email = "citizen_b@ecolink.vn",
            FullName = "Citizen B",
            Role = UserRole.User,
            Status = UserStatus.Active
        };
        db.Users.Add(citizenB);
        await db.SaveChangesAsync();

        var (fbSvcA, _, _, _) = CreateServices(db, citizenA.Id, "User");
        var r1 = await fbSvcA.SubmitFeedbackAsync(citizenA.Id, new SubmitFeedbackDto { PredictedLabel = "glass", UserReportedLabel = "plastic" });
        await fbSvcA.SubmitFeedbackAsync(citizenB.Id, new SubmitFeedbackDto { PredictedLabel = "metal", UserReportedLabel = "paper" });

        var (fbSvcStaff, _, _, _) = CreateServices(db, staff.Id, "Staff");
        await fbSvcStaff.ReviewFeedbackAsync(staff.Id, r1.ReportId, new ReviewAiFeedbackRequest
        {
            Decision = "ApproveAndLabel",
            VerifiedLabel = "plastic"
        });

        var myList = await fbSvcA.GetMyFeedbackReportsAsync(citizenA.Id, status: "Reviewed");
        myList.TotalRecords.Should().Be(1);
        myList.Items.Should().ContainSingle(x => x.Id == r1.ReportId && x.AdminVerifiedLabel == "plastic" && x.Status == "Reviewed");
    }

    [Fact]
    public async Task UTC005_ReviewFeedbackAsync_ApproveAndLabelOneClick_CreatesVerifiedDatasetItemAndAuditLog_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var (citizen, staff) = await SeedUsersAndCategoriesAsync(db);
        var (feedbackSvc, _, _, _) = CreateServices(db, staff.Id, "Staff");

        var (scanId, _) = await feedbackSvc.RecordScanHistoryAsync(
            Encoding.UTF8.GetBytes("paper-bag-sample"),
            "bag.jpg",
            "image/jpeg",
            citizen.Id,
            new AiScanResultDto
            {
                Success = true,
                ImageWidth = 800,
                ImageHeight = 600,
                TopPrediction = new WastePredictionDto { Label = "cardboard", Confidence = 0.65 }
            });

        var submitted = await feedbackSvc.SubmitFeedbackAsync(citizen.Id, new SubmitFeedbackDto
        {
            ScanId = scanId,
            UserReportedLabel = "paper"
        });

        var detail = await feedbackSvc.ReviewFeedbackAsync(staff.Id, submitted.ReportId, new ReviewAiFeedbackRequest
        {
            Decision = "ApproveAndLabel",
            VerifiedLabel = "paper",
            ReviewNote = "Đúng là túi giấy kraft mỏng.",
            YoloAnnotations = new List<YoloBoundingBoxDto>
            {
                new() { CategoryCode = "paper", XCenter = 0.5, YCenter = 0.5, Width = 0.6, Height = 0.7 }
            }
        });

        detail.Status.Should().Be("Reviewed");
        detail.AdminVerifiedLabel.Should().Be("paper");
        detail.DatasetItemId.Should().NotBeNull();
        detail.DatasetItemStatus.Should().Be("VerifiedLabeled");
        detail.ExistingYoloAnnotations.Should().ContainSingle();

        var audit = await db.AuditLogs.OrderByDescending(a => a.CreatedAt).FirstAsync();
        audit.Action.Should().Be("REVIEW_AI_FEEDBACK");
        audit.ActorUserId.Should().Be(staff.Id);
    }

    [Fact]
    public async Task UTC006_ReviewApproveToQueueThenAssignGroundTruthLabelWithYolo_PromotesToVerifiedLabeled_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var (citizen, staff) = await SeedUsersAndCategoriesAsync(db);
        var (feedbackSvc, curationSvc, _, _) = CreateServices(db, staff.Id, "Staff");

        var submitted = await feedbackSvc.SubmitFeedbackAsync(citizen.Id, new SubmitFeedbackDto
        {
            ImageUrl = "https://cdn.ecolink.test/metal-can.jpg",
            PredictedLabel = "plastic",
            UserReportedLabel = "metal",
            ConfidenceScore = 0.52
        });

        var reviewed = await feedbackSvc.ReviewFeedbackAsync(staff.Id, submitted.ReportId, new ReviewAiFeedbackRequest
        {
            Decision = "ApproveToQueue",
            ReviewNote = "Chuyển kho vẽ khung"
        });
        reviewed.DatasetItemStatus.Should().Be("Unlabeled");

        var queue = await curationSvc.GetLabelingQueueAsync(new AiLabelingQueueFilterRequest { UnlabeledOnly = true });
        queue.TotalRecords.Should().Be(1);

        var labeled = await curationSvc.AssignGroundTruthLabelAsync(staff.Id, reviewed.DatasetItemId!.Value, new AssignGroundTruthLabelRequest
        {
            GroundTruthLabel = "metal",
            QualityNote = "Lon nhôm rõ nét",
            YoloAnnotations = new List<YoloBoundingBoxDto>
            {
                new() { CategoryCode = "metal", XCenter = 0.45, YCenter = 0.52, Width = 0.30, Height = 0.55 }
            }
        });

        labeled.Status.Should().Be("VerifiedLabeled");
        labeled.GroundTruthLabel.Should().Be("metal");
        labeled.YoloAnnotations.Should().ContainSingle();
    }

    [Fact]
    public async Task UTC007_BatchAssignLabelsAsync_MultipleValidUnlabeledItems_UpdatesAtomicallyAndLogsAudit_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var (_, staff) = await SeedUsersAndCategoriesAsync(db);
        var (_, curationSvc, _, _) = CreateServices(db, staff.Id, "Staff");

        var items = Enumerable.Range(1, 3).Select(idx => new AiDatasetItem
        {
            SourceType = DatasetItemSourceType.LowConfidenceAuto,
            ImageUrl = $"https://cdn.ecolink.test/box-{idx}.jpg",
            ImageSha256 = AiCategoryNormalizer.ComputeSha256Hex($"box-{idx}"),
            Status = DatasetItemStatus.Unlabeled
        }).ToList();
        db.AiDatasetItems.AddRange(items);
        await db.SaveChangesAsync();

        var result = await curationSvc.BatchAssignLabelsAsync(staff.Id, new BatchAssignLabelsRequest
        {
            ItemIds = items.Select(x => x.Id).ToList(),
            GroundTruthLabel = "cardboard",
            QualityNote = "Lô bìa carton đồng nhất"
        });

        result.UpdatedCount.Should().Be(3);
        result.GroundTruthLabel.Should().Be("cardboard");
        (await db.AiDatasetItems.CountAsync(i => i.Status == DatasetItemStatus.VerifiedLabeled)).Should().Be(3);
        (await db.AuditLogs.AnyAsync(a => a.Action == "BATCH_ASSIGN_AI_LABELS")).Should().BeTrue();
    }

    [Fact]
    public async Task UTC008_CreateDatasetVersionAsync_ThirtyVerifiedSamples_Splits702010AndSyncsToAi_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var (_, staff) = await SeedUsersAndCategoriesAsync(db);
        var (_, curationSvc, _, _) = CreateServices(db, staff.Id, "Staff");

        var cats = await db.WasteCategories
            .Where(c => c.Code == "plastic" || c.Code == "paper" || c.Code == "metal")
            .ToListAsync();

        foreach (var cat in cats)
        {
            for (var i = 0; i < 10; i++)
            {
                await SeedVerifiedItemAsync(db, cat, $"{cat.Code}-{i}");
            }
        }

        var ver = await curationSvc.CreateDatasetVersionAsync(staff.Id, new CreateDatasetVersionRequest
        {
            VersionCode = "v1.0.0",
            VersionName = "Release T11/2026",
            TrainRatio = 70,
            ValRatio = 20,
            TestRatio = 10,
            SyncToAiService = true
        });

        ver.TotalSamples.Should().Be(30);
        ver.TrainCount.Should().Be(21);
        ver.ValCount.Should().Be(6);
        ver.TestCount.Should().Be(3);
        ver.IsBalanced.Should().BeTrue();
        ver.ImbalanceRatio.Should().Be(1.00m);
        ver.Status.Should().Be("SyncedToAi");
    }

    [Fact]
    public async Task UTC009_CreateDatasetVersionAsync_StickyStratifiedSplit_PreventsTrainTestLeakageAcrossVersions_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var (_, staff) = await SeedUsersAndCategoriesAsync(db);
        var (_, curationSvc, _, _) = CreateServices(db, staff.Id, "Staff");

        var plasticCat = await db.WasteCategories.FirstAsync(c => c.Code == "plastic");
        for (var i = 0; i < 10; i++)
        {
            await SeedVerifiedItemAsync(db, plasticCat, $"p1-{i}");
        }

        var v1 = await curationSvc.CreateDatasetVersionAsync(staff.Id, new CreateDatasetVersionRequest
        {
            VersionCode = "v1.0.0",
            VersionName = "Baseline v1.0.0",
            SyncToAiService = false
        });

        var v1TestIds = await db.AiDatasetVersionItems
            .Where(vi => vi.VersionId == v1.Id && vi.AssignedSplit == DatasetSplitType.Test)
            .Select(vi => vi.DatasetItemId)
            .ToListAsync();
        var v1ValIds = await db.AiDatasetVersionItems
            .Where(vi => vi.VersionId == v1.Id && vi.AssignedSplit == DatasetSplitType.Validation)
            .Select(vi => vi.DatasetItemId)
            .ToListAsync();

        // Nạp thêm 10 mẫu mới rồi phát hành v1.1.0
        for (var i = 0; i < 10; i++)
        {
            await SeedVerifiedItemAsync(db, plasticCat, $"p2-{i}");
        }

        var v2 = await curationSvc.CreateDatasetVersionAsync(staff.Id, new CreateDatasetVersionRequest
        {
            VersionCode = "v1.1.0",
            VersionName = "Expanded v1.1.0",
            SyncToAiService = false
        });

        var v2Items = await db.AiDatasetVersionItems
            .Where(vi => vi.VersionId == v2.Id)
            .ToListAsync();

        var v2TrainIds = v2Items.Where(x => x.AssignedSplit == DatasetSplitType.Train).Select(x => x.DatasetItemId).ToHashSet();
        var v2TestIds = v2Items.Where(x => x.AssignedSplit == DatasetSplitType.Test).Select(x => x.DatasetItemId).ToHashSet();
        var v2ValIds = v2Items.Where(x => x.AssignedSplit == DatasetSplitType.Validation).Select(x => x.DatasetItemId).ToHashSet();

        // BR-CLS-07: Không mẫu Test/Validation nào của v1.0.0 được chuyển sang Train của v1.1.0
        v2TestIds.Should().Contain(v1TestIds);
        v2ValIds.Should().Contain(v1ValIds);
        v2TrainIds.Intersect(v1TestIds).Should().BeEmpty();
        v2TrainIds.Intersect(v1ValIds).Should().BeEmpty();
        v2.TotalSamples.Should().Be(20);
        v2.TrainCount.Should().Be(14);
        v2.ValCount.Should().Be(4);
        v2.TestCount.Should().Be(2);
    }

    [Fact]
    public async Task UTC010_ExportDatasetVersionAsync_YoloAndImageFolder_ProducesValidZipArchivesWithManifest_Normal()
    {
        using var db = TestDbContextFactory.Create();
        var (_, staff) = await SeedUsersAndCategoriesAsync(db);
        var (_, curationSvc, _, _) = CreateServices(db, staff.Id, "Staff");

        var metalCat = await db.WasteCategories.FirstAsync(c => c.Code == "metal");
        await SeedVerifiedItemAsync(db, metalCat, "m1");
        await SeedVerifiedItemAsync(db, metalCat, "m2");

        var v1 = await curationSvc.CreateDatasetVersionAsync(staff.Id, new CreateDatasetVersionRequest
        {
            VersionCode = "v1.0.0",
            VersionName = "Export Check",
            SyncToAiService = false
        });

        var yoloZip = await curationSvc.ExportDatasetVersionAsync(staff.Id, v1.Id, "yolo");
        yoloZip.ContentType.Should().Be("application/zip");
        yoloZip.FileName.Should().Be("ecolink-dataset-v1.0.0-yolo.zip");

        using (var ms = new MemoryStream(yoloZip.Content))
        using (var arc = new ZipArchive(ms, ZipArchiveMode.Read))
        {
            arc.Entries.Select(e => e.FullName).Should().Contain(new[] { "data.yaml", "manifest.json" });
            arc.Entries.Should().Contain(e => e.FullName.StartsWith("labels/train/") && e.FullName.EndsWith(".txt"));
        }

        var imgFolderZip = await curationSvc.ExportDatasetVersionAsync(staff.Id, v1.Id, "imagefolder");
        using (var ms2 = new MemoryStream(imgFolderZip.Content))
        using (var arc2 = new ZipArchive(ms2, ZipArchiveMode.Read))
        {
            arc2.Entries.Select(e => e.FullName).Should().Contain("manifest.json");
            arc2.Entries.Should().Contain(e => e.FullName.Contains("/metal/"));
        }

        (await db.AuditLogs.CountAsync(a => a.Action == "EXPORT_AI_DATASET_VERSION")).Should().Be(2);
    }

    // =========================================================================
    // ABNORMAL TEST CASES (UTC011 -> UTC018) — 8 TCs
    // =========================================================================

    [Fact]
    public async Task UTC011_SubmitFeedbackAsync_UnknownOrInactiveCategory_ThrowsBusinessRuleBrCls02_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var (citizen, _) = await SeedUsersAndCategoriesAsync(db);
        db.WasteCategories.Add(new WasteCategory
        {
            Code = "inactive_foam",
            Name = "Xốp ngừng thu gom",
            StandardBinColor = "Đen",
            Status = "Inactive"
        });
        await db.SaveChangesAsync();

        var (feedbackSvc, _, _, _) = CreateServices(db, citizen.Id, "User");

        var actUnknown = () => feedbackSvc.SubmitFeedbackAsync(citizen.Id, new SubmitFeedbackDto
        {
            UserReportedLabel = "nuclear_waste"
        });
        var ex1 = await actUnknown.Should().ThrowAsync<BusinessRuleException>();
        ex1.Which.RuleCode.Should().Be("BR-CLS-02");

        var actInactive = () => feedbackSvc.SubmitFeedbackAsync(citizen.Id, new SubmitFeedbackDto
        {
            UserReportedLabel = "inactive_foam"
        });
        var ex2 = await actInactive.Should().ThrowAsync<BusinessRuleException>();
        ex2.Which.RuleCode.Should().Be("BR-CLS-02");
    }

    [Fact]
    public async Task UTC012_SubmitFeedbackAsync_DuplicateScanId_ThrowsConflictException_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var (citizen, _) = await SeedUsersAndCategoriesAsync(db);
        var (feedbackSvc, _, _, _) = CreateServices(db, citizen.Id, "User");

        var (scanId, _) = await feedbackSvc.RecordScanHistoryAsync(
            Encoding.UTF8.GetBytes("dup-scan-image"),
            "dup.jpg",
            "image/jpeg",
            citizen.Id,
            new AiScanResultDto { Success = true, TopPrediction = new WastePredictionDto { Label = "paper", Confidence = 0.7 } });

        await feedbackSvc.SubmitFeedbackAsync(citizen.Id, new SubmitFeedbackDto
        {
            ScanId = scanId,
            UserReportedLabel = "cardboard"
        });

        var secondCall = () => feedbackSvc.SubmitFeedbackAsync(citizen.Id, new SubmitFeedbackDto
        {
            ScanId = scanId,
            UserReportedLabel = "plastic"
        });
        await secondCall.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UTC013_SubmitFeedbackAsync_MissingScanId_ThrowsNotFoundException_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var (citizen, _) = await SeedUsersAndCategoriesAsync(db);
        var (feedbackSvc, _, _, _) = CreateServices(db, citizen.Id, "User");

        var act = () => feedbackSvc.SubmitFeedbackAsync(citizen.Id, new SubmitFeedbackDto
        {
            ScanId = Guid.NewGuid(),
            UserReportedLabel = "glass"
        });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UTC014_ReviewFeedbackAsync_RejectRequiresNoteMin5AndExcludesAutoCandidate_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var (citizen, staff) = await SeedUsersAndCategoriesAsync(db);
        var (feedbackSvc, _, _, _) = CreateServices(db, staff.Id, "Staff");

        // Ảnh OOD tự động nạp vào Unlabeled
        var (scanId, _) = await feedbackSvc.RecordScanHistoryAsync(
            Encoding.UTF8.GetBytes("blurry-dark-image"),
            "blur.jpg",
            "image/jpeg",
            citizen.Id,
            new AiScanResultDto
            {
                Success = true,
                IsOutOfDistribution = true,
                ConfidenceThreshold = 0.60,
                TopPrediction = new WastePredictionDto { Label = "trash", Confidence = 0.25 }
            });

        var submitted = await feedbackSvc.SubmitFeedbackAsync(citizen.Id, new SubmitFeedbackDto
        {
            ScanId = scanId,
            UserReportedLabel = "plastic"
        });

        var validator = new ReviewAiFeedbackRequestValidator();
        (await validator.ValidateAsync(new ReviewAiFeedbackRequest { Decision = "Reject", ReviewNote = "Mờ" })).IsValid.Should().BeFalse();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            feedbackSvc.ReviewFeedbackAsync(staff.Id, submitted.ReportId, new ReviewAiFeedbackRequest
            {
                Decision = "Reject",
                ReviewNote = "Mờ"
            }));

        var rejected = await feedbackSvc.ReviewFeedbackAsync(staff.Id, submitted.ReportId, new ReviewAiFeedbackRequest
        {
            Decision = "Reject",
            ReviewNote = "Ảnh bị nhòe hoàn toàn không thấy vật thể"
        });

        rejected.Status.Should().Be("Rejected");
        var linkedItem = await db.AiDatasetItems.FirstAsync(i => i.SourceScanId == scanId);
        linkedItem.Status.Should().Be(DatasetItemStatus.Excluded);
    }

    [Fact]
    public async Task UTC015_ReviewFeedbackAsync_AlreadyReviewedReport_ThrowsConflictException_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var (citizen, staff) = await SeedUsersAndCategoriesAsync(db);
        var (feedbackSvc, _, _, _) = CreateServices(db, staff.Id, "Staff");

        var submitted = await feedbackSvc.SubmitFeedbackAsync(citizen.Id, new SubmitFeedbackDto
        {
            PredictedLabel = "glass",
            UserReportedLabel = "plastic"
        });

        await feedbackSvc.ReviewFeedbackAsync(staff.Id, submitted.ReportId, new ReviewAiFeedbackRequest
        {
            Decision = "ApproveAndLabel",
            VerifiedLabel = "plastic"
        });

        var secondReview = () => feedbackSvc.ReviewFeedbackAsync(staff.Id, submitted.ReportId, new ReviewAiFeedbackRequest
        {
            Decision = "Reject",
            ReviewNote = "Thử duyệt đè lần hai"
        });

        await secondReview.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UTC016_BatchAssignLabelsAsync_ContainsUnknownItemId_ThrowsNotFoundAndRollsBackAll_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var (_, staff) = await SeedUsersAndCategoriesAsync(db);
        var (_, curationSvc, _, _) = CreateServices(db, staff.Id, "Staff");

        var validItem = new AiDatasetItem
        {
            SourceType = DatasetItemSourceType.StaffUpload,
            ImageUrl = "https://cdn.ecolink.test/u1.jpg",
            ImageSha256 = AiCategoryNormalizer.ComputeSha256Hex("u1"),
            Status = DatasetItemStatus.Unlabeled
        };
        db.AiDatasetItems.Add(validItem);
        await db.SaveChangesAsync();

        var act = () => curationSvc.BatchAssignLabelsAsync(staff.Id, new BatchAssignLabelsRequest
        {
            ItemIds = new List<Guid> { validItem.Id, Guid.NewGuid() },
            GroundTruthLabel = "glass"
        });

        await act.Should().ThrowAsync<NotFoundException>();
        var unchanged = await db.AiDatasetItems.AsNoTracking().FirstAsync(i => i.Id == validItem.Id);
        unchanged.Status.Should().Be(DatasetItemStatus.Unlabeled);
        unchanged.GroundTruthLabel.Should().BeNull();
    }

    [Fact]
    public async Task UTC017_CreateDatasetVersionAsync_EmptyVerifiedPoolOrDuplicateVersionCode_ThrowsExpectedErrors_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var (_, staff) = await SeedUsersAndCategoriesAsync(db);
        var (_, curationSvc, _, _) = CreateServices(db, staff.Id, "Staff");

        // Chỉ có mẫu Unlabeled và Excluded -> phải chặn (BR-CLS-06)
        db.AiDatasetItems.Add(new AiDatasetItem
        {
            ImageUrl = "https://cdn.ecolink.test/unlabeled.jpg",
            ImageSha256 = AiCategoryNormalizer.ComputeSha256Hex("unlabeled-only"),
            Status = DatasetItemStatus.Unlabeled
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            curationSvc.CreateDatasetVersionAsync(staff.Id, new CreateDatasetVersionRequest
            {
                VersionCode = "v1.0.0",
                VersionName = "Empty Pool Test"
            }));

        var plasticCat = await db.WasteCategories.FirstAsync(c => c.Code == "plastic");
        await SeedVerifiedItemAsync(db, plasticCat, "verified-one");
        await curationSvc.CreateDatasetVersionAsync(staff.Id, new CreateDatasetVersionRequest
        {
            VersionCode = "v1.0.0",
            VersionName = "First Valid",
            SyncToAiService = false
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            curationSvc.CreateDatasetVersionAsync(staff.Id, new CreateDatasetVersionRequest
            {
                VersionCode = "v1.0.0",
                VersionName = "Duplicate Code",
                SyncToAiService = false
            }));
    }

    [Fact]
    public async Task UTC018_AiServiceOfflineOnCurateSyncAndReload_SavesPublishedWithWarningWithoutCrashing_Abnormal()
    {
        using var db = TestDbContextFactory.Create();
        var (_, staff) = await SeedUsersAndCategoriesAsync(db);
        var (_, curationSvc, aiMock, _) = CreateServices(db, staff.Id, "Staff");

        aiMock.CurateDatasetAsync(Arg.Any<CurateDatasetSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CurateDatasetSyncResponseDto
            {
                Success = false,
                VersionCode = "v1.0.0",
                ErrorMessage = "AI Vision Service unavailable"
            }));

        var cat = await db.WasteCategories.FirstAsync(c => c.Code == "paper");
        await SeedVerifiedItemAsync(db, cat, "offline-paper-1");

        var version = await curationSvc.CreateDatasetVersionAsync(staff.Id, new CreateDatasetVersionRequest
        {
            VersionCode = "v1.0.0",
            VersionName = "Offline Sync Fallback",
            SyncToAiService = true
        });

        version.Status.Should().Be("Published");
        version.AiSyncWarning.Should().Contain("unavailable");

        // Kiểm tra trực tiếp AiVisionHttpClient xử lý ngoại lệ mạng cho Curate & Reload
        var handler = MockHttpMessageHandler.FromException(new HttpRequestException("Connection refused"));
        var httpClient = new HttpClient(handler);
        var visionClient = new AiVisionHttpClient(httpClient, TestDbContextFactory.CreateConfiguration(), NullLogger<AiVisionHttpClient>.Instance);
        var reloadRes = await visionClient.ReloadModelsAsync();
        reloadRes.Success.Should().BeFalse();
        reloadRes.ErrorMessage.Should().Contain("Không thể kết nối AI Service");
    }

    // =========================================================================
    // BOUNDARY TEST CASES (UTC019 -> UTC022) — 4 TCs
    // =========================================================================

    [Fact]
    public async Task UTC019_AssignGroundTruthLabelRequestValidator_YoloCoordinateBoundaries_ValidatesStrictUnitInterval_Boundary()
    {
        var validator = new AssignGroundTruthLabelRequestValidator();

        var validBoundary = new AssignGroundTruthLabelRequest
        {
            GroundTruthLabel = "plastic",
            YoloAnnotations = new List<YoloBoundingBoxDto>
            {
                new() { CategoryCode = "plastic", XCenter = 0.0, YCenter = 1.0, Width = 1.0, Height = 0.001 }
            }
        };
        (await validator.ValidateAsync(validBoundary)).IsValid.Should().BeTrue();

        var invalidZeroWidth = new AssignGroundTruthLabelRequest
        {
            GroundTruthLabel = "plastic",
            YoloAnnotations = new List<YoloBoundingBoxDto>
            {
                new() { CategoryCode = "plastic", XCenter = 0.5, YCenter = 0.5, Width = 0.0, Height = 0.5 }
            }
        };
        (await validator.ValidateAsync(invalidZeroWidth)).IsValid.Should().BeFalse();

        var invalidOverflow = new AssignGroundTruthLabelRequest
        {
            GroundTruthLabel = "plastic",
            YoloAnnotations = new List<YoloBoundingBoxDto>
            {
                new() { CategoryCode = "plastic", XCenter = 1.01, YCenter = 0.5, Width = 0.4, Height = 0.5 }
            }
        };
        (await validator.ValidateAsync(invalidOverflow)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UTC020_CreateDatasetVersionRequestValidator_RatiosAndSemverBoundaries_Enforces100PercentSum_Boundary()
    {
        var validator = new CreateDatasetVersionRequestValidator();

        (await validator.ValidateAsync(new CreateDatasetVersionRequest
        {
            VersionCode = "v1.0.0",
            VersionName = "Standard 70/20/10",
            TrainRatio = 70,
            ValRatio = 20,
            TestRatio = 10
        })).IsValid.Should().BeTrue();

        (await validator.ValidateAsync(new CreateDatasetVersionRequest
        {
            VersionCode = "v2.0.0",
            VersionName = "Boundary 100/0/0",
            TrainRatio = 100,
            ValRatio = 0,
            TestRatio = 0
        })).IsValid.Should().BeTrue();

        (await validator.ValidateAsync(new CreateDatasetVersionRequest
        {
            VersionCode = "v1.0.1",
            VersionName = "Bad Sum",
            TrainRatio = 70,
            ValRatio = 20,
            TestRatio = 5
        })).IsValid.Should().BeFalse();

        (await validator.ValidateAsync(new CreateDatasetVersionRequest
        {
            VersionCode = "1.0",
            VersionName = "Bad Semver",
            TrainRatio = 70,
            ValRatio = 20,
            TestRatio = 10
        })).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UTC021_DatasetOverview_ImbalanceRatioThreshold250_FlagsImbalanceAndWeakestClass_Boundary()
    {
        using var db = TestDbContextFactory.Create();
        var (_, staff) = await SeedUsersAndCategoriesAsync(db);
        var (_, curationSvc, _, _) = CreateServices(db, staff.Id, "Staff");

        var plastic = await db.WasteCategories.FirstAsync(c => c.Code == "plastic");
        var glass = await db.WasteCategories.FirstAsync(c => c.Code == "glass");

        // Đúng ranh giới 5 : 2 = 2.50 -> IsBalanced = true
        for (var i = 0; i < 5; i++) await SeedVerifiedItemAsync(db, plastic, $"b-p-{i}");
        for (var i = 0; i < 2; i++) await SeedVerifiedItemAsync(db, glass, $"b-g-{i}");

        var atThreshold = await curationSvc.GetDatasetOverviewAsync();
        atThreshold.Summary.ImbalanceRatio.Should().Be(2.50m);
        atThreshold.Summary.IsBalanced.Should().BeTrue();

        // Thêm 7 mẫu plastic -> 12 : 2 = 6.00 > 2.50 -> IsBalanced = false
        for (var i = 5; i < 12; i++) await SeedVerifiedItemAsync(db, plastic, $"b-p-{i}");

        var imbalanced = await curationSvc.GetDatasetOverviewAsync();
        imbalanced.Summary.ImbalanceRatio.Should().Be(6.00m);
        imbalanced.Summary.IsBalanced.Should().BeFalse();
        imbalanced.Summary.WeakestCategory.Should().Be("glass");
        imbalanced.ByCategory.First(c => c.CategoryCode == "glass").NeedsMoreSamples.Should().BeTrue();
    }

    [Fact]
    public async Task UTC022_BatchAssignLabelsRequestValidator_ItemCountBoundaries1And200_EnforcesLimit_Boundary()
    {
        var validator = new BatchAssignLabelsRequestValidator();

        (await validator.ValidateAsync(new BatchAssignLabelsRequest
        {
            ItemIds = new List<Guid> { Guid.NewGuid() },
            GroundTruthLabel = "paper"
        })).IsValid.Should().BeTrue();

        (await validator.ValidateAsync(new BatchAssignLabelsRequest
        {
            ItemIds = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToList(),
            GroundTruthLabel = "paper"
        })).IsValid.Should().BeTrue();

        (await validator.ValidateAsync(new BatchAssignLabelsRequest
        {
            ItemIds = new List<Guid>(),
            GroundTruthLabel = "paper"
        })).IsValid.Should().BeFalse();

        (await validator.ValidateAsync(new BatchAssignLabelsRequest
        {
            ItemIds = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList(),
            GroundTruthLabel = "paper"
        })).IsValid.Should().BeFalse();
    }
}
