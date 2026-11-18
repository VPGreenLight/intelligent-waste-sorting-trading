using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardCyclesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leaderboard_cycles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    period_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    top1_bonus_points = table.Column<long>(type: "bigint", nullable: false, defaultValue: 500L),
                    top2_bonus_points = table.Column<long>(type: "bigint", nullable: false, defaultValue: 300L),
                    top3_bonus_points = table.Column<long>(type: "bigint", nullable: false, defaultValue: 150L),
                    winners_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    created_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    finalized_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    finalized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leaderboard_cycles", x => x.id);
                    table.CheckConstraint("chk_leaderboard_cycles_bonus_nonneg", "top1_bonus_points >= 0 AND top2_bonus_points >= 0 AND top3_bonus_points >= 0");
                    table.CheckConstraint("chk_leaderboard_cycles_window", "ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_leaderboard_cycles_created_by",
                        column: x => x.created_by_admin_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_leaderboard_cycles_finalized_by",
                        column: x => x.finalized_by_admin_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_leaderboard_cycles_status_dates",
                table: "leaderboard_cycles",
                columns: new[] { "status", "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "IX_leaderboard_cycles_created_by_admin_id",
                table: "leaderboard_cycles",
                column: "created_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "IX_leaderboard_cycles_finalized_by_admin_id",
                table: "leaderboard_cycles",
                column: "finalized_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "uq_leaderboard_cycles_single_active",
                table: "leaderboard_cycles",
                column: "is_active",
                unique: true,
                filter: "is_active = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leaderboard_cycles");
        }
    }
}
