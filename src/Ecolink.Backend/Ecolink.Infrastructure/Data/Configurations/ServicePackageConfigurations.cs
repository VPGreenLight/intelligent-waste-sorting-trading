using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

/// <summary>
/// Cấu hình bảng service_packages (Danh mục gói dịch vụ 3 cấp bậc BASIC - PREMIUM - PRO - UC A-10).
/// </summary>
public class ServicePackageConfiguration : IEntityTypeConfiguration<ServicePackage>
{
    public void Configure(EntityTypeBuilder<ServicePackage> builder)
    {
        builder.ToTable("service_packages", t =>
        {
            t.HasCheckConstraint(
                "chk_service_packages_pricing_mode",
                "(is_default_free = TRUE AND price_vnd = 0 AND duration_days = 0) OR (is_default_free = FALSE AND price_vnd >= 2000 AND duration_days BETWEEN 1 AND 730)");
        });

        builder.HasKey(p => p.Id).HasName("pk_service_packages");

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.PackageCode).HasColumnName("package_code").HasMaxLength(50).IsRequired();
        builder.Property(p => p.PackageName).HasColumnName("package_name").HasMaxLength(150).IsRequired();
        builder.Property(p => p.TargetRole).HasColumnName("target_role").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description");
        builder.Property(p => p.IsDefaultFree).HasColumnName("is_default_free").HasDefaultValue(false).IsRequired();
        builder.Property(p => p.RequiresVerifiedKyb).HasColumnName("requires_verified_kyb").HasDefaultValue(false).IsRequired();
        builder.Property(p => p.PriceVnd).HasColumnName("price_vnd").HasColumnType("numeric(14,0)").IsRequired();
        builder.Property(p => p.DurationDays).HasColumnName("duration_days").IsRequired();
        builder.Property(p => p.FeaturesJson).HasColumnName("features_json").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(p => p.DisplayOrder).HasColumnName("display_order").HasDefaultValue(100).IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(p => p.PackageCode).IsUnique().HasDatabaseName("uq_service_packages_package_code");
        builder.HasIndex(p => new { p.TargetRole, p.IsActive, p.DisplayOrder })
            .HasDatabaseName("idx_service_packages_role_active")
            .HasFilter("is_deleted = FALSE");

        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasData(
            new ServicePackage
            {
                Id = CanonicalServicePackages.BasicId,
                PackageCode = CanonicalServicePackages.BasicCode,
                PackageName = "Basic (Mặc định)",
                TargetRole = ServiceTargetRole.All,
                Description = "Gói mặc định miễn phí với đầy đủ tính năng phân loại rác AI và bàn giao cơ bản.",
                IsDefaultFree = true,
                RequiresVerifiedKyb = false,
                PriceVnd = 0m,
                DurationDays = 0,
                FeaturesJson = CanonicalServicePackages.BasicFeaturesJson,
                IsActive = true,
                DisplayOrder = 10,
                CreatedAt = CanonicalServicePackages.SeedTimestampUtc,
                CreatedBy = "System",
                IsDeleted = false
            },
            new ServicePackage
            {
                Id = CanonicalServicePackages.PremiumId,
                PackageCode = CanonicalServicePackages.PremiumCode,
                PackageName = "Premium (Tắt quảng cáo)",
                TargetRole = ServiceTargetRole.All,
                Description = "Tắt hoàn toàn banner quảng cáo trong 30 ngày kèm đặc quyền hội viên EcoPlus.",
                IsDefaultFree = false,
                RequiresVerifiedKyb = false,
                PriceVnd = 59_000m,
                DurationDays = 30,
                FeaturesJson = CanonicalServicePackages.PremiumFeaturesJson,
                IsActive = true,
                DisplayOrder = 20,
                CreatedAt = CanonicalServicePackages.SeedTimestampUtc,
                CreatedBy = "System",
                IsDeleted = false
            },
            new ServicePackage
            {
                Id = CanonicalServicePackages.ProId,
                PackageCode = CanonicalServicePackages.ProCode,
                PackageName = "Pro (Tích xanh & Ưu tiên bản đồ)",
                TargetRole = ServiceTargetRole.Recycler,
                Description = "Gói thương mại 1 năm dành cho Cơ sở thu mua đã xác minh KYB: Tắt quảng cáo, Huy hiệu Tích xanh và Ghim Nổi bật ưu tiên trên bản đồ.",
                IsDefaultFree = false,
                RequiresVerifiedKyb = true,
                PriceVnd = 1_000_000m,
                DurationDays = 365,
                FeaturesJson = CanonicalServicePackages.ProFeaturesJson,
                IsActive = true,
                DisplayOrder = 30,
                CreatedAt = CanonicalServicePackages.SeedTimestampUtc,
                CreatedBy = "System",
                IsDeleted = false
            });
    }
}

