using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GamificationRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_checkin_logs",
                columns: table => new
                {
                    checkin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checkin_date = table.Column<DateOnly>(type: "date", nullable: false),
                    streak_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    points_awarded = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_checkin_logs", x => x.checkin_id);
                    table.ForeignKey(
                        name: "fk_daily_checkin_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eco_facts",
                columns: table => new
                {
                    fact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    unlock_tree_level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    created_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eco_facts", x => x.fact_id);
                    table.CheckConstraint("ck_eco_fact_level_min", "unlock_tree_level >= 1");
                    table.ForeignKey(
                        name: "fk_eco_facts_staff",
                        column: x => x.created_by_staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "gamification_config",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    daily_non_transaction_cap = table.Column<long>(type: "bigint", nullable: false),
                    checkin_base_points = table.Column<long>(type: "bigint", nullable: false),
                    streak_bonus_increment = table.Column<long>(type: "bigint", nullable: false),
                    streak_bonus_cap = table.Column<long>(type: "bigint", nullable: false),
                    daily_tree_care_limit = table.Column<int>(type: "integer", nullable: false),
                    voucher_validity_days = table.Column<int>(type: "integer", nullable: false),
                    default_points_per_kg = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gamification_config", x => x.id);
                    table.CheckConstraint("ck_gamification_config_single", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "gift_inventory",
                columns: table => new
                {
                    gift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gift_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    gift_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Physical"),
                    description = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    points_required = table.Column<long>(type: "bigint", nullable: false),
                    stock_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gift_inventory", x => x.gift_id);
                    table.CheckConstraint("ck_gift_points_nonneg", "points_required >= 0");
                    table.CheckConstraint("ck_gift_stock_nonneg", "stock_quantity >= 0");
                });

            migrationBuilder.CreateTable(
                name: "point_rate_rules",
                columns: table => new
                {
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waste_category_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    points_per_kg = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_point_rate_rules", x => x.rule_id);
                    table.CheckConstraint("ck_point_rate_nonneg", "points_per_kg >= 0");
                });

            migrationBuilder.CreateTable(
                name: "tree_level_thresholds",
                columns: table => new
                {
                    level = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    required_points = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tree_level_thresholds", x => x.level);
                    table.CheckConstraint("ck_tree_threshold_nonneg", "required_points >= 0");
                });

            migrationBuilder.CreateTable(
                name: "user_point_wallets",
                columns: table => new
                {
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_balance = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    total_earned = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    total_spent = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_point_wallets", x => x.wallet_id);
                    table.CheckConstraint("ck_wallet_balance_nonneg", "current_balance >= 0");
                    table.ForeignKey(
                        name: "fk_user_point_wallets_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "virtual_tree_progress",
                columns: table => new
                {
                    progress_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tree_level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    growth_points = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    total_watered = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    daily_water_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_water_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_watered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_virtual_tree_progress", x => x.progress_id);
                    table.CheckConstraint("ck_tree_level_min", "tree_level >= 1");
                    table.ForeignKey(
                        name: "fk_virtual_tree_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gift_redemptions",
                columns: table => new
                {
                    redemption_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points_spent = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    redemption_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucher_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    delivery_address = table.Column<string>(type: "text", nullable: true),
                    redeemed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    fulfilled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gift_redemptions", x => x.redemption_id);
                    table.ForeignKey(
                        name: "fk_gift_redemptions_gift",
                        column: x => x.gift_id,
                        principalTable: "gift_inventory",
                        principalColumn: "gift_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_gift_redemptions_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "point_transactions",
                columns: table => new
                {
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points = table.Column<long>(type: "bigint", nullable: false),
                    action_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_point_transactions", x => x.transaction_id);
                    table.ForeignKey(
                        name: "fk_point_transactions_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_point_transactions_wallet",
                        column: x => x.wallet_id,
                        principalTable: "user_point_wallets",
                        principalColumn: "wallet_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_daily_checkin_user_date",
                table: "daily_checkin_logs",
                columns: new[] { "user_id", "checkin_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eco_facts_created_by_staff_id",
                table: "eco_facts",
                column: "created_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_eco_facts_status_level",
                table: "eco_facts",
                columns: new[] { "status", "unlock_tree_level" });

            migrationBuilder.CreateIndex(
                name: "ix_gift_inventory_status_type",
                table: "gift_inventory",
                columns: new[] { "status", "gift_type" });

            migrationBuilder.CreateIndex(
                name: "ix_gift_redemptions_gift",
                table: "gift_redemptions",
                columns: new[] { "gift_id", "redeemed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_gift_redemptions_user",
                table: "gift_redemptions",
                columns: new[] { "user_id", "redeemed_at" });

            migrationBuilder.CreateIndex(
                name: "ux_gift_redemptions_code",
                table: "gift_redemptions",
                column: "redemption_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_gift_redemptions_voucher",
                table: "gift_redemptions",
                column: "voucher_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_point_rate_rules_category",
                table: "point_rate_rules",
                column: "waste_category_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_point_transactions_user_created",
                table: "point_transactions",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_point_transactions_wallet_id",
                table: "point_transactions",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "ux_point_transactions_idem",
                table: "point_transactions",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_user_point_wallets_user",
                table: "user_point_wallets",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_virtual_tree_user",
                table: "virtual_tree_progress",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_checkin_logs");

            migrationBuilder.DropTable(
                name: "eco_facts");

            migrationBuilder.DropTable(
                name: "gamification_config");

            migrationBuilder.DropTable(
                name: "gift_redemptions");

            migrationBuilder.DropTable(
                name: "point_rate_rules");

            migrationBuilder.DropTable(
                name: "point_transactions");

            migrationBuilder.DropTable(
                name: "tree_level_thresholds");

            migrationBuilder.DropTable(
                name: "virtual_tree_progress");

            migrationBuilder.DropTable(
                name: "gift_inventory");

            migrationBuilder.DropTable(
                name: "user_point_wallets");
        }
    }
}
