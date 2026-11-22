using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiFeedbackAndDatasetCurationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "dataset_item_id",
                table: "ai_feedback_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "detected_objects_snapshot",
                table: "ai_feedback_logs",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "image_sha256",
                table: "ai_feedback_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model_used",
                table: "ai_feedback_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "yolov8_detect");

            migrationBuilder.AddColumn<string>(
                name: "review_decision",
                table: "ai_feedback_logs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_note",
                table: "ai_feedback_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                table: "ai_feedback_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by_staff_id",
                table: "ai_feedback_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "scan_id",
                table: "ai_feedback_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ai_feedback_logs",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "ai_dataset_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    version_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    train_ratio = table.Column<int>(type: "integer", nullable: false, defaultValue: 70),
                    val_ratio = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    test_ratio = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    total_samples = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    train_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    val_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    test_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    class_distribution_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    imbalance_ratio = table.Column<decimal>(type: "numeric(6,2)", nullable: false, defaultValue: 1.00m),
                    is_balanced = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Published"),
                    synced_to_ai_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "System"),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_dataset_versions", x => x.id);
                    table.CheckConstraint("chk_ai_dataset_versions_ratios", "train_ratio > 0 AND val_ratio >= 0 AND test_ratio >= 0 AND (train_ratio + val_ratio + test_ratio = 100)");
                    table.ForeignKey(
                        name: "fk_ai_dataset_versions_staff",
                        column: x => x.created_by_staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_scan_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    image_storage_key = table.Column<string>(type: "text", nullable: true),
                    image_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    image_width = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    image_height = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    model_used = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "yolov8_detect"),
                    predicted_label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "unknown"),
                    confidence_score = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    is_out_of_distribution = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    detected_objects_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    scan_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Completed"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "System"),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_scan_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_scan_histories_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ai_dataset_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "UserFeedback"),
                    source_scan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_feedback_id = table.Column<Guid>(type: "uuid", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    image_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    image_width = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    image_height = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    predicted_label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    predicted_confidence = table.Column<double>(type: "double precision", nullable: true),
                    ground_truth_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ground_truth_label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    yolo_annotations_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Unlabeled"),
                    sticky_split = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    labeled_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    labeled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    quality_note = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("pk_ai_dataset_items", x => x.id);
                    table.CheckConstraint("chk_ai_dataset_items_verified_label", "status <> 'VerifiedLabeled' OR (ground_truth_category_id IS NOT NULL AND ground_truth_label IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_ai_dataset_items_category",
                        column: x => x.ground_truth_category_id,
                        principalTable: "waste_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ai_dataset_items_feedback",
                        column: x => x.source_feedback_id,
                        principalTable: "ai_feedback_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ai_dataset_items_scan",
                        column: x => x.source_scan_id,
                        principalTable: "ai_scan_histories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ai_dataset_items_staff",
                        column: x => x.labeled_by_staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_dataset_version_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dataset_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_split = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category_code_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    yolo_annotations_snapshot = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_dataset_version_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_dataset_version_items_item",
                        column: x => x.dataset_item_id,
                        principalTable: "ai_dataset_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ai_dataset_version_items_ver",
                        column: x => x.version_id,
                        principalTable: "ai_dataset_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_ai_feedback_logs_dataset_item_id",
                table: "ai_feedback_logs",
                column: "dataset_item_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_feedback_logs_status_created",
                table: "ai_feedback_logs",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedback_logs_reviewed_by_staff_id",
                table: "ai_feedback_logs",
                column: "reviewed_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "uq_ai_feedback_logs_scan_id",
                table: "ai_feedback_logs",
                column: "scan_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_ai_dataset_items_queue",
                table: "ai_dataset_items",
                columns: new[] { "status", "ground_truth_label", "created_at" },
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "idx_ai_dataset_items_split",
                table: "ai_dataset_items",
                columns: new[] { "sticky_split", "ground_truth_label" },
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ai_dataset_items_ground_truth_category_id",
                table: "ai_dataset_items",
                column: "ground_truth_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_dataset_items_labeled_by_staff_id",
                table: "ai_dataset_items",
                column: "labeled_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_dataset_items_source_feedback_id",
                table: "ai_dataset_items",
                column: "source_feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_dataset_items_source_scan_id",
                table: "ai_dataset_items",
                column: "source_scan_id");

            migrationBuilder.CreateIndex(
                name: "uq_ai_dataset_items_sha256",
                table: "ai_dataset_items",
                column: "image_sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_ai_dataset_version_items_split",
                table: "ai_dataset_version_items",
                columns: new[] { "version_id", "assigned_split", "category_code_snapshot" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_dataset_version_items_dataset_item_id",
                table: "ai_dataset_version_items",
                column: "dataset_item_id");

            migrationBuilder.CreateIndex(
                name: "uq_ai_dataset_version_items_pair",
                table: "ai_dataset_version_items",
                columns: new[] { "version_id", "dataset_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_dataset_versions_created_by_staff_id",
                table: "ai_dataset_versions",
                column: "created_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "uq_ai_dataset_versions_code",
                table: "ai_dataset_versions",
                column: "version_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_ai_scan_histories_ood",
                table: "ai_scan_histories",
                columns: new[] { "is_out_of_distribution", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_ai_scan_histories_sha256",
                table: "ai_scan_histories",
                column: "image_sha256");

            migrationBuilder.CreateIndex(
                name: "idx_ai_scan_histories_user",
                table: "ai_scan_histories",
                columns: new[] { "user_id", "created_at" },
                filter: "is_deleted = FALSE");

            migrationBuilder.AddForeignKey(
                name: "fk_ai_feedback_logs_reviewer",
                table: "ai_feedback_logs",
                column: "reviewed_by_staff_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_ai_feedback_logs_scan",
                table: "ai_feedback_logs",
                column: "scan_id",
                principalTable: "ai_scan_histories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ai_feedback_logs_reviewer",
                table: "ai_feedback_logs");

            migrationBuilder.DropForeignKey(
                name: "fk_ai_feedback_logs_scan",
                table: "ai_feedback_logs");

            migrationBuilder.DropTable(
                name: "ai_dataset_version_items");

            migrationBuilder.DropTable(
                name: "ai_dataset_items");

            migrationBuilder.DropTable(
                name: "ai_dataset_versions");

            migrationBuilder.DropTable(
                name: "ai_scan_histories");

            migrationBuilder.DropIndex(
                name: "idx_ai_feedback_logs_dataset_item_id",
                table: "ai_feedback_logs");

            migrationBuilder.DropIndex(
                name: "idx_ai_feedback_logs_status_created",
                table: "ai_feedback_logs");

            migrationBuilder.DropIndex(
                name: "IX_ai_feedback_logs_reviewed_by_staff_id",
                table: "ai_feedback_logs");

            migrationBuilder.DropIndex(
                name: "uq_ai_feedback_logs_scan_id",
                table: "ai_feedback_logs");

            migrationBuilder.DropColumn(
                name: "dataset_item_id",
                table: "ai_feedback_logs");

            migrationBuilder.DropColumn(
                name: "detected_objects_snapshot",
                table: "ai_feedback_logs");

            migrationBuilder.DropColumn(
                name: "image_sha256",
                table: "ai_feedback_logs");

            migrationBuilder.DropColumn(
                name: "model_used",
                table: "ai_feedback_logs");

            migrationBuilder.DropColumn(
                name: "review_decision",
                table: "ai_feedback_logs");

            migrationBuilder.DropColumn(
                name: "review_note",
                table: "ai_feedback_logs");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "ai_feedback_logs");

            migrationBuilder.DropColumn(
                name: "reviewed_by_staff_id",
                table: "ai_feedback_logs");

            migrationBuilder.DropColumn(
                name: "scan_id",
                table: "ai_feedback_logs");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ai_feedback_logs");
        }
    }
}
