using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

/// <summary>
/// Cấu hình bảng platform_policy_sections lưu Điều khoản dịch vụ &amp; Chính sách môi trường (/terms).
/// </summary>
public class PlatformPolicySectionConfiguration : IEntityTypeConfiguration<PlatformPolicySection>
{
    public void Configure(EntityTypeBuilder<PlatformPolicySection> b)
    {
        b.ToTable("platform_policy_sections");
        b.HasKey(x => x.Id).HasName("pk_platform_policy_sections");

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
        b.Property(x => x.Category).HasColumnName("category").HasMaxLength(30).HasDefaultValue("TERMS").IsRequired();
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        b.Property(x => x.Content).HasColumnName("content").IsRequired();
        b.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(1).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("ACTIVE").IsRequired();
        b.Property(x => x.UpdatedByAdminId).HasColumnName("updated_by_admin_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_platform_policy_sections_code");

        b.HasIndex(x => new { x.Status, x.DisplayOrder })
            .HasDatabaseName("ix_platform_policy_sections_status_order");
    }
}
