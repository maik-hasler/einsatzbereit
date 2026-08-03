using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	// A single, deliberately multi-part migration for the 2026-07-25 1.0
	// readiness data-model audit (lens:data-model): stale invitation name
	// snapshots, dead organization coordinate columns, missing hot-path
	// indexes, an achievement Key backfill, optimistic concurrency tokens,
	// and the organization-graph foreign keys the schema never had. Each
	// section below is commented with which issue it's for - see those
	// issues for the full rationale rather than duplicating it here.
	public partial class ApplyDataModelHardening : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_achievement_user_id_name",
				table: "achievement");

			migrationBuilder.DropColumn(
				name: "invitee_name",
				table: "organization_invitation");

			migrationBuilder.DropColumn(
				name: "organization_name",
				table: "organization_invitation");

			migrationBuilder.DropColumn(
				name: "address_latitude",
				table: "organization");

			migrationBuilder.DropColumn(
				name: "address_longitude",
				table: "organization");

			migrationBuilder.AddColumn<uint>(
				name: "xmin",
				table: "volunteer_opportunity",
				type: "xid",
				rowVersion: true,
				nullable: false,
				defaultValue: 0u);

			migrationBuilder.AddColumn<uint>(
				name: "xmin",
				table: "organization_invitation",
				type: "xid",
				rowVersion: true,
				nullable: false,
				defaultValue: 0u);

			migrationBuilder.AddColumn<uint>(
				name: "xmin",
				table: "organization",
				type: "xid",
				rowVersion: true,
				nullable: false,
				defaultValue: 0u);

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "time_slot_end_date_time",
				table: "engagement",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "time_slot_start_date_time",
				table: "engagement",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<uint>(
				name: "xmin",
				table: "engagement",
				type: "xid",
				rowVersion: true,
				nullable: false,
				defaultValue: 0u);

			// #1198: backfill pre-existing NULL keys from the badge catalog's
			// Name -> Key mapping (appsettings.json's Achievements:Badges) before
			// the column is made NOT NULL below - both "Century" (the current
			// catalog Name) and "Centurion" are mapped in case that badge was
			// already renamed, matching the exact scenario the issue describes.
			migrationBuilder.Sql(@"
				UPDATE achievement SET key = CASE name
					WHEN 'First Step' THEN 'first-step'
					WHEN 'Dedicated' THEN 'dedicated-5'
					WHEN 'Century' THEN 'centurion-100'
					WHEN 'Centurion' THEN 'centurion-100'
					WHEN 'On a Roll' THEN 'on-a-roll-7'
					WHEN 'Weekly Hero' THEN 'weekly-hero-4'
					WHEN 'Early Adopter' THEN 'early-adopter'
					ELSE lower(regexp_replace(trim(name), '[^a-zA-Z0-9]+', '-', 'g'))
				END
				WHERE key IS NULL;");

			migrationBuilder.AlterColumn<string>(
				name: "key",
				table: "achievement",
				type: "character varying(100)",
				maxLength: 100,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(100)",
				oldMaxLength: 100,
				oldNullable: true);

			migrationBuilder.CreateIndex(
				name: "ix_volunteer_opportunity_address_latitude_address_longitude",
				table: "volunteer_opportunity",
				columns: new[] { "address_latitude", "address_longitude" });

			migrationBuilder.CreateIndex(
				name: "ix_time_slot_end_date_time",
				table: "time_slot",
				column: "end_date_time");

			migrationBuilder.CreateIndex(
				name: "ix_outbox_message_occurred_on_utc",
				table: "outbox_message",
				column: "occurred_on_utc",
				filter: "processed_on_utc IS NULL");

			migrationBuilder.CreateIndex(
				name: "ix_organization_invitation_organization_id_invitee_id",
				table: "organization_invitation",
				columns: new[] { "organization_id", "invitee_id" },
				unique: true,
				filter: "status = 'Pending'");

			migrationBuilder.CreateIndex(
				name: "ix_organization_name",
				table: "organization",
				column: "name");

			migrationBuilder.CreateIndex(
				name: "ix_achievement_user_id_key",
				table: "achievement",
				columns: new[] { "user_id", "key" },
				unique: true);

			// #1200: supports the city filter's "lower(address_city) LIKE
			// '%x%'" - a leading wildcard no plain btree index can serve. No
			// EF Core Fluent API for an expression + gin_trgm_ops index, so
			// this is raw SQL rather than a CreateIndex call above.
			migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
			migrationBuilder.Sql(@"
				CREATE INDEX IF NOT EXISTS ix_volunteer_opportunity_address_city_trgm
				ON volunteer_opportunity USING gin (lower(address_city) gin_trgm_ops);");

			// #1191: clean up any existing orphans before constraining - an org
			// whose blocking-opportunity check happened to pass (only draft/past
			// opportunities, per GetBlockingOpportunitiesForOrganizationAsync)
			// could previously be deleted while still-unconstrained
			// volunteer_opportunity/membership/invitation/dashboard-layout rows
			// pointed at it. Idempotent - a no-op once no orphans remain.
			migrationBuilder.Sql(
				"DELETE FROM organization_membership WHERE organization_id NOT IN (SELECT id FROM organization);");
			migrationBuilder.Sql(
				"DELETE FROM organization_dashboard_layout WHERE organization_id NOT IN (SELECT id FROM organization);");
			migrationBuilder.Sql(
				"DELETE FROM organization_invitation WHERE organization_id NOT IN (SELECT id FROM organization);");
			// Cascades to time_slot (existing FK) and sets engagement.time_slot_id
			// to NULL (existing FK) automatically - the orphaned opportunity's
			// engagements are left alone, same as any other deleted-opportunity
			// engagement history (#667, #1203).
			migrationBuilder.Sql(
				"DELETE FROM volunteer_opportunity WHERE organization_id NOT IN (SELECT id FROM organization);");

			migrationBuilder.AddForeignKey(
				name: "fk_organization_dashboard_layout_organization_organization_id",
				table: "organization_dashboard_layout",
				column: "organization_id",
				principalTable: "organization",
				principalColumn: "id",
				onDelete: ReferentialAction.Cascade);

			migrationBuilder.AddForeignKey(
				name: "fk_organization_invitation_organization_organization_id",
				table: "organization_invitation",
				column: "organization_id",
				principalTable: "organization",
				principalColumn: "id",
				onDelete: ReferentialAction.Cascade);

			migrationBuilder.AddForeignKey(
				name: "fk_organization_membership_organization_organization_id",
				table: "organization_membership",
				column: "organization_id",
				principalTable: "organization",
				principalColumn: "id",
				onDelete: ReferentialAction.Cascade);

			migrationBuilder.AddForeignKey(
				name: "fk_volunteer_opportunity_organization_organization_id",
				table: "volunteer_opportunity",
				column: "organization_id",
				principalTable: "organization",
				principalColumn: "id",
				onDelete: ReferentialAction.Cascade);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "fk_organization_dashboard_layout_organization_organization_id",
				table: "organization_dashboard_layout");

			migrationBuilder.DropForeignKey(
				name: "fk_organization_invitation_organization_organization_id",
				table: "organization_invitation");

			migrationBuilder.DropForeignKey(
				name: "fk_organization_membership_organization_organization_id",
				table: "organization_membership");

			migrationBuilder.DropForeignKey(
				name: "fk_volunteer_opportunity_organization_organization_id",
				table: "volunteer_opportunity");

			migrationBuilder.Sql("DROP INDEX IF EXISTS ix_volunteer_opportunity_address_city_trgm;");

			migrationBuilder.DropIndex(
				name: "ix_volunteer_opportunity_address_latitude_address_longitude",
				table: "volunteer_opportunity");

			migrationBuilder.DropIndex(
				name: "ix_time_slot_end_date_time",
				table: "time_slot");

			migrationBuilder.DropIndex(
				name: "ix_outbox_message_occurred_on_utc",
				table: "outbox_message");

			migrationBuilder.DropIndex(
				name: "ix_organization_invitation_organization_id_invitee_id",
				table: "organization_invitation");

			migrationBuilder.DropIndex(
				name: "ix_organization_name",
				table: "organization");

			migrationBuilder.DropIndex(
				name: "ix_achievement_user_id_key",
				table: "achievement");

			migrationBuilder.DropColumn(
				name: "xmin",
				table: "volunteer_opportunity");

			migrationBuilder.DropColumn(
				name: "xmin",
				table: "organization_invitation");

			migrationBuilder.DropColumn(
				name: "xmin",
				table: "organization");

			migrationBuilder.DropColumn(
				name: "time_slot_end_date_time",
				table: "engagement");

			migrationBuilder.DropColumn(
				name: "time_slot_start_date_time",
				table: "engagement");

			migrationBuilder.DropColumn(
				name: "xmin",
				table: "engagement");

			migrationBuilder.AddColumn<string>(
				name: "invitee_name",
				table: "organization_invitation",
				type: "text",
				nullable: false,
				defaultValue: "");

			migrationBuilder.AddColumn<string>(
				name: "organization_name",
				table: "organization_invitation",
				type: "text",
				nullable: false,
				defaultValue: "");

			migrationBuilder.AddColumn<double>(
				name: "address_latitude",
				table: "organization",
				type: "double precision",
				nullable: true);

			migrationBuilder.AddColumn<double>(
				name: "address_longitude",
				table: "organization",
				type: "double precision",
				nullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "key",
				table: "achievement",
				type: "character varying(100)",
				maxLength: 100,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(100)",
				oldMaxLength: 100);

			migrationBuilder.CreateIndex(
				name: "ix_achievement_user_id_name",
				table: "achievement",
				columns: new[] { "user_id", "name" },
				unique: true);
		}
	}
}
