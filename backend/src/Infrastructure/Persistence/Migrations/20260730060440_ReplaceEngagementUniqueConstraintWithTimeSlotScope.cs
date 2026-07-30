using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class ReplaceEngagementUniqueConstraintWithTimeSlotScope : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_engagement_volunteer_id_opportunity_id",
				table: "engagement");

			migrationBuilder.CreateIndex(
				name: "ix_engagement_volunteer_id_opportunity_id",
				table: "engagement",
				columns: new[] { "volunteer_id", "opportunity_id" },
				unique: true,
				filter: "time_slot_id IS NULL");

			migrationBuilder.CreateIndex(
				name: "ix_engagement_volunteer_id_time_slot_id",
				table: "engagement",
				columns: new[] { "volunteer_id", "time_slot_id" },
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_engagement_volunteer_id_opportunity_id",
				table: "engagement");

			migrationBuilder.DropIndex(
				name: "ix_engagement_volunteer_id_time_slot_id",
				table: "engagement");

			migrationBuilder.CreateIndex(
				name: "ix_engagement_volunteer_id_opportunity_id",
				table: "engagement",
				columns: new[] { "volunteer_id", "opportunity_id" },
				unique: true);
		}
	}
}
