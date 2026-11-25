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

namespace Ecolink.UnitTests.Module14_BlogContent;

/// <summary>
/// Bộ kiểm thử đơn vị chuẩn hóa cho Module 14 / M-14 (FE-16): Blog &amp; Content Management
/// (BlogPostService &amp; SaveBlogPostRequestValidator / RejectBlogPostRequestValidator / ReviewBlogPostRequestValidator).
/// Gồm 20 Unit Test Cases (UTC001 -> UTC020: 9 Normal, 7 Abnormal, 4 Boundary).
/// </summary>
[Trait("Module", "Module14_BlogContent")]
public class BlogContentUnitTests
{
    private const string ValidCover = "https://cdn.ecolink.test/blog-media/cover.jpg";

    private static (BlogPostService Service, IAuditLogService Audit) CreateService(
        ApplicationDbContext db,
        Guid actorId,
        string role = "Staff")
    {
        var cur = Substitute.For<ICurrentUserService>();
        cur.UserId.Returns((Guid?)actorId);
        cur.Role.Returns(role);
        cur.Email.Returns($"{role.ToLowerInvariant()}@ecolink.vn");
        cur.IpAddress.Returns("127.0.0.1");
        cur.UserAgent.Returns("EcoLink-UnitTest/1.0");

        var audit = new AuditLogService(db, cur);
        var svc = new BlogPostService(db, NullLogger<BlogPostService>.Instance, audit);
        return (svc, audit);
    }

    private static User MakeUser(string name, UserRole role, UserStatus status = UserStatus.Active) => new()
    {
        Id = Guid.NewGuid(),
        Username = $"u_{Guid.NewGuid():N}"[..16],
        Email = $"u_{Guid.NewGuid():N}@ecolink.vn",
        FullName = name,
        Role = role,
        Status = status
    };

    private static SaveBlogPostRequest ValidRequest(
        string title = "Hướng dẫn phân loại nhựa PET và HDPE tại nhà",
        bool submit = false,
        string? cover = ValidCover,
        string? content = "<p>Nội dung chi tiết về phân loại rác tại nguồn.</p>",
        params string[] tags) => new()
        {
            Title = title,
            Summary = "Tóm tắt ngắn gọn cho người dân.",
            Content = content,
            CoverImageUrl = cover,
            Tags = tags.ToList(),
            SubmitForApproval = submit
        };

    // ============================================================
    // 1. S-08 Staff Authoring, Slugify, Submit & Recall (UTC001 -> UTC013)
    // ============================================================

    [Fact]
    public async Task UTC001_CreateAsync_DraftWithVietnameseTitle_ShouldSlugifyAndNormalizeUniqueTags()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Biên Tập", UserRole.Staff);
        db.Users.Add(staff);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var draft = await svc.CreateAsync(staff.Id, new SaveBlogPostRequest
        {
            Title = "  Đổi rác nhựa nhận quà xanh tại Hà Nội!  ",
            Summary = "  Tóm tắt hữu ích  ",
            Content = null,
            CoverImageUrl = null,
            Tags = new List<string> { "Nhựa PET", "nhựa pet", "Kinh tế tuần hoàn" },
            SubmitForApproval = false
        });

