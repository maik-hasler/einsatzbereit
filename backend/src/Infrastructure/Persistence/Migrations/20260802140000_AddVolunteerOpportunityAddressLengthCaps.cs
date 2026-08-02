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
