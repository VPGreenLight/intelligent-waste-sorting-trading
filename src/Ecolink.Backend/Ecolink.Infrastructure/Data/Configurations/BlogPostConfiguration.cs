using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ecolink.Infrastructure.Data.Configurations;

/// <summary>Cấu hình bảng blog_posts theo báo cáo thiết kế (BlogPost, mục 3.6.2).</summary>
public class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    // Chuỗi lưu DB đúng như báo cáo: Draft, Pending_Approval, Approved, Rejected
    private static readonly ValueConverter<BlogPostStatus, string> StatusConverter = new(
        v => v == BlogPostStatus.PendingApproval ? "Pending_Approval" : v.ToString(),
        s => s == "Pending_Approval" ? BlogPostStatus.PendingApproval : Enum.Parse<BlogPostStatus>(s));

    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.ToTable("blog_posts");
        builder.HasKey(p => p.Id).HasName("pk_blog_posts");

        builder.Property(p => p.Id).HasColumnName("post_id");
        builder.Property(p => p.AuthorId).HasColumnName("author_staff_id").IsRequired();
        builder.Property(p => p.Title).HasColumnName("title").HasMaxLength(250).IsRequired();
        builder.Property(p => p.Slug).HasColumnName("slug").HasMaxLength(280).IsRequired();
        builder.Property(p => p.Summary).HasColumnName("summary");
        builder.Property(p => p.Content).HasColumnName("content").IsRequired();
        builder.Property(p => p.CoverImageUrl).HasColumnName("cover_image_url");
        builder.Property(p => p.Tags).HasColumnName("tags").HasDefaultValue("[]").IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").HasConversion(StatusConverter).HasMaxLength(20)
            .HasDefaultValue(BlogPostStatus.Draft).IsRequired();
        builder.Property(p => p.ApprovedByAdminId).HasColumnName("approved_by_admin_id");
        builder.Property(p => p.RejectionReason).HasColumnName("rejection_reason");
        builder.Property(p => p.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(p => p.ApprovedAt).HasColumnName("approved_at");
        builder.Property(p => p.PublishedAt).HasColumnName("published_at");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasOne<User>().WithMany()
            .HasForeignKey(p => p.AuthorId)
            .HasConstraintName("fk_blog_posts_author")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(p => p.ApprovedByAdminId)
            .HasConstraintName("fk_blog_posts_approved_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Slug).IsUnique().HasDatabaseName("ux_blog_posts_slug");
        builder.HasIndex(p => new { p.Status, p.PublishedAt }).HasDatabaseName("ix_blog_posts_status_published");
        builder.HasIndex(p => new { p.AuthorId, p.Status }).HasDatabaseName("ix_blog_posts_author_status");
    }
}

/// <summary>Cấu hình bảng notifications (báo cáo mục 3.7.4).</summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id).HasName("pk_notifications");

        builder.Property(n => n.Id).HasColumnName("notification_id");
        builder.Property(n => n.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(n => n.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasColumnName("message").IsRequired();
        builder.Property(n => n.NotificationType).HasColumnName("notification_type").HasMaxLength(50).IsRequired();
        builder.Property(n => n.ReferenceType).HasColumnName("reference_type").HasMaxLength(50);
        builder.Property(n => n.ReferenceId).HasColumnName("reference_id");
        builder.Property(n => n.IsRead).HasColumnName("is_read").HasDefaultValue(false).IsRequired();
        builder.Property(n => n.ReadAt).HasColumnName("read_at");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasOne<User>().WithMany()
            .HasForeignKey(n => n.UserId)
            .HasConstraintName("fk_notifications_users")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt }).HasDatabaseName("ix_notifications_user_read_created");
    }
}

/// <summary>Cấu hình bảng audit_logs (báo cáo mục 3.7.5).</summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id).HasName("pk_audit_logs");

        builder.Property(a => a.Id).HasColumnName("audit_id");
        builder.Property(a => a.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasColumnName("entity_type").HasMaxLength(100);
        builder.Property(a => a.EntityId).HasColumnName("entity_id").HasMaxLength(100);
        builder.Property(a => a.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
        builder.Property(a => a.NewValues).HasColumnName("new_values").HasColumnType("jsonb");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasOne<User>().WithMany()
            .HasForeignKey(a => a.ActorUserId)
            .HasConstraintName("fk_audit_logs_users")
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => new { a.EntityType, a.EntityId }).HasDatabaseName("ix_audit_logs_entity");
        builder.HasIndex(a => a.CreatedAt).HasDatabaseName("ix_audit_logs_created");
    }
}
