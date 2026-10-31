using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

/// <summary>
/// Cấu hình bảng user_eco_profiles theo chuẩn snake_case tại database_design_and_audit_guidelines.md.
/// </summary>
public class UserEcoProfileConfiguration : IEntityTypeConfiguration<UserEcoProfile>
{
    public void Configure(EntityTypeBuilder<UserEcoProfile> builder)
    {
        builder.ToTable("user_eco_profiles");
        builder.HasKey(e => e.Id).HasName("pk_user_eco_profiles");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.CurrentGreenPoints).HasColumnName("current_green_points").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.TotalLifetimePoints).HasColumnName("total_lifetime_points").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.TreeLevel).HasColumnName("tree_level").HasDefaultValue(1).IsRequired();
        builder.Property(e => e.TreeExperience).HasColumnName("tree_experience").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.TotalWasteScanned).HasColumnName("total_waste_scanned").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.TotalRecycledKg).HasColumnName("total_recycled_kg").HasPrecision(12, 3).HasDefaultValue(0m).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.UserId).IsUnique().HasDatabaseName("uq_user_eco_profiles_user_id");

        builder.HasOne(e => e.User)
            .WithOne(u => u.EcoProfile)
            .HasForeignKey<UserEcoProfile>(e => e.UserId)
            .HasConstraintName("fk_user_eco_profiles_users")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Cấu hình bảng collection_partners theo chuẩn snake_case.
/// </summary>
public class CollectionPartnerConfiguration : IEntityTypeConfiguration<CollectionPartner>
{
    public void Configure(EntityTypeBuilder<CollectionPartner> builder)
    {
        builder.ToTable("collection_partners");
        builder.HasKey(p => p.Id).HasName("pk_collection_partners");

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(p => p.FacilityName).HasColumnName("facility_name").HasMaxLength(150).IsRequired();
        builder.Property(p => p.Address).HasColumnName("address").IsRequired();
        builder.Property(p => p.Latitude).HasColumnName("latitude").IsRequired();
        builder.Property(p => p.Longitude).HasColumnName("longitude").IsRequired();
        builder.Property(p => p.OperatingHours).HasColumnName("operating_hours").HasMaxLength(100);
        builder.Property(p => p.ContactPhone).HasColumnName("contact_phone").HasMaxLength(20).IsRequired();
        builder.Property(p => p.IsVerified).HasColumnName("is_verified").HasDefaultValue(false).IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(p => p.UserId).IsUnique().HasDatabaseName("uq_collection_partners_user_id");

        builder.HasOne(p => p.User)
            .WithOne(u => u.PartnerProfile)
            .HasForeignKey<CollectionPartner>(p => p.UserId)
            .HasConstraintName("fk_collection_partners_users")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Cấu hình bảng accepted_materials theo chuẩn snake_case.
/// </summary>
public class AcceptedMaterialConfiguration : IEntityTypeConfiguration<AcceptedMaterial>
{
    public void Configure(EntityTypeBuilder<AcceptedMaterial> builder)
    {
        builder.ToTable("accepted_materials");
        builder.HasKey(m => m.Id).HasName("pk_accepted_materials");

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.PartnerId).HasColumnName("partner_id").IsRequired();
        builder.Property(m => m.WasteCategoryId).HasColumnName("waste_category_id").IsRequired();
        builder.Property(m => m.PricePerKg).HasColumnName("price_per_kg").HasPrecision(12, 2).IsRequired();
        builder.Property(m => m.MinimumQuantityKg).HasColumnName("minimum_quantity_kg").HasPrecision(12, 3).HasDefaultValue(1.0m).IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(m => m.PartnerId).HasDatabaseName("idx_accepted_materials_partner_id");
        builder.HasIndex(m => m.WasteCategoryId).HasDatabaseName("idx_accepted_materials_waste_category_id");

        builder.HasOne(m => m.Partner)
            .WithMany(p => p.AcceptedMaterials)
            .HasForeignKey(m => m.PartnerId)
            .HasConstraintName("fk_accepted_materials_collection_partners")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.WasteCategory)
            .WithMany(c => c.AcceptedMaterials)
            .HasForeignKey(m => m.WasteCategoryId)
            .HasConstraintName("fk_accepted_materials_waste_categories")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Cấu hình bảng trade_orders theo chuẩn snake_case tại database_design_and_audit_guidelines.md.
/// </summary>
public class TradeOrderConfiguration : IEntityTypeConfiguration<TradeOrder>
{
    public void Configure(EntityTypeBuilder<TradeOrder> builder)
    {
        builder.ToTable("trade_orders");
        builder.HasKey(o => o.Id).HasName("pk_trade_orders");

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(o => o.PartnerId).HasColumnName("partner_id").IsRequired();
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(o => o.ScheduledAt).HasColumnName("scheduled_at").IsRequired();
        builder.Property(o => o.EstimatedWeightKg).HasColumnName("estimated_weight_kg").HasPrecision(12, 3).IsRequired();
        builder.Property(o => o.Note).HasColumnName("note");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(o => o.UserId).HasDatabaseName("idx_trade_orders_user_id");
        builder.HasIndex(o => o.PartnerId).HasDatabaseName("idx_trade_orders_partner_id");

        builder.HasOne(o => o.User)
            .WithMany(u => u.TradeOrders)
            .HasForeignKey(o => o.UserId)
            .HasConstraintName("fk_trade_orders_users")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Partner)
            .WithMany(p => p.ReceivedOrders)
            .HasForeignKey(o => o.PartnerId)
            .HasConstraintName("fk_trade_orders_collection_partners")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Cấu hình bảng transaction_receipts theo chuẩn snake_case tại database_design_and_audit_guidelines.md.
/// </summary>
public class TransactionReceiptConfiguration : IEntityTypeConfiguration<TransactionReceipt>
{
    public void Configure(EntityTypeBuilder<TransactionReceipt> builder)
    {
        builder.ToTable("transaction_receipts");
        builder.HasKey(r => r.Id).HasName("pk_transaction_receipts");

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TradeOrderId).HasColumnName("trade_order_id").IsRequired();
        builder.Property(r => r.ActualWeightKg).HasColumnName("actual_weight_kg").HasPrecision(12, 3).IsRequired();
        builder.Property(r => r.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2).IsRequired();
        builder.Property(r => r.TotalAmountCash).HasColumnName("total_amount_cash").HasPrecision(14, 2).IsRequired();
        builder.Property(r => r.PointsAwarded).HasColumnName("points_awarded").IsRequired();
        builder.Property(r => r.QrSecurityCode).HasColumnName("qr_security_code").HasMaxLength(100).IsRequired();
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => r.TradeOrderId).IsUnique().HasDatabaseName("uq_transaction_receipts_trade_order_id");

        builder.HasOne(r => r.TradeOrder)
            .WithOne(o => o.Receipt)
            .HasForeignKey<TransactionReceipt>(r => r.TradeOrderId)
            .HasConstraintName("fk_transaction_receipts_trade_orders")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

