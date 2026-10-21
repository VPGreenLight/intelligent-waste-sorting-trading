using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(t => t.Id).HasName("pk_password_reset_tokens");

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(255).IsRequired();
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(t => t.UsedAt).HasColumnName("used_at");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("uq_password_reset_tokens_token_hash");

        builder.HasOne(t => t.User)
            .WithMany(u => u.PasswordResetTokens)
            .HasForeignKey(t => t.UserId)
            .HasConstraintName("fk_password_reset_tokens_users")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(r => r.Id).HasName("pk_refresh_tokens");

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.TokenHash).HasColumnName("token_hash").HasMaxLength(255).IsRequired();
        builder.Property(r => r.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(r => r.IsRevoked).HasColumnName("is_revoked").HasDefaultValue(false).IsRequired();
        builder.Property(r => r.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash").HasMaxLength(255);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(r => r.CreatedByIp).HasColumnName("created_by_ip").HasMaxLength(50);

        builder.HasIndex(r => r.TokenHash).IsUnique().HasDatabaseName("uq_refresh_tokens_token_hash");

        builder.HasOne(r => r.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(r => r.UserId)
            .HasConstraintName("fk_refresh_tokens_users")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
