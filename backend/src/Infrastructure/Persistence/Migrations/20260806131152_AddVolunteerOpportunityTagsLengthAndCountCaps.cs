using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddVolunteerOpportunityTagsLengthAndCountCaps : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// #1678: idempotent pre-check so a pre-existing overlong/over-count value
			// can't make the AlterColumn/AddCheckConstraint below fail and crash-loop
			// the backend on every startup (Database__MigrateOnStartup) - see the
			// identical rationale in AddVolunteerOpportunityAddressLengthCaps.
			migrationBuilder.Sql(
				"UPDATE volunteer_opportunity SET tags = (SELECT array_agg(left(t, 50)) FROM unnest(tags) AS t) WHERE EXISTS (SELECT 1 FROM unnest(tags) AS t WHERE length(t) > 50);");
			migrationBuilder.Sql(
				"UPDATE volunteer_opportunity SET tags = tags[1:20] WHERE cardinality(tags) > 20;");

			migrationBuilder.AlterColumn<string[]>(
				name: "tags",
				table: "volunteer_opportunity",
				type: "character varying(50)[]",
				nullable: false,
				oldClrType: typeof(string[]),
				oldType: "text[]");

			migrationBuilder.AddCheckConstraint(
				name: "ck_volunteer_opportunity_tags_count",
				table: "volunteer_opportunity",
				sql: "cardinality(tags) <= 20");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_volunteer_opportunity_tags_count",
				table: "volunteer_opportunity");

			migrationBuilder.AlterColumn<string[]>(
				name: "tags",
				table: "volunteer_opportunity",
				type: "text[]",
				nullable: false,
				oldClrType: typeof(string[]),
				oldType: "character varying(50)[]");
		}
	}
}
