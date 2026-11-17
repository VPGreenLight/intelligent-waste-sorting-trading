using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

/// <summary>
/// Cấu hình bảng ai_scan_histories (Lưu vết phiên quét ảnh gốc + SHA-256 + OOD - U-01, U-04).
/// </summary>
public class AiScanHistoryConfiguration : IEntityTypeConfiguration<AiScanHistory>
{
    public void Configure(EntityTypeBuilder<AiScanHistory> builder)
    {
        builder.ToTable("ai_scan_histories");
        builder.HasKey(s => s.Id).HasName("pk_ai_scan_histories");

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.UserId).HasColumnName("user_id");
        builder.Property(s => s.ImageUrl).HasColumnName("image_url").IsRequired();
        builder.Property(s => s.ImageStorageKey).HasColumnName("image_storage_key");
        builder.Property(s => s.ImageSha256).HasColumnName("image_sha256").HasMaxLength(64).IsRequired();
        builder.Property(s => s.ImageWidth).HasColumnName("image_width").HasDefaultValue(0).IsRequired();
        builder.Property(s => s.ImageHeight).HasColumnName("image_height").HasDefaultValue(0).IsRequired();
        builder.Property(s => s.ModelUsed).HasColumnName("model_used").HasMaxLength(50).HasDefaultValue("yolov8_detect").IsRequired();
        builder.Property(s => s.PredictedLabel).HasColumnName("predicted_label").HasMaxLength(50).HasDefaultValue("unknown").IsRequired();
        builder.Property(s => s.ConfidenceScore).HasColumnName("confidence_score").HasDefaultValue(0d).IsRequired();
        builder.Property(s => s.IsOutOfDistribution).HasColumnName("is_out_of_distribution").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.DetectedObjectsJson).HasColumnName("detected_objects_json").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Property(s => s.ScanStatus).HasColumnName("scan_status").HasMaxLength(30).HasDefaultValue("Completed").IsRequired();

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(s => new { s.UserId, s.CreatedAt })
            .HasDatabaseName("idx_ai_scan_histories_user")
            .HasFilter("is_deleted = FALSE");
        builder.HasIndex(s => s.ImageSha256).HasDatabaseName("idx_ai_scan_histories_sha256");
        builder.HasIndex(s => new { s.IsOutOfDistribution, s.CreatedAt }).HasDatabaseName("idx_ai_scan_histories_ood");

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .HasConstraintName("fk_ai_scan_histories_users")
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

