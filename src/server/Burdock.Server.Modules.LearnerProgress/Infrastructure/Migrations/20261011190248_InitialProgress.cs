using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Burdock.Server.Modules.LearnerProgress.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "progress");

            migrationBuilder.CreateTable(
                name: "events",
                schema: "progress",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    server_seq = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => new { x.user_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "user_seq",
                schema: "progress",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_seq = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_seq", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_events_user_local_date",
                schema: "progress",
                table: "events",
                columns: new[] { "user_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "ux_events_user_seq",
                schema: "progress",
                table: "events",
                columns: new[] { "user_id", "server_seq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "events",
                schema: "progress");

            migrationBuilder.DropTable(
                name: "user_seq",
                schema: "progress");
        }
    }
}
