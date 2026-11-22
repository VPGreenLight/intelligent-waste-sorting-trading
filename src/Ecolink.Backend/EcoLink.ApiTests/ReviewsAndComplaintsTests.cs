using System.Net.Http.Headers;
using System.Text.Json;
using Xunit.Sdk;

namespace EcoLink.ApiTests;

/// <summary>
/// Bộ kiểm thử tích hợp HTTP End-to-End cho Module M-11 / FE-10 (Module 12 trong Report-5.2):
/// Rating, Review &amp; Complaint Resolution (12 REST APIs STT 103–114 + DisputeReceiptSync + GeoMatching).
/// Bao phủ đủ 16 kịch bản ITC-RVW-001 -&gt; ITC-RVW-016 (RVW01..RVW16).
/// </summary>
[Trait("Category", "ReviewsAndComplaints")]
public class ReviewsAndComplaintsTests
{
    private sealed record IsolatedRecycler(Guid RecyclerId, string Token, string FacilityName);

    private static IsolatedRecycler? _sharedRecycler;
    private static (Guid StaffId, string Token)? _sharedComplaintStaff;

    private static async Task<IsolatedRecycler> GetSharedVerifiedRecyclerAsync() =>
        _sharedRecycler ??= await CreateFreshVerifiedRecyclerAsync();

    private static async Task<(Guid StaffId, string Token)> GetSharedComplaintStaffAsync() =>
        _sharedComplaintStaff ??= await CreateStaffWithPermissionsAsync("HANDLE_COMPLAINT");

    private static async Task<IsolatedRecycler> CreateFreshVerifiedRecyclerAsync()
    {
        var (recyclerId, username, password, regToken) = await OnboardingHelper.RegisterFreshCitizenAsync();
        var appPayload = OnboardingHelper.ValidApplicationPayload();
        var facilityName = (string)appPayload["facilityName"]!;
        (await ApiJson.PostAsync(OnboardingRoutes.Base, appPayload, regToken)).ShouldBe(201);

        var adminToken = await Tokens.GetAsync("admin");
        (await ApiJson.PutAsync(OnboardingRoutes.Verification(recyclerId), new
        {
            isApproved = true,
            rejectionReason = (string?)null
        }, adminToken)).ShouldBe(200);

        var reLogin = (await Api.PostJsonAsync(Routes.Login, new
        {
            usernameOrEmail = username,
            password
        })).ShouldBe(200);
        var recyclerToken = Api.ExtractToken(reLogin.Body)!;

        // Đặt giá plastic min 1kg + mở lịch 7 ngày 00:00-23:59 + bật Online
        var plastic = await RecyclerHelper.GetCategoryAsync("plastic");
        (await ApiJson.PutAsync(RecyclerRoutes.Prices, new
        {
            items = new[]
            {
                RecyclerHelper.BulkItem(plastic.Id, 8000m, 1m, true, "Nhựa sạch")
            }
        }, recyclerToken)).ShouldBe(200);

        var days = Enumerable.Range(0, 7)
            .Select(d => new { dayOfWeek = d, isClosed = false, openTime = "00:00", closeTime = "23:59" })
            .ToList();
        (await ApiJson.PutAsync(RecyclerRoutes.Schedule, new { days }, recyclerToken)).ShouldBe(200);
        (await ApiJson.PatchAsync(RecyclerRoutes.Availability, new { isOnline = true }, recyclerToken)).ShouldBe(200);

        return new IsolatedRecycler(recyclerId, recyclerToken, facilityName);
    }

    private static async Task<(Guid UserId, string Token)> CreateFreshCitizenAsync()
    {
        var (userId, _, _, token) = await OnboardingHelper.RegisterFreshCitizenAsync();
        return (userId, token);
    }

    private static async Task<Guid> CreateAcceptedHandoverAsync(string citizenToken, Guid recyclerId, string recyclerToken, decimal weight = 5m)
    {
        var payload = await HandoverHelper.PayloadAsync(recyclerId: recyclerId, categoryCodes: new[] { "plastic" }, weight: weight);
        var created = (await ApiJson.PostAsync(HandoverRoutes.Base, payload, citizenToken)).ShouldBe(201);
        var requestId = created.Data().GetProperty("id").GetGuid();
        (await ApiJson.PostAsync(HandoverRoutes.Accept(requestId), new { }, recyclerToken)).ShouldBe(200);
        return requestId;
    }

