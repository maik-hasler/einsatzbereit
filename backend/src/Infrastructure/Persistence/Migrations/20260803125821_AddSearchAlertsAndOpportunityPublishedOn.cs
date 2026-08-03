using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddSearchAlertsAndOpportunityPublishedOn : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_notification_kind_valid",
				table: "notification");

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "published_on",
				table: "volunteer_opportunity",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.CreateTable(
				name: "search_alert",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					occurrence = table.Column<string>(type: "text", nullable: true),
					participation_type = table.Column<string>(type: "text", nullable: true),
					is_remote = table.Column<bool>(type: "boolean", nullable: true),
					center_latitude = table.Column<double>(type: "double precision", nullable: true),
					center_longitude = table.Column<double>(type: "double precision", nullable: true),
					radius_km = table.Column<double>(type: "double precision", nullable: true),
					categories = table.Column<string[]>(type: "text[]", nullable: false),
					tag = table.Column<string>(type: "text", nullable: true),
					last_notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_search_alert", x => x.id);
					table.CheckConstraint("ck_search_alert_occurrence_valid", "occurrence IS NULL OR occurrence IN ('OneTime', 'Recurring')");
					table.CheckConstraint("ck_search_alert_participation_type_valid", "participation_type IS NULL OR participation_type IN ('ScheduledSlots', 'IndividualContact')");
				});

			migrationBuilder.CreateIndex(
				name: "ix_volunteer_opportunity_status_published_on",
				table: "volunteer_opportunity",
				columns: new[] { "status", "published_on" });

			migrationBuilder.AddCheckConstraint(
				name: "ck_notification_kind_valid",
				table: "notification",
				sql: "kind IN ('EngagementCreated', 'EngagementConfirmed', 'EngagementCancelled', 'EngagementWithdrawn', 'OpportunityUpdated', 'OpportunityDeleted', 'OpportunityUnpublished', 'OpportunityCancelled', 'InvitationReceived', 'NewMatchingOpportunity')");

			migrationBuilder.CreateIndex(
				name: "ix_search_alert_last_notified_at",
				table: "search_alert",
				column: "last_notified_at");

			migrationBuilder.CreateIndex(
				name: "ix_search_alert_user_id",
				table: "search_alert",
				column: "user_id",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "search_alert");

			migrationBuilder.DropIndex(
				name: "ix_volunteer_opportunity_status_published_on",
				table: "volunteer_opportunity");

			migrationBuilder.DropCheckConstraint(
				name: "ck_notification_kind_valid",
				table: "notification");

			migrationBuilder.DropColumn(
				name: "published_on",
				table: "volunteer_opportunity");

			migrationBuilder.AddCheckConstraint(
				name: "ck_notification_kind_valid",
				table: "notification",
				sql: "kind IN ('EngagementCreated', 'EngagementConfirmed', 'EngagementCancelled', 'EngagementWithdrawn', 'OpportunityUpdated', 'OpportunityDeleted', 'OpportunityUnpublished', 'OpportunityCancelled', 'InvitationReceived')");
		}
	}
}
