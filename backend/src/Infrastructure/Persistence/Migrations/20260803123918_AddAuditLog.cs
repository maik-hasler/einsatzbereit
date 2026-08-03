using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddAuditLog : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "audit_log",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
					action_type = table.Column<string>(type: "text", nullable: false),
					subject_type = table.Column<string>(type: "text", nullable: false),
					subject_id = table.Column<Guid>(type: "uuid", nullable: false),
					reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
					created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_audit_log", x => x.id);
					table.CheckConstraint("ck_audit_log_action_type_valid", "action_type IN ('UserPromotedToAdmin', 'UserDemotedFromAdmin', 'UserEnabled', 'UserDisabled', 'UserShadowDeleted', 'UserRestored', 'OrganizationShadowDeleted', 'OrganizationRestored', 'VolunteerOpportunityShadowDeleted', 'VolunteerOpportunityRestored', 'EngagementCancelled')");
					table.CheckConstraint("ck_audit_log_subject_type_valid", "subject_type IN ('User', 'Organization', 'VolunteerOpportunity', 'Engagement')");
				});

			migrationBuilder.CreateIndex(
				name: "ix_audit_log_created_on",
				table: "audit_log",
				column: "created_on");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "audit_log");
		}
	}
}
