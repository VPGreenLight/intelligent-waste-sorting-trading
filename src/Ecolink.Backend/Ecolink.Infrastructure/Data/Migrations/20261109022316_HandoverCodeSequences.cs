using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class HandoverCodeSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "seq_handover_receipt_code");

            migrationBuilder.CreateSequence(
                name: "seq_handover_request_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "seq_handover_receipt_code");

            migrationBuilder.DropSequence(
                name: "seq_handover_request_code");
        }
    }
}
