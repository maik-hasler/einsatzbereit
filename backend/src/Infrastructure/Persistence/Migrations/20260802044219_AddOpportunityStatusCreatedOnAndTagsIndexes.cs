using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddOpportunityStatusCreatedOnAndTagsIndexes : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateIndex(
				name: "ix_volunteer_opportunity_status_created_on",
				table: "volunteer_opportunity",
				columns: new[] { "status", "created_on" });

			migrationBuilder.CreateIndex(
				name: "ix_volunteer_opportunity_tags",
				table: "volunteer_opportunity",
				column: "tags")
				.Annotation("Npgsql:IndexMethod", "gin");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_volunteer_opportunity_status_created_on",
				table: "volunteer_opportunity");

			migrationBuilder.DropIndex(
				name: "ix_volunteer_opportunity_tags",
				table: "volunteer_opportunity");
		}
	}
}
