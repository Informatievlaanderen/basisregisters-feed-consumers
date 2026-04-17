using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Basisregisters.FeedConsumers.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "changefeed");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "feed_states",
                schema: "changefeed",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    event_position = table.Column<long>(type: "bigint", nullable: false),
                    page = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feed_states", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "municipalities",
                schema: "changefeed",
                columns: table => new
                {
                    persistent_uri = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    nis_code = table.Column<string>(type: "text", nullable: false),
                    version_id = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    official_language_dutch = table.Column<bool>(type: "boolean", nullable: true),
                    official_language_french = table.Column<bool>(type: "boolean", nullable: true),
                    official_language_german = table.Column<bool>(type: "boolean", nullable: true),
                    official_language_english = table.Column<bool>(type: "boolean", nullable: true),
                    facility_language_dutch = table.Column<bool>(type: "boolean", nullable: true),
                    facility_language_french = table.Column<bool>(type: "boolean", nullable: true),
                    facility_language_german = table.Column<bool>(type: "boolean", nullable: true),
                    facility_language_english = table.Column<bool>(type: "boolean", nullable: true),
                    name_dutch = table.Column<string>(type: "text", nullable: true),
                    name_french = table.Column<string>(type: "text", nullable: true),
                    name_german = table.Column<string>(type: "text", nullable: true),
                    name_english = table.Column<string>(type: "text", nullable: true),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_municipalities", x => x.persistent_uri);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feed_states",
                schema: "changefeed");

            migrationBuilder.DropTable(
                name: "municipalities",
                schema: "changefeed");
        }
    }
}
