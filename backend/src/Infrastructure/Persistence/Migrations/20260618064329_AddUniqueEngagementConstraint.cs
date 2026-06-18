using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddUniqueEngagementConstraint : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(@"
DELETE FROM engagement
WHERE id IN (
    SELECT id FROM (
        SELECT id,
               ROW_NUMBER() OVER (PARTITION BY volunteer_id, opportunity_id ORDER BY id) AS rn
        FROM engagement
    ) AS duplicates
    WHERE rn > 1
);
");

			migrationBuilder.CreateIndex(
				name: "ix_engagement_volunteer_id_opportunity_id",
				table: "engagement",
				columns: new[] { "volunteer_id", "opportunity_id" },
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_engagement_volunteer_id_opportunity_id",
				table: "engagement");
		}
	}
}
