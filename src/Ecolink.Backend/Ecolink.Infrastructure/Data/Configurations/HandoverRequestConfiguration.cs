using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

/// <summary>Cấu hình bảng handover_requests theo báo cáo thiết kế (HandoverRequest, mục 3.3.2).</summary>
public class HandoverRequestConfiguration : IEntityTypeConfiguration<HandoverRequest>
{
    public void Configure(EntityTypeBuilder<HandoverRequest> builder)
    {
        builder.ToTable("handover_requests");
        builder.HasKey(h => h.Id).HasName("pk_handover_requests");

        builder.Property(h => h.Id).HasColumnName("request_id");
        builder.Property(h => h.RequestCode).HasColumnName("request_code").HasMaxLength(50).IsRequired();
        builder.Property(h => h.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(h => h.RecyclerId).HasColumnName("recycler_id").IsRequired();
        builder.Property(h => h.PickupAddress).HasColumnName("pickup_address").IsRequired();
        builder.Property(h => h.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
        builder.Property(h => h.Longitude).HasColumnName("longitude").HasPrecision(11, 7);
        builder.Property(h => h.WastePhotoUrl).HasColumnName("waste_photo_url");
        builder.Property(h => h.ProposedTimeSlot).HasColumnName("proposed_time_slot").IsRequired();
        builder.Property(h => h.ConfirmedTimeSlot).HasColumnName("confirmed_time_slot");
        builder.Property(h => h.ActualHandoverAt).HasColumnName("actual_handover_at");
        builder.Property(h => h.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(Ecolink.Domain.Enums.HandoverStatus.Pending).IsRequired();
        builder.Property(h => h.UserNote).HasColumnName("user_note");
        builder.Property(h => h.RecyclerNote).HasColumnName("recycler_note");
        builder.Property(h => h.CancelReason).HasColumnName("cancel_reason");
        builder.Property(h => h.CancelledBy).HasColumnName("cancelled_by");
        builder.Property(h => h.IsLateCancellation).HasColumnName("is_late_cancellation").HasDefaultValue(false).IsRequired();
        builder.Property(h => h.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(h => h.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasOne<User>().WithMany()
            .HasForeignKey(h => h.UserId)
            .HasConstraintName("fk_handover_requests_users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(h => h.CancelledBy)
            .HasConstraintName("fk_handover_requests_cancelled_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Recycler).WithMany()
            .HasForeignKey(h => h.RecyclerId)
            .HasConstraintName("fk_handover_requests_recycler_profiles")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => h.RequestCode).IsUnique().HasDatabaseName("ux_handover_requests_request_code");
        builder.HasIndex(h => new { h.UserId, h.Status }).HasDatabaseName("ix_handover_requests_user_status");
        builder.HasIndex(h => new { h.RecyclerId, h.Status }).HasDatabaseName("ix_handover_requests_recycler_status");
        builder.HasIndex(h => new { h.Status, h.CreatedAt }).HasDatabaseName("ix_handover_requests_status_created");
    }
}

/// <summary>Cấu hình bảng handover_request_items (báo cáo mục 3.3.3).</summary>
public class HandoverRequestItemConfiguration : IEntityTypeConfiguration<HandoverRequestItem>
{
    public void Configure(EntityTypeBuilder<HandoverRequestItem> builder)
    {
        builder.ToTable("handover_request_items", t =>
        {
            // >= 0: dòng phát sinh khi cân tại chỗ (AF-01 của R-06) không có khối lượng ước tính
            t.HasCheckConstraint("ck_handover_request_items_estimated_non_negative", "estimated_weight_kg >= 0");
        });
        builder.HasKey(i => i.Id).HasName("pk_handover_request_items");

        builder.Property(i => i.Id).HasColumnName("item_id");
        builder.Property(i => i.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(i => i.WasteCategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(i => i.EstimatedWeightKg).HasColumnName("estimated_weight_kg").HasPrecision(12, 3).HasDefaultValue(0m).IsRequired();
        builder.Property(i => i.ActualWeightKg).HasColumnName("actual_weight_kg").HasPrecision(12, 3);
        builder.Property(i => i.AgreedPricePerKg).HasColumnName("agreed_price_per_kg").HasPrecision(12, 2);
        builder.Property(i => i.ItemNote).HasColumnName("item_note");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasOne(i => i.Request).WithMany(r => r.Items)
            .HasForeignKey(i => i.RequestId)
            .HasConstraintName("fk_handover_request_items_requests")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.WasteCategory).WithMany()
            .HasForeignKey(i => i.WasteCategoryId)
            .HasConstraintName("fk_handover_request_items_waste_categories")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng handover_receipts (báo cáo mục 3.3.5).</summary>
public class HandoverReceiptConfiguration : IEntityTypeConfiguration<HandoverReceipt>
{
    public void Configure(EntityTypeBuilder<HandoverReceipt> builder)
    {
        builder.ToTable("handover_receipts", t =>
        {
            t.HasCheckConstraint("ck_handover_receipts_weight_positive", "total_weight_kg > 0");
            t.HasCheckConstraint("ck_handover_receipts_amount_non_negative", "total_amount_cash >= 0");
            t.HasCheckConstraint("ck_handover_receipts_points_non_negative", "points_awarded >= 0");
        });
        builder.HasKey(r => r.Id).HasName("pk_handover_receipts");

        builder.Property(r => r.Id).HasColumnName("receipt_id");
        builder.Property(r => r.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(r => r.ReceiptCode).HasColumnName("receipt_code").HasMaxLength(50).IsRequired();
        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.RecyclerId).HasColumnName("recycler_id").IsRequired();
        builder.Property(r => r.TotalWeightKg).HasColumnName("total_weight_kg").HasPrecision(12, 3).IsRequired();
        builder.Property(r => r.TotalAmountCash).HasColumnName("total_amount_cash").HasPrecision(14, 2).IsRequired();
        builder.Property(r => r.PointsAwarded).HasColumnName("points_awarded").HasDefaultValue(0L).IsRequired();
        builder.Property(r => r.ScaleProofPhotoUrl).HasColumnName("scale_proof_photo_url").IsRequired();
        builder.Property(r => r.ReceiptImageUrl).HasColumnName("receipt_image_url");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(r => r.ConfirmedByUserAt).HasColumnName("confirmed_by_user_at");

        builder.HasOne(r => r.Request).WithOne()
            .HasForeignKey<HandoverReceipt>(r => r.RequestId)
            .HasConstraintName("fk_handover_receipts_requests")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(r => r.UserId)
            .HasConstraintName("fk_handover_receipts_users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RecyclerProfile>().WithMany()
            .HasForeignKey(r => r.RecyclerId)
            .HasConstraintName("fk_handover_receipts_recycler_profiles")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.RequestId).IsUnique().HasDatabaseName("ux_handover_receipts_request");
        builder.HasIndex(r => r.ReceiptCode).IsUnique().HasDatabaseName("ux_handover_receipts_receipt_code");
        builder.HasIndex(r => new { r.UserId, r.ConfirmedByUserAt }).HasDatabaseName("ix_handover_receipts_user_confirmed");
        builder.HasIndex(r => new { r.RecyclerId, r.ConfirmedByUserAt }).HasDatabaseName("ix_handover_receipts_recycler_confirmed");
    }
}
