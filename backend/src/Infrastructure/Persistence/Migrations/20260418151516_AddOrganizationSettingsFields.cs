using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationSettingsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address_city",
                table: "organization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_house_number",
                table: "organization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_street",
                table: "organization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_zip_code",
                table: "organization",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                table: "organization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "organization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "organization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website",
                table: "organization",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address_city",
                table: "organization");

            migrationBuilder.DropColumn(
                name: "address_house_number",
                table: "organization");

            migrationBuilder.DropColumn(
                name: "address_street",
                table: "organization");

            migrationBuilder.DropColumn(
                name: "address_zip_code",
                table: "organization");

            migrationBuilder.DropColumn(
                name: "contact_email",
                table: "organization");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                table: "organization");

            migrationBuilder.DropColumn(
                name: "description",
                table: "organization");

            migrationBuilder.DropColumn(
                name: "website",
                table: "organization");
        }
    }
}
