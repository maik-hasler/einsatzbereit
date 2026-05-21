using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddCheckInFields : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "check_in_pin",
				table: "volunteer_opportunity",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<bool>(
				name: "is_checked_in",
				table: "engagement",
				type: "boolean",
				nullable: false,
				defaultValue: false);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "check_in_pin",
				table: "volunteer_opportunity");

			migrationBuilder.DropColumn(
				name: "is_checked_in",
				table: "engagement");
		}
	}
}
