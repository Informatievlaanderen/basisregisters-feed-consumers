using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Basisregisters.FeedConsumers.Migrations
{
    /// <inheritdoc />
    public partial class AddBuilding_BuildingUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "buildings",
                schema: "changefeed",
                columns: table => new
                {
                    persistent_uri = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    persistent_local_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    geometry_method = table.Column<string>(type: "text", nullable: false),
                    geometry = table.Column<Geometry>(type: "geometry", nullable: false),
                    version_id = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buildings", x => x.persistent_uri);
                });

            migrationBuilder.CreateTable(
                name: "buildingunit_addresses",
                schema: "changefeed",
                columns: table => new
                {
                    buildingunit_persistent_local_id = table.Column<int>(type: "integer", nullable: false),
                    address_persistent_local_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buildingunit_addresses", x => new { x.buildingunit_persistent_local_id, x.address_persistent_local_id });
                });

            migrationBuilder.CreateTable(
                name: "buildingunits",
                schema: "changefeed",
                columns: table => new
                {
                    persistent_uri = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    persistent_local_id = table.Column<int>(type: "integer", nullable: false),
                    building_persistent_local_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<Geometry>(type: "geometry", nullable: false),
                    geometry_method = table.Column<string>(type: "text", nullable: false),
                    function = table.Column<string>(type: "text", nullable: false),
                    has_deviation = table.Column<bool>(type: "boolean", nullable: false),
                    version_id = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buildingunits", x => x.persistent_uri);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buildings_geometry",
                schema: "changefeed",
                table: "buildings",
                column: "geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_buildings_is_removed",
                schema: "changefeed",
                table: "buildings",
                column: "is_removed");

            migrationBuilder.CreateIndex(
                name: "IX_buildings_persistent_local_id",
                schema: "changefeed",
                table: "buildings",
                column: "persistent_local_id");

            migrationBuilder.CreateIndex(
                name: "IX_buildings_status",
                schema: "changefeed",
                table: "buildings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_buildingunit_addresses_address_persistent_local_id",
                schema: "changefeed",
                table: "buildingunit_addresses",
                column: "address_persistent_local_id");

            migrationBuilder.CreateIndex(
                name: "IX_buildingunit_addresses_buildingunit_persistent_local_id",
                schema: "changefeed",
                table: "buildingunit_addresses",
                column: "buildingunit_persistent_local_id");

            migrationBuilder.CreateIndex(
                name: "IX_buildingunits_building_persistent_local_id",
                schema: "changefeed",
                table: "buildingunits",
                column: "building_persistent_local_id");

            migrationBuilder.CreateIndex(
                name: "IX_buildingunits_is_removed",
                schema: "changefeed",
                table: "buildingunits",
                column: "is_removed");

            migrationBuilder.CreateIndex(
                name: "IX_buildingunits_persistent_local_id",
                schema: "changefeed",
                table: "buildingunits",
                column: "persistent_local_id");

            migrationBuilder.CreateIndex(
                name: "IX_buildingunits_position",
                schema: "changefeed",
                table: "buildingunits",
                column: "position")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_buildingunits_status",
                schema: "changefeed",
                table: "buildingunits",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buildings",
                schema: "changefeed");

            migrationBuilder.DropTable(
                name: "buildingunit_addresses",
                schema: "changefeed");

            migrationBuilder.DropTable(
                name: "buildingunits",
                schema: "changefeed");
        }
    }
}
