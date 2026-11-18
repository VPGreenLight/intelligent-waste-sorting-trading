using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Ecolink.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BlogContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Id",
                table: "recycler_profiles");

            migrationBuilder.AlterColumn<Point>(
                name: "geom",
                table: "recycler_profiles",
                type: "geometry(Point, 4326)",
                nullable: true,
                oldClrType: typeof(Point),
                oldType: "geometry(Point, 4326)");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    audit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.audit_id);
                    table.ForeignKey(
                        name: "fk_audit_logs_users",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "blog_posts",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    slug = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    cover_image_url = table.Column<string>(type: "text", nullable: true),
                    tags = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    approved_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blog_posts", x => x.post_id);
                    table.ForeignKey(
                        name: "fk_blog_posts_approved_by",
                        column: x => x.approved_by_admin_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_blog_posts_author",
                        column: x => x.author_staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.notification_id);
                    table.ForeignKey(
                        name: "fk_notifications_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_actor_user_id",
                table: "audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_blog_posts_approved_by_admin_id",
                table: "blog_posts",
                column: "approved_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_blog_posts_author_status",
                table: "blog_posts",
                columns: new[] { "author_staff_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_blog_posts_status_published",
                table: "blog_posts",
                columns: new[] { "status", "published_at" });

            migrationBuilder.CreateIndex(
                name: "ux_blog_posts_slug",
                table: "blog_posts",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_read_created",
                table: "notifications",
                columns: new[] { "user_id", "is_read", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "blog_posts");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.AlterColumn<Point>(
                name: "geom",
                table: "recycler_profiles",
                type: "geometry(Point, 4326)",
                nullable: false,
                oldClrType: typeof(Point),
                oldType: "geometry(Point, 4326)",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "recycler_profiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
