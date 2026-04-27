using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Basisregisters.FeedConsumers.Migrations
{
    /// <inheritdoc />
    public partial class AddAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "addresses",
                schema: "changefeed",
                columns: table => new
                {
                    persistent_uri = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    persistent_local_id = table.Column<int>(type: "integer", nullable: false),
                    postal_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    street_name_persistent_local_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    house_number = table.Column<string>(type: "text", nullable: false),
                    box_number = table.Column<string>(type: "text", nullable: true),
                    geometry = table.Column<Geometry>(type: "geometry", nullable: false),
                    position_method = table.Column<string>(type: "text", nullable: false),
                    position_specification = table.Column<string>(type: "text", nullable: false),
                    officially_assigned = table.Column<bool>(type: "boolean", nullable: false),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false),
                    version_id = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_addresses", x => x.persistent_uri);
                });

            migrationBuilder.CreateIndex(
                name: "IX_addresses_box_number",
                schema: "changefeed",
                table: "addresses",
                column: "box_number");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_geometry",
                schema: "changefeed",
                table: "addresses",
                column: "geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_house_number",
                schema: "changefeed",
                table: "addresses",
                column: "house_number");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_persistent_local_id",
                schema: "changefeed",
                table: "addresses",
                column: "persistent_local_id");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_postal_code",
                schema: "changefeed",
                table: "addresses",
                column: "postal_code");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_is_removed",
                schema: "changefeed",
                table: "addresses",
                column: "is_removed");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_is_removed_status",
                schema: "changefeed",
                table: "addresses",
                columns: new[] { "is_removed", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_addresses_status",
                schema: "changefeed",
                table: "addresses",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_street_name_persistent_local_id",
                schema: "changefeed",
                table: "addresses",
                column: "street_name_persistent_local_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "addresses",
                schema: "changefeed");
        }
    }
}
