using System.Text.Json;
using Xunit.Sdk;

namespace EcoLink.ApiTests;

public static class BlogRoutes
{
    public const string Staff = "/api/v1/staff/blog-posts";
    public const string Queue = "/api/v1/admin/blog-posts/queue";
    public const string Public = "/api/v1/blog-posts";

    public static string StaffOne(Guid id) => $"{Staff}/{id}";
    public static string Submit(Guid id) => $"{Staff}/{id}/submit";
    public static string Recall(Guid id) => $"{Staff}/{id}/recall";
    public static string AdminOne(Guid id) => $"/api/v1/admin/blog-posts/{id}";
    public static string Approve(Guid id) => $"/api/v1/admin/blog-posts/{id}/approve";
    public static string Reject(Guid id) => $"/api/v1/admin/blog-posts/{id}/reject";
    public static string PublicOne(string slug) => $"{Public}/{Uri.EscapeDataString(slug)}";
}

/// <summary>
/// Hàm tiện ích cho test Blog Content Management. Cần tài khoản Staff (xem BLOG_README.md, script seed_staff.sql):
/// mặc định blogstaff / citizen123, đổi bằng biến môi trường ECOLINK_STAFF_USER, ECOLINK_STAFF_PASSWORD.
/// </summary>
public static class BlogHelper
{
    public const string Cover = "https://example.com/blog/cover.jpg";

    private static readonly Lazy<Task<string>> StaffTokenLazy = new(async () =>
    {
        var user = Environment.GetEnvironmentVariable("ECOLINK_STAFF_USER") ?? "blogstaff";
        var password = Environment.GetEnvironmentVariable("ECOLINK_STAFF_PASSWORD") ?? "citizen123";
        var r = await Api.PostJsonAsync(Routes.Login, new { usernameOrEmail = user, password });
        if (r.Status != 200)
            throw new XunitException($"Không đăng nhập được tài khoản Staff '{user}' ({r.Status}). Hãy chạy seed_staff.sql trước. {r.Raw}");
        return Api.ExtractToken(r.Body) ?? throw new XunitException("Đăng nhập Staff không trả về token.");
    });

    public static Task<string> Staff() => StaffTokenLazy.Value;
    public static Task<string> Admin() => Tokens.GetAsync("admin");
    public static Task<string> Citizen() => Tokens.GetAsync("citizen");

    public static string UniqueTitle(string prefix = "Bài viết kiểm thử") => $"{prefix} {TestData.Unique(12)}";

    public static Dictionary<string, object?> Payload(string? title = null, string? summary = "Tóm tắt ngắn.",
        string? content = "<p>Nội dung bài viết về phân loại rác.</p>", string? cover = Cover,
        string[]? tags = null, bool submit = false) => new()
        {
            ["title"] = title ?? UniqueTitle(),
            ["summary"] = summary,
            ["content"] = content,
            ["coverImageUrl"] = cover,
            ["tags"] = tags ?? Array.Empty<string>(),
            ["submitForApproval"] = submit,
        };

    public static async Task<ApiResult> CreateRawAsync(Dictionary<string, object?> payload) =>
        await ApiJson.PostAsync(BlogRoutes.Staff, payload, await Staff());

    public static async Task<JsonElement> CreateAsync(Dictionary<string, object?>? payload = null)
    {
        var r = (await CreateRawAsync(payload ?? Payload())).ShouldBe(201);
        return r.Data();
    }

    public static async Task<Guid> DraftAsync(string? title = null) =>
        (await CreateAsync(Payload(title))).GetProperty("id").GetGuid();

    public static async Task<Guid> PendingAsync(string? title = null, string[]? tags = null) =>
        (await CreateAsync(Payload(title, tags: tags, submit: true))).GetProperty("id").GetGuid();

    public static async Task<Guid> ApprovedAsync(string? title = null, string[]? tags = null)
    {
        var id = await PendingAsync(title, tags);
        (await ApiJson.PostAsync(BlogRoutes.Approve(id), null, await Admin())).ShouldBe(200);
        return id;
    }

    public static async Task<JsonElement> MineAsync(Guid id) =>
        (await Api.GetAsync(BlogRoutes.StaffOne(id), await Staff())).ShouldBe(200).Data();

    public static string S(this JsonElement e, string name) => e.GetProperty(name).GetString()!;
}

public class BlogAccessTests
{
    [Fact]
    public async Task Staff_List_WithoutToken_Returns401() =>
        (await Api.GetAsync(BlogRoutes.Staff)).ShouldBe(401);

    [Fact]
    public async Task Staff_List_AsCitizen_Returns403() =>
        (await Api.GetAsync(BlogRoutes.Staff, await BlogHelper.Citizen())).ShouldBe(403);

