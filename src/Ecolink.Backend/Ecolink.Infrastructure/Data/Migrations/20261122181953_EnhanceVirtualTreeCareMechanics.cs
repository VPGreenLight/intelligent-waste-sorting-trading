using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceVirtualTreeCareMechanics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "care_exp",
                table: "virtual_tree_progress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "daily_fertilize_count",
                table: "virtual_tree_progress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "fertilizer_packs",
                table: "virtual_tree_progress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "harvest_ready",
                table: "virtual_tree_progress",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "hydration_percent",
                table: "virtual_tree_progress",
                type: "integer",
                nullable: false,
                defaultValue: 75);

            migrationBuilder.AddColumn<DateOnly>(
                name: "last_fertilize_date",
                table: "virtual_tree_progress",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_hydration_at",
                table: "virtual_tree_progress",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateOnly>(
                name: "last_scan_fertilizer_date",
                table: "virtual_tree_progress",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_fertilized",
                table: "virtual_tree_progress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_harvested",
                table: "virtual_tree_progress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS platform_policy_sections (
                    id uuid PRIMARY KEY,
                    code character varying(40) NOT NULL,
                    category character varying(30) NOT NULL DEFAULT 'TERMS',
                    title character varying(200) NOT NULL,
                    content text NOT NULL,
                    display_order integer NOT NULL DEFAULT 1,
                    status character varying(20) NOT NULL DEFAULT 'ACTIVE',
                    updated_by_admin_id uuid NULL,
                    created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                    updated_at timestamp with time zone NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ux_platform_policy_sections_code ON platform_policy_sections (code);
                CREATE INDEX IF NOT EXISTS ix_platform_policy_sections_status_order ON platform_policy_sections (status, display_order);
            ");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tree_care_exp_nonneg",
                table: "virtual_tree_progress",
                sql: "care_exp >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tree_fert_packs_nonneg",
                table: "virtual_tree_progress",
                sql: "fertilizer_packs >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tree_hydration_range",
                table: "virtual_tree_progress",
                sql: "hydration_percent >= 0 AND hydration_percent <= 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tree_care_exp_nonneg",
                table: "virtual_tree_progress");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tree_fert_packs_nonneg",
                table: "virtual_tree_progress");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tree_hydration_range",
                table: "virtual_tree_progress");

            migrationBuilder.DropColumn(
                name: "care_exp",
                table: "virtual_tree_progress");

            migrationBuilder.DropColumn(
                name: "daily_fertilize_count",
                table: "virtual_tree_progress");

            migrationBuilder.DropColumn(
                name: "fertilizer_packs",
                table: "virtual_tree_progress");

            migrationBuilder.DropColumn(
                name: "harvest_ready",
                table: "virtual_tree_progress");

            migrationBuilder.DropColumn(
                name: "hydration_percent",
                table: "virtual_tree_progress");

            migrationBuilder.DropColumn(
                name: "last_fertilize_date",
                table: "virtual_tree_progress");

            migrationBuilder.DropColumn(
                name: "last_hydration_at",
                table: "virtual_tree_progress");

            migrationBuilder.DropColumn(
                name: "last_scan_fertilizer_date",
                table: "virtual_tree_progress");

            migrationBuilder.DropColumn(
                name: "total_fertilized",
                table: "virtual_tree_progress");

            migrationBuilder.DropColumn(
                name: "total_harvested",
                table: "virtual_tree_progress");
        }
    }
}
