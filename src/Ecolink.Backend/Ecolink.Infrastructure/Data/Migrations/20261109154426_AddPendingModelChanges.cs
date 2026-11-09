using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "accepted_at",
                table: "trade_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "agreed_price_per_kg",
                table: "trade_orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category_code",
                table: "trade_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "confirmed_time_slot",
                table: "trade_orders",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "initial_price_per_kg",
                table: "trade_orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_address",
                table: "trade_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "proposed_time_slot",
                table: "trade_orders",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recycler_user_id",
                table: "trade_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trade_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    message_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message_content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    attachment_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    proposal_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    proposed_price_per_kg = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    proposed_weight_kg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    proposed_time_slot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    proposal_data = table.Column<string>(type: "jsonb", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_chat_messages_trade_orders",
                        column: x => x.trade_order_id,
                        principalTable: "trade_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_chat_messages_users",
                        column: x => x.sender_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_trade_orders_recycler_user_id",
                table: "trade_orders",
                column: "recycler_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_order_id_proposal_status",
                table: "chat_messages",
                columns: new[] { "trade_order_id", "proposal_status" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_order_id_sent_at",
                table: "chat_messages",
                columns: new[] { "trade_order_id", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_sender_user_id",
                table: "chat_messages",
                column: "sender_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_trade_orders_recycler_profiles",
                table: "trade_orders",
                column: "recycler_user_id",
                principalTable: "recycler_profiles",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_trade_orders_recycler_profiles",
                table: "trade_orders");

            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropIndex(
                name: "idx_trade_orders_recycler_user_id",
                table: "trade_orders");

            migrationBuilder.DropColumn(
                name: "accepted_at",
                table: "trade_orders");

            migrationBuilder.DropColumn(
                name: "agreed_price_per_kg",
                table: "trade_orders");

            migrationBuilder.DropColumn(
                name: "category_code",
                table: "trade_orders");

            migrationBuilder.DropColumn(
                name: "confirmed_time_slot",
                table: "trade_orders");

            migrationBuilder.DropColumn(
                name: "initial_price_per_kg",
                table: "trade_orders");

            migrationBuilder.DropColumn(
                name: "pickup_address",
                table: "trade_orders");

            migrationBuilder.DropColumn(
                name: "proposed_time_slot",
                table: "trade_orders");

            migrationBuilder.DropColumn(
                name: "recycler_user_id",
                table: "trade_orders");
        }
    }
}
