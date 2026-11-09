using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecyclerPriceListAndAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "busy_reason",
                table: "recycler_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "weekly_schedule",
                table: "recycler_profiles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recycler_price_list",
                columns: table => new
                {
                    price_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recycler_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_per_kg = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    min_quantity_kg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false, defaultValue: 1.0m),
                    collection_conditions = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recycler_price_list", x => x.price_id);
                    table.CheckConstraint("ck_recycler_price_list_min_quantity_positive", "min_quantity_kg > 0");
                    table.CheckConstraint("ck_recycler_price_list_price_positive", "price_per_kg > 0");
                    table.ForeignKey(
                        name: "fk_recycler_price_list_recycler_profiles",
                        column: x => x.recycler_id,
                        principalTable: "recycler_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recycler_price_list_waste_categories",
                        column: x => x.category_id,
                        principalTable: "WasteCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recycler_price_list_category_id",
                table: "recycler_price_list",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ux_recycler_price_list_recycler_category",
                table: "recycler_price_list",
                columns: new[] { "recycler_id", "category_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recycler_price_list");

            migrationBuilder.DropColumn(
                name: "busy_reason",
                table: "recycler_profiles");

            migrationBuilder.DropColumn(
                name: "weekly_schedule",
                table: "recycler_profiles");
        }
    }
}
