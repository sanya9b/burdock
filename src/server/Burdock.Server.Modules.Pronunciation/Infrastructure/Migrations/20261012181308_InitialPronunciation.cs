using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Burdock.Server.Modules.Pronunciation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPronunciation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pronunciation");

            migrationBuilder.CreateTable(
                name: "clips",
                schema: "pronunciation",
                columns: table => new
                {
                    lexeme_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voice = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    engine_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    text = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    path = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    bytes = table.Column<long>(type: "bigint", nullable: false),
                    requested_by = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rendered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clips", x => new { x.lexeme_id, x.voice });
                });

            migrationBuilder.CreateIndex(
                name: "ix_clips_status_engine",
                schema: "pronunciation",
                table: "clips",
                columns: new[] { "status", "engine_version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clips",
                schema: "pronunciation");
        }
    }
}
