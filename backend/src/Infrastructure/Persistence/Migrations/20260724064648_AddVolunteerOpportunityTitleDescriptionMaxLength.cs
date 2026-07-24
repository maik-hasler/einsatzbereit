using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddVolunteerOpportunityTitleDescriptionMaxLength : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AlterColumn<string>(
				name: "title",
				table: "volunteer_opportunity",
				type: "character varying(200)",
				maxLength: 200,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "text");

			migrationBuilder.AlterColumn<string>(
				name: "description",
				table: "volunteer_opportunity",
				type: "character varying(5000)",
				maxLength: 5000,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "text");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AlterColumn<string>(
				name: "title",
				table: "volunteer_opportunity",
				type: "text",
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(200)",
				oldMaxLength: 200);

			migrationBuilder.AlterColumn<string>(
				name: "description",
				table: "volunteer_opportunity",
				type: "text",
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(5000)",
				oldMaxLength: 5000);
		}
	}
}