/// <summary>
/// Cấu hình bảng service_orders (Đơn thanh toán VietQR động - UC U-16, R-07, A-11).
/// </summary>
public class ServiceOrderConfiguration : IEntityTypeConfiguration<ServiceOrder>
{
    public void Configure(EntityTypeBuilder<ServiceOrder> builder)
    {
        builder.ToTable("service_orders");
        builder.HasKey(o => o.Id).HasName("pk_service_orders");

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.OrderCode).HasColumnName("order_code").HasMaxLength(50).IsRequired();
        builder.Property(o => o.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(o => o.PackageId).HasColumnName("package_id").IsRequired();

        builder.Property(o => o.PackageCodeSnapshot).HasColumnName("package_code_snapshot").HasMaxLength(50).IsRequired();
        builder.Property(o => o.PackageNameSnapshot).HasColumnName("package_name_snapshot").HasMaxLength(150).IsRequired();
        builder.Property(o => o.DurationDaysSnapshot).HasColumnName("duration_days_snapshot").IsRequired();
        builder.Property(o => o.FeaturesSnapshot).HasColumnName("features_snapshot").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();

        builder.Property(o => o.ExpectedAmountVnd).HasColumnName("expected_amount_vnd").HasColumnType("numeric(14,0)").IsRequired();
        builder.Property(o => o.PaidAmountVnd).HasColumnName("paid_amount_vnd").HasColumnType("numeric(14,0)");
        builder.Property(o => o.PaymentMethod).HasColumnName("payment_method").HasMaxLength(30).HasDefaultValue("VIETQR").IsRequired();
        builder.Property(o => o.Description).HasColumnName("description").HasMaxLength(255).IsRequired();
        builder.Property(o => o.QrPayload).HasColumnName("qr_payload");
        builder.Property(o => o.QrCodeUrl).HasColumnName("qr_code_url").IsRequired();
        builder.Property(o => o.CheckoutUrl).HasColumnName("checkout_url");
        builder.Property(o => o.TransferContent).HasColumnName("transfer_content").HasMaxLength(100).IsRequired();
        builder.Property(o => o.BankCode).HasColumnName("bank_code").HasMaxLength(30).IsRequired();
        builder.Property(o => o.BankName).HasColumnName("bank_name").HasMaxLength(100).IsRequired();
        builder.Property(o => o.AccountNo).HasColumnName("account_no").HasMaxLength(50).IsRequired();
        builder.Property(o => o.AccountName).HasColumnName("account_name").HasMaxLength(150).IsRequired();

        builder.Property(o => o.GatewayOrderCode).HasColumnName("gateway_order_code");
        builder.Property(o => o.PaymentReference).HasColumnName("payment_reference").HasMaxLength(255);
        builder.Property(o => o.GatewayTransactionNo).HasColumnName("gateway_transaction_no").HasMaxLength(100);

        builder.Property(o => o.OrderStatus).HasColumnName("order_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(o => o.PaymentStatus).HasColumnName("payment_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(o => o.ReconciliationStatus).HasColumnName("reconciliation_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(o => o.AnomalyType).HasColumnName("anomaly_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(o => o.AnomalyNote).HasColumnName("anomaly_note");

        builder.Property(o => o.ResolvedBy).HasColumnName("resolved_by");
        builder.Property(o => o.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(o => o.ResolutionNote).HasColumnName("resolution_note");
        builder.Property(o => o.ResolutionBankRef).HasColumnName("resolution_bank_ref").HasMaxLength(100);

        builder.Property(o => o.QrExpiresAt).HasColumnName("qr_expires_at").IsRequired();
        builder.Property(o => o.PaidAt).HasColumnName("paid_at");

        builder.Property(o => o.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System").IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(o => o.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(o => o.DeletedAt).HasColumnName("deleted_at");

        // Optimistic Concurrency Control chuẩn PostgreSQL (system column xmin)
        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(o => o.OrderCode).IsUnique().HasDatabaseName("uq_service_orders_order_code");
        builder.HasIndex(o => o.GatewayOrderCode).IsUnique().HasDatabaseName("uq_service_orders_gateway_order_code");
        builder.HasIndex(o => o.PaymentReference).IsUnique().HasDatabaseName("uq_service_orders_payment_reference");
        builder.HasIndex(o => new { o.UserId, o.CreatedAt })
            .HasDatabaseName("idx_service_orders_user_created")
            .HasFilter("is_deleted = FALSE");
        builder.HasIndex(o => new { o.ReconciliationStatus, o.OrderStatus, o.CreatedAt })
            .HasDatabaseName("idx_service_orders_reconciliation");
        builder.HasIndex(o => new { o.OrderStatus, o.PaidAt })
            .HasDatabaseName("idx_service_orders_paid_created")
            .HasFilter("order_status = 'Paid'");

        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .HasConstraintName("fk_service_orders_users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Package)
            .WithMany(p => p.Orders)
            .HasForeignKey(o => o.PackageId)
            .HasConstraintName("fk_service_orders_packages")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.ResolverUser)
            .WithMany()
            .HasForeignKey(o => o.ResolvedBy)
            .HasConstraintName("fk_service_orders_resolver")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}

/// <summary>
/// Cấu hình bảng user_subscriptions (Thuê bao đặc quyền đang hiệu lực - BR-PAY-04).
/// </summary>
public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("user_subscriptions", t =>
        {
            t.HasCheckConstraint("chk_user_subscriptions_window", "expires_at > starts_at");
        });

        builder.HasKey(s => s.Id).HasName("pk_user_subscriptions");

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.PackageId).HasColumnName("package_id").IsRequired();
        builder.Property(s => s.SourceOrderId).HasColumnName("source_order_id").IsRequired();
        builder.Property(s => s.PackageCode).HasColumnName("package_code").HasMaxLength(50).IsRequired();
        builder.Property(s => s.FeaturesSnapshot).HasColumnName("features_snapshot").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(s => s.StartsAt).HasColumnName("starts_at").IsRequired();
        builder.Property(s => s.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(s => s.RevokedAt).HasColumnName("revoked_at");
        builder.Property(s => s.RevokeReason).HasColumnName("revoke_reason");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(s => s.SourceOrderId).IsUnique().HasDatabaseName("uq_user_subscriptions_source_order");
        builder.HasIndex(s => new { s.UserId, s.Status, s.ExpiresAt })
            .HasDatabaseName("idx_user_subscriptions_active_lookup")
            .HasFilter("is_deleted = FALSE");

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .HasConstraintName("fk_user_subscriptions_users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Package)
            .WithMany(p => p.Subscriptions)
            .HasForeignKey(s => s.PackageId)
            .HasConstraintName("fk_user_subscriptions_packages")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SourceOrder)
            .WithOne(o => o.Subscription)
            .HasForeignKey<UserSubscription>(s => s.SourceOrderId)
            .HasConstraintName("fk_user_subscriptions_orders")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

/// <summary>
/// Cấu hình bảng payment_webhook_logs (Log callback ngân hàng thô & đối soát - BR-PAY-02, A-11).
/// </summary>
public class PaymentWebhookLogConfiguration : IEntityTypeConfiguration<PaymentWebhookLog>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookLog> builder)
    {
        builder.ToTable("payment_webhook_logs");
        builder.HasKey(w => w.Id).HasName("pk_payment_webhook_logs");

        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.WebhookCode).HasColumnName("webhook_code").HasMaxLength(50).IsRequired();
        builder.Property(w => w.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(150).IsRequired();
        builder.Property(w => w.GatewaySource).HasColumnName("gateway_source").HasMaxLength(30).IsRequired();
        builder.Property(w => w.MatchedOrderId).HasColumnName("matched_order_id");
        builder.Property(w => w.ParsedOrderCode).HasColumnName("parsed_order_code").HasMaxLength(50);
        builder.Property(w => w.ReceivedAmountVnd).HasColumnName("received_amount_vnd").HasColumnType("numeric(14,0)").IsRequired();
        builder.Property(w => w.BankReference).HasColumnName("bank_reference").HasMaxLength(150);
        builder.Property(w => w.TransferDescription).HasColumnName("transfer_description");
        builder.Property(w => w.SignatureValid).HasColumnName("signature_valid").HasDefaultValue(false).IsRequired();
        builder.Property(w => w.RawPayload).HasColumnName("raw_payload").HasColumnType("jsonb").IsRequired();
        builder.Property(w => w.ProcessingResult).HasColumnName("processing_result").HasMaxLength(50).IsRequired();
        builder.Property(w => w.AnomalyType).HasColumnName("anomaly_type").HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.Property(w => w.ResolvedBy).HasColumnName("resolved_by");
        builder.Property(w => w.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(w => w.ResolutionNote).HasColumnName("resolution_note");
        builder.Property(w => w.ResolutionBankRef).HasColumnName("resolution_bank_ref").HasMaxLength(100);
        builder.Property(w => w.ReceivedAt).HasColumnName("received_at").HasDefaultValueSql("NOW()").IsRequired();

        // Map inherited BaseEntity timestamps without creating conflicting unused columns if not present in DDL
        builder.Ignore(w => w.CreatedAt);
        builder.Ignore(w => w.UpdatedAt);

        builder.HasIndex(w => w.WebhookCode).IsUnique().HasDatabaseName("uq_payment_webhook_logs_webhook_code");
        builder.HasIndex(w => w.IdempotencyKey).IsUnique().HasDatabaseName("uq_payment_webhook_logs_idempotency");
        builder.HasIndex(w => new { w.MatchedOrderId, w.ReceivedAt }).HasDatabaseName("idx_payment_webhook_logs_order");
        builder.HasIndex(w => new { w.ProcessingResult, w.ReceivedAt }).HasDatabaseName("idx_payment_webhook_logs_result");

        builder.HasOne(w => w.MatchedOrder)
            .WithMany(o => o.WebhookLogs)
            .HasForeignKey(w => w.MatchedOrderId)
            .HasConstraintName("fk_payment_webhook_logs_orders")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(w => w.ResolverUser)
            .WithMany()
            .HasForeignKey(w => w.ResolvedBy)
            .HasConstraintName("fk_payment_webhook_logs_resolver")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
