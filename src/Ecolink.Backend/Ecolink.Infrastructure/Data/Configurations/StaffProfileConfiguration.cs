using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

public class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.ToTable("staff_profiles");
        builder.HasKey(s => s.UserId).HasName("pk_staff_profiles");

        builder.Property(s => s.UserId).HasColumnName("user_id");
        builder.Property(s => s.StaffCode).HasColumnName("staff_code").HasMaxLength(30).IsRequired();
        builder.Property(s => s.Department).HasColumnName("department").HasMaxLength(100);
        builder.Property(s => s.Position).HasColumnName("position").HasMaxLength(100);
        builder.Property(s => s.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("ACTIVE").IsRequired();

        // Audit fields
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");

        // Unique Staff Code
        builder.HasIndex(s => s.StaffCode).IsUnique().HasDatabaseName("uq_staff_profiles_staff_code");

        // 1-1 with User
        builder.HasOne(s => s.User)
            .WithOne(u => u.StaffProfile)
            .HasForeignKey<StaffProfile>(s => s.UserId)
            .HasConstraintName("fk_staff_profiles_users")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

public class StaffPermissionAssignmentConfiguration : IEntityTypeConfiguration<StaffPermissionAssignment>
{
    public void Configure(EntityTypeBuilder<StaffPermissionAssignment> builder)
    {
        builder.ToTable("staff_permission_assignments");
        builder.HasKey(p => p.Id).HasName("pk_staff_permission_assignments");

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.StaffId).HasColumnName("staff_id").IsRequired();
        builder.Property(p => p.PermissionCode).HasColumnName("permission_code").HasMaxLength(50).IsRequired();
        builder.Property(p => p.GrantedBy).HasColumnName("granted_by").IsRequired();
        builder.Property(p => p.GrantedAt).HasColumnName("granted_at").HasDefaultValueSql("NOW()").IsRequired();

        // Composite unique index
        builder.HasIndex(p => new { p.StaffId, p.PermissionCode })
            .IsUnique()
            .HasDatabaseName("uq_staff_permission_assignments_staff_perm");

        builder.HasOne(p => p.StaffProfile)
            .WithMany(s => s.Permissions)
            .HasForeignKey(p => p.StaffId)
            .HasConstraintName("fk_staff_permission_assignments_staff_profiles")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
