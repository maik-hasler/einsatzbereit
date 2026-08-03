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
			// #1194: without this, any pre-existing row whose title/description
			// already exceeds the new cap would make the AlterColumn below fail
			// with "value too long for type character varying", and because
			// Database__MigrateOnStartup runs this on every boot, a single such
			// row would put the backend in a permanent migration-retry crash
			// loop. Idempotent (no-ops once every row is already within range),
			// so safe to run again on a database that already passed it.
			migrationBuilder.Sql(
				"UPDATE volunteer_opportunity SET title = left(title, 200) WHERE length(title) > 200;");
			migrationBuilder.Sql(
				"UPDATE volunteer_opportunity SET description = left(description, 5000) WHERE length(description) > 5000;");

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
