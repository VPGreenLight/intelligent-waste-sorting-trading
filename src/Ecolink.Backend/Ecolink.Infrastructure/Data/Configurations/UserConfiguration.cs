using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id).HasName("pk_users");

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
        builder.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(100).IsRequired();
        builder.Property(u => u.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
        builder.Property(u => u.AvatarUrl).HasColumnName("avatar_url");
        builder.Property(u => u.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(u => u.IsGoogleAuth).HasColumnName("is_google_auth").HasDefaultValue(false);
        builder.Property(u => u.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(u => u.FailedLoginAttempts).HasColumnName("failed_login_attempts").HasDefaultValue(0);
        builder.Property(u => u.LockoutUntil).HasColumnName("lockout_until");

        // Audit fields
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(u => u.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(u => u.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(u => u.DeletedAt).HasColumnName("deleted_at");

        // Unique Indexes (BR-AUTH-01)
        builder.HasIndex(u => u.Username).IsUnique().HasDatabaseName("uq_users_username");
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("uq_users_email");

        // Soft delete query filter
        builder.HasQueryFilter(u => !u.IsDeleted);

        // Ignore computed backward-compatibility property
        builder.Ignore(u => u.IsActive);
    }
}
