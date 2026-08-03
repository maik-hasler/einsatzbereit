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
