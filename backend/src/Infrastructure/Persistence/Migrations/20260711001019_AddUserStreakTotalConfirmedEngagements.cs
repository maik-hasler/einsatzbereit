using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddUserStreakTotalConfirmedEngagements : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
				name: "total_confirmed_engagements",
				table: "user_streak",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			// Backfill: seed the new monotonic counter from each volunteer's current
			// live-confirmed count, so anyone already close to (or past) a milestone
			// threshold under the old buggy exact-match logic isn't set back to zero.
			// A user_streak row exists for every volunteer with a confirmed engagement,
			// since confirming always creates one if missing (see
			// ConfirmEngagementCommandHandler).
			migrationBuilder.Sql("""
				UPDATE user_streak us
				SET total_confirmed_engagements = sub.confirmed_count
				FROM (
					SELECT volunteer_id, COUNT(*) AS confirmed_count
					FROM engagement
					WHERE status = 'Confirmed'
					GROUP BY volunteer_id
				) sub
				WHERE us.user_id = sub.volunteer_id
				AND sub.confirmed_count > us.total_confirmed_engagements;
				""");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "total_confirmed_engagements",
				table: "user_streak");
		}
	}
}
