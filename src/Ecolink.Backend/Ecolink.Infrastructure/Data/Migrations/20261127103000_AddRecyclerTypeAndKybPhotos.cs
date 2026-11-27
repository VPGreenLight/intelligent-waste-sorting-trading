using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecyclerTypeAndKybPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_photo_urls_json",
                table: "recycler_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_card_back_url",
                table: "recycler_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_card_front_url",
                table: "recycler_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recycler_type",
                table: "recycler_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Facility");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "business_photo_urls_json",
                table: "recycler_profiles");

            migrationBuilder.DropColumn(
                name: "identity_card_back_url",
                table: "recycler_profiles");

            migrationBuilder.DropColumn(
                name: "identity_card_front_url",
                table: "recycler_profiles");

            migrationBuilder.DropColumn(
                name: "recycler_type",
                table: "recycler_profiles");
        }
    }
}
