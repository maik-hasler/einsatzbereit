using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddOpportunityCoordinates : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<double>(
				name: "address_latitude",
				table: "volunteer_opportunity",
				type: "double precision",
				nullable: true);

			migrationBuilder.AddColumn<double>(
				name: "address_longitude",
				table: "volunteer_opportunity",
				type: "double precision",
				nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "address_latitude",
				table: "volunteer_opportunity");

			migrationBuilder.DropColumn(
				name: "address_longitude",
				table: "volunteer_opportunity");
		}
	}
}
