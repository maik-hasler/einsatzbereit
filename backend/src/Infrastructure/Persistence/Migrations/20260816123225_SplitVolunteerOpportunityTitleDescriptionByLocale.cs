using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class SplitVolunteerOpportunityTitleDescriptionByLocale : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.RenameColumn(
				name: "title",
				table: "volunteer_opportunity",
				newName: "title_de");

			migrationBuilder.RenameColumn(
				name: "description",
				table: "volunteer_opportunity",
				newName: "description_de");

			migrationBuilder.AddColumn<string>(
				name: "description_en",
				table: "volunteer_opportunity",
				type: "character varying(5000)",
				maxLength: 5000,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "title_en",
				table: "volunteer_opportunity",
				type: "character varying(200)",
				maxLength: 200,
				nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "description_en",
				table: "volunteer_opportunity");

			migrationBuilder.DropColumn(
				name: "title_en",
				table: "volunteer_opportunity");

			migrationBuilder.RenameColumn(
				name: "title_de",
				table: "volunteer_opportunity",
				newName: "title");

			migrationBuilder.RenameColumn(
				name: "description_de",
				table: "volunteer_opportunity",
				newName: "description");
		}
	}
}
