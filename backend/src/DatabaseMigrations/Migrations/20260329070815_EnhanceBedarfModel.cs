using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrations.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceBedarfModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "adresse_hausnummer",
                table: "bedarfe",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "adresse_ort",
                table: "bedarfe",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "adresse_plz",
                table: "bedarfe",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "adresse_strasse",
                table: "bedarfe",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "frequenz",
                table: "bedarfe",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "published_on",
                table: "bedarfe",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "adresse_hausnummer",
                table: "bedarfe");

            migrationBuilder.DropColumn(
                name: "adresse_ort",
                table: "bedarfe");

            migrationBuilder.DropColumn(
                name: "adresse_plz",
                table: "bedarfe");

            migrationBuilder.DropColumn(
                name: "adresse_strasse",
                table: "bedarfe");

            migrationBuilder.DropColumn(
                name: "frequenz",
                table: "bedarfe");

            migrationBuilder.DropColumn(
                name: "published_on",
                table: "bedarfe");
        }
    }
}
