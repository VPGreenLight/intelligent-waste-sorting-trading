using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

public class RecyclerProfileConfiguration : IEntityTypeConfiguration<RecyclerProfile>
{
    public void Configure(EntityTypeBuilder<RecyclerProfile> builder)
    {
        builder.ToTable("recycler_profiles");
        builder.HasKey(r => r.UserId).HasName("pk_recycler_profiles");

        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.FacilityName).HasColumnName("facility_name").HasMaxLength(150).IsRequired();
        builder.Property(r => r.RepresentativeName).HasColumnName("representative_name").HasMaxLength(100);
        builder.Property(r => r.BusinessAddress).HasColumnName("business_address").IsRequired();
        builder.Property(r => r.Latitude).HasColumnName("latitude").HasPrecision(10, 7).IsRequired();
        builder.Property(r => r.Longitude).HasColumnName("longitude").HasPrecision(11, 7).IsRequired();

        // Cột không gian PostGIS Point SRID 4326 (WGS 84)
        builder.Property(r => r.Location)
            .HasColumnName("geom")
            .HasColumnType("geometry(Point, 4326)")
            .IsRequired();

        builder.Property(r => r.OperatingHours).HasColumnName("operating_hours").HasMaxLength(100).HasDefaultValue("08:00 - 18:00");
        builder.Property(r => r.ContactPhone).HasColumnName("contact_phone").HasMaxLength(20).IsRequired();
        builder.Property(r => r.Description).HasColumnName("description");
        builder.Property(r => r.LegalPermitUrl).HasColumnName("legal_permit_url");
        builder.Property(r => r.BusinessPhotoUrl).HasColumnName("business_photo_url");

        builder.Property(r => r.VerificationStatus).HasColumnName("verification_status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.ActiveStatus).HasColumnName("active_status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.RejectionReason).HasColumnName("rejection_reason");
        builder.Property(r => r.BusyReason).HasColumnName("busy_reason").HasMaxLength(100);
        builder.Property(r => r.WeeklyScheduleJson).HasColumnName("weekly_schedule").HasColumnType("jsonb");

        // Audit fields
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");

        // 1-1 relationship with User
        builder.HasOne(r => r.User)
            .WithOne(u => u.RecyclerProfile)
            .HasForeignKey<RecyclerProfile>(r => r.UserId)
            .HasConstraintName("fk_recycler_profiles_users")
            .OnDelete(DeleteBehavior.Cascade);

        // GiST Spatial Index cho PostGIS ST_DWithin
        builder.HasIndex(r => r.Location)
            .HasMethod("GIST")
            .HasDatabaseName("gix_recycler_profiles_geom");

        // Soft delete query filter
        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
