using System.Net.Http.Headers;
using System.Text.Json;
using Xunit.Sdk;

namespace EcoLink.ApiTests;

/// <summary>
/// Đường dẫn và hàm tiện ích dùng cho kiểm thử tích hợp Module 3:
/// Tiếp nhận &amp; Xét duyệt Cơ sở Thu gom (Recycler Onboarding &amp; Verification: U-17, S-04)
/// tích hợp liên module với Module 1 (Auth RBAC) và Module 4 (RecyclerPrices Seed).
/// </summary>
public static class OnboardingRoutes
{
    public const string Base = "/api/v1/recycler-applications";
    public const string UploadDocument = "/api/v1/recycler-applications/upload-document";
    public const string Me = "/api/v1/recycler-applications/me";

    public static string Detail(Guid userId) => $"{Base}/{userId}";
    public static string Verification(Guid userId) => $"{Base}/{userId}/verification";
}

public static class OnboardingHelper
{
    /// <summary>Đăng ký một Citizen mới độc lập và trả về (UserId, Username, Password, Token).</summary>
    public static async Task<(Guid UserId, string Username, string Password, string Token)> RegisterFreshCitizenAsync()
    {
        var payload = TestData.ValidRegister();
        var reg = (await Api.PostJsonAsync(Routes.Register, payload)).ShouldBeIn(200, 201);
        var token = Api.ExtractToken(reg.Body);
        Guid userId = Guid.Empty;

        if (Api.Unwrap(reg.Body).TryGetProperty("user", out var u) && u.TryGetProperty("id", out var idProp))
        {
            userId = idProp.GetGuid();
        }

        if (string.IsNullOrEmpty(token) || userId == Guid.Empty)
        {
            var login = (await Api.PostJsonAsync(Routes.Login, new
            {
                usernameOrEmail = payload.Str("username"),
                password = payload.Str("password")
            })).ShouldBe(200);

            token = Api.ExtractToken(login.Body)
                    ?? throw new XunitException("Could not extract token for fresh citizen.");
            userId = Api.Unwrap(login.Body).GetProperty("user").GetProperty("id").GetGuid();
        }

        return (userId, payload.Str("username"), payload.Str("password"), token!);
    }

    /// <summary>Tạo MultipartFormDataContent chứa ảnh PNG hợp lệ để upload chứng từ.</summary>
    public static MultipartFormDataContent CreateDocumentMultipart(string documentType = "permit", string fileName = "license.png")
    {
        var form = new MultipartFormDataContent();
        var pngBytes = TestImages.Png(64, 64);
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(documentType), "documentType");
        return form;
    }

    /// <summary>Payload hợp lệ chuẩn để nộp hồ sơ nâng cấp lên Recycler (U-17).</summary>
    public static Dictionary<string, object?> ValidApplicationPayload(string? suffix = null)
    {
        suffix ??= TestData.Unique(6);
        return new Dictionary<string, object?>
        {
            ["facilityName"] = $"Vựa Tái Chế Hòa Lạc {suffix}",
            ["representativeName"] = $"Nguyễn Văn Đại Diện {suffix}",
            ["businessAddress"] = "Khu Công Nghệ Cao Hòa Lạc, Thạch Thất, Hà Nội",
            ["latitude"] = 21.0135,
            ["longitude"] = 105.5265,
            ["businessLicenseUrl"] = $"https://cdn.ecolink.vn/onboarding-docs/permit_{suffix}.png",
            ["facilityPhotoUrl"] = $"https://cdn.ecolink.vn/onboarding-docs/facility_{suffix}.png",
            ["contactPhone"] = "0987654321",
            ["openingHours"] = "07:30 - 18:00 (Thứ 2 - Thứ 7)",
            ["taxCode"] = "0109876543"
        };
    }
}

