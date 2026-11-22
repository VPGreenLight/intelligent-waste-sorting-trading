using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingReviewAndComplaintModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "average_rating",
                table: "recycler_profiles",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "total_reviews",
                table: "recycler_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "transaction_complaints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    complaint_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    complainant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    complainant_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Citizen"),
                    respondent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    evidence_urls_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Open"),
                    assigned_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    taken_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    resolution_note = table.Column<string>(type: "text", nullable: true),
                    resolved_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    points_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    warning_logged = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("pk_transaction_complaints", x => x.id);
                    table.ForeignKey(
                        name: "fk_transaction_complaints_assigned",
                        column: x => x.assigned_staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_transaction_complaints_complainant",
                        column: x => x.complainant_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_complaints_receipt",
                        column: x => x.receipt_id,
                        principalTable: "handover_receipts",
                        principalColumn: "receipt_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_transaction_complaints_request",
                        column: x => x.request_id,
                        principalTable: "handover_requests",
                        principalColumn: "request_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_complaints_resolved_by",
                        column: x => x.resolved_by_staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_complaints_respondent",
                        column: x => x.respondent_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transaction_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recycler_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating_stars = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Visible"),
                    report_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_report_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    moderated_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    moderated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    moderation_reason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("pk_transaction_reviews", x => x.id);
                    table.CheckConstraint("chk_transaction_reviews_stars", "rating_stars BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_transaction_reviews_moderator",
                        column: x => x.moderated_by_staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_reviews_recycler",
                        column: x => x.recycler_id,
                        principalTable: "recycler_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_reviews_reporter",
                        column: x => x.reported_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_transaction_reviews_request",
                        column: x => x.request_id,
                        principalTable: "handover_requests",
                        principalColumn: "request_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_reviews_reviewer",
                        column: x => x.reviewer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_transaction_complaints_parties",
                table: "transaction_complaints",
                columns: new[] { "complainant_id", "respondent_id", "created_at" },
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "idx_transaction_complaints_status_created",
                table: "transaction_complaints",
                columns: new[] { "status", "created_at" },
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_complaints_assigned_staff_id",
                table: "transaction_complaints",
                column: "assigned_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_complaints_receipt_id",
                table: "transaction_complaints",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_complaints_resolved_by_staff_id",
                table: "transaction_complaints",
                column: "resolved_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_complaints_respondent_id",
                table: "transaction_complaints",
                column: "respondent_id");

            migrationBuilder.CreateIndex(
                name: "uq_transaction_complaints_active_request",
                table: "transaction_complaints",
                column: "request_id",
                unique: true,
                filter: "status IN ('Open', 'Investigating') AND is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "uq_transaction_complaints_code",
                table: "transaction_complaints",
                column: "complaint_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_transaction_reviews_recycler_status",
                table: "transaction_reviews",
                columns: new[] { "recycler_id", "status", "created_at" },
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "idx_transaction_reviews_reviewer",
                table: "transaction_reviews",
                columns: new[] { "reviewer_id", "created_at" },
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_reviews_moderated_by_staff_id",
                table: "transaction_reviews",
                column: "moderated_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_reviews_reported_by_user_id",
                table: "transaction_reviews",
                column: "reported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_transaction_reviews_request_reviewer",
                table: "transaction_reviews",
                columns: new[] { "request_id", "reviewer_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transaction_complaints");

            migrationBuilder.DropTable(
                name: "transaction_reviews");

            migrationBuilder.DropColumn(
                name: "average_rating",
                table: "recycler_profiles");

            migrationBuilder.DropColumn(
                name: "total_reviews",
                table: "recycler_profiles");
        }
    }
}
