using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddReportDismissedAuditActionType : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_audit_log_action_type_valid",
				table: "audit_log");

			migrationBuilder.AddCheckConstraint(
				name: "ck_audit_log_action_type_valid",
				table: "audit_log",
				sql: "action_type IN ('UserPromotedToAdmin', 'UserDemotedFromAdmin', 'UserEnabled', 'UserDisabled', 'UserShadowDeleted', 'UserRestored', 'OrganizationShadowDeleted', 'OrganizationRestored', 'VolunteerOpportunityShadowDeleted', 'VolunteerOpportunityRestored', 'EngagementCancelled', 'ReportDismissed')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_audit_log_action_type_valid",
				table: "audit_log");

			migrationBuilder.AddCheckConstraint(
				name: "ck_audit_log_action_type_valid",
				table: "audit_log",
				sql: "action_type IN ('UserPromotedToAdmin', 'UserDemotedFromAdmin', 'UserEnabled', 'UserDisabled', 'UserShadowDeleted', 'UserRestored', 'OrganizationShadowDeleted', 'OrganizationRestored', 'VolunteerOpportunityShadowDeleted', 'VolunteerOpportunityRestored', 'EngagementCancelled')");
		}
	}
}
