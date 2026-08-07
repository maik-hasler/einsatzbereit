using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddEngagementTimeSlotIdPendingAutoCheckInIndex : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateIndex(
				name: "ix_engagement_time_slot_id_pending_auto_check_in",
				table: "engagement",
				column: "time_slot_id",
				filter: "status = 'Confirmed' AND is_checked_in = false AND time_slot_id IS NOT NULL");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_engagement_time_slot_id_pending_auto_check_in",
				table: "engagement");
		}
	}
}