/// <summary>
/// Bộ kiểm thử tích hợp (Integration Tests) cho Module 3: Recycler Onboarding &amp; Verification (U-17, S-04).
/// Kiểm chứng luồng tích hợp xuyên suốt: Citizen đăng ký (M1) -> Upload chứng từ &amp; Nộp hồ sơ (M3)
/// -> Staff/Admin thẩm định Từ chối/Phê duyệt (M3) -> Tự động nâng cấp Role &amp; khởi tạo bảng giá mặc định (M4).
/// </summary>
[Trait("Category", "Onboarding")]
public class OnboardingTests
{
    // ======================= ONB-01: Upload chứng từ (U-17) =======================

    [Fact]
    public async Task ONB01_UploadDocument_PermitAndFacilityPhoto_ReturnsValidAttachmentUrls()
    {
        var (_, _, _, citizenToken) = await OnboardingHelper.RegisterFreshCitizenAsync();

        using var permitForm = OnboardingHelper.CreateDocumentMultipart("permit", "giay-phep-kinh-doanh.png");
        var permitRes = (await Api.SendAsync(HttpMethod.Post, OnboardingRoutes.UploadDocument, permitForm, citizenToken))
            .ShouldBe(200);

        Assert.True(permitRes.Body.GetProperty("success").GetBoolean());
        var permitData = permitRes.Data();
        Assert.False(string.IsNullOrWhiteSpace(permitData.Str("fileUrl")));
        Assert.Equal("permit", permitData.Str("documentType"));

        using var photoForm = OnboardingHelper.CreateDocumentMultipart("facility-photo", "anh-co-so.png");
        var photoRes = (await Api.SendAsync(HttpMethod.Post, OnboardingRoutes.UploadDocument, photoForm, citizenToken))
            .ShouldBe(200);

        Assert.False(string.IsNullOrWhiteSpace(photoRes.Data().Str("fileUrl")));
    }

    // ======================= ONB-02: Phân quyền nộp đơn (RBAC) =======================

    [Fact]
    public async Task ONB02_SubmitApplication_WithoutTokenOrAlreadyVerifiedCollector_FailsAppropriately()
    {
        var payload = OnboardingHelper.ValidApplicationPayload();

        // 1. Không có JWT token -> 401 Unauthorized
        (await ApiJson.PostAsync(OnboardingRoutes.Base, payload)).ShouldBe(401);

        // 2. Tài khoản Collector đã là Recycler -> bị chặn bởi BR-MTH-06 (400 Bad Request)
        var collectorToken = await Tokens.GetAsync("collector");
        (await ApiJson.PostAsync(OnboardingRoutes.Base, payload, collectorToken))
            .ShouldFailWith(400, "MSG-MTH-06");
    }

    // ======================= ONB-03: Validation tọa độ & chứng từ =======================

    [Theory]
    [InlineData("invalid-lat")]
    [InlineData("invalid-lng")]
    [InlineData("missing-facility-name")]
    [InlineData("missing-license-url")]
    public async Task ONB03_SubmitApplication_WithInvalidInput_Returns400WithMsgMth06(string scenario)
    {
        var (_, _, _, citizenToken) = await OnboardingHelper.RegisterFreshCitizenAsync();
        var payload = OnboardingHelper.ValidApplicationPayload();

        switch (scenario)
        {
            case "invalid-lat":
                payload["latitude"] = 95.5;
                break;
            case "invalid-lng":
                payload["longitude"] = 190.0;
                break;
            case "missing-facility-name":
                payload["facilityName"] = "";
                break;
            case "missing-license-url":
                payload["businessLicenseUrl"] = "";
                break;
        }

        (await ApiJson.PostAsync(OnboardingRoutes.Base, payload, citizenToken))
            .ShouldFailWith(400, "MSG-MTH-06");
    }

    // ======================= ONB-04: Citizen nộp đơn & tra cứu trạng thái /me =======================

