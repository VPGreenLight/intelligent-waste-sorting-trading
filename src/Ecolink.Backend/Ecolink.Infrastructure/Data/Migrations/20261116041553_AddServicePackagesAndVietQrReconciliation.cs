using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePackagesAndVietQrReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    package_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    target_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_default_free = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    requires_verified_kyb = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    price_vnd = table.Column<decimal>(type: "numeric(14,0)", nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    features_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "System"),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_packages", x => x.id);
                    table.CheckConstraint("chk_service_packages_pricing_mode", "(is_default_free = TRUE AND price_vnd = 0 AND duration_days = 0) OR (is_default_free = FALSE AND price_vnd >= 2000 AND duration_days BETWEEN 1 AND 730)");
                });

            migrationBuilder.CreateTable(
                name: "service_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_code_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    package_name_snapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    duration_days_snapshot = table.Column<int>(type: "integer", nullable: false),
                    features_snapshot = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    expected_amount_vnd = table.Column<decimal>(type: "numeric(14,0)", nullable: false),
                    paid_amount_vnd = table.Column<decimal>(type: "numeric(14,0)", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "VIETQR"),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    qr_payload = table.Column<string>(type: "text", nullable: true),
                    qr_code_url = table.Column<string>(type: "text", nullable: false),
                    checkout_url = table.Column<string>(type: "text", nullable: true),
                    transfer_content = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bank_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    account_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    gateway_order_code = table.Column<long>(type: "bigint", nullable: true),
                    payment_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    gateway_transaction_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    order_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payment_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reconciliation_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    anomaly_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    anomaly_note = table.Column<string>(type: "text", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "text", nullable: true),
                    resolution_bank_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    qr_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "System"),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_orders_packages",
                        column: x => x.package_id,
                        principalTable: "service_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_orders_resolver",
                        column: x => x.resolved_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_orders_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_webhook_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    gateway_source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    matched_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parsed_order_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    received_amount_vnd = table.Column<decimal>(type: "numeric(14,0)", nullable: false),
                    bank_reference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    transfer_description = table.Column<string>(type: "text", nullable: true),
                    signature_valid = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    raw_payload = table.Column<string>(type: "jsonb", nullable: false),
                    processing_result = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    anomaly_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "text", nullable: true),
                    resolution_bank_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_webhook_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_webhook_logs_orders",
                        column: x => x.matched_order_id,
                        principalTable: "service_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_webhook_logs_resolver",
                        column: x => x.resolved_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    features_snapshot = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoke_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "System"),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_subscriptions", x => x.id);
                    table.CheckConstraint("chk_user_subscriptions_window", "expires_at > starts_at");
                    table.ForeignKey(
                        name: "fk_user_subscriptions_orders",
                        column: x => x.source_order_id,
                        principalTable: "service_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_subscriptions_packages",
                        column: x => x.package_id,
                        principalTable: "service_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_subscriptions_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "service_packages",
                columns: new[] { "id", "created_at", "created_by", "deleted_at", "description", "display_order", "duration_days", "features_json", "is_active", "is_default_free", "package_code", "package_name", "price_vnd", "target_role", "updated_at", "updated_by" },
                values: new object[] { new Guid("51000001-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, "Gói mặc định miễn phí với đầy đủ tính năng phân loại rác AI và bàn giao cơ bản.", 10, 0, "{\"tierName\":\"Basic\",\"adFree\":false,\"dailyPointMultiplier\":1.0,\"ecoPlusBadge\":false,\"verifiedBlueBadge\":false,\"featuredMapMarker\":false,\"mapPriorityBoost\":0}", true, true, "BASIC", "Basic (Mặc định)", 0m, "All", null, null });

            migrationBuilder.InsertData(
                table: "service_packages",
                columns: new[] { "id", "created_at", "created_by", "deleted_at", "description", "display_order", "duration_days", "features_json", "is_active", "package_code", "package_name", "price_vnd", "target_role", "updated_at", "updated_by" },
                values: new object[] { new Guid("51000001-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, "Tắt hoàn toàn banner quảng cáo trong 30 ngày kèm đặc quyền hội viên EcoPlus.", 20, 30, "{\"tierName\":\"Premium\",\"adFree\":true,\"dailyPointMultiplier\":1.2,\"ecoPlusBadge\":true,\"verifiedBlueBadge\":false,\"featuredMapMarker\":false,\"mapPriorityBoost\":0}", true, "PREMIUM", "Premium (Tắt quảng cáo)", 59000m, "All", null, null });

            migrationBuilder.InsertData(
                table: "service_packages",
                columns: new[] { "id", "created_at", "created_by", "deleted_at", "description", "display_order", "duration_days", "features_json", "is_active", "package_code", "package_name", "price_vnd", "requires_verified_kyb", "target_role", "updated_at", "updated_by" },
                values: new object[] { new Guid("51000002-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, "Gói thương mại 1 năm dành cho Cơ sở thu mua đã xác minh KYB: Tắt quảng cáo, Huy hiệu Tích xanh và Ghim Nổi bật ưu tiên trên bản đồ.", 30, 365, "{\"tierName\":\"Pro\",\"adFree\":true,\"dailyPointMultiplier\":1.2,\"ecoPlusBadge\":true,\"verifiedBlueBadge\":true,\"featuredMapMarker\":true,\"mapPriorityBoost\":100}", true, "PRO", "Pro (Tích xanh & Ưu tiên bản đồ)", 1000000m, true, "Recycler", null, null });

            migrationBuilder.CreateIndex(
                name: "idx_payment_webhook_logs_order",
                table: "payment_webhook_logs",
                columns: new[] { "matched_order_id", "received_at" });

            migrationBuilder.CreateIndex(
                name: "idx_payment_webhook_logs_result",
                table: "payment_webhook_logs",
                columns: new[] { "processing_result", "received_at" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_logs_resolved_by",
                table: "payment_webhook_logs",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "uq_payment_webhook_logs_idempotency",
                table: "payment_webhook_logs",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_payment_webhook_logs_webhook_code",
                table: "payment_webhook_logs",
                column: "webhook_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_service_orders_paid_created",
                table: "service_orders",
                columns: new[] { "order_status", "paid_at" },
                filter: "order_status = 'Paid'");

            migrationBuilder.CreateIndex(
                name: "idx_service_orders_reconciliation",
                table: "service_orders",
                columns: new[] { "reconciliation_status", "order_status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_service_orders_user_created",
                table: "service_orders",
                columns: new[] { "user_id", "created_at" },
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_service_orders_package_id",
                table: "service_orders",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_orders_resolved_by",
                table: "service_orders",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "uq_service_orders_gateway_order_code",
                table: "service_orders",
                column: "gateway_order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_service_orders_order_code",
                table: "service_orders",
                column: "order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_service_orders_payment_reference",
                table: "service_orders",
                column: "payment_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_service_packages_role_active",
                table: "service_packages",
                columns: new[] { "target_role", "is_active", "display_order" },
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "uq_service_packages_package_code",
                table: "service_packages",
                column: "package_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_user_subscriptions_active_lookup",
                table: "user_subscriptions",
                columns: new[] { "user_id", "status", "expires_at" },
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_package_id",
                table: "user_subscriptions",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "uq_user_subscriptions_source_order",
                table: "user_subscriptions",
                column: "source_order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_webhook_logs");

            migrationBuilder.DropTable(
                name: "user_subscriptions");

            migrationBuilder.DropTable(
                name: "service_orders");

            migrationBuilder.DropTable(
                name: "service_packages");
        }
    }
}
