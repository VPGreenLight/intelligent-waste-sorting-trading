using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

/// <summary>Cấu hình bảng recycler_price_list theo báo cáo thiết kế (RecyclerPriceList, mục 3.3.1).</summary>
public class RecyclerPriceConfiguration : IEntityTypeConfiguration<RecyclerPrice>
{
    public void Configure(EntityTypeBuilder<RecyclerPrice> builder)
    {
        builder.ToTable("recycler_price_list", t =>
        {
            t.HasCheckConstraint("ck_recycler_price_list_price_positive", "price_per_kg > 0");
            t.HasCheckConstraint("ck_recycler_price_list_min_quantity_positive", "min_quantity_kg > 0");
        });
        builder.HasKey(p => p.Id).HasName("pk_recycler_price_list");

        builder.Property(p => p.Id).HasColumnName("price_id");
        builder.Property(p => p.RecyclerId).HasColumnName("recycler_id").IsRequired();
        builder.Property(p => p.WasteCategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(p => p.PricePerKg).HasColumnName("price_per_kg").HasPrecision(12, 2).IsRequired();
        builder.Property(p => p.MinQuantityKg).HasColumnName("min_quantity_kg").HasPrecision(12, 3).HasDefaultValue(1.0m).IsRequired();
        builder.Property(p => p.CollectionConditions).HasColumnName("collection_conditions").HasMaxLength(200);
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.EffectiveFrom).HasColumnName("effective_from").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(p => p.EffectiveTo).HasColumnName("effective_to");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasOne(p => p.Recycler)
            .WithMany(r => r.Prices)
            .HasForeignKey(p => p.RecyclerId)
            .HasConstraintName("fk_recycler_price_list_recycler_profiles")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.WasteCategory)
            .WithMany()
            .HasForeignKey(p => p.WasteCategoryId)
            .HasConstraintName("fk_recycler_price_list_waste_categories")
            .OnDelete(DeleteBehavior.Restrict);

        // Mỗi cơ sở chỉ có một dòng giá cho mỗi loại phế liệu
        builder.HasIndex(p => new { p.RecyclerId, p.WasteCategoryId })
            .IsUnique()
            .HasDatabaseName("ux_recycler_price_list_recycler_category");
    }
}
