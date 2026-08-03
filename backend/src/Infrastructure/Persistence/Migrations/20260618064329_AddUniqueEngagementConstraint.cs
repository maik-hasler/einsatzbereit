using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	// #1195: this dedupe keeps the row with the lowest id, and every id is
	// Guid.CreateVersion7() (time-ordered), so it keeps the *earliest* row per
	// (volunteer_id, opportunity_id) regardless of status. Duplicates only
	// existed because HasEngagementAsync excludes Withdrawn/Cancelled, so the
	// classic pair was [old Withdrawn, new Confirmed] - meaning this could have
	// deleted a live Confirmed engagement and kept a dead Withdrawn one on any
	// database that still had such a pair when it ran. Left as-is rather than
	// rewritten: this migration already ran (including on staging), and EF
	// Core migrations are not meant to be edited after being applied - editing
	// the file wouldn't undo whatever it already did, only make the on-disk
	// history misleading about what actually executed. Nothing before or since
	// has needed a second run: the unique index this migration adds now stops
	// duplicate pairs like this from being created in the first place, and
	// this project has no real deployment to have inherited pre-existing
	// duplicates into (see root AGENTS.md's Test Users note - staging is
	// disposable demo/QA infrastructure, not a production database that was
	// ever migrated through this). A real deployment upgrading through this
	// migration with genuine pre-existing duplicates would need a status-aware
	// dedupe instead (ORDER BY status priority, id DESC) plus an archive of the
	// losing rows rather than an unconditional delete.
	public partial class AddUniqueEngagementConstraint : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"DELETE FROM engagement " +
				"WHERE id IN (" +
				"SELECT id FROM (" +
				"SELECT id, ROW_NUMBER() OVER (PARTITION BY volunteer_id, opportunity_id ORDER BY id) AS rn " +
				"FROM engagement" +
				") AS duplicates " +
				"WHERE rn > 1" +
				");");

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
