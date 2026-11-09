using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class HandoverReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_handover_request_items_estimated_positive",
                table: "handover_request_items");

            migrationBuilder.CreateTable(
                name: "handover_receipts",
                columns: table => new
                {
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recycler_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_weight_kg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    total_amount_cash = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    points_awarded = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    scale_proof_photo_url = table.Column<string>(type: "text", nullable: false),
                    receipt_image_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    confirmed_by_user_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_handover_receipts", x => x.receipt_id);
                    table.CheckConstraint("ck_handover_receipts_amount_non_negative", "total_amount_cash >= 0");
                    table.CheckConstraint("ck_handover_receipts_points_non_negative", "points_awarded >= 0");
                    table.CheckConstraint("ck_handover_receipts_weight_positive", "total_weight_kg > 0");
                    table.ForeignKey(
                        name: "fk_handover_receipts_recycler_profiles",
                        column: x => x.recycler_id,
                        principalTable: "recycler_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handover_receipts_requests",
                        column: x => x.request_id,
                        principalTable: "handover_requests",
                        principalColumn: "request_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handover_receipts_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_handover_request_items_estimated_non_negative",
                table: "handover_request_items",
                sql: "estimated_weight_kg >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_handover_receipts_recycler_confirmed",
                table: "handover_receipts",
                columns: new[] { "recycler_id", "confirmed_by_user_at" });

            migrationBuilder.CreateIndex(
                name: "ix_handover_receipts_user_confirmed",
                table: "handover_receipts",
                columns: new[] { "user_id", "confirmed_by_user_at" });

            migrationBuilder.CreateIndex(
                name: "ux_handover_receipts_receipt_code",
                table: "handover_receipts",
                column: "receipt_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_handover_receipts_request",
                table: "handover_receipts",
                column: "request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "handover_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_handover_request_items_estimated_non_negative",
                table: "handover_request_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_handover_request_items_estimated_positive",
                table: "handover_request_items",
                sql: "estimated_weight_kg > 0");
        }
    }
}
