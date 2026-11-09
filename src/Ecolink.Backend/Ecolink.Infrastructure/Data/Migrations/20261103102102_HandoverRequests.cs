using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class HandoverRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "recycler_profiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "handover_requests",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recycler_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pickup_address = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(11,7)", precision: 11, scale: 7, nullable: true),
                    waste_photo_url = table.Column<string>(type: "text", nullable: true),
                    proposed_time_slot = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    confirmed_time_slot = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    actual_handover_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    user_note = table.Column<string>(type: "text", nullable: true),
                    recycler_note = table.Column<string>(type: "text", nullable: true),
                    cancel_reason = table.Column<string>(type: "text", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_late_cancellation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_handover_requests", x => x.request_id);
                    table.ForeignKey(
                        name: "fk_handover_requests_cancelled_by",
                        column: x => x.cancelled_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handover_requests_recycler_profiles",
                        column: x => x.recycler_id,
                        principalTable: "recycler_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handover_requests_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "handover_request_items",
                columns: table => new
                {
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estimated_weight_kg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false, defaultValue: 0m),
                    actual_weight_kg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    agreed_price_per_kg = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    item_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_handover_request_items", x => x.item_id);
                    table.CheckConstraint("ck_handover_request_items_estimated_positive", "estimated_weight_kg > 0");
                    table.ForeignKey(
                        name: "fk_handover_request_items_requests",
                        column: x => x.request_id,
                        principalTable: "handover_requests",
                        principalColumn: "request_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_handover_request_items_waste_categories",
                        column: x => x.category_id,
                        principalTable: "waste_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_handover_request_items_category_id",
                table: "handover_request_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_handover_request_items_request_id",
                table: "handover_request_items",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_handover_requests_cancelled_by",
                table: "handover_requests",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "ix_handover_requests_recycler_status",
                table: "handover_requests",
                columns: new[] { "recycler_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_handover_requests_status_created",
                table: "handover_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_handover_requests_user_status",
                table: "handover_requests",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_handover_requests_request_code",
                table: "handover_requests",
                column: "request_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "handover_request_items");

            migrationBuilder.DropTable(
                name: "handover_requests");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "recycler_profiles");
        }
    }
}