    private static async Task<Guid> CreateAwaitingReceiptHandoverAsync(string citizenToken, Guid recyclerId, string recyclerToken, decimal weight = 5m)
    {
        var requestId = await CreateAcceptedHandoverAsync(citizenToken, recyclerId, recyclerToken, weight);
        var detailRes = (await Api.GetAsync(HandoverRoutes.RcOne(requestId), recyclerToken)).ShouldBe(200);
        var items = detailRes.Data().GetProperty("items").EnumerateArray().Select(i => new ReceiptHelper.ItemInfo(
            i.GetProperty("id").GetGuid(),
            i.GetProperty("wasteCategoryId").GetGuid(),
            i.GetProperty("wasteCategoryCode").GetString()!,
            i.GetProperty("estimatedWeightKg").GetDecimal())).ToList();

        var body = ReceiptHelper.Body(items, new[] { weight });
        (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(requestId), body, recyclerToken)).ShouldBe(201);
        return requestId;
    }

    private static async Task<Guid> CreateCompletedHandoverAsync(string citizenToken, Guid recyclerId, string recyclerToken, decimal weight = 5m)
    {
        var requestId = await CreateAwaitingReceiptHandoverAsync(citizenToken, recyclerId, recyclerToken, weight);
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(requestId), null, citizenToken)).ShouldBe(200);
        return requestId;
    }

    private static async Task<(Guid StaffId, string Token)> CreateStaffWithPermissionsAsync(params string[] permissions)
    {
        var adminToken = await Tokens.GetAsync("admin");
        var suffix = TestData.Unique(8);
        var email = $"rvw_staff_{suffix}@ecolink.vn";
        const string pass = "StaffPass@123";

        var createRes = (await Api.PostJsonAsync("/api/v1/admin/staff", new
        {
            fullName = $"Arbitrator {suffix}",
            email,
            initialPassword = pass,
            department = "Arbitration",
            position = "Dispute Officer"
        }, adminToken)).ShouldBe(201);

        var staffId = Api.Unwrap(createRes.Body).GetProperty("staffId").GetGuid();
        if (permissions.Length > 0)
        {
            (await ApiJson.PutAsync($"/api/v1/admin/staff/{staffId}/permissions", new
            {
                permissions
            }, adminToken)).ShouldBe(200);
        }

        var login = (await Api.PostJsonAsync(Routes.Login, new
        {
            usernameOrEmail = email,
            password = pass
        })).ShouldBe(200);

        return (staffId, Api.ExtractToken(login.Body)!);
    }

    // ITC-RVW-001
    [Fact]
    public async Task RVW01_CreateReview_CompletedHandover_UpdatesRecyclerAverageAndPublicDiscovery()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();
        var requestId = await CreateCompletedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);

        var createRes = (await ApiJson.PostAsync($"/api/v1/handover-requests/{requestId}/reviews", new
        {
            ratingStars = 5,
            comment = "Cân chuẩn xác, thanh toán nhanh gọn và rất thân thiện."
        }, citizenToken)).ShouldBe(201);

        Assert.Equal("MSG-RVW-01", createRes.MessageCode());
        var reviewData = createRes.Data();
        Assert.Equal(5, reviewData.GetProperty("ratingStars").GetInt32());
        Assert.Equal(5.0m, reviewData.GetProperty("updatedRecyclerAverageRating").GetDecimal());
        Assert.Equal(1, reviewData.GetProperty("updatedRecyclerTotalReviews").GetInt32());

        // Kiểm tra công khai (Guest) trên GET /recyclers/{id}/reviews & GeoMatching /recyclers/{id}
        var publicReviews = (await Api.GetAsync($"/api/v1/recyclers/{recycler.RecyclerId}/reviews")).ShouldBe(200).Data();
        Assert.Equal(5.0m, publicReviews.GetProperty("averageRating").GetDecimal());
        Assert.Equal(1, publicReviews.GetProperty("totalReviews").GetInt32());
        Assert.Equal(1, publicReviews.GetProperty("starDistribution").GetProperty("fiveStars").GetInt32());

        var partnerDetail = (await Api.GetAsync($"/api/v1/recyclers/{recycler.RecyclerId}")).ShouldBe(200).Data();
        Assert.Equal(5.0m, partnerDetail.GetProperty("averageRating").GetDecimal());
        Assert.Equal(1, partnerDetail.GetProperty("totalReviews").GetInt32());
    }

    // ITC-RVW-002
    [Fact]
    public async Task RVW02_CreateReview_BlocksIncompleteHandover400AndDuplicateReview409()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();

        var acceptedId = await CreateAcceptedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);
        (await ApiJson.PostAsync($"/api/v1/handover-requests/{acceptedId}/reviews", new
        {
            ratingStars = 5
        }, citizenToken)).ShouldFailWith(400, "MSG-RVW-02");

        var completedId = await CreateCompletedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);
        (await ApiJson.PostAsync($"/api/v1/handover-requests/{completedId}/reviews", new
        {
            ratingStars = 4
        }, citizenToken)).ShouldBe(201);

        // Đánh giá lần 2 cùng đơn -> 409 Conflict (BR-RVW-01)
        (await ApiJson.PostAsync($"/api/v1/handover-requests/{completedId}/reviews", new
        {
            ratingStars = 1
        }, citizenToken)).ShouldFailWith(409, "MSG-RVW-02");
    }

    // ITC-RVW-003
    [Fact]
    public async Task RVW03_ReviewImmutabilityAndAuthorOnlyDelete_BlocksPutAndForeignDelete403()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();
        var (_, outsiderToken) = await CreateFreshCitizenAsync();
        var requestId = await CreateCompletedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);

        var created = (await ApiJson.PostAsync($"/api/v1/handover-requests/{requestId}/reviews", new
        {
            ratingStars = 2,
            comment = "Nhận xét ban đầu trước khi thử quyền xóa."
        }, citizenToken)).ShouldBe(201).Data();

        var reviewId = created.GetProperty("reviewId").GetGuid();

        // BR-RVW-02: Không hỗ trợ PUT sửa điểm sao
        var putRes = await ApiJson.PutAsync($"/api/v1/reviews/{reviewId}", new { ratingStars = 5 }, citizenToken);
        Assert.True(putRes.Status is 404 or 405, $"Expected 404/405 for disallowed review edit, got {putRes.Status}");

        // BR-RVW-03: Cơ sở bị đánh giá & người ngoài không được phép xóa bài -> 403 Forbidden
        (await ApiJson.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}", null, recycler.Token)).ShouldBe(403);
        (await ApiJson.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}", null, outsiderToken)).ShouldBe(403);
    }

    // ITC-RVW-004
    [Fact]
    public async Task RVW04_DeleteMyReview_RecalculatesRecyclerAverageImmediately()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, c1Token) = await CreateFreshCitizenAsync();
        var (_, c2Token) = await CreateFreshCitizenAsync();

        var h1 = await CreateCompletedHandoverAsync(c1Token, recycler.RecyclerId, recycler.Token);
        var h2 = await CreateCompletedHandoverAsync(c2Token, recycler.RecyclerId, recycler.Token);

        (await ApiJson.PostAsync($"/api/v1/handover-requests/{h1}/reviews", new { ratingStars = 5 }, c1Token)).ShouldBe(201);
        var r2 = (await ApiJson.PostAsync($"/api/v1/handover-requests/{h2}/reviews", new { ratingStars = 3 }, c2Token)).ShouldBe(201).Data();
        Assert.Equal(4.0m, r2.GetProperty("updatedRecyclerAverageRating").GetDecimal());
        Assert.Equal(2, r2.GetProperty("updatedRecyclerTotalReviews").GetInt32());

        var review2Id = r2.GetProperty("reviewId").GetGuid();
        var delRes = (await ApiJson.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{review2Id}", null, c2Token)).ShouldBe(200);
        Assert.Equal("MSG-RVW-03", delRes.MessageCode());
        Assert.Equal(5.0m, delRes.Data().GetProperty("updatedRecyclerAverageRating").GetDecimal());
        Assert.Equal(1, delRes.Data().GetProperty("updatedRecyclerTotalReviews").GetInt32());

        var myReviews = (await Api.GetAsync("/api/v1/reviews/me", c1Token)).ShouldBe(200).Data();
        Assert.Equal(1, myReviews.GetProperty("totalRecords").GetInt32());
    }

    // ITC-RVW-005
    [Fact]
    public async Task RVW05_ReportAndStaffModerateReview_HidesFromPublicAndLogsAudit()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();
        var (_, staffToken) = await CreateStaffWithPermissionsAsync("HANDLE_COMPLAINT");
        var adminToken = await Tokens.GetAsync("admin");

        var requestId = await CreateCompletedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);
        var created = (await ApiJson.PostAsync($"/api/v1/handover-requests/{requestId}/reviews", new
        {
            ratingStars = 1,
            comment = "Bình luận spam quảng cáo số điện thoại lạ."
        }, citizenToken)).ShouldBe(201).Data();
        var reviewId = created.GetProperty("reviewId").GetGuid();

        var reportRes = (await ApiJson.PostAsync($"/api/v1/reviews/{reviewId}/report", new
        {
            reason = "Nội dung spam số điện thoại rác vi phạm quy định cộng đồng."
        }, recycler.Token)).ShouldBe(200).Data();
        Assert.Equal("Reported", reportRes.Str("status"));

        var modRes = (await ApiJson.PatchAsync($"/api/v1/staff/reviews/{reviewId}/moderate", new
        {
            action = "Hide",
            moderationReason = "Ẩn đánh giá spam quảng cáo theo kiểm duyệt Staff."
        }, staffToken)).ShouldBe(200);
        Assert.Equal("MSG-RVW-03", modRes.MessageCode());
        Assert.Equal("Hidden", modRes.Data().Str("status"));
        Assert.Equal(0m, modRes.Data().GetProperty("updatedRecyclerAverageRating").GetDecimal());

        var publicSummary = (await Api.GetAsync($"/api/v1/recyclers/{recycler.RecyclerId}/reviews")).ShouldBe(200).Data();
        Assert.Equal(0, publicSummary.GetProperty("totalReviews").GetInt32());

        var auditRes = (await Api.GetAsync("/api/v1/admin/audit-logs", adminToken, new Dictionary<string, string>
        {
            ["action"] = "MODERATE_REVIEW",
            ["pageSize"] = "20"
        })).ShouldBe(200).Data();
        var hasLog = auditRes.GetProperty("items").EnumerateArray()
            .Any(x => string.Equals(x.Str("entityId"), reviewId.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.True(hasLog, "Expected MODERATE_REVIEW audit log entry.");
    }

    // ITC-RVW-006
    [Fact]
    public async Task RVW06_UploadComplaintEvidenceAndCitizenSubmit_ReturnsValidUrlAndCmpCode()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();
        var requestId = await CreateCompletedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);

        using var form = new MultipartFormDataContent();
        var bytes = TestImages.Png(64, 64);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", "scale-evidence.png");

        var uploadRes = (await Api.SendAsync(HttpMethod.Post, "/api/v1/complaints/upload-evidence", form, citizenToken)).ShouldBe(200);
        var evidenceUrl = uploadRes.Data().Str("fileUrl")!;
        Assert.False(string.IsNullOrWhiteSpace(evidenceUrl));
        Assert.Contains("complaint-evidences", uploadRes.Data().Str("objectKey") ?? string.Empty);

        var cmpRes = (await ApiJson.PostAsync("/api/v1/complaints", new
        {
            handoverRequestId = requestId,
            category = "ScaleDiscrepancy",
            description = "Khối lượng thùng carton thực tế trên cân điện tử là 18.5 kg nhưng biên nhận chỉ ghi 14.0 kg.",
            evidencePhotoUrls = new[] { evidenceUrl }
        }, citizenToken)).ShouldBe(201);

        Assert.Equal("MSG-DSP-01", cmpRes.MessageCode());
        var data = cmpRes.Data();
        Assert.StartsWith("CMP-", data.Str("complaintCode"));
        Assert.Equal("Open", data.Str("status"));
        Assert.Equal("Citizen", data.Str("complainantRole"));
        Assert.Single(data.GetProperty("evidencePhotoUrls").EnumerateArray());
    }

    // ITC-RVW-007
    [Fact]
    public async Task RVW07_SubmitComplaint_BilateralByRecyclerForNoShow_SucceedsWithRecyclerRole()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (citizenId, citizenToken) = await CreateFreshCitizenAsync();
        var requestId = await CreateAcceptedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);

        var cmpRes = (await ApiJson.PostAsync("/api/v1/complaints", new
        {
            handoverRequestId = requestId,
            category = "NoShow",
            description = "Nhân viên thu gom đến đúng giờ hẹn tại điểm đón nhưng người bán tắt máy không giao hàng."
        }, recycler.Token)).ShouldBe(201);

        var data = cmpRes.Data();
        Assert.Equal("Recycler", data.Str("complainantRole"));
        Assert.Equal(recycler.RecyclerId, data.GetProperty("complainantId").GetGuid());
        Assert.Equal(citizenId, data.GetProperty("respondentId").GetGuid());
        Assert.Equal("NoShow", data.Str("category"));
        Assert.Equal("Open", data.Str("status"));
    }

    // ITC-RVW-008
    [Fact]
    public async Task RVW08_DisputeReceiptEndpoint_AutoCreatesComplaintInStaffQueue()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();
        var (_, staffToken) = await CreateStaffWithPermissionsAsync("HANDLE_COMPLAINT");
        var requestId = await CreateAwaitingReceiptHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);

        (await ApiJson.PostAsync($"/api/v1/handover-requests/{requestId}/receipt/dispute", new
        {
            reason = "Số cân thực tế tại cửa không khớp với biên nhận đã lập.",
            evidencePhotoUrls = new[] { "https://cdn.ecolink.test/evidence/dispute-receipt.jpg" }
        }, citizenToken)).ShouldBe(200);

        var queue = (await Api.GetAsync("/api/v1/staff/complaints", staffToken, new Dictionary<string, string>
        {
            ["status"] = "Open",
            ["pageSize"] = "50"
        })).ShouldBe(200).Data();

        var found = queue.GetProperty("complaints").GetProperty("items").EnumerateArray()
            .FirstOrDefault(x => x.GetProperty("requestId").GetGuid() == requestId);
        Assert.Equal(JsonValueKind.Object, found.ValueKind);
        Assert.Equal("ScaleDiscrepancy", found.Str("category"));
        Assert.Equal("Disputed", found.Str("handoverStatus"));
    }

    // ITC-RVW-009
    [Fact]
    public async Task RVW09_SubmitComplaint_DuplicateActiveComplaintSameRequest_Returns409MsgDsp04()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();
        var requestId = await CreateCompletedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);

        (await ApiJson.PostAsync("/api/v1/complaints", new
        {
            handoverRequestId = requestId,
            category = "PaymentIssue",
            description = "Đơn đã hoàn tất nhưng cơ sở chưa thanh toán phần tiền mặt còn thiếu."
        }, citizenToken)).ShouldBe(201);

        (await ApiJson.PostAsync("/api/v1/complaints", new
        {
            handoverRequestId = requestId,
            category = "Misconduct",
            description = "Phía còn lại gửi khiếu nại trùng khi phiếu đang ở trạng thái Open."
        }, recycler.Token)).ShouldFailWith(409, "MSG-DSP-04");
    }

    // ITC-RVW-010
    [Fact]
    public async Task RVW10_ComplaintPrivacyIsolation_BlocksOutsiderAndIsolatesGetMyComplaints()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, ownerToken) = await CreateFreshCitizenAsync();
        var (_, outsiderToken) = await CreateFreshCitizenAsync();
        var requestId = await CreateCompletedHandoverAsync(ownerToken, recycler.RecyclerId, recycler.Token);

        var created = (await ApiJson.PostAsync("/api/v1/complaints", new
        {
            handoverRequestId = requestId,
            category = "Other",
            description = "Yêu cầu kiểm tra hóa đơn thanh toán giữa chủ đơn và cơ sở thu mua."
        }, ownerToken)).ShouldBe(201).Data();
        var complaintId = created.GetProperty("complaintId").GetGuid();

        // Outsider không được phép nộp hộ -> 403 Forbidden
        (await ApiJson.PostAsync("/api/v1/complaints", new
        {
            handoverRequestId = requestId,
            category = "Other",
            description = "Tài khoản bên ngoài cố gửi khiếu nại hộ người khác trên đơn không thuộc mình."
        }, outsiderToken)).ShouldBe(403);

        // Cô lập /complaints/me (BR-DSP-06): Outsider tuyệt đối không thấy phiếu này; cả Owner lẫn Recycler đều thấy
        var outsiderMine = (await Api.GetAsync("/api/v1/complaints/me", outsiderToken)).ShouldBe(200).Data();
        Assert.DoesNotContain(outsiderMine.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("complaintId").GetGuid() == complaintId);

        var ownerMine = (await Api.GetAsync("/api/v1/complaints/me", ownerToken)).ShouldBe(200).Data();
        Assert.Contains(ownerMine.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("complaintId").GetGuid() == complaintId);

        var recyclerMine = (await Api.GetAsync("/api/v1/complaints/me", recycler.Token,
            new Dictionary<string, string> { ["role"] = "against" })).ShouldBe(200).Data();
        Assert.Contains(recyclerMine.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("complaintId").GetGuid() == complaintId);
    }

    // ITC-RVW-011
    [Fact]
    public async Task RVW11_StaffComplaintEndpoints_RequireHandleComplaintPermission_Returns401And403()
    {
        var zero = "00000000-0000-0000-0000-000000000001";
        (await Api.GetAsync("/api/v1/staff/complaints")).ShouldBe(401);

        var citizenToken = await HandoverHelper.Citizen();
        (await Api.GetAsync("/api/v1/staff/complaints", citizenToken)).ShouldBe(403);

        var (_, reviewOnlyStaffToken) = await CreateStaffWithPermissionsAsync("REVIEW_AI");
        (await Api.GetAsync("/api/v1/staff/complaints", reviewOnlyStaffToken)).ShouldBe(403);
        (await Api.GetAsync($"/api/v1/staff/complaints/{zero}", reviewOnlyStaffToken)).ShouldBe(403);
        (await ApiJson.PatchAsync($"/api/v1/staff/reviews/{zero}/moderate", new
        {
            action = "Hide",
            moderationReason = "Thử gọi khi thiếu quyền HANDLE_COMPLAINT."
        }, reviewOnlyStaffToken)).ShouldBe(403);

        var (_, complaintStaffToken) = await CreateStaffWithPermissionsAsync("HANDLE_COMPLAINT");
        (await Api.GetAsync("/api/v1/staff/complaints", complaintStaffToken)).ShouldBe(200);
    }

    // ITC-RVW-012
    [Fact]
    public async Task RVW12_StaffComplaintDossier_ClaimsTicketAndAggregatesReceiptAndChatHistory()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();
        var (staffId, staffToken) = await CreateStaffWithPermissionsAsync("HANDLE_COMPLAINT");

        var requestId = await CreateAcceptedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);

        // Gửi tin nhắn thương lượng trên Chatbox (M-08) để kiểm chứng Hồ sơ 360 đọc được bất biến (BR-NEG-01, BR-DSP-06)
        (await ApiJson.PostAsync($"/api/v1/trade/orders/{requestId}/messages", new
        {
            messageType = "Text",
            messageContent = "Cửa hàng xác nhận nhận đủ 5kg nhựa PET sạch lúc 14h nhé."
        }, recycler.Token)).ShouldBe(201);

        // Hoàn tất biên nhận có ảnh đồng hồ cân
        var detailRes = (await Api.GetAsync(HandoverRoutes.RcOne(requestId), recycler.Token)).ShouldBe(200);
        var items = detailRes.Data().GetProperty("items").EnumerateArray().Select(i => new ReceiptHelper.ItemInfo(
            i.GetProperty("id").GetGuid(),
            i.GetProperty("wasteCategoryId").GetGuid(),
            i.GetProperty("wasteCategoryCode").GetString()!,
            i.GetProperty("estimatedWeightKg").GetDecimal())).ToList();
        (await ApiJson.PostAsync(HandoverRoutes.RcReceipt(requestId), ReceiptHelper.Body(items, new[] { 5m }), recycler.Token)).ShouldBe(201);
        (await ApiJson.PostAsync(HandoverRoutes.Confirm(requestId), null, citizenToken)).ShouldBe(200);

        var cmpRes = (await ApiJson.PostAsync("/api/v1/complaints", new
        {
            handoverRequestId = requestId,
            category = "ScaleDiscrepancy",
            description = "Yêu cầu đối soát lại ảnh cân biên nhận và tin nhắn thỏa thuận lúc trưa.",
            evidencePhotoUrls = new[] { "https://cdn.ecolink.test/evidence/360.jpg" }
        }, citizenToken)).ShouldBe(201);
        var complaintId = cmpRes.Data().GetProperty("complaintId").GetGuid();

        var dossier = (await Api.GetAsync($"/api/v1/staff/complaints/{complaintId}", staffToken,
            new Dictionary<string, string> { ["claim"] = "true" })).ShouldBe(200).Data();

        Assert.Equal("Investigating", dossier.GetProperty("complaint").Str("status"));
        Assert.Equal(staffId, dossier.GetProperty("complaint").GetProperty("assignedStaffId").GetGuid());
        Assert.Equal(JsonValueKind.Object, dossier.GetProperty("receipt").ValueKind);
        Assert.NotEmpty(dossier.GetProperty("receipt").GetProperty("scaleProofPhotoUrls").EnumerateArray());
        Assert.Contains(dossier.GetProperty("negotiationMessages").EnumerateArray(),
            m => (m.Str("content") ?? string.Empty).Contains("PET sạch"));
    }

    // ITC-RVW-013
    [Fact]
    public async Task RVW13_ResolveComplaint_ValidatesMin10CharNote400AndBlocksReResolve409()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();
        var (_, staffToken) = await CreateStaffWithPermissionsAsync("HANDLE_COMPLAINT");
        var requestId = await CreateCompletedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);

        var complaintId = (await ApiJson.PostAsync("/api/v1/complaints", new
        {
            handoverRequestId = requestId,
            category = "Other",
            description = "Kiểm tra ràng buộc giải trình biên bản trọng tài tối thiểu 10 ký tự."
        }, citizenToken)).ShouldBe(201).Data().GetProperty("complaintId").GetGuid();

        // Biên bản < 10 ký tự -> 400 Bad Request
        (await ApiJson.PatchAsync($"/api/v1/staff/complaints/{complaintId}/resolve", new
        {
            resolutionOutcome = "Dismissed",
            resolutionNote = "OK xong"
        }, staffToken)).ShouldFailWith(400);

        // Chốt hợp lệ -> 200 OK (MSG-DSP-02)
        var resolved = (await ApiJson.PatchAsync($"/api/v1/staff/complaints/{complaintId}/resolve", new
        {
            resolutionOutcome = "Dismissed",
            resolutionNote = "Đã kiểm tra đầy đủ ảnh cân và tin nhắn, bác bỏ khiếu nại."
        }, staffToken)).ShouldBe(200);
        Assert.Equal("MSG-DSP-02", resolved.MessageCode());

        // Cố tình giải quyết lại lần 2 -> 409 Conflict
        (await ApiJson.PatchAsync($"/api/v1/staff/complaints/{complaintId}/resolve", new
        {
            resolutionOutcome = "UpholdComplainant",
            resolutionNote = "Thử thay đổi lại phán quyết sau khi đã đóng hồ sơ."
        }, staffToken)).ShouldFailWith(409);
    }

    // ITC-RVW-014
    [Fact]
    public async Task RVW14_ResolveComplaint_CancelDisputedOrder_ClawsBackPointsAndIssuesWarning()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();
        var (_, staffToken) = await CreateStaffWithPermissionsAsync("HANDLE_COMPLAINT");
        var adminToken = await Tokens.GetAsync("admin");

        // Hoàn tất đơn 5kg => +50 GreenPoints trên DB
        var requestId = await CreateCompletedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token, weight: 5m);
        var meBefore = (await Api.GetAsync(Routes.Me, citizenToken)).ShouldBe(200);
        var pointsBefore = Api.Unwrap(meBefore.Body).GetProperty("currentGreenPoints").GetInt32();
        Assert.True(pointsBefore >= 50, $"Expected >= 50 points after completing 5kg order, got {pointsBefore}");

        var complaintId = (await ApiJson.PostAsync("/api/v1/complaints", new
        {
            handoverRequestId = requestId,
            category = "ScaleDiscrepancy",
            description = "Phát hiện gian lận cân nặng thực tế sau khi xác nhận biên nhận giao hàng."
        }, citizenToken)).ShouldBe(201).Data().GetProperty("complaintId").GetGuid();

        var resolveRes = (await ApiJson.PatchAsync($"/api/v1/staff/complaints/{complaintId}/resolve", new
        {
            resolutionOutcome = "CancelDisputedOrder",
            resolutionNote = "Xác nhận sai phạm cân đo, hủy giao dịch, thu hồi điểm và cảnh cáo đối tác.",
            issueWarningToRespondent = true,
            warningReason = "Cân thiếu khối lượng phế liệu thực tế của người dân."
        }, staffToken)).ShouldBe(200).Data();

        Assert.Equal("Resolved", resolveRes.Str("status"));
        Assert.Equal("Cancelled", resolveRes.Str("handoverStatus"));
        Assert.True(resolveRes.GetProperty("pointsRevoked").GetBoolean());
        Assert.True(resolveRes.GetProperty("warningLogged").GetBoolean());

        var meAfter = (await Api.GetAsync(Routes.Me, citizenToken)).ShouldBe(200);
        var pointsAfter = Api.Unwrap(meAfter.Body).GetProperty("currentGreenPoints").GetInt32();
        Assert.Equal(Math.Max(0, pointsBefore - 50), pointsAfter);

        // Kiểm tra Governance Account Detail thấy WarningCount == 1 và AccountModerationLog gắn RelatedComplaintId
        var respondentDetail = (await Api.GetAsync($"/api/v1/staff/accounts/{recycler.RecyclerId}", adminToken)).ShouldBe(200).Data();
        Assert.Equal(1, respondentDetail.GetProperty("warningCount").GetInt32());
        Assert.Contains(respondentDetail.GetProperty("moderationHistory").EnumerateArray(),
            m => m.GetProperty("relatedComplaintId").GetGuid() == complaintId && m.Str("actionType") == "WARN");
    }

    // ITC-RVW-015
    [Fact]
    public async Task RVW15_ResolveComplaint_ConfirmReceiptAsIs_RestoresDisputedOrderToCompletedAndAwardsPoints()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();
        var (_, staffToken) = await CreateStaffWithPermissionsAsync("HANDLE_COMPLAINT");
        var requestId = await CreateAwaitingReceiptHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token, weight: 5m);

        (await ApiJson.PostAsync($"/api/v1/handover-requests/{requestId}/receipt/dispute", new
        {
            reason = "Nghi ngờ sai lệch cân điện tử trước khi bấm xác nhận hoàn tất."
        }, citizenToken)).ShouldBe(200);

        var queue = (await Api.GetAsync("/api/v1/staff/complaints", staffToken,
            new Dictionary<string, string> { ["status"] = "Open", ["pageSize"] = "50" })).ShouldBe(200).Data();
        var complaintId = queue.GetProperty("complaints").GetProperty("items").EnumerateArray()
            .First(x => x.GetProperty("requestId").GetGuid() == requestId)
            .GetProperty("complaintId").GetGuid();

        var resolved = (await ApiJson.PatchAsync($"/api/v1/staff/complaints/{complaintId}/resolve", new
        {
            resolutionOutcome = "ConfirmReceiptAsIs",
            resolutionNote = "Ảnh đồng hồ cân hoàn toàn chính xác 5.0 kg. Công nhận giao dịch hoàn tất."
        }, staffToken)).ShouldBe(200).Data();

        Assert.Equal("Resolved", resolved.Str("status"));
        Assert.Equal("Completed", resolved.Str("handoverStatus"));

        var meAfter = (await Api.GetAsync(Routes.Me, citizenToken)).ShouldBe(200);
        Assert.True(Api.Unwrap(meAfter.Body).GetProperty("currentGreenPoints").GetInt32() >= 50);
    }

    // ITC-RVW-016
    [Fact]
    public async Task RVW16_BoundaryMax5EvidencePhotos_FilterQueueAndSyncAnalyticsOpenDisputes()
    {
        var recycler = await CreateFreshVerifiedRecyclerAsync();
        var (_, citizenToken) = await CreateFreshCitizenAsync();
        var (_, staffToken) = await CreateStaffWithPermissionsAsync("HANDLE_COMPLAINT");
        var adminToken = await Tokens.GetAsync("admin");
        var requestId = await CreateCompletedHandoverAsync(citizenToken, recycler.RecyclerId, recycler.Token);

        // 6 ảnh vượt giới hạn biên tối đa 5 ảnh -> 400 Bad Request (BR-DSP-02)
        var sixUrls = Enumerable.Range(1, 6).Select(i => $"https://cdn.ecolink.test/evidence/{i}.jpg").ToArray();
        (await ApiJson.PostAsync("/api/v1/complaints", new
        {
            handoverRequestId = requestId,
            category = "Misconduct",
            description = "Kiểm tra ràng buộc tối đa 5 ảnh bằng chứng đính kèm theo BR-DSP-02.",
            evidencePhotoUrls = sixUrls
        }, citizenToken)).ShouldFailWith(400);

        // 5 ảnh biên hợp lệ -> 201 Created
        var fiveUrls = sixUrls.Take(5).ToArray();
        var created = (await ApiJson.PostAsync("/api/v1/complaints", new
        {
            handoverRequestId = requestId,
            category = "Misconduct",
            description = "Gửi đúng 5 ảnh bằng chứng hợp lệ về thái độ thiếu hợp tác khi thu gom.",
            evidencePhotoUrls = fiveUrls
        }, citizenToken)).ShouldBe(201).Data();

        Assert.Equal(5, created.GetProperty("evidencePhotoUrls").GetArrayLength());

        var filtered = (await Api.GetAsync("/api/v1/staff/complaints", staffToken, new Dictionary<string, string>
        {
            ["status"] = "Open",
            ["category"] = "Misconduct",
            ["search"] = created.Str("complaintCode")!
        })).ShouldBe(200).Data();
        Assert.Equal(1, filtered.GetProperty("complaints").GetProperty("totalRecords").GetInt32());

        var kpi = (await Api.GetAsync("/api/v1/admin/analytics/kpi-summary", adminToken,
            new Dictionary<string, string> { ["period"] = "all" })).ShouldBe(200).Data();
        Assert.True(kpi.GetProperty("operationalBacklog").GetProperty("openComplaintsCount").GetInt32() >= 1);
    }
}
