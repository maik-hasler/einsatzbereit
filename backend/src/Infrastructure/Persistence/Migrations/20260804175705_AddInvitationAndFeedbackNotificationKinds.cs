using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddInvitationAndFeedbackNotificationKinds : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_notification_kind_valid",
				table: "notification");

			migrationBuilder.AddCheckConstraint(
				name: "ck_notification_kind_valid",
				table: "notification",
				sql: "kind IN ('EngagementCreated', 'EngagementConfirmed', 'EngagementCancelled', 'EngagementWithdrawn', 'OpportunityUpdated', 'OpportunityDeleted', 'OpportunityUnpublished', 'OpportunityCancelled', 'InvitationReceived', 'NewMatchingOpportunity', 'InvitationAccepted', 'InvitationDeclined', 'FeedbackSubmitted')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_notification_kind_valid",
				table: "notification");

			migrationBuilder.AddCheckConstraint(
				name: "ck_notification_kind_valid",
				table: "notification",
				sql: "kind IN ('EngagementCreated', 'EngagementConfirmed', 'EngagementCancelled', 'EngagementWithdrawn', 'OpportunityUpdated', 'OpportunityDeleted', 'OpportunityUnpublished', 'OpportunityCancelled', 'InvitationReceived', 'NewMatchingOpportunity')");
		}
	}
}
