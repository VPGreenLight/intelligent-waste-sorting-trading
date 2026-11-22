using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

/// <summary>
/// Cấu hình bảng transaction_reviews (U-10: Đánh giá sao, báo cáo vi phạm &amp; kiểm duyệt - BR-RVW-01..03).
/// </summary>
public class TransactionReviewConfiguration : IEntityTypeConfiguration<TransactionReview>
{
    public void Configure(EntityTypeBuilder<TransactionReview> builder)
    {
        builder.ToTable("transaction_reviews", t =>
        {
            t.HasCheckConstraint("chk_transaction_reviews_stars", "rating_stars BETWEEN 1 AND 5");
        });

        builder.HasKey(r => r.Id).HasName("pk_transaction_reviews");

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(r => r.ReviewerId).HasColumnName("reviewer_id").IsRequired();
        builder.Property(r => r.RecyclerId).HasColumnName("recycler_id").IsRequired();
        builder.Property(r => r.RatingStars).HasColumnName("rating_stars").IsRequired();
        builder.Property(r => r.Comment).HasColumnName("comment").HasMaxLength(1000);
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).HasDefaultValue(ReviewStatus.Visible).IsRequired();
        builder.Property(r => r.ReportCount).HasColumnName("report_count").HasDefaultValue(0).IsRequired();
        builder.Property(r => r.LastReportReason).HasColumnName("last_report_reason").HasMaxLength(500);
        builder.Property(r => r.ReportedByUserId).HasColumnName("reported_by_user_id");
        builder.Property(r => r.ModeratedByStaffId).HasColumnName("moderated_by_staff_id");
        builder.Property(r => r.ModeratedAt).HasColumnName("moderated_at");
        builder.Property(r => r.ModerationReason).HasColumnName("moderation_reason");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");

        builder.Property<uint>("xmin").IsRowVersion();

        builder.HasIndex(r => new { r.RequestId, r.ReviewerId })
            .IsUnique()
            .HasDatabaseName("uq_transaction_reviews_request_reviewer");

        builder.HasIndex(r => new { r.RecyclerId, r.Status, r.CreatedAt })
            .HasDatabaseName("idx_transaction_reviews_recycler_status")
            .HasFilter("is_deleted = FALSE");

        builder.HasIndex(r => new { r.ReviewerId, r.CreatedAt })
            .HasDatabaseName("idx_transaction_reviews_reviewer")
            .HasFilter("is_deleted = FALSE");

        builder.HasOne(r => r.Request)
            .WithMany()
            .HasForeignKey(r => r.RequestId)
            .HasConstraintName("fk_transaction_reviews_request")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerId)
            .HasConstraintName("fk_transaction_reviews_reviewer")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Recycler)
            .WithMany()
            .HasForeignKey(r => r.RecyclerId)
            .HasConstraintName("fk_transaction_reviews_recycler")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReportedByUser)
            .WithMany()
            .HasForeignKey(r => r.ReportedByUserId)
            .HasConstraintName("fk_transaction_reviews_reporter")
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(r => r.ModeratedByStaff)
            .WithMany()
            .HasForeignKey(r => r.ModeratedByStaffId)
            .HasConstraintName("fk_transaction_reviews_moderator")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}

/// <summary>
/// Cấu hình bảng transaction_complaints (U-11 &amp; S-05: Khiếu nại 2 chiều &amp; Trọng tài 360 - BR-DSP-01..06).
/// </summary>
public class TransactionComplaintConfiguration : IEntityTypeConfiguration<TransactionComplaint>
{
    public void Configure(EntityTypeBuilder<TransactionComplaint> builder)
    {
        builder.ToTable("transaction_complaints");
        builder.HasKey(c => c.Id).HasName("pk_transaction_complaints");

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.ComplaintCode).HasColumnName("complaint_code").HasMaxLength(32).IsRequired();
        builder.Property(c => c.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(c => c.ReceiptId).HasColumnName("receipt_id");
        builder.Property(c => c.ComplainantId).HasColumnName("complainant_id").IsRequired();
        builder.Property(c => c.ComplainantRole).HasColumnName("complainant_role").HasMaxLength(20).HasDefaultValue("Citizen").IsRequired();
        builder.Property(c => c.RespondentId).HasColumnName("respondent_id").IsRequired();
        builder.Property(c => c.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description").IsRequired();
        builder.Property(c => c.EvidenceUrlsJson).HasColumnName("evidence_urls_json").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).HasDefaultValue(ComplaintStatus.Open).IsRequired();
        builder.Property(c => c.AssignedStaffId).HasColumnName("assigned_staff_id");
        builder.Property(c => c.TakenAt).HasColumnName("taken_at");
        builder.Property(c => c.ResolutionOutcome).HasColumnName("resolution_outcome").HasConversion<string>().HasMaxLength(40);
        builder.Property(c => c.ResolutionNote).HasColumnName("resolution_note");
        builder.Property(c => c.ResolvedByStaffId).HasColumnName("resolved_by_staff_id");
        builder.Property(c => c.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(c => c.PointsRevoked).HasColumnName("points_revoked").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.WarningLogged).HasColumnName("warning_logged").HasDefaultValue(false).IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");

        builder.Property<uint>("xmin").IsRowVersion();

        builder.HasIndex(c => c.ComplaintCode)
            .IsUnique()
            .HasDatabaseName("uq_transaction_complaints_code");

        // Chặn tuyệt đối nhiều hơn 1 khiếu nại Open/Investigating trên cùng 1 HandoverRequest (BR-DSP-01)
        builder.HasIndex(c => c.RequestId)
            .IsUnique()
            .HasDatabaseName("uq_transaction_complaints_active_request")
            .HasFilter("status IN ('Open', 'Investigating') AND is_deleted = FALSE");

        builder.HasIndex(c => new { c.Status, c.CreatedAt })
            .HasDatabaseName("idx_transaction_complaints_status_created")
            .HasFilter("is_deleted = FALSE");

        builder.HasIndex(c => new { c.ComplainantId, c.RespondentId, c.CreatedAt })
            .HasDatabaseName("idx_transaction_complaints_parties")
            .HasFilter("is_deleted = FALSE");

        builder.HasOne(c => c.Request)
            .WithMany()
            .HasForeignKey(c => c.RequestId)
            .HasConstraintName("fk_transaction_complaints_request")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Receipt)
            .WithMany()
            .HasForeignKey(c => c.ReceiptId)
            .HasConstraintName("fk_transaction_complaints_receipt")
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(c => c.Complainant)
            .WithMany()
            .HasForeignKey(c => c.ComplainantId)
            .HasConstraintName("fk_transaction_complaints_complainant")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Respondent)
            .WithMany()
            .HasForeignKey(c => c.RespondentId)
            .HasConstraintName("fk_transaction_complaints_respondent")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.AssignedStaff)
            .WithMany()
            .HasForeignKey(c => c.AssignedStaffId)
            .HasConstraintName("fk_transaction_complaints_assigned")
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(c => c.ResolvedByStaff)
            .WithMany()
            .HasForeignKey(c => c.ResolvedByStaffId)
            .HasConstraintName("fk_transaction_complaints_resolved_by")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
