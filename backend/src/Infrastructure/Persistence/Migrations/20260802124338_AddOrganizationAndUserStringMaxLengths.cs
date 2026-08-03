using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddOrganizationAndUserStringMaxLengths : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// #1194: idempotent pre-check so a pre-existing overlong value can't
			// make an AlterColumn below fail and crash-loop the backend on every
			// startup (Database__MigrateOnStartup) - see the identical rationale
			// in AddVolunteerOpportunityTitleDescriptionMaxLength.
			migrationBuilder.Sql(
				"UPDATE \"user\" SET phone = left(phone, 30) WHERE length(phone) > 30;");
			migrationBuilder.Sql(
				"UPDATE \"user\" SET bio = left(bio, 1000) WHERE length(bio) > 1000;");
			migrationBuilder.Sql(
				"UPDATE organization SET website = left(website, 500) WHERE length(website) > 500;");
			migrationBuilder.Sql(
				"UPDATE organization SET name = left(name, 100) WHERE length(name) > 100;");
			migrationBuilder.Sql(
				"UPDATE organization SET description = left(description, 1000) WHERE length(description) > 1000;");
			migrationBuilder.Sql(
				"UPDATE organization SET contact_phone = left(contact_phone, 30) WHERE length(contact_phone) > 30;");
			migrationBuilder.Sql(
				"UPDATE organization SET contact_email = left(contact_email, 254) WHERE length(contact_email) > 254;");
			migrationBuilder.Sql(
				"UPDATE organization SET address_street = left(address_street, 200) WHERE length(address_street) > 200;");
			migrationBuilder.Sql(
				"UPDATE organization SET address_house_number = left(address_house_number, 20) WHERE length(address_house_number) > 20;");
			migrationBuilder.Sql(
				"UPDATE organization SET address_city = left(address_city, 100) WHERE length(address_city) > 100;");

			migrationBuilder.AlterColumn<string>(
				name: "phone",
				table: "user",
				type: "character varying(30)",
				maxLength: 30,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "text",
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "bio",
				table: "user",
				type: "character varying(1000)",
				maxLength: 1000,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "text",
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "website",
				table: "organization",
				type: "character varying(500)",
				maxLength: 500,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "text",
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "name",
				table: "organization",
				type: "character varying(100)",
				maxLength: 100,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "text");

			migrationBuilder.AlterColumn<string>(
				name: "description",
				table: "organization",
				type: "character varying(1000)",
				maxLength: 1000,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "text",
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "contact_phone",
				table: "organization",
				type: "character varying(30)",
				maxLength: 30,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "text",
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "contact_email",
				table: "organization",
				type: "character varying(254)",
				maxLength: 254,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "text",
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "address_street",
				table: "organization",
				type: "character varying(200)",
				maxLength: 200,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "text",
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "address_house_number",
				table: "organization",
				type: "character varying(20)",
				maxLength: 20,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "text",
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "address_city",
				table: "organization",
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
				name: "phone",
				table: "user",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(30)",
				oldMaxLength: 30,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "bio",
				table: "user",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(1000)",
				oldMaxLength: 1000,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "website",
				table: "organization",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(500)",
				oldMaxLength: 500,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "name",
				table: "organization",
				type: "text",
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(100)",
				oldMaxLength: 100);

			migrationBuilder.AlterColumn<string>(
				name: "description",
				table: "organization",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(1000)",
				oldMaxLength: 1000,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "contact_phone",
				table: "organization",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(30)",
				oldMaxLength: 30,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "contact_email",
				table: "organization",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(254)",
				oldMaxLength: 254,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "address_street",
				table: "organization",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(200)",
				oldMaxLength: 200,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "address_house_number",
				table: "organization",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(20)",
				oldMaxLength: 20,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "address_city",
				table: "organization",
				type: "text",
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(100)",
				oldMaxLength: 100,
				oldNullable: true);
		}
	}
}
