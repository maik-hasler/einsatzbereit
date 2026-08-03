using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddVolunteerOpportunityAddressLengthCaps : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// #1194: idempotent pre-check so a pre-existing overlong value can't
			// make the AlterColumn below fail and crash-loop the backend on
			// every startup (Database__MigrateOnStartup) - see the identical
			// rationale in AddVolunteerOpportunityTitleDescriptionMaxLength.
			migrationBuilder.Sql(
				"UPDATE volunteer_opportunity SET address_street = left(address_street, 200) WHERE length(address_street) > 200;");
			migrationBuilder.Sql(
				"UPDATE volunteer_opportunity SET address_house_number = left(address_house_number, 20) WHERE length(address_house_number) > 20;");
			migrationBuilder.Sql(
				"UPDATE volunteer_opportunity SET address_city = left(address_city, 100) WHERE length(address_city) > 100;");

			migrationBuilder.AlterColumn<string>(
				name: "address_street",
				table: "volunteer_opportunity",
				type: "character varying(200)",
				maxLength: 200,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "text",
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "address_house_number",
				table: "volunteer_opportunity",
				type: "character varying(20)",
				maxLength: 20,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "text",
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "address_city",
				table: "volunteer_opportunity",
				type: "character varying(100)",
				maxLength: 100,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "text",
				oldNullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AlterColumn<string>(
				name: "address_street",
				table: "volunteer_opportunity",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(200)",
				oldMaxLength: 200,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "address_house_number",
				table: "volunteer_opportunity",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(20)",
				oldMaxLength: 20,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "address_city",
				table: "volunteer_opportunity",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(100)",
				oldMaxLength: 100,
				oldNullable: true);
		}
	}
}