    [Fact]
    public async Task Staff_List_AsAdmin_Returns403() =>
        (await Api.GetAsync(BlogRoutes.Staff, await BlogHelper.Admin())).ShouldBe(403);

    [Fact]
    public async Task Staff_Create_AsAdmin_Returns403() =>
        (await ApiJson.PostAsync(BlogRoutes.Staff, BlogHelper.Payload(), await BlogHelper.Admin())).ShouldBe(403);

    [Fact]
    public async Task Admin_Queue_WithoutToken_Returns401() =>
        (await Api.GetAsync(BlogRoutes.Queue)).ShouldBe(401);

    [Fact]
    public async Task Admin_Queue_AsStaff_Returns403() =>
        (await Api.GetAsync(BlogRoutes.Queue, await BlogHelper.Staff())).ShouldBe(403);

    [Fact]
    public async Task Admin_Queue_AsCitizen_Returns403() =>
        (await Api.GetAsync(BlogRoutes.Queue, await BlogHelper.Citizen())).ShouldBe(403);

    [Fact]
    public async Task Admin_Approve_AsStaff_Returns403()
    {
        var id = await BlogHelper.PendingAsync();
        (await ApiJson.PostAsync(BlogRoutes.Approve(id), null, await BlogHelper.Staff())).ShouldBe(403);
        Assert.Equal("Pending_Approval", (await BlogHelper.MineAsync(id)).S("status"));
    }

    [Fact]
    public async Task Admin_Reject_AsStaff_Returns403()
    {
        var id = await BlogHelper.PendingAsync();
        (await ApiJson.PostAsync(BlogRoutes.Reject(id), new { feedback = "x" }, await BlogHelper.Staff())).ShouldBe(403);
    }

    [Fact]
    public async Task Public_List_WithoutToken_Returns200() =>
        (await Api.GetAsync(BlogRoutes.Public)).ShouldBe(200);
}

public class BlogStaffTests
{
    // ---------- Tạo bài ----------
    [Fact]
    public async Task Create_Draft_Returns201WithDraftStatus()
    {
        var title = BlogHelper.UniqueTitle();
        var r = (await BlogHelper.CreateRawAsync(BlogHelper.Payload(title, tags: new[] { "tai-che", "nhua" }))).ShouldBe(201);
        var d = r.Data();
        Assert.Equal("Draft", d.S("status"));
        Assert.Equal(title, d.S("title"));
        Assert.False(string.IsNullOrWhiteSpace(d.S("slug")));
        Assert.True(d.GetProperty("canEdit").GetBoolean());
        Assert.True(d.GetProperty("canSubmit").GetBoolean());
        Assert.True(d.GetProperty("canDelete").GetBoolean());
        Assert.False(d.GetProperty("canRecall").GetBoolean());
        Assert.Equal(2, d.GetProperty("tags").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, d.GetProperty("publishedAt").ValueKind);
    }

    [Fact]
    public async Task Create_Draft_AllowsEmptyContentAndCover()
    {
        var r = await BlogHelper.CreateRawAsync(BlogHelper.Payload(content: null, cover: null));
        r.ShouldBe(201);
        Assert.Equal("Draft", r.Data().S("status"));
    }

    [Fact]
    public async Task Create_WithSubmit_SetsPendingApprovalAndMessageCode()
    {
        var r = (await BlogHelper.CreateRawAsync(BlogHelper.Payload(submit: true))).ShouldBe(201);
        Assert.Equal("Pending_Approval", r.Data().S("status"));
        Assert.Equal("MSG-ADM-06", r.MessageCode());
        Assert.True(r.Data().GetProperty("canRecall").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, r.Data().GetProperty("submittedAt").ValueKind);
    }

    [Fact]
    public async Task Create_WithSubmit_WithoutCover_Returns400()
    {
        var r = await BlogHelper.CreateRawAsync(BlogHelper.Payload(cover: null, submit: true));
        r.ShouldFailWith(400, "MSG-AUTH-02");
    }

    [Fact]
    public async Task Create_WithSubmit_WithoutContent_Returns400()
    {
        var r = await BlogHelper.CreateRawAsync(BlogHelper.Payload(content: "   ", submit: true));
        r.ShouldFailWith(400, "MSG-AUTH-02");
    }

    [Fact]
    public async Task Create_WithoutTitle_Returns400()
    {
        var p = BlogHelper.Payload();
        p.Remove("title");
        (await BlogHelper.CreateRawAsync(p)).ShouldBe(400);
    }

