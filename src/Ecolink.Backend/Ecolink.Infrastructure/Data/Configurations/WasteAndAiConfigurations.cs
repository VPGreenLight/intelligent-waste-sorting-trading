using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

/// <summary>
/// Cấu hình bảng waste_categories (MasterWasteCategory) theo 02_ai_waste_classification_module.dbml & Table design.md (Mục 2.1).
/// </summary>
public class WasteCategoryConfiguration : IEntityTypeConfiguration<WasteCategory>
{
    public void Configure(EntityTypeBuilder<WasteCategory> builder)
    {
        builder.ToTable("waste_categories");
        builder.HasKey(c => c.Id).HasName("pk_waste_categories");

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description");
        builder.Property(c => c.StandardBinColor).HasColumnName("standard_bin_color").HasMaxLength(50).IsRequired();
        builder.Property(c => c.IconUrl).HasColumnName("icon_url");
        builder.Property(c => c.IsRecyclable).HasColumnName("is_recyclable").HasDefaultValue(true).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Active").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("uq_waste_categories_code");
    }
}

/// <summary>
/// Cấu hình bảng sorting_guidance_rules (SortingGuidanceRule) theo 02_ai_waste_classification_module.dbml & Table design.md (Mục 2.2).
/// </summary>
public class SortingRuleConfiguration : IEntityTypeConfiguration<SortingRule>
{
    public void Configure(EntityTypeBuilder<SortingRule> builder)
    {
        builder.ToTable("sorting_guidance_rules");
        builder.HasKey(r => r.Id).HasName("pk_sorting_guidance_rules");

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.WasteCategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(r => r.Title).HasColumnName("title").HasMaxLength(150).HasDefaultValue(string.Empty).IsRequired();
        builder.Property(r => r.PreparationSteps).HasColumnName("preparation_steps").IsRequired();
        builder.Property(r => r.SortingInstruction).HasColumnName("sorting_instruction").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(r => r.HandlingAction).HasColumnName("handling_action").IsRequired();
        builder.Property(r => r.HazardWarning).HasColumnName("hazard_warning");
        builder.Property(r => r.ApplicableRegion).HasColumnName("applicable_region").HasMaxLength(100).HasDefaultValue("VN_NATIONAL").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("ACTIVE").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        // Quan hệ 1-1 với WasteCategory theo BR-SRT-01
        builder.HasIndex(r => r.WasteCategoryId).IsUnique().HasDatabaseName("uq_sorting_guidance_rules_category_id");

        builder.HasOne(r => r.WasteCategory)
            .WithOne(c => c.SortingRule)
            .HasForeignKey<SortingRule>(r => r.WasteCategoryId)
            .HasConstraintName("fk_sorting_guidance_rules_waste_categories")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Cấu hình bảng ai_feedback_logs theo chuẩn snake_case tại database_design_and_audit_guidelines.md.
/// </summary>
public class AIFeedbackLogConfiguration : IEntityTypeConfiguration<AIFeedbackLog>
{
    public void Configure(EntityTypeBuilder<AIFeedbackLog> builder)
    {
        builder.ToTable("ai_feedback_logs");
        builder.HasKey(f => f.Id).HasName("pk_ai_feedback_logs");

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.UserId).HasColumnName("user_id");
        builder.Property(f => f.ImageUrl).HasColumnName("image_url").IsRequired();
        builder.Property(f => f.PredictedLabel).HasColumnName("predicted_label").HasMaxLength(50).IsRequired();
        builder.Property(f => f.ConfidenceScore).HasColumnName("confidence_score").IsRequired();
        builder.Property(f => f.UserReportedLabel).HasColumnName("user_reported_label").HasMaxLength(50).IsRequired();
        builder.Property(f => f.AdminVerifiedLabel).HasColumnName("admin_verified_label").HasMaxLength(50);
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.Note).HasColumnName("note");
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(f => f.UserId).HasDatabaseName("idx_ai_feedback_logs_user_id");

        builder.HasOne(f => f.User)
            .WithMany(u => u.Feedbacks)
            .HasForeignKey(f => f.UserId)
            .HasConstraintName("fk_ai_feedback_logs_users")
            .OnDelete(DeleteBehavior.SetNull);
    }
}

