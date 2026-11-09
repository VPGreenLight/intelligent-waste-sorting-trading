using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeSnakeCaseSchemaAndDesignColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcceptedMaterials_CollectionPartners_PartnerId",
                table: "AcceptedMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_AcceptedMaterials_WasteCategories_WasteCategoryId",
                table: "AcceptedMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_AIFeedbackLogs_users_UserId",
                table: "AIFeedbackLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionPartners_users_UserId",
                table: "CollectionPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_SortingRule_WasteCategories_WasteCategoryId",
                table: "SortingRule");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeOrders_CollectionPartners_PartnerId",
                table: "TradeOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeOrders_users_UserId",
                table: "TradeOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_TransactionReceipts_TradeOrders_TradeOrderId",
                table: "TransactionReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_UserEcoProfiles_users_UserId",
                table: "UserEcoProfiles");

            migrationBuilder.DropForeignKey(
                name: "fk_recycler_price_list_waste_categories",
                table: "recycler_price_list");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WasteCategories",
                table: "WasteCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserEcoProfiles",
                table: "UserEcoProfiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TransactionReceipts",
                table: "TransactionReceipts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TradeOrders",
                table: "TradeOrders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SortingRule",
                table: "SortingRule");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CollectionPartners",
                table: "CollectionPartners");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AIFeedbackLogs",
                table: "AIFeedbackLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AcceptedMaterials",
                table: "AcceptedMaterials");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "recycler_profiles");

            migrationBuilder.RenameTable(
                name: "WasteCategories",
                newName: "waste_categories");

            migrationBuilder.RenameTable(
                name: "UserEcoProfiles",
                newName: "user_eco_profiles");

            migrationBuilder.RenameTable(
                name: "TransactionReceipts",
                newName: "transaction_receipts");

            migrationBuilder.RenameTable(
                name: "TradeOrders",
                newName: "trade_orders");

            migrationBuilder.RenameTable(
                name: "SortingRule",
                newName: "sorting_guidance_rules");

            migrationBuilder.RenameTable(
                name: "CollectionPartners",
                newName: "collection_partners");

            migrationBuilder.RenameTable(
                name: "AIFeedbackLogs",
                newName: "ai_feedback_logs");

            migrationBuilder.RenameTable(
                name: "AcceptedMaterials",
                newName: "accepted_materials");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "waste_categories",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "waste_categories",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "waste_categories",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "waste_categories",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "waste_categories",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "StandardBinColor",
                table: "waste_categories",
                newName: "standard_bin_color");

            migrationBuilder.RenameColumn(
                name: "IconUrl",
                table: "waste_categories",
                newName: "icon_url");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "waste_categories",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "user_eco_profiles",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "user_eco_profiles",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "user_eco_profiles",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TreeLevel",
                table: "user_eco_profiles",
                newName: "tree_level");

            migrationBuilder.RenameColumn(
                name: "TreeExperience",
                table: "user_eco_profiles",
                newName: "tree_experience");

            migrationBuilder.RenameColumn(
                name: "TotalWasteScanned",
                table: "user_eco_profiles",
                newName: "total_waste_scanned");

            migrationBuilder.RenameColumn(
                name: "TotalRecycledKg",
                table: "user_eco_profiles",
                newName: "total_recycled_kg");

            migrationBuilder.RenameColumn(
                name: "TotalLifetimePoints",
                table: "user_eco_profiles",
                newName: "total_lifetime_points");

            migrationBuilder.RenameColumn(
                name: "CurrentGreenPoints",
                table: "user_eco_profiles",
                newName: "current_green_points");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "user_eco_profiles",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_UserEcoProfiles_UserId",
                table: "user_eco_profiles",
                newName: "uq_user_eco_profiles_user_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "transaction_receipts",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "transaction_receipts",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                table: "transaction_receipts",
                newName: "unit_price");

            migrationBuilder.RenameColumn(
                name: "TradeOrderId",
                table: "transaction_receipts",
                newName: "trade_order_id");

            migrationBuilder.RenameColumn(
                name: "TotalAmountCash",
                table: "transaction_receipts",
                newName: "total_amount_cash");

            migrationBuilder.RenameColumn(
                name: "QrSecurityCode",
                table: "transaction_receipts",
                newName: "qr_security_code");

            migrationBuilder.RenameColumn(
                name: "PointsAwarded",
                table: "transaction_receipts",
                newName: "points_awarded");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "transaction_receipts",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                table: "transaction_receipts",
                newName: "completed_at");

            migrationBuilder.RenameColumn(
                name: "ActualWeightKg",
                table: "transaction_receipts",
                newName: "actual_weight_kg");

            migrationBuilder.RenameIndex(
                name: "IX_TransactionReceipts_TradeOrderId",
                table: "transaction_receipts",
                newName: "uq_transaction_receipts_trade_order_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "trade_orders",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Note",
                table: "trade_orders",
                newName: "note");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "trade_orders",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "trade_orders",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "trade_orders",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ScheduledAt",
                table: "trade_orders",
                newName: "scheduled_at");

            migrationBuilder.RenameColumn(
                name: "PartnerId",
                table: "trade_orders",
                newName: "partner_id");

            migrationBuilder.RenameColumn(
                name: "EstimatedWeightKg",
                table: "trade_orders",
                newName: "estimated_weight_kg");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "trade_orders",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_TradeOrders_UserId",
                table: "trade_orders",
                newName: "idx_trade_orders_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_TradeOrders_PartnerId",
                table: "trade_orders",
                newName: "idx_trade_orders_partner_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "sorting_guidance_rules",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WasteCategoryId",
                table: "sorting_guidance_rules",
                newName: "category_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "sorting_guidance_rules",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "PreparationSteps",
                table: "sorting_guidance_rules",
                newName: "preparation_steps");

            migrationBuilder.RenameColumn(
                name: "HazardWarning",
                table: "sorting_guidance_rules",
                newName: "hazard_warning");

            migrationBuilder.RenameColumn(
                name: "HandlingAction",
                table: "sorting_guidance_rules",
                newName: "handling_action");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "sorting_guidance_rules",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ApplicableRegion",
                table: "sorting_guidance_rules",
                newName: "applicable_region");

            migrationBuilder.RenameIndex(
                name: "IX_SortingRule_WasteCategoryId",
                table: "sorting_guidance_rules",
                newName: "uq_sorting_guidance_rules_category_id");

            migrationBuilder.RenameColumn(
                name: "Longitude",
                table: "collection_partners",
                newName: "longitude");

            migrationBuilder.RenameColumn(
                name: "Latitude",
                table: "collection_partners",
                newName: "latitude");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "collection_partners",
                newName: "address");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "collection_partners",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "collection_partners",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "collection_partners",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "OperatingHours",
                table: "collection_partners",
                newName: "operating_hours");

            migrationBuilder.RenameColumn(
                name: "IsVerified",
                table: "collection_partners",
                newName: "is_verified");

            migrationBuilder.RenameColumn(
                name: "FacilityName",
                table: "collection_partners",
                newName: "facility_name");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "collection_partners",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ContactPhone",
                table: "collection_partners",
                newName: "contact_phone");

            migrationBuilder.RenameIndex(
                name: "IX_CollectionPartners_UserId",
                table: "collection_partners",
                newName: "uq_collection_partners_user_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "ai_feedback_logs",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Note",
                table: "ai_feedback_logs",
                newName: "note");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ai_feedback_logs",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserReportedLabel",
                table: "ai_feedback_logs",
                newName: "user_reported_label");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "ai_feedback_logs",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "ai_feedback_logs",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "PredictedLabel",
                table: "ai_feedback_logs",
                newName: "predicted_label");

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "ai_feedback_logs",
                newName: "image_url");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "ai_feedback_logs",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ConfidenceScore",
                table: "ai_feedback_logs",
                newName: "confidence_score");

            migrationBuilder.RenameColumn(
                name: "AdminVerifiedLabel",
                table: "ai_feedback_logs",
                newName: "admin_verified_label");

            migrationBuilder.RenameIndex(
                name: "IX_AIFeedbackLogs_UserId",
                table: "ai_feedback_logs",
                newName: "idx_ai_feedback_logs_user_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "accepted_materials",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WasteCategoryId",
                table: "accepted_materials",
                newName: "waste_category_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "accepted_materials",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "PricePerKg",
                table: "accepted_materials",
                newName: "price_per_kg");

            migrationBuilder.RenameColumn(
                name: "PartnerId",
                table: "accepted_materials",
                newName: "partner_id");

            migrationBuilder.RenameColumn(
                name: "MinimumQuantityKg",
                table: "accepted_materials",
                newName: "minimum_quantity_kg");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "accepted_materials",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_AcceptedMaterials_WasteCategoryId",
                table: "accepted_materials",
                newName: "idx_accepted_materials_waste_category_id");

            migrationBuilder.RenameIndex(
                name: "IX_AcceptedMaterials_PartnerId",
                table: "accepted_materials",
                newName: "idx_accepted_materials_partner_id");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "waste_categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "waste_categories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "standard_bin_color",
                table: "waste_categories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "waste_categories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<bool>(
                name: "is_recyclable",
                table: "waste_categories",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "waste_categories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AlterColumn<int>(
                name: "tree_level",
                table: "user_eco_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "tree_experience",
                table: "user_eco_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "total_waste_scanned",
                table: "user_eco_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_recycled_kg",
                table: "user_eco_profiles",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<int>(
                name: "total_lifetime_points",
                table: "user_eco_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "current_green_points",
                table: "user_eco_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "user_eco_profiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "transaction_receipts",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_amount_cash",
                table: "transaction_receipts",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "qr_security_code",
                table: "transaction_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "transaction_receipts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "completed_at",
                table: "transaction_receipts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<decimal>(
                name: "actual_weight_kg",
                table: "transaction_receipts",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "trade_orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "estimated_weight_kg",
                table: "trade_orders",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "trade_orders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "sorting_guidance_rules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "applicable_region",
                table: "sorting_guidance_rules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "VN_NATIONAL",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "sorting_instruction",
                table: "sorting_guidance_rules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "sorting_guidance_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ACTIVE");

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "sorting_guidance_rules",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "operating_hours",
                table: "collection_partners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "is_verified",
                table: "collection_partners",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "facility_name",
                table: "collection_partners",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "collection_partners",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "contact_phone",
                table: "collection_partners",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "ai_feedback_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "user_reported_label",
                table: "ai_feedback_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "predicted_label",
                table: "ai_feedback_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "ai_feedback_logs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "admin_verified_label",
                table: "ai_feedback_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "price_per_kg",
                table: "accepted_materials",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "minimum_quantity_kg",
                table: "accepted_materials",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 1.0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "accepted_materials",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddPrimaryKey(
                name: "pk_waste_categories",
                table: "waste_categories",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_eco_profiles",
                table: "user_eco_profiles",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_transaction_receipts",
                table: "transaction_receipts",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_trade_orders",
                table: "trade_orders",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sorting_guidance_rules",
                table: "sorting_guidance_rules",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_collection_partners",
                table: "collection_partners",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_ai_feedback_logs",
                table: "ai_feedback_logs",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_accepted_materials",
                table: "accepted_materials",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "uq_waste_categories_code",
                table: "waste_categories",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_accepted_materials_collection_partners",
                table: "accepted_materials",
                column: "partner_id",
                principalTable: "collection_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_accepted_materials_waste_categories",
                table: "accepted_materials",
                column: "waste_category_id",
                principalTable: "waste_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ai_feedback_logs_users",
                table: "ai_feedback_logs",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_collection_partners_users",
                table: "collection_partners",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_recycler_price_list_waste_categories",
                table: "recycler_price_list",
                column: "category_id",
                principalTable: "waste_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sorting_guidance_rules_waste_categories",
                table: "sorting_guidance_rules",
                column: "category_id",
                principalTable: "waste_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_trade_orders_collection_partners",
                table: "trade_orders",
                column: "partner_id",
                principalTable: "collection_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_trade_orders_users",
                table: "trade_orders",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_transaction_receipts_trade_orders",
                table: "transaction_receipts",
                column: "trade_order_id",
                principalTable: "trade_orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_eco_profiles_users",
                table: "user_eco_profiles",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_accepted_materials_collection_partners",
                table: "accepted_materials");

            migrationBuilder.DropForeignKey(
                name: "fk_accepted_materials_waste_categories",
                table: "accepted_materials");

            migrationBuilder.DropForeignKey(
                name: "fk_ai_feedback_logs_users",
                table: "ai_feedback_logs");

            migrationBuilder.DropForeignKey(
                name: "fk_collection_partners_users",
                table: "collection_partners");

            migrationBuilder.DropForeignKey(
                name: "fk_sorting_guidance_rules_waste_categories",
                table: "sorting_guidance_rules");

            migrationBuilder.DropForeignKey(
                name: "fk_trade_orders_collection_partners",
                table: "trade_orders");

            migrationBuilder.DropForeignKey(
                name: "fk_trade_orders_users",
                table: "trade_orders");

            migrationBuilder.DropForeignKey(
                name: "fk_transaction_receipts_trade_orders",
                table: "transaction_receipts");

            migrationBuilder.DropForeignKey(
                name: "fk_user_eco_profiles_users",
                table: "user_eco_profiles");

            migrationBuilder.DropPrimaryKey(
                name: "pk_waste_categories",
                table: "waste_categories");

            migrationBuilder.DropIndex(
                name: "uq_waste_categories_code",
                table: "waste_categories");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_eco_profiles",
                table: "user_eco_profiles");

            migrationBuilder.DropPrimaryKey(
                name: "pk_transaction_receipts",
                table: "transaction_receipts");

            migrationBuilder.DropPrimaryKey(
                name: "pk_trade_orders",
                table: "trade_orders");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sorting_guidance_rules",
                table: "sorting_guidance_rules");

            migrationBuilder.DropPrimaryKey(
                name: "pk_collection_partners",
                table: "collection_partners");

            migrationBuilder.DropPrimaryKey(
                name: "pk_ai_feedback_logs",
                table: "ai_feedback_logs");

            migrationBuilder.DropPrimaryKey(
                name: "pk_accepted_materials",
                table: "accepted_materials");

            migrationBuilder.DropColumn(
                name: "is_recyclable",
                table: "waste_categories");

            migrationBuilder.DropColumn(
                name: "status",
                table: "waste_categories");

            migrationBuilder.DropColumn(
                name: "sorting_instruction",
                table: "sorting_guidance_rules");

            migrationBuilder.DropColumn(
                name: "status",
                table: "sorting_guidance_rules");

            migrationBuilder.DropColumn(
                name: "title",
                table: "sorting_guidance_rules");

            migrationBuilder.RenameTable(
                name: "waste_categories",
                newName: "WasteCategories");

            migrationBuilder.RenameTable(
                name: "user_eco_profiles",
                newName: "UserEcoProfiles");

            migrationBuilder.RenameTable(
                name: "transaction_receipts",
                newName: "TransactionReceipts");

            migrationBuilder.RenameTable(
                name: "trade_orders",
                newName: "TradeOrders");

            migrationBuilder.RenameTable(
                name: "sorting_guidance_rules",
                newName: "SortingRule");

            migrationBuilder.RenameTable(
                name: "collection_partners",
                newName: "CollectionPartners");

            migrationBuilder.RenameTable(
                name: "ai_feedback_logs",
                newName: "AIFeedbackLogs");

            migrationBuilder.RenameTable(
                name: "accepted_materials",
                newName: "AcceptedMaterials");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "WasteCategories",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "WasteCategories",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "code",
                table: "WasteCategories",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "WasteCategories",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "WasteCategories",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "standard_bin_color",
                table: "WasteCategories",
                newName: "StandardBinColor");

            migrationBuilder.RenameColumn(
                name: "icon_url",
                table: "WasteCategories",
                newName: "IconUrl");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "WasteCategories",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "UserEcoProfiles",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "UserEcoProfiles",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "UserEcoProfiles",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tree_level",
                table: "UserEcoProfiles",
                newName: "TreeLevel");

            migrationBuilder.RenameColumn(
                name: "tree_experience",
                table: "UserEcoProfiles",
                newName: "TreeExperience");

            migrationBuilder.RenameColumn(
                name: "total_waste_scanned",
                table: "UserEcoProfiles",
                newName: "TotalWasteScanned");

            migrationBuilder.RenameColumn(
                name: "total_recycled_kg",
                table: "UserEcoProfiles",
                newName: "TotalRecycledKg");

            migrationBuilder.RenameColumn(
                name: "total_lifetime_points",
                table: "UserEcoProfiles",
                newName: "TotalLifetimePoints");

            migrationBuilder.RenameColumn(
                name: "current_green_points",
                table: "UserEcoProfiles",
                newName: "CurrentGreenPoints");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "UserEcoProfiles",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "uq_user_eco_profiles_user_id",
                table: "UserEcoProfiles",
                newName: "IX_UserEcoProfiles_UserId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "TransactionReceipts",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "TransactionReceipts",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "unit_price",
                table: "TransactionReceipts",
                newName: "UnitPrice");

            migrationBuilder.RenameColumn(
                name: "trade_order_id",
                table: "TransactionReceipts",
                newName: "TradeOrderId");

            migrationBuilder.RenameColumn(
                name: "total_amount_cash",
                table: "TransactionReceipts",
                newName: "TotalAmountCash");

            migrationBuilder.RenameColumn(
                name: "qr_security_code",
                table: "TransactionReceipts",
                newName: "QrSecurityCode");

            migrationBuilder.RenameColumn(
                name: "points_awarded",
                table: "TransactionReceipts",
                newName: "PointsAwarded");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "TransactionReceipts",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "completed_at",
                table: "TransactionReceipts",
                newName: "CompletedAt");

            migrationBuilder.RenameColumn(
                name: "actual_weight_kg",
                table: "TransactionReceipts",
                newName: "ActualWeightKg");

            migrationBuilder.RenameIndex(
                name: "uq_transaction_receipts_trade_order_id",
                table: "TransactionReceipts",
                newName: "IX_TransactionReceipts_TradeOrderId");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "TradeOrders",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "note",
                table: "TradeOrders",
                newName: "Note");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "TradeOrders",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "TradeOrders",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "TradeOrders",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "scheduled_at",
                table: "TradeOrders",
                newName: "ScheduledAt");

            migrationBuilder.RenameColumn(
                name: "partner_id",
                table: "TradeOrders",
                newName: "PartnerId");

            migrationBuilder.RenameColumn(
                name: "estimated_weight_kg",
                table: "TradeOrders",
                newName: "EstimatedWeightKg");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "TradeOrders",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "idx_trade_orders_user_id",
                table: "TradeOrders",
                newName: "IX_TradeOrders_UserId");

            migrationBuilder.RenameIndex(
                name: "idx_trade_orders_partner_id",
                table: "TradeOrders",
                newName: "IX_TradeOrders_PartnerId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "SortingRule",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "SortingRule",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "preparation_steps",
                table: "SortingRule",
                newName: "PreparationSteps");

            migrationBuilder.RenameColumn(
                name: "hazard_warning",
                table: "SortingRule",
                newName: "HazardWarning");

            migrationBuilder.RenameColumn(
                name: "handling_action",
                table: "SortingRule",
                newName: "HandlingAction");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "SortingRule",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "category_id",
                table: "SortingRule",
                newName: "WasteCategoryId");

            migrationBuilder.RenameColumn(
                name: "applicable_region",
                table: "SortingRule",
                newName: "ApplicableRegion");

            migrationBuilder.RenameIndex(
                name: "uq_sorting_guidance_rules_category_id",
                table: "SortingRule",
                newName: "IX_SortingRule_WasteCategoryId");

            migrationBuilder.RenameColumn(
                name: "longitude",
                table: "CollectionPartners",
                newName: "Longitude");

            migrationBuilder.RenameColumn(
                name: "latitude",
                table: "CollectionPartners",
                newName: "Latitude");

            migrationBuilder.RenameColumn(
                name: "address",
                table: "CollectionPartners",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "CollectionPartners",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "CollectionPartners",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "CollectionPartners",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "operating_hours",
                table: "CollectionPartners",
                newName: "OperatingHours");

            migrationBuilder.RenameColumn(
                name: "is_verified",
                table: "CollectionPartners",
                newName: "IsVerified");

            migrationBuilder.RenameColumn(
                name: "facility_name",
                table: "CollectionPartners",
                newName: "FacilityName");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "CollectionPartners",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "contact_phone",
                table: "CollectionPartners",
                newName: "ContactPhone");

            migrationBuilder.RenameIndex(
                name: "uq_collection_partners_user_id",
                table: "CollectionPartners",
                newName: "IX_CollectionPartners_UserId");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "AIFeedbackLogs",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "note",
                table: "AIFeedbackLogs",
                newName: "Note");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "AIFeedbackLogs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_reported_label",
                table: "AIFeedbackLogs",
                newName: "UserReportedLabel");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "AIFeedbackLogs",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "AIFeedbackLogs",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "predicted_label",
                table: "AIFeedbackLogs",
                newName: "PredictedLabel");

            migrationBuilder.RenameColumn(
                name: "image_url",
                table: "AIFeedbackLogs",
                newName: "ImageUrl");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "AIFeedbackLogs",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "confidence_score",
                table: "AIFeedbackLogs",
                newName: "ConfidenceScore");

            migrationBuilder.RenameColumn(
                name: "admin_verified_label",
                table: "AIFeedbackLogs",
                newName: "AdminVerifiedLabel");

            migrationBuilder.RenameIndex(
                name: "idx_ai_feedback_logs_user_id",
                table: "AIFeedbackLogs",
                newName: "IX_AIFeedbackLogs_UserId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "AcceptedMaterials",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "waste_category_id",
                table: "AcceptedMaterials",
                newName: "WasteCategoryId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "AcceptedMaterials",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "price_per_kg",
                table: "AcceptedMaterials",
                newName: "PricePerKg");

            migrationBuilder.RenameColumn(
                name: "partner_id",
                table: "AcceptedMaterials",
                newName: "PartnerId");

            migrationBuilder.RenameColumn(
                name: "minimum_quantity_kg",
                table: "AcceptedMaterials",
                newName: "MinimumQuantityKg");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "AcceptedMaterials",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "idx_accepted_materials_waste_category_id",
                table: "AcceptedMaterials",
                newName: "IX_AcceptedMaterials_WasteCategoryId");

            migrationBuilder.RenameIndex(
                name: "idx_accepted_materials_partner_id",
                table: "AcceptedMaterials",
                newName: "IX_AcceptedMaterials_PartnerId");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "staff_profiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "recycler_profiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WasteCategories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "WasteCategories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "StandardBinColor",
                table: "WasteCategories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "WasteCategories",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<int>(
                name: "TreeLevel",
                table: "UserEcoProfiles",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "TreeExperience",
                table: "UserEcoProfiles",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "TotalWasteScanned",
                table: "UserEcoProfiles",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalRecycledKg",
                table: "UserEcoProfiles",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,3)",
                oldPrecision: 12,
                oldScale: 3,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "TotalLifetimePoints",
                table: "UserEcoProfiles",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "CurrentGreenPoints",
                table: "UserEcoProfiles",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "UserEcoProfiles",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "TransactionReceipts",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmountCash",
                table: "TransactionReceipts",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "QrSecurityCode",
                table: "TransactionReceipts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TransactionReceipts",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CompletedAt",
                table: "TransactionReceipts",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualWeightKg",
                table: "TransactionReceipts",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,3)",
                oldPrecision: 12,
                oldScale: 3);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "TradeOrders",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedWeightKg",
                table: "TradeOrders",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,3)",
                oldPrecision: 12,
                oldScale: 3);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeOrders",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "SortingRule",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<string>(
                name: "ApplicableRegion",
                table: "SortingRule",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldDefaultValue: "VN_NATIONAL");

            migrationBuilder.AlterColumn<string>(
                name: "OperatingHours",
                table: "CollectionPartners",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsVerified",
                table: "CollectionPartners",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "FacilityName",
                table: "CollectionPartners",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "CollectionPartners",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<string>(
                name: "ContactPhone",
                table: "CollectionPartners",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "AIFeedbackLogs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "UserReportedLabel",
                table: "AIFeedbackLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "PredictedLabel",
                table: "AIFeedbackLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "AIFeedbackLogs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<string>(
                name: "AdminVerifiedLabel",
                table: "AIFeedbackLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PricePerKg",
                table: "AcceptedMaterials",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "MinimumQuantityKg",
                table: "AcceptedMaterials",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,3)",
                oldPrecision: 12,
                oldScale: 3,
                oldDefaultValue: 1.0m);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "AcceptedMaterials",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WasteCategories",
                table: "WasteCategories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserEcoProfiles",
                table: "UserEcoProfiles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TransactionReceipts",
                table: "TransactionReceipts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TradeOrders",
                table: "TradeOrders",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SortingRule",
                table: "SortingRule",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CollectionPartners",
                table: "CollectionPartners",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AIFeedbackLogs",
                table: "AIFeedbackLogs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AcceptedMaterials",
                table: "AcceptedMaterials",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AcceptedMaterials_CollectionPartners_PartnerId",
                table: "AcceptedMaterials",
                column: "PartnerId",
                principalTable: "CollectionPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AcceptedMaterials_WasteCategories_WasteCategoryId",
                table: "AcceptedMaterials",
                column: "WasteCategoryId",
                principalTable: "WasteCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AIFeedbackLogs_users_UserId",
                table: "AIFeedbackLogs",
                column: "UserId",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionPartners_users_UserId",
                table: "CollectionPartners",
                column: "UserId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SortingRule_WasteCategories_WasteCategoryId",
                table: "SortingRule",
                column: "WasteCategoryId",
                principalTable: "WasteCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeOrders_CollectionPartners_PartnerId",
                table: "TradeOrders",
                column: "PartnerId",
                principalTable: "CollectionPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeOrders_users_UserId",
                table: "TradeOrders",
                column: "UserId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionReceipts_TradeOrders_TradeOrderId",
                table: "TransactionReceipts",
                column: "TradeOrderId",
                principalTable: "TradeOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserEcoProfiles_users_UserId",
                table: "UserEcoProfiles",
                column: "UserId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