    [Theory]
    [InlineData("Ngắn quá")]            // 8 ký tự
    [InlineData("         ")]
    public async Task Create_TitleTooShort_Returns400(string title) =>
        (await BlogHelper.CreateRawAsync(BlogHelper.Payload(title))).ShouldBe(400);

    [Fact]
    public async Task Create_TitleTooLong_Returns400() =>
        (await BlogHelper.CreateRawAsync(BlogHelper.Payload(new string('a', 151)))).ShouldBe(400);

    [Fact]
    public async Task Create_TitleBoundaries_Accepted()
    {
        (await BlogHelper.CreateRawAsync(BlogHelper.Payload("Đúng mười!"))).ShouldBe(201);
        (await BlogHelper.CreateRawAsync(BlogHelper.Payload(new string('b', 140) + TestData.Unique(10)))).ShouldBe(201);
    }

    [Fact]
    public async Task Create_SummaryTooLong_Returns400() =>
        (await BlogHelper.CreateRawAsync(BlogHelper.Payload(summary: new string('s', 501)))).ShouldBe(400);

    [Fact]
    public async Task Create_MoreThanFiveTags_Returns400() =>
        (await BlogHelper.CreateRawAsync(BlogHelper.Payload(tags: new[] { "aa", "bb", "cc", "dd", "ee", "ff" })))
            .ShouldFailWith(400, "MSG-AUTH-02");

    [Theory]
    [InlineData("a")]
    [InlineData("-abc")]
    [InlineData("ab$c")]
    public async Task Create_InvalidTag_Returns400(string tag) =>
        (await BlogHelper.CreateRawAsync(BlogHelper.Payload(tags: new[] { tag }))).ShouldFailWith(400, "MSG-AUTH-02");

    [Fact]
    public async Task Create_DuplicateTags_AreDeduplicated()
    {
        var d = (await BlogHelper.CreateRawAsync(BlogHelper.Payload(tags: new[] { "nhua", "NHUA", "giay" }))).ShouldBe(201).Data();
        Assert.Equal(2, d.GetProperty("tags").GetArrayLength());
    }