    [Fact]
    public async Task ONB04_SubmitApplication_ValidCitizen_CreatesPendingApplicationAndRetrievableViaGetMe()
    {
        var (userId, _, _, citizenToken) = await OnboardingHelper.RegisterFreshCitizenAsync();

        // Trước khi nộp đơn -> GET /me trả về 404 NotFound
        (await Api.GetAsync(OnboardingRoutes.Me, citizenToken)).ShouldBe(404);

        // Nộp đơn hợp lệ -> 201 Created kèm MSG-MTH-03
        var payload = OnboardingHelper.ValidApplicationPayload();
        var submitRes = (await ApiJson.PostAsync(OnboardingRoutes.Base, payload, citizenToken))
            .ShouldBe(201);

        Assert.Equal("MSG-MTH-03", submitRes.MessageCode());
        var created = submitRes.Data();
        Assert.Equal(userId, created.GetProperty("userId").GetGuid());
        Assert.Equal("Pending", created.Str("verificationStatus"));
        Assert.Equal((string)payload["facilityName"]!, created.Str("facilityName"));

        // Tra cứu lại qua GET /api/v1/recycler-applications/me -> 200 OK
        var meRes = (await Api.GetAsync(OnboardingRoutes.Me, citizenToken)).ShouldBe(200);
        Assert.Equal("Pending", meRes.Data().Str("verificationStatus"));
    }

    // ======================= ONB-05: Chặn nộp trùng khi đang Pending (BR-MTH-06) =======================

    [Fact]
    public async Task ONB05_SubmitApplication_DuplicateWhenAlreadyPending_Returns400WithMsgMth06()
    {
        var (_, _, _, citizenToken) = await OnboardingHelper.RegisterFreshCitizenAsync();
        var payload = OnboardingHelper.ValidApplicationPayload();

        (await ApiJson.PostAsync(OnboardingRoutes.Base, payload, citizenToken)).ShouldBe(201);

        // Nộp lần 2 khi hồ sơ đang chờ duyệt -> 400 Bad Request (MSG-MTH-06)
        (await ApiJson.PostAsync(OnboardingRoutes.Base, payload, citizenToken))
            .ShouldFailWith(400, "MSG-MTH-06");
    }

    // ======================= ONB-06: Phân quyền Staff/Admin xem danh sách & chi tiết (S-04) =======================

    [Fact]
    public async Task ONB06_StaffReview_ListAndDetail_EnforcesStaffOrAdminRole()
    {
        var (userId, _, _, citizenToken) = await OnboardingHelper.RegisterFreshCitizenAsync();
        var payload = OnboardingHelper.ValidApplicationPayload();
        (await ApiJson.PostAsync(OnboardingRoutes.Base, payload, citizenToken)).ShouldBe(201);

        // Citizen thường không được phép truy cập danh sách thẩm định của Staff -> 403 Forbidden
        (await Api.GetAsync(OnboardingRoutes.Base, citizenToken)).ShouldBe(403);
        (await Api.GetAsync(OnboardingRoutes.Detail(userId), citizenToken)).ShouldBe(403);

        // Admin/Staff truy cập danh sách và chi tiết thành công -> 200 OK
        var adminToken = await Tokens.GetAsync("admin");
        var listRes = (await Api.GetAsync(OnboardingRoutes.Base, adminToken, new Dictionary<string, string>
        {
            ["status"] = "Pending",
            ["pageNumber"] = "1",
            ["pageSize"] = "20"
        })).ShouldBe(200);

        Assert.True(listRes.Data().GetProperty("totalCount").GetInt32() >= 1);

        var detailRes = (await Api.GetAsync(OnboardingRoutes.Detail(userId), adminToken)).ShouldBe(200);
        Assert.Equal(userId, detailRes.Data().GetProperty("userId").GetGuid());
        Assert.Equal((string)payload["facilityName"]!, detailRes.Data().Str("facilityName"));
    }

    // ======================= ONB-07: Từ chối hồ sơ kèm lý do & Nộp lại (S-04 -> U-17) =======================