        draft.Status.Should().Be("Draft");
        draft.Slug.Should().Be("doi-rac-nhua-nhan-qua-xanh-tai-ha-noi");
        draft.Tags.Should().HaveCount(2); // khử trùng lặp thẻ không phân biệt hoa thường
        draft.CanEdit.Should().BeTrue();
        draft.CanSubmit.Should().BeTrue();
        draft.CanRecall.Should().BeFalse();
        draft.CanDelete.Should().BeTrue();
    }

    [Fact]
    public async Task UTC002_CreateAsync_DuplicateTitle_ShouldAppendNumericSuffixAndClampLongSlug()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Biên Tập", UserRole.Staff);
        db.Users.Add(staff);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var first = await svc.CreateAsync(staff.Id, ValidRequest("Phân loại rác sinh hoạt chuẩn EcoLink!"));
        var second = await svc.CreateAsync(staff.Id, ValidRequest("Phân loại rác sinh hoạt chuẩn EcoLink!"));

        first.Slug.Should().Be("phan-loai-rac-sinh-hoat-chuan-ecolink");
        second.Slug.Should().Be("phan-loai-rac-sinh-hoat-chuan-ecolink-2");

        var longPost = await svc.CreateAsync(staff.Id, ValidRequest(new string('a', 150)));
        longPost.Slug.Length.Should().BeLessOrEqualTo(200);
    }

    [Fact]
    public async Task UTC003_CreateAsync_WithSubmitForApprovalTrue_ShouldTransitionToPendingAndNotifyAdmins()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff A", UserRole.Staff);
        var admin = MakeUser("Admin Duyệt", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var created = await svc.CreateAsync(staff.Id, ValidRequest(submit: true));

        created.Status.Should().Be("Pending_Approval");
        created.SubmittedAt.Should().NotBeNull();
        created.CanEdit.Should().BeFalse();
        created.CanRecall.Should().BeTrue();
        (await db.Notifications.CountAsync(n => n.UserId == admin.Id && n.ReferenceId == created.Id)).Should().Be(1);
    }

    [Fact]
    public async Task UTC004_CreateAsync_SubmitWithoutCoverOrContent_ShouldThrowMSGAuth02()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Thiếu Trường", UserRole.Staff);
        db.Users.Add(staff);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var exNoCover = await FluentActions.Invoking(() => svc.CreateAsync(staff.Id, ValidRequest(submit: true, cover: null)))
            .Should().ThrowAsync<BusinessRuleException>();
        exNoCover.Which.MessageCode.Should().Be("MSG-AUTH-02");

        var exNoContent = await FluentActions.Invoking(() => svc.CreateAsync(staff.Id, ValidRequest(submit: true, content: "   ")))
            .Should().ThrowAsync<BusinessRuleException>();
        exNoContent.Which.MessageCode.Should().Be("MSG-AUTH-02");
    }

    [Fact]
    public async Task UTC005_UpdateAsync_OnDraft_ShouldUpdateFieldsAndRegenerateSlug()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Sửa", UserRole.Staff);
        db.Users.Add(staff);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var draft = await svc.CreateAsync(staff.Id, ValidRequest("Tiêu đề bản nháp ban đầu"));
        var updated = await svc.UpdateAsync(staff.Id, draft.Id, ValidRequest("Tiêu đề bài viết sau khi cập nhật mới", false, ValidCover, "<p>Mới</p>", "tai-che"));

        updated.Title.Should().Be("Tiêu đề bài viết sau khi cập nhật mới");
        updated.Slug.Should().Be("tieu-de-bai-viet-sau-khi-cap-nhat-moi");
        updated.Tags.Should().ContainSingle("tai-che");
    }

    [Fact]
    public async Task UTC006_UpdateAsync_OnPendingOrApprovedPost_ShouldThrowMSGGen01()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff", UserRole.Staff);
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var pending = await svc.CreateAsync(staff.Id, ValidRequest("Bài đang chờ duyệt không được sửa", submit: true));

        var exPending = await FluentActions.Invoking(() => svc.UpdateAsync(staff.Id, pending.Id, ValidRequest()))
            .Should().ThrowAsync<BusinessRuleException>();
        exPending.Which.MessageCode.Should().Be("MSG-GEN-01");

        await svc.ApproveAsync(admin.Id, pending.Id);
        var exApproved = await FluentActions.Invoking(() => svc.UpdateAsync(staff.Id, pending.Id, ValidRequest()))
            .Should().ThrowAsync<BusinessRuleException>();
        exApproved.Which.MessageCode.Should().Be("MSG-GEN-01");
    }

    [Fact]
    public async Task UTC007_GetMineAndUpdate_ByDifferentStaffAuthor_ShouldThrowNotFoundException()
    {
        using var db = TestDbContextFactory.Create();
        var authorA = MakeUser("Staff A", UserRole.Staff);
        var authorB = MakeUser("Staff B", UserRole.Staff);
        db.Users.AddRange(authorA, authorB);
        await db.SaveChangesAsync();

        var (svcA, _) = CreateService(db, authorA.Id);
        var post = await svcA.CreateAsync(authorA.Id, ValidRequest("Bài riêng của Staff A"));

        await FluentActions.Invoking(() => svcA.GetMineAsync(authorB.Id, post.Id))
            .Should().ThrowAsync<NotFoundException>();
        await FluentActions.Invoking(() => svcA.UpdateAsync(authorB.Id, post.Id, ValidRequest()))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UTC008_SubmitAsync_ValidDraft_ShouldTransitionToPendingAndNotifyAdmins()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Nộp", UserRole.Staff);
        var admin = MakeUser("Admin Nhận", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var draft = await svc.CreateAsync(staff.Id, ValidRequest());
        var submitted = await svc.SubmitAsync(staff.Id, draft.Id);

        submitted.Status.Should().Be("Pending_Approval");
        submitted.SubmittedAt.Should().NotBeNull();
        (await db.Notifications.AnyAsync(n => n.UserId == admin.Id && n.ReferenceId == draft.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task UTC009_SubmitAsync_OnAlreadyApprovedPost_ShouldThrowMSGGen01()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff", UserRole.Staff);
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var post = await svc.CreateAsync(staff.Id, ValidRequest(submit: true));
        await svc.ApproveAsync(admin.Id, post.Id);

        var ex = await FluentActions.Invoking(() => svc.SubmitAsync(staff.Id, post.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        ex.Which.MessageCode.Should().Be("MSG-GEN-01");
    }

    [Fact]
    public async Task UTC010_RecallAsync_OnPendingPost_ShouldRevertToDraftAndNotifyAdmins()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Thu Hồi", UserRole.Staff);
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var pending = await svc.CreateAsync(staff.Id, ValidRequest(submit: true));
        var recalled = await svc.RecallAsync(staff.Id, pending.Id);

        recalled.Status.Should().Be("Draft");
        recalled.SubmittedAt.Should().BeNull();
        recalled.CanEdit.Should().BeTrue();
        (await db.Notifications.CountAsync(n => n.UserId == admin.Id && n.ReferenceId == pending.Id)).Should().Be(2);
    }

    [Fact]
    public async Task UTC011_RecallAsync_OnApprovedPost_ShouldThrowMSGGen01()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff", UserRole.Staff);
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var post = await svc.CreateAsync(staff.Id, ValidRequest(submit: true));
        await svc.ApproveAsync(admin.Id, post.Id);

        var ex = await FluentActions.Invoking(() => svc.RecallAsync(staff.Id, post.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        ex.Which.MessageCode.Should().Be("MSG-GEN-01");
    }

    [Fact]
    public async Task UTC012_DeleteAsync_OnDraftPost_ShouldRemoveRecordFromDatabase()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Xóa", UserRole.Staff);
        db.Users.Add(staff);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var draft = await svc.CreateAsync(staff.Id, ValidRequest());
        await svc.DeleteAsync(staff.Id, draft.Id);

        (await db.BlogPosts.AnyAsync(p => p.Id == draft.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task UTC013_DeleteAsync_OnPendingOrApprovedPost_ShouldThrowMSGGen01()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff", UserRole.Staff);
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        var pending = await svc.CreateAsync(staff.Id, ValidRequest(submit: true));

        var exPending = await FluentActions.Invoking(() => svc.DeleteAsync(staff.Id, pending.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        exPending.Which.MessageCode.Should().Be("MSG-GEN-01");

        await svc.ApproveAsync(admin.Id, pending.Id);
        var exApproved = await FluentActions.Invoking(() => svc.DeleteAsync(staff.Id, pending.Id))
            .Should().ThrowAsync<BusinessRuleException>();
        exApproved.Which.MessageCode.Should().Be("MSG-GEN-01");
    }

    // ============================================================
    // 2. A-09 Admin Queue, Approve & Reject + G-01 Public View (UTC014 -> UTC020)
    // ============================================================

    [Fact]
    public async Task UTC014_ListQueueAsync_ShouldFilterPendingOnlyAndSortBySubmittedAt()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Nguyễn Tác Giả", UserRole.Staff);
        db.Users.Add(staff);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, staff.Id);
        await svc.CreateAsync(staff.Id, ValidRequest("Bài nháp không vào hàng đợi", submit: false));
        var first = await svc.CreateAsync(staff.Id, ValidRequest("Bài nộp đầu tiên về phân loại", submit: true));
        await Task.Delay(25);
        var second = await svc.CreateAsync(staff.Id, ValidRequest("Bài nộp thứ hai về kim loại", submit: true));

        var oldest = await svc.ListQueueAsync(null, "oldest", 1, 10);
        oldest.TotalCount.Should().Be(2);
        oldest.Items[0].Id.Should().Be(first.Id);

        var newest = await svc.ListQueueAsync("kim loại", "newest", 1, 10);
        newest.TotalCount.Should().Be(1);
        newest.Items[0].Id.Should().Be(second.Id);
    }

    [Fact]
    public async Task UTC015_ApproveAsync_OnPendingPost_ShouldPublishNotifyAuthorAndLogAudit()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff", UserRole.Staff);
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, admin.Id, "Admin");
        var pending = await svc.CreateAsync(staff.Id, ValidRequest(submit: true));
        var approved = await svc.ApproveAsync(admin.Id, pending.Id);

        approved.Status.Should().Be("Approved");
        approved.PublishedAt.Should().NotBeNull();
        approved.ApprovedAt.Should().NotBeNull();
        approved.RejectionReason.Should().BeNull();
        (await db.Notifications.AnyAsync(n => n.UserId == staff.Id && n.ReferenceId == pending.Id && n.Title == "Bài viết đã được duyệt")).Should().BeTrue();
        (await db.AuditLogs.AnyAsync(a => a.Action == "BLOG_APPROVE" && a.EntityId == pending.Id.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task UTC016_RejectAsync_WithValidFeedback_ShouldReturnToDraftWithFeedbackNotifyAndLogAudit()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff", UserRole.Staff);
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, admin.Id, "Admin");
        var pending = await svc.CreateAsync(staff.Id, ValidRequest(submit: true));
        var rejected = await svc.RejectAsync(admin.Id, pending.Id, "Cần bổ sung số liệu nguồn Bộ TNMT.");

        rejected.Status.Should().Be("Draft");
        rejected.RejectionReason.Should().Be("Cần bổ sung số liệu nguồn Bộ TNMT.");
        rejected.SubmittedAt.Should().BeNull();
        rejected.CanEdit.Should().BeTrue();
        (await db.Notifications.AnyAsync(n => n.UserId == staff.Id && n.ReferenceId == pending.Id && n.Message.Contains("Bộ TNMT"))).Should().BeTrue();
        (await db.AuditLogs.AnyAsync(a => a.Action == "BLOG_REJECT" && a.EntityId == pending.Id.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task UTC017_RejectAsync_WithBlankFeedback_ShouldThrowMSGAuth02()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff", UserRole.Staff);
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, admin.Id, "Admin");
        var pending = await svc.CreateAsync(staff.Id, ValidRequest(submit: true));

        var ex = await FluentActions.Invoking(() => svc.RejectAsync(admin.Id, pending.Id, "   "))
            .Should().ThrowAsync<BusinessRuleException>();
        ex.Which.MessageCode.Should().Be("MSG-AUTH-02");
    }

    [Fact]
    public async Task UTC018_ListPublishedAsync_ShouldMatchWholeTagOnlyAndExcludeDraftOrFuturePosts()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff", UserRole.Staff);
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, admin.Id, "Admin");
        await svc.CreateAsync(staff.Id, ValidRequest("Bài nháp chưa duyệt", false, ValidCover, "<p>A</p>", "nhua"));
        var pExact = await svc.CreateAsync(staff.Id, ValidRequest("Bài chuẩn thẻ nhựa chính xác", true, ValidCover, "<p>B</p>", "nhua"));
        var pPrefix = await svc.CreateAsync(staff.Id, ValidRequest("Bài thẻ mở rộng nhua-pet", true, ValidCover, "<p>C</p>", "nhua-pet"));
        await svc.ApproveAsync(admin.Id, pExact.Id);
        await svc.ApproveAsync(admin.Id, pPrefix.Id);

        var res = await svc.ListPublishedAsync(null, "nhua", 1, 10);
        res.TotalCount.Should().Be(1);
        res.Items.Single().Id.Should().Be(pExact.Id);
    }

    [Fact]
    public async Task UTC019_ListMineAsync_ShouldFilterByStatusSearchAndClampPageBounds()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Filter", UserRole.Staff);
        var admin = MakeUser("Admin", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, admin.Id, "Admin");
        await svc.CreateAsync(staff.Id, ValidRequest("Nháp phân loại thủy tinh"));
        var pub = await svc.CreateAsync(staff.Id, ValidRequest("Bài xuất bản giấy carton", submit: true));
        await svc.ApproveAsync(admin.Id, pub.Id);

        var onlyPublished = await svc.ListMineAsync(staff.Id, "Published", "carton", -1, 9999);
        onlyPublished.Page.Should().Be(1);
        onlyPublished.PageSize.Should().Be(50);
        onlyPublished.TotalCount.Should().Be(1);
        onlyPublished.Items[0].Id.Should().Be(pub.Id);
    }

    [Fact]
    public async Task UTC020_GetPublishedBySlugAndValidators_ShouldEnforceTitleTagsFeedbackAndNotFoundBounds()
    {
        using var db = TestDbContextFactory.Create();
        var staff = MakeUser("Staff Slug", UserRole.Staff);
        var admin = MakeUser("Admin Slug", UserRole.Admin);
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();

        var (svc, _) = CreateService(db, admin.Id, "Admin");
        var draft = await svc.CreateAsync(staff.Id, ValidRequest("Bài nháp không xem được theo slug"));
        await FluentActions.Invoking(() => svc.GetPublishedBySlugAsync(draft.Slug))
            .Should().ThrowAsync<NotFoundException>();

        var pending = await svc.CreateAsync(staff.Id, ValidRequest("Bài viết công khai hợp lệ theo slug", submit: true));
        await svc.ApproveAsync(admin.Id, pending.Id);
        var pub = await svc.GetPublishedBySlugAsync(pending.Slug);
        pub.AuthorName.Should().Be("Staff Slug");

        var saveVal = new SaveBlogPostRequestValidator();
        saveVal.Validate(new SaveBlogPostRequest { Title = "9 ký tự!!", SubmitForApproval = false }).IsValid.Should().BeFalse();
        saveVal.Validate(new SaveBlogPostRequest { Title = new string('t', 10), Tags = Enumerable.Range(1, 6).Select(i => $"tag{i}").ToList() }).IsValid.Should().BeFalse();
        saveVal.Validate(ValidRequest(submit: true)).IsValid.Should().BeTrue();

        var reviewVal = new ReviewBlogPostRequestValidator();
        reviewVal.Validate(new ReviewBlogPostRequest { Action = "Reject", Feedback = "" }).IsValid.Should().BeFalse();
        reviewVal.Validate(new ReviewBlogPostRequest { Action = "Reject", Feedback = "Cần sửa lại mở bài." }).IsValid.Should().BeTrue();
        reviewVal.Validate(new ReviewBlogPostRequest { Approve = true }).IsValid.Should().BeTrue();

        var rejectVal = new RejectBlogPostRequestValidator();
        rejectVal.Validate(new RejectBlogPostRequest { Feedback = "" }).IsValid.Should().BeFalse();
        rejectVal.Validate(new RejectBlogPostRequest { Feedback = new string('x', 2001) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UTC021_SeedDefaultBlogPostsCoreAsync_ShouldSeedIdempotentlyWithValidSlugsCoversAndWorkflowStates()
    {
        using var db = TestDbContextFactory.Create();
        var (svc, _) = CreateService(db, Guid.NewGuid());

        // Chắc chắn ListPublishedAsync trên InMemory DB không tự ý seed làm nhiễu các test rỗng
        var beforeSeed = await svc.ListPublishedAsync(null, null, 1, 20);
        beforeSeed.TotalCount.Should().Be(0);

        await svc.SeedDefaultBlogPostsCoreAsync();
        await svc.SeedDefaultBlogPostsCoreAsync(); // Gọi lại không tạo trùng

        (await db.BlogPosts.CountAsync()).Should().Be(6);
        (await db.Users.CountAsync(u => u.Username == "staff")).Should().Be(1);
        (await db.Users.CountAsync(u => u.Username == "admin")).Should().Be(1);

        var published = await svc.ListPublishedAsync(null, null, 1, 20);
        published.TotalCount.Should().Be(4);
        published.Items[0].Title.Should().Contain("Khủng hoảng rác thải nhựa đại dương");
        published.Items.Should().OnlyContain(p =>
            !string.IsNullOrWhiteSpace(p.Slug) &&
            !string.IsNullOrWhiteSpace(p.CoverImageUrl) &&
            p.CoverImageUrl!.StartsWith("/assets/insights/", StringComparison.Ordinal) &&
            p.Author.FullName == "Lê Thu Hà");

        var featuredDetail = await svc.GetPublishedBySlugAsync(published.Items[0].Slug);
        featuredDetail.Content.Should().Contain("EPR");

        var staffUser = await db.Users.FirstAsync(u => u.Username == "staff");
        var mine = await svc.ListMineAsync(staffUser.Id, null, null, 1, 20);
        mine.TotalCount.Should().Be(6);

        var queue = await svc.ListQueueAsync(null, "oldest", 1, 20);
        queue.TotalCount.Should().Be(1);
        queue.Items[0].Status.Should().Be("Pending_Approval");
    }
}