    [Fact]
    public async Task Create_CoverWithUnsupportedFormat_Returns400()
    {
        var r = await BlogHelper.CreateRawAsync(BlogHelper.Payload(cover: "https://example.com/blog/cover.gif"));
        r.ShouldFailWith(400, "MSG-CLS-01");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/a.jpg")]
    public async Task Create_CoverInvalidUrl_Returns400(string cover) =>
        (await BlogHelper.CreateRawAsync(BlogHelper.Payload(cover: cover))).ShouldFailWith(400, "MSG-AUTH-02");

    [Fact]
    public async Task Create_SameTitle_GetsDifferentSlugs()
    {
        var title = BlogHelper.UniqueTitle("Tiêu đề trùng");
        var a = (await BlogHelper.CreateAsync(BlogHelper.Payload(title))).S("slug");
        var b = (await BlogHelper.CreateAsync(BlogHelper.Payload(title))).S("slug");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task Create_VietnameseTitle_ProducesAsciiSlug()
    {
        var d = await BlogHelper.CreateAsync(BlogHelper.Payload("Hướng dẫn phân loại rác đúng cách " + TestData.Unique(6)));
        Assert.StartsWith("huong-dan-phan-loai-rac-dung-cach-", d.S("slug"));
        Assert.Matches("^[a-z0-9-]+$", d.S("slug"));
    }

    // ---------- Sửa bài ----------
    [Fact]
    public async Task Update_Draft_ChangesFieldsAndSlug()
    {
        var id = await BlogHelper.DraftAsync();
        var title = BlogHelper.UniqueTitle("Tiêu đề đã sửa");
        var r = (await ApiJson.PutAsync(BlogRoutes.StaffOne(id),
            BlogHelper.Payload(title, summary: "Tóm tắt mới", tags: new[] { "moi" }), await BlogHelper.Staff())).ShouldBe(200);
        Assert.Equal(title, r.Data().S("title"));
        Assert.Equal("Tóm tắt mới", r.Data().S("summary"));
        Assert.StartsWith("tieu-de-da-sua-", r.Data().S("slug"));
        Assert.Equal("Draft", r.Data().S("status"));
    }

    [Fact]
    public async Task Update_WithSubmit_MovesToPending()
    {
        var id = await BlogHelper.DraftAsync();
        var r = (await ApiJson.PutAsync(BlogRoutes.StaffOne(id), BlogHelper.Payload(submit: true), await BlogHelper.Staff())).ShouldBe(200);
        Assert.Equal("Pending_Approval", r.Data().S("status"));
        Assert.Equal("MSG-ADM-06", r.MessageCode());
    }

    [Fact]
    public async Task Update_WithSubmitButNoCover_Returns400AndStaysDraft()
    {
        var id = await BlogHelper.DraftAsync();
        var p = BlogHelper.Payload(cover: null, submit: true);
        (await ApiJson.PutAsync(BlogRoutes.StaffOne(id), p, await BlogHelper.Staff())).ShouldFailWith(400, "MSG-AUTH-02");
        Assert.Equal("Draft", (await BlogHelper.MineAsync(id)).S("status"));
    }

    [Fact]
    public async Task Update_PendingPost_Returns400()
    {
        var id = await BlogHelper.PendingAsync();
        (await ApiJson.PutAsync(BlogRoutes.StaffOne(id), BlogHelper.Payload(), await BlogHelper.Staff())).ShouldBe(400);
    }

    [Fact]
    public async Task Update_ApprovedPost_Returns400()
    {
        var id = await BlogHelper.ApprovedAsync();
        (await ApiJson.PutAsync(BlogRoutes.StaffOne(id), BlogHelper.Payload(), await BlogHelper.Staff())).ShouldBe(400);
    }

    [Fact]
    public async Task Update_UnknownPost_Returns404() =>
        (await ApiJson.PutAsync(BlogRoutes.StaffOne(Guid.NewGuid()), BlogHelper.Payload(), await BlogHelper.Staff())).ShouldBe(404);

    // ---------- Nộp / thu hồi / xóa ----------
    [Fact]
    public async Task Submit_Draft_MovesToPending()
    {
        var id = await BlogHelper.DraftAsync();
        var r = (await ApiJson.PostAsync(BlogRoutes.Submit(id), null, await BlogHelper.Staff())).ShouldBe(200);
        Assert.Equal("Pending_Approval", r.Data().S("status"));
        Assert.Equal("MSG-ADM-06", r.MessageCode());
    }

    [Fact]
    public async Task Submit_DraftWithoutCover_Returns400()
    {
        var id = (await BlogHelper.CreateAsync(BlogHelper.Payload(cover: null))).GetProperty("id").GetGuid();
        (await ApiJson.PostAsync(BlogRoutes.Submit(id), null, await BlogHelper.Staff())).ShouldFailWith(400, "MSG-AUTH-02");
    }

    [Fact]
    public async Task Submit_DraftWithoutContent_Returns400()
    {
        var id = (await BlogHelper.CreateAsync(BlogHelper.Payload(content: null))).GetProperty("id").GetGuid();
        (await ApiJson.PostAsync(BlogRoutes.Submit(id), null, await BlogHelper.Staff())).ShouldFailWith(400, "MSG-AUTH-02");
    }

    [Fact]
    public async Task Submit_Twice_SecondReturns400()
    {
        var id = await BlogHelper.PendingAsync();
        (await ApiJson.PostAsync(BlogRoutes.Submit(id), null, await BlogHelper.Staff())).ShouldBe(400);
    }

    [Fact]
    public async Task Submit_Unknown_Returns404() =>
        (await ApiJson.PostAsync(BlogRoutes.Submit(Guid.NewGuid()), null, await BlogHelper.Staff())).ShouldBe(404);

    [Fact]
    public async Task Recall_Pending_ReturnsToDraft()
    {
        var id = await BlogHelper.PendingAsync();
        var r = (await ApiJson.PostAsync(BlogRoutes.Recall(id), null, await BlogHelper.Staff())).ShouldBe(200);
        Assert.Equal("Draft", r.Data().S("status"));
        Assert.Equal(JsonValueKind.Null, r.Data().GetProperty("submittedAt").ValueKind);
        Assert.True(r.Data().GetProperty("canEdit").GetBoolean());
    }

    [Fact]
    public async Task Recall_Draft_Returns400()
    {
        var id = await BlogHelper.DraftAsync();
        (await ApiJson.PostAsync(BlogRoutes.Recall(id), null, await BlogHelper.Staff())).ShouldBe(400);
    }

    [Fact]
    public async Task Recall_Approved_Returns400()
    {
        var id = await BlogHelper.ApprovedAsync();
        (await ApiJson.PostAsync(BlogRoutes.Recall(id), null, await BlogHelper.Staff())).ShouldBe(400);
    }

    [Fact]
    public async Task Recall_RemovesPostFromAdminQueue()
    {
        var title = BlogHelper.UniqueTitle("Thu hồi khỏi hàng đợi");
        var id = await BlogHelper.PendingAsync(title);
        await ApiJson.PostAsync(BlogRoutes.Recall(id), null, await BlogHelper.Staff());
        var q = (await Api.GetAsync(BlogRoutes.Queue, await BlogHelper.Admin(),
            new Dictionary<string, string> { ["search"] = title })).ShouldBe(200).Data();
        Assert.Equal(0, q.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Delete_Draft_RemovesPost()
    {
        var id = await BlogHelper.DraftAsync();
        (await ApiJson.SendAsync(HttpMethod.Delete, BlogRoutes.StaffOne(id), null, await BlogHelper.Staff())).ShouldBe(200);
        (await Api.GetAsync(BlogRoutes.StaffOne(id), await BlogHelper.Staff())).ShouldBe(404);
    }

    [Fact]
    public async Task Delete_Pending_Returns400AndKeepsPost()
    {
        var id = await BlogHelper.PendingAsync();
        (await ApiJson.SendAsync(HttpMethod.Delete, BlogRoutes.StaffOne(id), null, await BlogHelper.Staff())).ShouldBe(400);
        (await Api.GetAsync(BlogRoutes.StaffOne(id), await BlogHelper.Staff())).ShouldBe(200);
    }

    [Fact]
    public async Task Delete_Approved_Returns400()
    {
        var id = await BlogHelper.ApprovedAsync();
        (await ApiJson.SendAsync(HttpMethod.Delete, BlogRoutes.StaffOne(id), null, await BlogHelper.Staff())).ShouldBe(400);
    }

    [Fact]
    public async Task Delete_Unknown_Returns404() =>
        (await ApiJson.SendAsync(HttpMethod.Delete, BlogRoutes.StaffOne(Guid.NewGuid()), null, await BlogHelper.Staff())).ShouldBe(404);

    // ---------- Danh sách / chi tiết của tôi ----------
    [Fact]
    public async Task GetMine_Unknown_Returns404() =>
        (await Api.GetAsync(BlogRoutes.StaffOne(Guid.NewGuid()), await BlogHelper.Staff())).ShouldBe(404);

    [Fact]
    public async Task ListMine_FilterByStatus_ReturnsOnlyThatStatus()
    {
        var pending = await BlogHelper.PendingAsync();
        await BlogHelper.DraftAsync();
        var r = (await Api.GetAsync(BlogRoutes.Staff, await BlogHelper.Staff(),
            new Dictionary<string, string> { ["status"] = "Pending_Approval", ["pageSize"] = "50" })).ShouldBe(200).Data();
        var items = r.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal("Pending_Approval", i.S("status")));
        Assert.Contains(items, i => i.GetProperty("id").GetGuid() == pending);
    }

    [Fact]
    public async Task ListMine_InvalidStatus_Returns400() =>
        (await Api.GetAsync(BlogRoutes.Staff, await BlogHelper.Staff(),
            new Dictionary<string, string> { ["status"] = "Nonsense" })).ShouldBe(400);

    [Fact]
    public async Task ListMine_SearchByTitle_FindsPost()
    {
        var title = BlogHelper.UniqueTitle("Tìm theo tiêu đề");
        var id = await BlogHelper.DraftAsync(title);
        var r = (await Api.GetAsync(BlogRoutes.Staff, await BlogHelper.Staff(),
            new Dictionary<string, string> { ["search"] = title })).ShouldBe(200).Data();
        Assert.Equal(1, r.GetProperty("totalCount").GetInt32());
        Assert.Equal(id, r.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ListMine_SearchTreatsPercentLiterally()
    {
        await BlogHelper.DraftAsync();
        var r = (await Api.GetAsync(BlogRoutes.Staff, await BlogHelper.Staff(),
            new Dictionary<string, string> { ["search"] = "%%%zz" + TestData.Unique(8) })).ShouldBe(200).Data();
        Assert.Equal(0, r.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMine_PageSizeIsClamped()
    {
        var r = (await Api.GetAsync(BlogRoutes.Staff, await BlogHelper.Staff(),
            new Dictionary<string, string> { ["pageSize"] = "1000", ["page"] = "0" })).ShouldBe(200).Data();
        Assert.True(r.GetProperty("pageSize").GetInt32() <= 50);
        Assert.Equal(1, r.GetProperty("page").GetInt32());
    }
}

public class BlogAdminTests
{
    [Fact]
    public async Task Queue_ContainsPendingButNotDraft()
    {
        var pendingTitle = BlogHelper.UniqueTitle("Hàng đợi chờ duyệt");
        var pending = await BlogHelper.PendingAsync(pendingTitle);
        var draft = await BlogHelper.DraftAsync(BlogHelper.UniqueTitle("Hàng đợi bản nháp"));

        var q = (await Api.GetAsync(BlogRoutes.Queue, await BlogHelper.Admin(),
            new Dictionary<string, string> { ["pageSize"] = "50", ["sort"] = "newest" })).ShouldBe(200).Data();
        var items = q.GetProperty("items").EnumerateArray().ToList();
        Assert.All(items, i => Assert.Equal("Pending_Approval", i.S("status")));
        Assert.Contains(items, i => i.GetProperty("id").GetGuid() == pending);
        Assert.DoesNotContain(items, i => i.GetProperty("id").GetGuid() == draft);
        var first = items[0];
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("author").S("fullName")));
    }

    [Fact]
    public async Task Queue_SearchByTitle_FindsOnlyMatching()
    {
        var title = BlogHelper.UniqueTitle("Duyệt tìm kiếm");
        var id = await BlogHelper.PendingAsync(title);
        await BlogHelper.PendingAsync();
        var q = (await Api.GetAsync(BlogRoutes.Queue, await BlogHelper.Admin(),
            new Dictionary<string, string> { ["search"] = title })).ShouldBe(200).Data();
        Assert.Equal(1, q.GetProperty("totalCount").GetInt32());
        Assert.Equal(id, q.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Queue_SortOldestAndNewest_AreOrderedBySubmittedAt()
    {
        await BlogHelper.PendingAsync();
        await BlogHelper.PendingAsync();
        foreach (var sort in new[] { "oldest", "newest" })
        {
            var q = (await Api.GetAsync(BlogRoutes.Queue, await BlogHelper.Admin(),
                new Dictionary<string, string> { ["sort"] = sort, ["pageSize"] = "50" })).ShouldBe(200).Data();
            var dates = q.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("submittedAt").GetDateTime()).ToList();
            var expected = sort == "oldest" ? dates.OrderBy(d => d).ToList() : dates.OrderByDescending(d => d).ToList();
            Assert.Equal(expected, dates);
        }
    }

    [Fact]
    public async Task GetForReview_ReturnsFullContent()
    {
        var id = await BlogHelper.PendingAsync();
        var d = (await Api.GetAsync(BlogRoutes.AdminOne(id), await BlogHelper.Admin())).ShouldBe(200).Data();
        Assert.Contains("Nội dung bài viết", d.S("content"));
        Assert.Equal(BlogHelper.Cover, d.S("coverImageUrl"));
    }

    [Fact]
    public async Task GetForReview_Unknown_Returns404() =>
        (await Api.GetAsync(BlogRoutes.AdminOne(Guid.NewGuid()), await BlogHelper.Admin())).ShouldBe(404);

    [Fact]
    public async Task Approve_PublishesPostAndReturnsMessageCode()
    {
        var title = BlogHelper.UniqueTitle("Được duyệt xuất bản");
        var id = await BlogHelper.PendingAsync(title);
        var r = (await ApiJson.PostAsync(BlogRoutes.Approve(id), null, await BlogHelper.Admin())).ShouldBe(200);
        Assert.Equal("MSG-ADM-05", r.MessageCode());
        Assert.Contains(title, r.Body.GetProperty("message").GetString());
        var d = r.Data();
        Assert.Equal("Approved", d.S("status"));
        Assert.NotEqual(JsonValueKind.Null, d.GetProperty("publishedAt").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, d.GetProperty("approvedAt").ValueKind);
        Assert.False(d.GetProperty("canEdit").GetBoolean());
        Assert.False(d.GetProperty("canRecall").GetBoolean());

        // Công khai xem được bằng slug, không cần đăng nhập
        var pub = (await Api.GetAsync(BlogRoutes.PublicOne(d.S("slug")))).ShouldBe(200).Data();
        Assert.Equal(title, pub.S("title"));
        Assert.Contains("Nội dung bài viết", pub.S("content"));
    }

    [Fact]
    public async Task Approve_RemovesPostFromQueue()
    {
        var title = BlogHelper.UniqueTitle("Rời hàng đợi");
        var id = await BlogHelper.PendingAsync(title);
        await ApiJson.PostAsync(BlogRoutes.Approve(id), null, await BlogHelper.Admin());
        var q = (await Api.GetAsync(BlogRoutes.Queue, await BlogHelper.Admin(),
            new Dictionary<string, string> { ["search"] = title })).ShouldBe(200).Data();
        Assert.Equal(0, q.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Approve_Twice_SecondReturns400()
    {
        var id = await BlogHelper.ApprovedAsync();
        (await ApiJson.PostAsync(BlogRoutes.Approve(id), null, await BlogHelper.Admin())).ShouldBe(400);
    }

    [Fact]
    public async Task Approve_Draft_Returns400()
    {
        var id = await BlogHelper.DraftAsync();
        (await ApiJson.PostAsync(BlogRoutes.Approve(id), null, await BlogHelper.Admin())).ShouldBe(400);
        Assert.Equal("Draft", (await BlogHelper.MineAsync(id)).S("status"));
    }

    [Fact]
    public async Task Approve_RecalledPost_Returns400()
    {
        var id = await BlogHelper.PendingAsync();
        await ApiJson.PostAsync(BlogRoutes.Recall(id), null, await BlogHelper.Staff());
        (await ApiJson.PostAsync(BlogRoutes.Approve(id), null, await BlogHelper.Admin())).ShouldBe(400);
    }

    [Fact]
    public async Task Approve_Unknown_Returns404() =>
        (await ApiJson.PostAsync(BlogRoutes.Approve(Guid.NewGuid()), null, await BlogHelper.Admin())).ShouldBe(404);

    [Fact]
    public async Task Approve_ConcurrentRequests_ExactlyOneWins()
    {
        var id = await BlogHelper.PendingAsync();
        var token = await BlogHelper.Admin();
        var results = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => ApiJson.PostAsync(BlogRoutes.Approve(id), null, token)));
        Assert.Equal(1, results.Count(r => r.Status == 200));
        Assert.Equal(3, results.Count(r => r.Status == 400));
    }

    [Fact]
    public async Task Reject_ReturnsPostToDraftWithFeedback()
    {
        var id = await BlogHelper.PendingAsync();
        var r = (await ApiJson.PostAsync(BlogRoutes.Reject(id), new { feedback = "Cần bổ sung nguồn tham khảo." },
            await BlogHelper.Admin())).ShouldBe(200);
        Assert.Equal("Draft", r.Data().S("status"));
        Assert.Equal("Cần bổ sung nguồn tham khảo.", r.Data().S("rejectionReason"));

        var mine = await BlogHelper.MineAsync(id);
        Assert.Equal("Cần bổ sung nguồn tham khảo.", mine.S("rejectionReason"));
        Assert.True(mine.GetProperty("canEdit").GetBoolean());
    }

    [Fact]
    public async Task Reject_ThenEditAndResubmit_ClearsFeedback()
    {
        var id = await BlogHelper.PendingAsync();
        await ApiJson.PostAsync(BlogRoutes.Reject(id), new { feedback = "Sửa lại phần mở đầu." }, await BlogHelper.Admin());
        var r = (await ApiJson.PutAsync(BlogRoutes.StaffOne(id), BlogHelper.Payload(submit: true), await BlogHelper.Staff())).ShouldBe(200);
        Assert.Equal("Pending_Approval", r.Data().S("status"));
        Assert.Equal(JsonValueKind.Null, r.Data().GetProperty("rejectionReason").ValueKind);
    }

    [Fact]
    public async Task Reject_WithoutFeedback_Returns400()
    {
        var id = await BlogHelper.PendingAsync();
        (await ApiJson.PostAsync(BlogRoutes.Reject(id), new { }, await BlogHelper.Admin())).ShouldBe(400);
        (await ApiJson.PostAsync(BlogRoutes.Reject(id), new { feedback = "   " }, await BlogHelper.Admin()))
            .ShouldFailWith(400, "MSG-AUTH-02");
        Assert.Equal("Pending_Approval", (await BlogHelper.MineAsync(id)).S("status"));
    }

    [Fact]
    public async Task Reject_FeedbackTooLong_Returns400()
    {
        var id = await BlogHelper.PendingAsync();
        (await ApiJson.PostAsync(BlogRoutes.Reject(id), new { feedback = new string('x', 2001) }, await BlogHelper.Admin())).ShouldBe(400);
    }

    [Fact]
    public async Task Reject_Draft_Returns400()
    {
        var id = await BlogHelper.DraftAsync();
        (await ApiJson.PostAsync(BlogRoutes.Reject(id), new { feedback = "x" }, await BlogHelper.Admin())).ShouldBe(400);
    }

    [Fact]
    public async Task Reject_Approved_Returns400AndStaysPublished()
    {
        var id = await BlogHelper.ApprovedAsync();
        (await ApiJson.PostAsync(BlogRoutes.Reject(id), new { feedback = "muộn rồi" }, await BlogHelper.Admin())).ShouldBe(400);
        Assert.Equal("Approved", (await BlogHelper.MineAsync(id)).S("status"));
    }

    [Fact]
    public async Task Reject_Unknown_Returns404() =>
        (await ApiJson.PostAsync(BlogRoutes.Reject(Guid.NewGuid()), new { feedback = "x" }, await BlogHelper.Admin())).ShouldBe(404);
}

public class BlogPublicTests
{
    [Fact]
    public async Task Public_DraftAndPendingPosts_AreNotVisible()
    {
        var draft = await BlogHelper.CreateAsync(BlogHelper.Payload());
        var pending = await BlogHelper.CreateAsync(BlogHelper.Payload(submit: true));
        (await Api.GetAsync(BlogRoutes.PublicOne(draft.S("slug")))).ShouldBe(404);
        (await Api.GetAsync(BlogRoutes.PublicOne(pending.S("slug")))).ShouldBe(404);
    }

    [Fact]
    public async Task Public_UnknownSlug_Returns404() =>
        (await Api.GetAsync(BlogRoutes.PublicOne("khong-ton-tai-" + TestData.Unique(10)))).ShouldBe(404);

    [Fact]
    public async Task Public_RejectedPost_IsNotVisible()
    {
        var created = await BlogHelper.CreateAsync(BlogHelper.Payload(submit: true));
        var id = created.GetProperty("id").GetGuid();
        await ApiJson.PostAsync(BlogRoutes.Reject(id), new { feedback = "chưa đạt" }, await BlogHelper.Admin());
        (await Api.GetAsync(BlogRoutes.PublicOne(created.S("slug")))).ShouldBe(404);
    }

    [Fact]
    public async Task Public_List_ShowsOnlyApprovedPosts()
    {
        var title = BlogHelper.UniqueTitle("Công khai danh sách");
        await BlogHelper.CreateAsync(BlogHelper.Payload(title + " nháp"));
        await BlogHelper.PendingAsync(title + " chờ");
        var approved = await BlogHelper.ApprovedAsync(title + " duyệt");

        var r = (await Api.GetAsync(BlogRoutes.Public,
            query: new Dictionary<string, string> { ["search"] = title })).ShouldBe(200).Data();
        Assert.Equal(1, r.GetProperty("totalCount").GetInt32());
        var item = r.GetProperty("items")[0];
        Assert.Equal(approved, item.GetProperty("id").GetGuid());
        Assert.Equal("Approved", item.S("status"));
    }

    [Fact]
    public async Task Public_List_FilterByTag()
    {
        var tag = "tg" + TestData.Unique(8);
        var tagged = await BlogHelper.ApprovedAsync(tags: new[] { tag });
        await BlogHelper.ApprovedAsync(tags: new[] { "khac" });
        var r = (await Api.GetAsync(BlogRoutes.Public,
            query: new Dictionary<string, string> { ["tag"] = tag })).ShouldBe(200).Data();
        Assert.Equal(1, r.GetProperty("totalCount").GetInt32());
        Assert.Equal(tagged, r.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Public_List_TagFilterMatchesWholeTagOnly()
    {
        var tag = "tg" + TestData.Unique(8);
        await BlogHelper.ApprovedAsync(tags: new[] { tag + "xx" });
        var r = (await Api.GetAsync(BlogRoutes.Public,
            query: new Dictionary<string, string> { ["tag"] = tag })).ShouldBe(200).Data();
        Assert.Equal(0, r.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Public_List_OrderedByNewestPublishedFirst()
    {
        var title = BlogHelper.UniqueTitle("Thứ tự xuất bản");
        await BlogHelper.ApprovedAsync(title + " A");
        await Task.Delay(50);
        var second = await BlogHelper.ApprovedAsync(title + " B");
        var r = (await Api.GetAsync(BlogRoutes.Public,
            query: new Dictionary<string, string> { ["search"] = title })).ShouldBe(200).Data();
        Assert.Equal(2, r.GetProperty("totalCount").GetInt32());
        Assert.Equal(second, r.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Public_Detail_DoesNotLeakInternalFields()
    {
        var id = await BlogHelper.ApprovedAsync();
        var slug = (await BlogHelper.MineAsync(id)).S("slug");
        var pub = (await Api.GetAsync(BlogRoutes.PublicOne(slug))).ShouldBe(200).Data();
        Assert.False(pub.TryGetProperty("rejectionReason", out _));
        Assert.False(pub.TryGetProperty("status", out _));
        Assert.False(string.IsNullOrWhiteSpace(pub.S("authorName")));
        Assert.NotEqual(JsonValueKind.Null, pub.GetProperty("publishedAt").ValueKind);
    }

    [Fact]
    public async Task Public_List_PageSizeIsClamped()
    {
        var r = (await Api.GetAsync(BlogRoutes.Public,
            query: new Dictionary<string, string> { ["pageSize"] = "9999" })).ShouldBe(200).Data();
        Assert.True(r.GetProperty("pageSize").GetInt32() <= 50);
    }

    [Fact]
    public async Task Public_List_AcceptsAuthenticatedUsersToo() =>
        (await Api.GetAsync(BlogRoutes.Public, await BlogHelper.Citizen())).ShouldBe(200);
}