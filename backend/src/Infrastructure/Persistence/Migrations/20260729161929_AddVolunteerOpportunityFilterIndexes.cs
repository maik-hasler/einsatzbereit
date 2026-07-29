using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddVolunteerOpportunityFilterIndexes : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AlterDatabase()
				.Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

			migrationBuilder.AddColumn<string>(
				name: "address_city_normalized",
				table: "volunteer_opportunity",
				type: "text",
				nullable: true,
				computedColumnSql: "lower(address_city)",
				stored: true);

			migrationBuilder.CreateIndex(
				name: "ix_volunteer_opportunity_address_latitude_address_longitude",
				table: "volunteer_opportunity",
				columns: new[] { "address_latitude", "address_longitude" });

			migrationBuilder.CreateIndex(
				name: "ix_volunteer_opportunity_city_normalized_trgm",
				table: "volunteer_opportunity",
				column: "address_city_normalized")
				.Annotation("Npgsql:IndexMethod", "gin")
				.Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

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
				name: "ix_volunteer_opportunity_address_latitude_address_longitude",
				table: "volunteer_opportunity");

			migrationBuilder.DropIndex(
				name: "ix_volunteer_opportunity_city_normalized_trgm",
				table: "volunteer_opportunity");

			migrationBuilder.DropIndex(
				name: "ix_volunteer_opportunity_status_created_on",
				table: "volunteer_opportunity");

			migrationBuilder.DropIndex(
				name: "ix_volunteer_opportunity_tags",
				table: "volunteer_opportunity");

			migrationBuilder.DropColumn(
				name: "address_city_normalized",
				table: "volunteer_opportunity");

			migrationBuilder.AlterDatabase()
				.OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
		}
	}
}