    [Fact]
    public async Task ONB07_StaffReview_RejectRequiresReason_AndAllowsCitizenToResubmit()
    {
        var (userId, _, _, citizenToken) = await OnboardingHelper.RegisterFreshCitizenAsync();
        var payload = OnboardingHelper.ValidApplicationPayload();
        (await ApiJson.PostAsync(OnboardingRoutes.Base, payload, citizenToken)).ShouldBe(201);

        var adminToken = await Tokens.GetAsync("admin");

        // 1. Từ chối nhưng để trống lý do -> 400 Bad Request
        (await ApiJson.PutAsync(OnboardingRoutes.Verification(userId), new
        {
            isApproved = false,
            rejectionReason = ""
        }, adminToken)).ShouldFailWith(400);

        // 2. Từ chối kèm lý do hợp lệ -> 200 OK (MSG-MTH-05), trạng thái chuyển sang Rejected
        const string reason = "Ảnh chụp giấy phép kinh doanh bị mờ, vui lòng chụp rõ nét bản gốc.";
        var rejectRes = (await ApiJson.PutAsync(OnboardingRoutes.Verification(userId), new
        {
            isApproved = false,
            rejectionReason = reason
        }, adminToken)).ShouldBe(200);

        Assert.Equal("MSG-MTH-05", rejectRes.MessageCode());
        Assert.Equal("Rejected", rejectRes.Data().Str("verificationStatus"));
        Assert.Equal(reason, rejectRes.Data().Str("rejectionReason"));

        // 3. Citizen bổ sung chứng từ và nộp lại hồ sơ -> 201 Created, quay lại trạng thái Pending
        payload["businessLicenseUrl"] = "https://cdn.ecolink.vn/onboarding-docs/permit_resubmitted_hd.png";
        var resubmitRes = (await ApiJson.PostAsync(OnboardingRoutes.Base, payload, citizenToken)).ShouldBe(201);
        Assert.Equal("Pending", resubmitRes.Data().Str("verificationStatus"));
    }

    // ======================= ONB-08: Phê duyệt -> Nâng cấp Role & Seed bảng giá (M3 -> M1 -> M4) =======================

    [Fact]
    public async Task ONB08_StaffReview_ApproveApplication_UpgradesUserRoleToRecyclerAndSeedsDefaultPrices()
    {
        var (userId, username, password, citizenToken) = await OnboardingHelper.RegisterFreshCitizenAsync();
        var payload = OnboardingHelper.ValidApplicationPayload();
        (await ApiJson.PostAsync(OnboardingRoutes.Base, payload, citizenToken)).ShouldBe(201);

        var adminToken = await Tokens.GetAsync("admin");

        // 1. Staff/Admin phê duyệt hồ sơ -> 200 OK (MSG-MTH-04), trạng thái Verified
        var approveRes = (await ApiJson.PutAsync(OnboardingRoutes.Verification(userId), new
        {
            isApproved = true,
            rejectionReason = (string?)null
        }, adminToken)).ShouldBe(200);

        Assert.Equal("MSG-MTH-04", approveRes.MessageCode());
        Assert.Equal("Verified", approveRes.Data().Str("verificationStatus"));

        // 2. Đăng nhập lại để nhận JWT mới với Role đã nâng cấp thành Recycler (Module 1 tích hợp)
        var reLogin = (await Api.PostJsonAsync(Routes.Login, new
        {
            usernameOrEmail = username,
            password
        })).ShouldBe(200);

        var newRecyclerToken = Api.ExtractToken(reLogin.Body)!;
        var roleAfterApprove = Api.Unwrap(reLogin.Body).GetProperty("user").Str("role");
        Assert.True(roleAfterApprove is "Recycler" or "Collector",
            $"Expected role to be upgraded to Recycler/Collector, got '{roleAfterApprove}'.");

        // 3. Kiểm tra Module 4: Cơ sở mới được duyệt truy cập được /api/v1/recycler/profile & /api/v1/recycler/prices
        var profileRes = (await Api.GetAsync(RecyclerRoutes.Profile, newRecyclerToken)).ShouldBe(200);
        Assert.Equal("Verified", profileRes.Data().Str("verificationStatus"));

        var prices = await RecyclerHelper.GetPricesAsync(newRecyclerToken);
        Assert.NotEmpty(prices);
    }
}

