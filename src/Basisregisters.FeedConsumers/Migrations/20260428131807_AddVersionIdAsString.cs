using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Basisregisters.FeedConsumers.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionIdAsString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "streetnames",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "postal_information",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "parcels",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "municipalities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "buildingunits",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "buildings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "addresses",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "streetnames");

            migrationBuilder.DropColumn(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "postal_information");

            migrationBuilder.DropColumn(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "parcels");

            migrationBuilder.DropColumn(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "municipalities");

            migrationBuilder.DropColumn(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "buildingunits");

            migrationBuilder.DropColumn(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "buildings");

            migrationBuilder.DropColumn(
                name: "version_id_as_string",
                schema: "changefeed",
                table: "addresses");
        }
    }
}
