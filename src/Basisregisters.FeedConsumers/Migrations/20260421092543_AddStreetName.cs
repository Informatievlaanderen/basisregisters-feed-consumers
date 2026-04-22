using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Basisregisters.FeedConsumers.Migrations
{
    /// <inheritdoc />
    public partial class AddStreetName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "streetnames",
                schema: "changefeed",
                columns: table => new
                {
                    persistent_uri = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    persistent_local_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    nis_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name_dutch = table.Column<string>(type: "text", nullable: true),
                    name_french = table.Column<string>(type: "text", nullable: true),
                    name_german = table.Column<string>(type: "text", nullable: true),
                    name_english = table.Column<string>(type: "text", nullable: true),
                    homonym_addition_dutch = table.Column<string>(type: "text", nullable: true),
                    homonym_addition_french = table.Column<string>(type: "text", nullable: true),
                    homonym_addition_german = table.Column<string>(type: "text", nullable: true),
                    homonym_addition_english = table.Column<string>(type: "text", nullable: true),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false),
                    version_id = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_streetnames", x => x.persistent_uri);
                });

            migrationBuilder.CreateIndex(
                name: "IX_streetnames_is_removed",
                schema: "changefeed",
                table: "streetnames",
                column: "is_removed");

            migrationBuilder.CreateIndex(
                name: "IX_streetnames_nis_code",
                schema: "changefeed",
                table: "streetnames",
                column: "nis_code");

            migrationBuilder.CreateIndex(
                name: "IX_streetnames_persistent_local_id",
                schema: "changefeed",
                table: "streetnames",
                column: "persistent_local_id");

            migrationBuilder.CreateIndex(
                name: "IX_streetnames_status",
                schema: "changefeed",
                table: "streetnames",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "streetnames",
                schema: "changefeed");
        }
    }
}
