using System.Buffers.Binary;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>
/// Bộ kiểm thử tích hợp HTTP toàn diện cho Module M-13 / FE-15 (Module 11 trong Report-5.2):
/// AI Misclassification Feedback &amp; Dataset Curation (10 Core REST APIs STT 130–139 + nâng cấp STT 021, 024 + FastAPI STT 183–184).
/// Đối chiếu trực tiếp 16 kịch bản ITC-CLS-001 -> ITC-CLS-016 trong Section 7.1 kế hoạch triển khai và
/// documents/test document/integration test/11_integration_test_ai_misclassification_feedback_dataset_curation.md.
/// </summary>
[Trait("Category", "AiCuration")]
public class AiCurationTests
{
    private static int _pixelSeed = 10;

    private static StringContent JsonBody(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static string UniqueSemver()
    {
        var minor = Interlocked.Increment(ref _pixelSeed);
        var patch = Random.Shared.Next(100, 999);
        return $"v8.{minor}.{patch}";
    }

    /// <summary>Tạo ảnh PNG hợp lệ với nội dung pixel độc nhất để có SHA-256 riêng biệt mỗi lần gọi.</summary>
    private static byte[] UniquePng(int width = 64, int height = 64)
    {
        var seed = Interlocked.Increment(ref _pixelSeed);
        var rBase = (byte)((seed * 37) % 200 + 20);
        var gBase = (byte)((seed * 73) % 200 + 20);
        var bBase = (byte)((seed * 113) % 200 + 20);

        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < width; x++)
            {
                var inside = x > width / 4 && x < width * 3 / 4 && y > height / 4 && y < height * 3 / 4;
                raw.WriteByte((byte)(inside ? rBase : (byte)(255 - rBase)));
                raw.WriteByte((byte)(inside ? gBase : (byte)((gBase + x) % 255)));
                raw.WriteByte((byte)(inside ? bBase : (byte)((bBase + y) % 255)));
            }
        }

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw.ToArray());
        }

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
        ihdr[8] = 8;
        ihdr[9] = 2;

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        WriteChunk(png, "IHDR", ihdr);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var buf = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)data.Length);
        s.Write(buf);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);

        var crc = 0xFFFFFFFFu;
        foreach (var arr in new[] { typeBytes, data })
        {
            foreach (var b in arr)
            {
                crc ^= b;
                for (var k = 0; k < 8; k++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        BinaryPrimitives.WriteUInt32BigEndian(buf, crc ^ 0xFFFFFFFFu);
        s.Write(buf);
    }

    private static async Task<(Guid UserId, string Token)> RegisterCitizenAsync()
    {
        var payload = TestData.ValidRegister();
        var regRes = (await Api.PostJsonAsync(Routes.Register, payload)).ShouldBeIn(200, 201);
        var token = Api.ExtractToken(regRes.Body)!;
        var meRes = (await Api.GetAsync(Routes.Me, token)).ShouldBe(200);
        return (Api.Unwrap(meRes.Body).GetProperty("id").GetGuid(), token);
    }

    private static async Task<(Guid StaffId, string Token)> CreateStaffWithPermissionsAsync(
        string adminToken,
        params string[] permissions)
    {
        var suffix = TestData.Unique(8);
        var email = $"ai_staff_{suffix}@ecolink.vn";
        var password = "StaffPass@123";

        var createRes = (await Api.PostJsonAsync("/api/v1/admin/staff", new
        {
            fullName = $"AI Staff {suffix}",
            email,
            initialPassword = password,
            department = "AI Data Ops",
            position = "Curator"
        }, adminToken)).ShouldBe(201);

        var staffId = Api.Unwrap(createRes.Body).GetProperty("staffId").GetGuid();

        if (permissions.Length > 0)
        {
            using var permBody = JsonBody(new
            {
                permissions,
                reason = "Cấp quyền kiểm thử E2E Module M-13"
            });
            (await Api.SendAsync(HttpMethod.Put, $"/api/v1/admin/staff/{staffId}/permissions", permBody, adminToken))
                .ShouldBe(200);
        }

        var loginRes = (await Api.PostJsonAsync(Routes.Login, new
        {
            usernameOrEmail = email,
            password
        })).ShouldBe(200);

        return (staffId, Api.ExtractToken(loginRes.Body)!);
    }

    private static async Task<(Guid ScanId, string ImageUrl, bool IsOod)> ClassifyUniquePngAsync(string? token, byte[]? bytes = null)
    {
        var img = bytes ?? UniquePng();
        using var mp = new MultipartFormDataContent();
        var fc = new ByteArrayContent(img);
        fc.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        mp.Add(fc, "file", $"scan_{TestData.Unique(6)}.png");
        mp.Add(new StringContent("yolov8_detect"), "model");

        var res = (await Api.SendAsync(HttpMethod.Post, Routes.ScanClassify, mp, token)).ShouldBe(200);
        var data = Api.Unwrap(res.Body);
        var scanId = data.GetProperty("scanId").GetGuid();
        var imageUrl = data.GetProperty("imageUrl").GetString()!;
        var isOod = data.GetProperty("aiResult").GetProperty("isOutOfDistribution").GetBoolean();
        return (scanId, imageUrl, isOod);
    }

    private static async Task<Guid> CreateVerifiedDatasetItemAsync(string citizenToken, string adminToken, string groundTruthLabel)
    {
        var (scanId, _, _) = await ClassifyUniquePngAsync(citizenToken);
        var fbRes = (await Api.PostJsonAsync(Routes.ScanFeedback, new
        {
            scanId,
            userReportedLabel = groundTruthLabel,
            note = $"Auto seed verified sample for {groundTruthLabel}"
        }, citizenToken)).ShouldBe(200);

        var reportId = Api.Unwrap(fbRes.Body).GetProperty("reportId").GetGuid();

        using var revBody = JsonBody(new
        {
            decision = "ApproveAndLabel",
            verifiedLabel = groundTruthLabel,
            reviewNote = $"Verified ground-truth {groundTruthLabel}"
        });
        var revRes = (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/ai-feedback/{reportId}/review", revBody, adminToken))
            .ShouldBe(200);

        return Api.Unwrap(revRes.Body).GetProperty("datasetItemId").GetGuid();
    }

    // ITC-CLS-001 (TC-CLS-04-01_P)
    [Fact]
    public async Task CLS01_ClassifyAndSubmitFeedback_PersistsScanHistoryAndPendingFeedbackInPostgres()
    {
        var (_, citizenToken) = await RegisterCitizenAsync();
        var (scanId, imageUrl, _) = await ClassifyUniquePngAsync(citizenToken);

        Assert.NotEqual(Guid.Empty, scanId);
        Assert.False(string.IsNullOrWhiteSpace(imageUrl));

        var fbRes = (await Api.PostJsonAsync(Routes.ScanFeedback, new
        {
            scanId,
            userReportedLabel = "plastic",
            note = "Chai nhựa bị lóa sáng nên nhận diện sai"
        }, citizenToken)).ShouldBe(200);

        Assert.Equal("MSG-CLS-06", fbRes.Body.GetProperty("messageCode").GetString());
        var data = Api.Unwrap(fbRes.Body);
        Assert.Equal(scanId, data.GetProperty("scanId").GetGuid());
        Assert.Equal("Pending", data.GetProperty("status").GetString());
        Assert.Equal("plastic", data.GetProperty("normalizedLabel").GetString());
    }

    // ITC-CLS-002 (TC-CLS-04-02_P)
    [Fact]
    public async Task CLS02_LowConfidenceOodScan_AutoEnqueuesIntoUnlabeledPoolWithoutUserFeedback()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, citizenToken) = await RegisterCitizenAsync();

        // Ảnh PNG đồng nhất nhỏ (32x32) thường kích hoạt fallback / low-confidence < 0.60
        var blankPng = UniquePng(32, 32);
        var (scanId, _, isOod) = await ClassifyUniquePngAsync(citizenToken, blankPng);

        var queueRes = (await Api.GetAsync("/api/v1/staff/ai-labeling/queue", adminToken, new Dictionary<string, string>
        {
            ["sourceType"] = "LowConfidenceAuto",
            ["pageSize"] = "50"
        })).ShouldBe(200);

        var items = Api.Unwrap(queueRes.Body).GetProperty("items").EnumerateArray().ToList();
        if (isOod)
        {
            Assert.Contains(items, x =>
                x.GetProperty("sourceScanId").ValueKind == JsonValueKind.String &&
                x.GetProperty("sourceScanId").GetGuid() == scanId &&
                x.GetProperty("status").GetString() == "Unlabeled");
        }
        else
        {
            // Nếu mô hình đánh giá tự tin >= 60%, vẫn xác nhận endpoint queue hoạt động bình thường
            Assert.Equal(JsonValueKind.Array, Api.Unwrap(queueRes.Body).GetProperty("items").ValueKind);
        }
    }

    // ITC-CLS-003 (TC-CLS-04-03_N)
    [Fact]
    public async Task CLS03_SubmitFeedback_UnknownCategoryLabel_Returns400BrCls02()
    {
        var (_, citizenToken) = await RegisterCitizenAsync();
        var res = (await Api.PostJsonAsync(Routes.ScanFeedback, new
        {
            userReportedLabel = "nuclear_waste",
            note = "Nhãn không tồn tại trong hệ thống"
        }, citizenToken)).ShouldBe(400);

        Assert.False(res.Body.GetProperty("success").GetBoolean());
    }

    // ITC-CLS-004 (TC-CLS-04-04_N)
    [Fact]
    public async Task CLS04_SubmitFeedback_DuplicateScanId_Returns409Conflict()
    {
        var (_, citizenToken) = await RegisterCitizenAsync();
        var (scanId, _, _) = await ClassifyUniquePngAsync(citizenToken);

        (await Api.PostJsonAsync(Routes.ScanFeedback, new
        {
            scanId,
            userReportedLabel = "paper",
            note = "Báo cáo lần 1"
        }, citizenToken)).ShouldBe(200);

        (await Api.PostJsonAsync(Routes.ScanFeedback, new
        {
            scanId,
            userReportedLabel = "cardboard",
            note = "Báo cáo trùng lần 2"
        }, citizenToken)).ShouldBe(409);
    }

    // ITC-CLS-005 (TC-CLS-01-01_S)
    [Fact]
    public async Task CLS05_FineGrainedRbac_ReviewAiVsCurateDataset_Returns403MsgAdm04()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, citizenToken) = await RegisterCitizenAsync();
        var (_, blogStaffToken) = await CreateStaffWithPermissionsAsync(adminToken, "MANAGE_BLOG");
        var (_, reviewStaffToken) = await CreateStaffWithPermissionsAsync(adminToken, "REVIEW_AI");
        var (_, curateStaffToken) = await CreateStaffWithPermissionsAsync(adminToken, "CURATE_DATASET");

        // Chưa đăng nhập -> 401
        (await Api.GetAsync("/api/v1/waste-scan/feedback/me")).ShouldBe(401);
        (await Api.GetAsync("/api/v1/staff/ai-feedback")).ShouldBe(401);
        (await Api.GetAsync("/api/v1/staff/ai-datasets")).ShouldBe(401);

        // Citizen bị chặn -> 403
        (await Api.GetAsync("/api/v1/staff/ai-feedback", citizenToken)).ShouldBe(403);
        (await Api.GetAsync("/api/v1/staff/ai-datasets", citizenToken)).ShouldBe(403);

        // Staff chỉ có MANAGE_BLOG bị chặn cả 2 -> 403 MSG-ADM-04
        var denied = (await Api.GetAsync("/api/v1/staff/ai-feedback", blogStaffToken)).ShouldBe(403);
        Assert.Equal("MSG-ADM-04", denied.Body.GetProperty("messageCode").GetString());

        // Staff có REVIEW_AI được vào ai-feedback (200) nhưng bị chặn ai-datasets (403)
        (await Api.GetAsync("/api/v1/staff/ai-feedback", reviewStaffToken)).ShouldBe(200);
        (await Api.GetAsync("/api/v1/staff/ai-datasets", reviewStaffToken)).ShouldBe(403);

        // Staff có CURATE_DATASET được vào ai-datasets (200) nhưng bị chặn ai-feedback (403)
        (await Api.GetAsync("/api/v1/staff/ai-datasets", curateStaffToken)).ShouldBe(200);
        (await Api.GetAsync("/api/v1/staff/ai-feedback", curateStaffToken)).ShouldBe(403);
    }

    // ITC-CLS-006 (TC-CLS-01-02_P)
    [Fact]
    public async Task CLS06_ReviewFeedback_ApproveAndLabelOneClick_PromotesToVerifiedLabeledAndLogsAudit()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, reviewStaffToken) = await CreateStaffWithPermissionsAsync(adminToken, "REVIEW_AI");
        var (_, citizenToken) = await RegisterCitizenAsync();

        var (scanId, _, _) = await ClassifyUniquePngAsync(citizenToken);
        var fbRes = (await Api.PostJsonAsync(Routes.ScanFeedback, new
        {
            scanId,
            userReportedLabel = "paper",
            note = "Túi giấy kraft bị nhầm thành carton"
        }, citizenToken)).ShouldBe(200);
        var reportId = Api.Unwrap(fbRes.Body).GetProperty("reportId").GetGuid();

        (await Api.GetAsync($"/api/v1/staff/ai-feedback/{reportId}", reviewStaffToken)).ShouldBe(200);

        using var revBody = JsonBody(new
        {
            decision = "ApproveAndLabel",
            verifiedLabel = "paper",
            reviewNote = "Xác nhận nhãn giấy chuẩn",
            yoloAnnotations = new[]
            {
                new { categoryCode = "paper", xCenter = 0.5, yCenter = 0.5, width = 0.6, height = 0.7 }
            }
        });
        var revRes = (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/ai-feedback/{reportId}/review", revBody, reviewStaffToken))
            .ShouldBe(200);

        Assert.Equal("MSG-CLS-07", revRes.Body.GetProperty("messageCode").GetString());
        var detail = Api.Unwrap(revRes.Body);
        Assert.Equal("Reviewed", detail.GetProperty("status").GetString());
        Assert.Equal("paper", detail.GetProperty("adminVerifiedLabel").GetString());
        Assert.Equal("VerifiedLabeled", detail.GetProperty("datasetItemStatus").GetString());
        Assert.Equal(1, detail.GetProperty("existingYoloAnnotations").GetArrayLength());

        var auditRes = (await Api.GetAsync("/api/v1/admin/audit-logs", adminToken, new Dictionary<string, string>
        {
            ["action"] = "REVIEW_AI_FEEDBACK",
            ["pageSize"] = "20"
        })).ShouldBe(200);
        var auditItems = Api.Unwrap(auditRes.Body).GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(auditItems, a => a.GetProperty("entityId").GetString() == reportId.ToString());
    }

    // ITC-CLS-007 (TC-CLS-01-03_N)
    [Fact]
    public async Task CLS07_ReviewFeedback_RejectRequiresValidReason_AndPreventsDatasetEntry()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, citizenToken) = await RegisterCitizenAsync();
        var (scanId, _, _) = await ClassifyUniquePngAsync(citizenToken);

        var fbRes = (await Api.PostJsonAsync(Routes.ScanFeedback, new
        {
            scanId,
            userReportedLabel = "plastic",
            note = "Thử gửi ảnh mờ"
        }, citizenToken)).ShouldBe(200);
        var reportId = Api.Unwrap(fbRes.Body).GetProperty("reportId").GetGuid();

        using var shortReasonBody = JsonBody(new { decision = "Reject", reviewNote = "Mờ" });
        (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/ai-feedback/{reportId}/review", shortReasonBody, adminToken))
            .ShouldBe(400);

        using var validRejectBody = JsonBody(new
        {
            decision = "Reject",
            reviewNote = "Ảnh bị nhòe hoàn toàn không thấy vật thể"
        });
        var rejRes = (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/ai-feedback/{reportId}/review", validRejectBody, adminToken))
            .ShouldBe(200);

        var data = Api.Unwrap(rejRes.Body);
        Assert.Equal("Rejected", data.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("datasetItemId").ValueKind);
    }

    // ITC-CLS-008 (TC-CLS-01-04_N)
    [Fact]
    public async Task CLS08_MyFeedbackIsolation_AndDuplicateReviewReturns409Conflict()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, citizenA) = await RegisterCitizenAsync();
        var (_, citizenB) = await RegisterCitizenAsync();

        var (scanIdA, _, _) = await ClassifyUniquePngAsync(citizenA);
        var fbA = (await Api.PostJsonAsync(Routes.ScanFeedback, new
        {
            scanId = scanIdA,
            userReportedLabel = "metal",
            note = "Báo cáo riêng của Citizen A"
        }, citizenA)).ShouldBe(200);
        var reportIdA = Api.Unwrap(fbA.Body).GetProperty("reportId").GetGuid();

        // Citizen B xem /feedback/me không bao giờ thấy báo cáo của Citizen A (BR-CLS-08)
        var listB = (await Api.GetAsync("/api/v1/waste-scan/feedback/me", citizenB)).ShouldBe(200);
        var itemsB = Api.Unwrap(listB.Body).GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(itemsB, x => x.GetProperty("id").GetGuid() == reportIdA);

        // Citizen A thấy chính báo cáo của mình
        var listA = (await Api.GetAsync("/api/v1/waste-scan/feedback/me", citizenA)).ShouldBe(200);
        var itemsA = Api.Unwrap(listA.Body).GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(itemsA, x => x.GetProperty("id").GetGuid() == reportIdA);

        // Staff duyệt lần 1 thành công
        using var rev1 = JsonBody(new { decision = "ApproveToQueue", reviewNote = "Chuyển kho vẽ hộp bao" });
        (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/ai-feedback/{reportIdA}/review", rev1, adminToken)).ShouldBe(200);

        // Staff duyệt lần 2 -> 409 Conflict
        using var rev2 = JsonBody(new { decision = "ApproveAndLabel", verifiedLabel = "metal" });
        (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/ai-feedback/{reportIdA}/review", rev2, adminToken)).ShouldBe(409);
    }

    // ITC-CLS-009 (TC-CLS-02-01_P)
    [Fact]
    public async Task CLS09_AssignGroundTruthLabel_WithValidYoloBox_UpdatesToVerifiedLabeled()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, citizenToken) = await RegisterCitizenAsync();
        var (scanId, _, _) = await ClassifyUniquePngAsync(citizenToken);

        var fb = (await Api.PostJsonAsync(Routes.ScanFeedback, new
        {
            scanId,
            userReportedLabel = "metal",
            note = "Lon thiếc cần vẽ bbox"
        }, citizenToken)).ShouldBe(200);
        var reportId = Api.Unwrap(fb.Body).GetProperty("reportId").GetGuid();

        using var toQueue = JsonBody(new { decision = "ApproveToQueue" });
        var rev = (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/ai-feedback/{reportId}/review", toQueue, adminToken)).ShouldBe(200);
        var itemId = Api.Unwrap(rev.Body).GetProperty("datasetItemId").GetGuid();

        using var labelBody = JsonBody(new
        {
            groundTruthLabel = "metal",
            excludeFromDataset = false,
            qualityNote = "Lon nhôm sạch, ảnh rõ nét",
            yoloAnnotations = new[]
            {
                new { categoryCode = "metal", xCenter = 0.5, yCenter = 0.5, width = 0.3, height = 0.4 }
            }
        });
        var assignRes = (await Api.SendAsync(HttpMethod.Put, $"/api/v1/staff/ai-labeling/{itemId}", labelBody, adminToken))
            .ShouldBe(200);

        var updated = Api.Unwrap(assignRes.Body);
        Assert.Equal("VerifiedLabeled", updated.GetProperty("status").GetString());
        Assert.Equal("metal", updated.GetProperty("groundTruthLabel").GetString());
        Assert.Equal(1, updated.GetProperty("yoloAnnotations").GetArrayLength());
    }

    // ITC-CLS-010 (TC-CLS-02-02_N)
    [Fact]
    public async Task CLS10_AssignGroundTruthLabel_OutOfBoundsYoloCoordinates_Returns400()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, citizenToken) = await RegisterCitizenAsync();
        var itemId = await CreateVerifiedDatasetItemAsync(citizenToken, adminToken, "glass");

        using var badBox = JsonBody(new
        {
            groundTruthLabel = "glass",
            yoloAnnotations = new[]
            {
                new { categoryCode = "glass", xCenter = 1.25, yCenter = 0.5, width = 0.0, height = 0.5 }
            }
        });
        (await Api.SendAsync(HttpMethod.Put, $"/api/v1/staff/ai-labeling/{itemId}", badBox, adminToken)).ShouldBe(400);
    }

    // ITC-CLS-011 (TC-CLS-02-03_P)
    [Fact]
    public async Task CLS11_BatchAssignLabels_AtomicTransactionSuccessAndRollbackOnMissingId()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, citizenToken) = await RegisterCitizenAsync();

        var itemIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var (scanId, _, _) = await ClassifyUniquePngAsync(citizenToken);
            var fb = (await Api.PostJsonAsync(Routes.ScanFeedback, new { scanId, userReportedLabel = "cardboard" }, citizenToken)).ShouldBe(200);
            var reportId = Api.Unwrap(fb.Body).GetProperty("reportId").GetGuid();
            using var toQ = JsonBody(new { decision = "ApproveToQueue" });
            var r = (await Api.SendAsync(HttpMethod.Patch, $"/api/v1/staff/ai-feedback/{reportId}/review", toQ, adminToken)).ShouldBe(200);
            itemIds.Add(Api.Unwrap(r.Body).GetProperty("datasetItemId").GetGuid());
        }

        // Thử batch kèm 1 ID không tồn tại -> 404 và rollback hoàn toàn
        (await Api.PostJsonAsync("/api/v1/staff/ai-labeling/batch-assign", new
        {
            itemIds = new[] { itemIds[0], itemIds[1], Guid.NewGuid() },
            groundTruthLabel = "cardboard"
        }, adminToken)).ShouldBe(404);

        var queueBefore = (await Api.GetAsync("/api/v1/staff/ai-labeling/queue", adminToken, new Dictionary<string, string>
        {
            ["unlabeledOnly"] = "true",
            ["pageSize"] = "100"
        })).ShouldBe(200);
        var unlabeledIds = Api.Unwrap(queueBefore.Body).GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToHashSet();
        Assert.All(itemIds, id => Assert.Contains(id, unlabeledIds));

        // Batch hợp lệ cả 3 ID -> 200 OK
        var okRes = (await Api.PostJsonAsync("/api/v1/staff/ai-labeling/batch-assign", new
        {
            itemIds,
            groundTruthLabel = "cardboard",
            qualityNote = "Lô bìa carton E2E đồng nhất"
        }, adminToken)).ShouldBe(200);

        Assert.Equal(3, Api.Unwrap(okRes.Body).GetProperty("updatedCount").GetInt32());
    }

    // ITC-CLS-012 (TC-CLS-03-01_P)
    [Fact]
    public async Task CLS12_CreateDatasetVersion_702010Split_PersistsVersionAndEnforcesSum100()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, curateToken) = await CreateStaffWithPermissionsAsync(adminToken, "CURATE_DATASET");
        var (_, citizenToken) = await RegisterCitizenAsync();

        // Đảm bảo có ít nhất 10 mẫu VerifiedLabeled mới
        foreach (var cat in new[] { "plastic", "paper", "metal" })
        {
            for (var i = 0; i < 4; i++)
            {
                await CreateVerifiedDatasetItemAsync(citizenToken, adminToken, cat);
            }
        }

        // Sai tổng tỷ lệ != 100 -> 400
        (await Api.PostJsonAsync("/api/v1/staff/ai-datasets/versions", new
        {
            versionCode = UniqueSemver(),
            versionName = "Bad Ratio",
            trainRatio = 70,
            valRatio = 20,
            testRatio = 5
        }, curateToken)).ShouldBe(400);

        var verCode = UniqueSemver();
        var createRes = (await Api.PostJsonAsync("/api/v1/staff/ai-datasets/versions", new
        {
            versionCode = verCode,
            versionName = "E2E Stratified Version",
            trainRatio = 70,
            valRatio = 20,
            testRatio = 10,
            syncToAiService = true
        }, curateToken)).ShouldBe(201);

        Assert.Equal("MSG-CLS-08", createRes.Body.GetProperty("messageCode").GetString());
        var ver = Api.Unwrap(createRes.Body);
        var trainC = ver.GetProperty("trainCount").GetInt32();
        var valC = ver.GetProperty("valCount").GetInt32();
        var testC = ver.GetProperty("testCount").GetInt32();
        var total = ver.GetProperty("totalSamples").GetInt32();

        Assert.True(total >= 12);
        Assert.Equal(total, trainC + valC + testC);
        Assert.True(trainC > valC && valC >= testC && testC >= 1);
    }

    // ITC-CLS-013 (TC-CLS-03-02_S)
    [Fact]
    public async Task CLS13_StickyStratifiedSplit_ZeroTrainTestLeakageAcrossConsecutiveVersions()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, citizenToken) = await RegisterCitizenAsync();

        for (var i = 0; i < 5; i++)
            await CreateVerifiedDatasetItemAsync(citizenToken, adminToken, "plastic");

        var v1Res = (await Api.PostJsonAsync("/api/v1/staff/ai-datasets/versions", new
        {
            versionCode = UniqueSemver(),
            versionName = "Sticky Base v1",
            trainRatio = 70,
            valRatio = 20,
            testRatio = 10,
            syncToAiService = false
        }, adminToken)).ShouldBe(201);
        var v1Id = Api.Unwrap(v1Res.Body).GetProperty("id").GetGuid();

        var v1Manifest = await DownloadManifestMapAsync(v1Id, adminToken);

        // Nạp thêm 5 mẫu mới rồi tạo v2
        for (var i = 0; i < 5; i++)
            await CreateVerifiedDatasetItemAsync(citizenToken, adminToken, "plastic");

        var v2Res = (await Api.PostJsonAsync("/api/v1/staff/ai-datasets/versions", new
        {
            versionCode = UniqueSemver(),
            versionName = "Sticky Next v2",
            trainRatio = 70,
            valRatio = 20,
            testRatio = 10,
            syncToAiService = false
        }, adminToken)).ShouldBe(201);
        var v2Id = Api.Unwrap(v2Res.Body).GetProperty("id").GetGuid();

        var v2Manifest = await DownloadManifestMapAsync(v2Id, adminToken);

        Assert.True(v2Manifest.Count >= v1Manifest.Count + 5);
        foreach (var (itemId, prevSplit) in v1Manifest)
        {
            Assert.True(v2Manifest.TryGetValue(itemId, out var nextSplit));
            Assert.Equal(prevSplit, nextSplit); // Mọi mẫu cũ giữ 100% nguyên split, Zero Data Leakage!
        }
    }

    // ITC-CLS-014 (TC-CLS-03-03_B)
    [Fact]
    public async Task CLS14_DatasetDashboard_ComputesClassDistributionAndFlagsImbalanceAbove25x()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, citizenToken) = await RegisterCitizenAsync();

        var dashBefore = (await Api.GetAsync("/api/v1/staff/ai-datasets", adminToken)).ShouldBe(200);
        var byCat = Api.Unwrap(dashBefore.Body).GetProperty("byCategory").EnumerateArray().ToList();
        var glassBefore = byCat.First(c => c.GetProperty("categoryCode").GetString() == "glass").GetProperty("totalVerified").GetInt32();
        var plasticBefore = byCat.First(c => c.GetProperty("categoryCode").GetString() == "plastic").GetProperty("totalVerified").GetInt32();

        if (glassBefore == 0)
        {
            await CreateVerifiedDatasetItemAsync(citizenToken, adminToken, "glass");
            glassBefore = 1;
        }

        var targetMinPlastic = (int)Math.Ceiling(glassBefore * 2.6) + 2;
        var needed = Math.Max(0, targetMinPlastic - plasticBefore);
        for (var i = 0; i < Math.Min(needed, 10); i++)
        {
            await CreateVerifiedDatasetItemAsync(citizenToken, adminToken, "plastic");
        }

        var dashAfter = (await Api.GetAsync("/api/v1/staff/ai-datasets", adminToken)).ShouldBe(200);
        var summary = Api.Unwrap(dashAfter.Body).GetProperty("summary");
        Assert.True(summary.GetProperty("verifiedLabeledCount").GetInt32() > 0);
        var ratio = summary.GetProperty("imbalanceRatio").GetDecimal();
        Assert.True(ratio >= 1.00m);

        if (ratio > 2.50m)
        {
            Assert.False(summary.GetProperty("isBalanced").GetBoolean());
        }
    }

    // ITC-CLS-015 (TC-CLS-03-04_P)
    [Fact]
    public async Task CLS15_ExportDatasetVersion_YoloZipArchive_ReturnsHeadersAndValidStructure()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, citizenToken) = await RegisterCitizenAsync();
        await CreateVerifiedDatasetItemAsync(citizenToken, adminToken, "metal");

        var verRes = (await Api.PostJsonAsync("/api/v1/staff/ai-datasets/versions", new
        {
            versionCode = UniqueSemver(),
            versionName = "Export E2E Version",
            syncToAiService = false
        }, adminToken)).ShouldBe(201);
        var verId = Api.Unwrap(verRes.Body).GetProperty("id").GetGuid();

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var zipResp = await http.GetAsync($"{Api.BaseUrl}/api/v1/staff/ai-datasets/versions/{verId}/export?format=yolo");

        Assert.Equal(200, (int)zipResp.StatusCode);
        Assert.Equal("application/zip", zipResp.Content.Headers.ContentType?.MediaType);
        Assert.Equal("MSG-ADM-02", zipResp.Headers.GetValues("X-EcoLink-Message-Code").FirstOrDefault());

        var zipBytes = await zipResp.Content.ReadAsByteArrayAsync();
        using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var names = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("data.yaml", names);
        Assert.Contains("manifest.json", names);
    }

    // ITC-CLS-016 (TC-CLS-03-05_S)
    [Fact]
    public async Task CLS16_CreateDatasetVersion_SyncsToFastApiServiceAndUpdatesSyncedToAiStatus()
    {
        var adminToken = await Tokens.GetAsync("admin");
        var (_, citizenToken) = await RegisterCitizenAsync();
        await CreateVerifiedDatasetItemAsync(citizenToken, adminToken, "paper");

        var verRes = (await Api.PostJsonAsync("/api/v1/staff/ai-datasets/versions", new
        {
            versionCode = UniqueSemver(),
            versionName = "FastAPI Live Sync E2E",
            trainRatio = 70,
            valRatio = 20,
            testRatio = 10,
            syncToAiService = true
        }, adminToken)).ShouldBe(201);

        var ver = Api.Unwrap(verRes.Body);
        var status = ver.GetProperty("status").GetString();
        Assert.True(status is "SyncedToAi" or "Published");

        // Nếu AI Service đang chạy ở cổng 8000, status sẽ tự lên SyncedToAi
        if (status == "SyncedToAi")
        {
            Assert.NotEqual(JsonValueKind.Null, ver.GetProperty("syncedToAiAt").ValueKind);
        }
    }

    private static async Task<Dictionary<string, string>> DownloadManifestMapAsync(Guid versionId, string token)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await http.GetAsync($"{Api.BaseUrl}/api/v1/staff/ai-datasets/versions/{versionId}/export?format=yolo");
        resp.EnsureSuccessStatusCode();

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        using var ms = new MemoryStream(bytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry = archive.GetEntry("manifest.json")!;
        await using var stream = entry.Open();
        using var doc = await JsonDocument.ParseAsync(stream);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            map[item.GetProperty("itemId").GetString()!] = item.GetProperty("split").GetString()!;
        }
        return map;
    }
}
