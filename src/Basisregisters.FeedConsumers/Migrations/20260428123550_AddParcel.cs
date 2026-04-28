using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Basisregisters.FeedConsumers.Migrations
{
    /// <inheritdoc />
    public partial class AddParcel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parcel_addresses",
                schema: "changefeed",
                columns: table => new
                {
                    vbr_capakey = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    address_persistent_local_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_addresses", x => new { x.vbr_capakey, x.address_persistent_local_id });
                });

            migrationBuilder.CreateTable(
                name: "parcels",
                schema: "changefeed",
                columns: table => new
                {
                    persistent_uri = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    vbr_capakey = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    capakey = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    version_id = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcels", x => x.persistent_uri);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parcel_addresses_address_persistent_local_id",
                schema: "changefeed",
                table: "parcel_addresses",
                column: "address_persistent_local_id");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_addresses_vbr_capakey",
                schema: "changefeed",
                table: "parcel_addresses",
                column: "vbr_capakey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parcel_addresses",
                schema: "changefeed");

            migrationBuilder.DropTable(
                name: "parcels",
                schema: "changefeed");
        }
    }
}
