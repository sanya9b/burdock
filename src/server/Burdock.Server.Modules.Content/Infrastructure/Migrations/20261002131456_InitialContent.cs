using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Burdock.Server.Modules.Content.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "content");

            migrationBuilder.CreateTable(
                name: "packs",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    pair = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    topic = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    object_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    min_app_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deprecated_after = table.Column<DateOnly>(type: "date", nullable: true),
                    attributions = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_packs", x => new { x.id, x.version });
                });

            migrationBuilder.CreateIndex(
                name: "ix_packs_pair_status",
                schema: "content",
                table: "packs",
                columns: new[] { "pair", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_packs_status",
                schema: "content",
                table: "packs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "packs",
                schema: "content");
        }
    }
}
