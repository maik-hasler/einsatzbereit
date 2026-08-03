using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddEnumCheckConstraints : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddCheckConstraint(
				name: "ck_volunteer_opportunity_category_valid",
				table: "volunteer_opportunity",
				sql: "category IN ('Social', 'Environment', 'Sport', 'Education', 'DisasterRelief', 'Health', 'Animals', 'Culture', 'Technology', 'Other')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_volunteer_opportunity_check_in_method_valid",
				table: "volunteer_opportunity",
				sql: "check_in_method IN ('None', 'QRCode', 'PINCode', 'Manual')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_volunteer_opportunity_occurrence_valid",
				table: "volunteer_opportunity",
				sql: "occurrence IN ('OneTime', 'Recurring')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_volunteer_opportunity_participation_type_valid",
				table: "volunteer_opportunity",
				sql: "participation_type IN ('ScheduledSlots', 'IndividualContact')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_volunteer_opportunity_status_valid",
				table: "volunteer_opportunity",
				sql: "status IN ('Draft', 'Published', 'Unpublished', 'Cancelled')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_user_preferred_contact_valid",
				table: "user",
				sql: "preferred_contact IN ('Email', 'Phone')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_reason_valid",
				table: "report",
				sql: "reason IN ('Spam', 'IllegalContent', 'Fraud', 'Harassment', 'Other')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_status_valid",
				table: "report",
				sql: "status IN ('Open', 'Dismissed', 'Actioned')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_target_type_valid",
				table: "report",
				sql: "target_type IN ('VolunteerOpportunity', 'Organization', 'User')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_organization_membership_role_valid",
				table: "organization_membership",
				sql: "role IN ('Member', 'Organizer')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_organization_invitation_intended_role_valid",
				table: "organization_invitation",
				sql: "intended_role IN ('Member', 'Organizer')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_organization_invitation_status_valid",
				table: "organization_invitation",
				sql: "status IN ('Pending', 'Accepted', 'Declined', 'Expired')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_notification_kind_valid",
				table: "notification",
				sql: "kind IN ('EngagementCreated', 'EngagementConfirmed', 'EngagementCancelled', 'EngagementWithdrawn', 'OpportunityUpdated', 'OpportunityDeleted', 'OpportunityUnpublished', 'OpportunityCancelled', 'InvitationReceived')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_engagement_status_valid",
				table: "engagement",
				sql: "status IN ('Pending', 'Confirmed', 'Cancelled', 'Withdrawn')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_achievement_type_valid",
				table: "achievement",
				sql: "type IN ('Milestone', 'Streak', 'Hidden')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_volunteer_opportunity_category_valid",
				table: "volunteer_opportunity");

			migrationBuilder.DropCheckConstraint(
				name: "ck_volunteer_opportunity_check_in_method_valid",
				table: "volunteer_opportunity");

			migrationBuilder.DropCheckConstraint(
				name: "ck_volunteer_opportunity_occurrence_valid",
				table: "volunteer_opportunity");

			migrationBuilder.DropCheckConstraint(
				name: "ck_volunteer_opportunity_participation_type_valid",
				table: "volunteer_opportunity");

			migrationBuilder.DropCheckConstraint(
				name: "ck_volunteer_opportunity_status_valid",
				table: "volunteer_opportunity");

			migrationBuilder.DropCheckConstraint(
				name: "ck_user_preferred_contact_valid",
				table: "user");

			migrationBuilder.DropCheckConstraint(
				name: "ck_report_reason_valid",
				table: "report");

			migrationBuilder.DropCheckConstraint(
				name: "ck_report_status_valid",
				table: "report");

			migrationBuilder.DropCheckConstraint(
				name: "ck_report_target_type_valid",
				table: "report");

			migrationBuilder.DropCheckConstraint(
				name: "ck_organization_membership_role_valid",
				table: "organization_membership");

			migrationBuilder.DropCheckConstraint(
				name: "ck_organization_invitation_intended_role_valid",
				table: "organization_invitation");

			migrationBuilder.DropCheckConstraint(
				name: "ck_organization_invitation_status_valid",
				table: "organization_invitation");

			migrationBuilder.DropCheckConstraint(
				name: "ck_notification_kind_valid",
				table: "notification");

			migrationBuilder.DropCheckConstraint(
				name: "ck_engagement_status_valid",
				table: "engagement");

			migrationBuilder.DropCheckConstraint(
				name: "ck_achievement_type_valid",
				table: "achievement");
		}
	}
}
