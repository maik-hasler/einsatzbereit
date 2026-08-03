using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddEngagementFeedbackRatingCheckConstraint : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddCheckConstraint(
				name: "CK_engagement_feedback_rating_range",
				table: "engagement",
				sql: "feedback_rating IS NULL OR feedback_rating BETWEEN 1 AND 5");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "CK_engagement_feedback_rating_range",
				table: "engagement");
		}
	}
}