/// <summary>
/// Cấu hình bảng ai_dataset_items (Kho mẫu gán nhãn Ground-Truth &amp; Sticky Split - S-02, S-03).
/// </summary>
public class AiDatasetItemConfiguration : IEntityTypeConfiguration<AiDatasetItem>
{
    public void Configure(EntityTypeBuilder<AiDatasetItem> builder)
    {
        builder.ToTable("ai_dataset_items", t =>
        {
            t.HasCheckConstraint(
                "chk_ai_dataset_items_verified_label",
                "status <> 'VerifiedLabeled' OR (ground_truth_category_id IS NOT NULL AND ground_truth_label IS NOT NULL)");
        });

        builder.HasKey(i => i.Id).HasName("pk_ai_dataset_items");

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.SourceType).HasColumnName("source_type").HasConversion<string>().HasMaxLength(30).HasDefaultValue(DatasetItemSourceType.UserFeedback).IsRequired();
        builder.Property(i => i.SourceScanId).HasColumnName("source_scan_id");
        builder.Property(i => i.SourceFeedbackId).HasColumnName("source_feedback_id");
        builder.Property(i => i.ImageUrl).HasColumnName("image_url").IsRequired();
        builder.Property(i => i.ImageSha256).HasColumnName("image_sha256").HasMaxLength(64).IsRequired();
        builder.Property(i => i.ImageWidth).HasColumnName("image_width").HasDefaultValue(0).IsRequired();
        builder.Property(i => i.ImageHeight).HasColumnName("image_height").HasDefaultValue(0).IsRequired();
        builder.Property(i => i.PredictedLabel).HasColumnName("predicted_label").HasMaxLength(50);
        builder.Property(i => i.PredictedConfidence).HasColumnName("predicted_confidence");
        builder.Property(i => i.GroundTruthCategoryId).HasColumnName("ground_truth_category_id");
        builder.Property(i => i.GroundTruthLabel).HasColumnName("ground_truth_label").HasMaxLength(50);
        builder.Property(i => i.YoloAnnotationsJson).HasColumnName("yolo_annotations_json").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).HasDefaultValue(DatasetItemStatus.Unlabeled).IsRequired();
        builder.Property(i => i.StickySplit).HasColumnName("sticky_split").HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.LabeledByStaffId).HasColumnName("labeled_by_staff_id");
        builder.Property(i => i.LabeledAt).HasColumnName("labeled_at");
        builder.Property(i => i.QualityNote).HasColumnName("quality_note");

        builder.Property(i => i.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(i => i.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System").IsRequired();
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(i => i.DeletedAt).HasColumnName("deleted_at");

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(i => i.ImageSha256).IsUnique().HasDatabaseName("uq_ai_dataset_items_sha256");
        builder.HasIndex(i => new { i.Status, i.GroundTruthLabel, i.CreatedAt })
            .HasDatabaseName("idx_ai_dataset_items_queue")
            .HasFilter("is_deleted = FALSE");
        builder.HasIndex(i => new { i.StickySplit, i.GroundTruthLabel })
            .HasDatabaseName("idx_ai_dataset_items_split")
            .HasFilter("is_deleted = FALSE");

        builder.HasOne(i => i.GroundTruthCategory)
            .WithMany()
            .HasForeignKey(i => i.GroundTruthCategoryId)
            .HasConstraintName("fk_ai_dataset_items_category")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(i => i.SourceScan)
            .WithMany()
            .HasForeignKey(i => i.SourceScanId)
            .HasConstraintName("fk_ai_dataset_items_scan")
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(i => i.SourceFeedback)
            .WithMany()
            .HasForeignKey(i => i.SourceFeedbackId)
            .HasConstraintName("fk_ai_dataset_items_feedback")
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(i => i.LabeledByStaff)
            .WithMany()
            .HasForeignKey(i => i.LabeledByStaffId)
            .HasConstraintName("fk_ai_dataset_items_staff")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

/// <summary>
/// Cấu hình bảng ai_dataset_versions (Quản lý phiên bản Dataset Train/Val/Test - S-03).
/// </summary>
public class AiDatasetVersionConfiguration : IEntityTypeConfiguration<AiDatasetVersion>
{
    public void Configure(EntityTypeBuilder<AiDatasetVersion> builder)
    {
        builder.ToTable("ai_dataset_versions", t =>
        {
            t.HasCheckConstraint(
                "chk_ai_dataset_versions_ratios",
                "train_ratio > 0 AND val_ratio >= 0 AND test_ratio >= 0 AND (train_ratio + val_ratio + test_ratio = 100)");
        });

        builder.HasKey(v => v.Id).HasName("pk_ai_dataset_versions");

        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.VersionCode).HasColumnName("version_code").HasMaxLength(30).IsRequired();
        builder.Property(v => v.VersionName).HasColumnName("version_name").HasMaxLength(150).IsRequired();
        builder.Property(v => v.Description).HasColumnName("description");
        builder.Property(v => v.TrainRatio).HasColumnName("train_ratio").HasDefaultValue(70).IsRequired();
        builder.Property(v => v.ValRatio).HasColumnName("val_ratio").HasDefaultValue(20).IsRequired();
        builder.Property(v => v.TestRatio).HasColumnName("test_ratio").HasDefaultValue(10).IsRequired();
        builder.Property(v => v.TotalSamples).HasColumnName("total_samples").HasDefaultValue(0).IsRequired();
        builder.Property(v => v.TrainCount).HasColumnName("train_count").HasDefaultValue(0).IsRequired();
        builder.Property(v => v.ValCount).HasColumnName("val_count").HasDefaultValue(0).IsRequired();
        builder.Property(v => v.TestCount).HasColumnName("test_count").HasDefaultValue(0).IsRequired();
        builder.Property(v => v.ClassDistributionJson).HasColumnName("class_distribution_json").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(v => v.ImbalanceRatio).HasColumnName("imbalance_ratio").HasColumnType("numeric(6,2)").HasDefaultValue(1.00m).IsRequired();
        builder.Property(v => v.IsBalanced).HasColumnName("is_balanced").HasDefaultValue(true).IsRequired();
        builder.Property(v => v.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).HasDefaultValue(DatasetVersionStatus.Published).IsRequired();
        builder.Property(v => v.SyncedToAiAt).HasColumnName("synced_to_ai_at");
        builder.Property(v => v.CreatedByStaffId).HasColumnName("created_by_staff_id").IsRequired();

        builder.Property(v => v.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(v => v.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System").IsRequired();
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");
        builder.Property(v => v.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(v => v.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(v => v.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(v => v.VersionCode).IsUnique().HasDatabaseName("uq_ai_dataset_versions_code");

        builder.HasOne(v => v.CreatedByStaff)
            .WithMany()
            .HasForeignKey(v => v.CreatedByStaffId)
            .HasConstraintName("fk_ai_dataset_versions_staff")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasQueryFilter(v => !v.IsDeleted);
    }
}

/// <summary>
/// Cấu hình bảng ai_dataset_version_items (Ảnh chụp phân tập cố định cho từng phiên bản Dataset).
/// </summary>
public class AiDatasetVersionItemConfiguration : IEntityTypeConfiguration<AiDatasetVersionItem>
{
    public void Configure(EntityTypeBuilder<AiDatasetVersionItem> builder)
    {
        builder.ToTable("ai_dataset_version_items");
        builder.HasKey(vi => vi.Id).HasName("pk_ai_dataset_version_items");

        builder.Property(vi => vi.Id).HasColumnName("id");
        builder.Property(vi => vi.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(vi => vi.DatasetItemId).HasColumnName("dataset_item_id").IsRequired();
        builder.Property(vi => vi.AssignedSplit).HasColumnName("assigned_split").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(vi => vi.CategoryCodeSnapshot).HasColumnName("category_code_snapshot").HasMaxLength(50).IsRequired();
        builder.Property(vi => vi.YoloAnnotationsSnapshot).HasColumnName("yolo_annotations_snapshot").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Property(vi => vi.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(vi => vi.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(vi => new { vi.VersionId, vi.DatasetItemId })
            .IsUnique()
            .HasDatabaseName("uq_ai_dataset_version_items_pair");

        builder.HasIndex(vi => new { vi.VersionId, vi.AssignedSplit, vi.CategoryCodeSnapshot })
            .HasDatabaseName("idx_ai_dataset_version_items_split");

        builder.HasOne(vi => vi.Version)
            .WithMany(v => v.Items)
            .HasForeignKey(vi => vi.VersionId)
            .HasConstraintName("fk_ai_dataset_version_items_ver")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(vi => vi.DatasetItem)
            .WithMany(i => i.VersionItems)
            .HasForeignKey(vi => vi.DatasetItemId)
            .HasConstraintName("fk_ai_dataset_version_items_item")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasQueryFilter(vi => vi.Version == null || !vi.Version.IsDeleted);
    }
}
