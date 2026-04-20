using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Basisregisters.FeedConsumers.Migrations
{
    /// <inheritdoc />
    public partial class AddPostalInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "nis_code",
                schema: "changefeed",
                table: "municipalities",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "postal_information",
                schema: "changefeed",
                columns: table => new
                {
                    persistent_uri = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    postal_code = table.Column<string>(type: "text", nullable: false),
                    nis_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false),
                    version_id = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_postal_information", x => x.persistent_uri);
                    table.UniqueConstraint("AK_postal_information_postal_code", x => x.postal_code);
                });

            migrationBuilder.CreateTable(
                name: "postal_information_name",
                schema: "changefeed",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<int>(type: "integer", nullable: false),
                    postal_code = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_postal_information_name", x => x.id);
                    table.ForeignKey(
                        name: "FK_postal_information_name_postal_information_postal_code",
                        column: x => x.postal_code,
                        principalSchema: "changefeed",
                        principalTable: "postal_information",
                        principalColumn: "postal_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_postal_information_nis_code",
                schema: "changefeed",
                table: "postal_information",
                column: "nis_code");

            migrationBuilder.CreateIndex(
                name: "IX_postal_information_postal_code",
                schema: "changefeed",
                table: "postal_information",
                column: "postal_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_postal_information_name_postal_code",
                schema: "changefeed",
                table: "postal_information_name",
                column: "postal_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "postal_information_name",
                schema: "changefeed");

            migrationBuilder.DropTable(
                name: "postal_information",
                schema: "changefeed");

            migrationBuilder.AlterColumn<string>(
                name: "nis_code",
                schema: "changefeed",
                table: "municipalities",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5);
        }
    }
}
