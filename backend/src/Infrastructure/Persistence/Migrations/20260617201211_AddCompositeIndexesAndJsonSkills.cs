using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddCompositeIndexesAndJsonSkills : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Convert pipe-delimited skills/languages to JSON arrays.
			// The LIKE '[%' guard makes this idempotent if run more than once.
			migrationBuilder.Sql("""
				UPDATE "user"
				SET skills = CASE
					WHEN skills IS NULL OR skills = '' THEN '[]'
					WHEN skills LIKE '[%' THEN skills
					ELSE array_to_json(string_to_array(skills, '|'))::text
				END,
				languages = CASE
					WHEN languages IS NULL OR languages = '' THEN '[]'
					WHEN languages LIKE '[%' THEN languages
					ELSE array_to_json(string_to_array(languages, '|'))::text
				END;
				""");

			migrationBuilder.CreateIndex(
				name: "ix_notification_recipient_id_created_on",
				table: "notification",
				columns: new[] { "recipient_id", "created_on" });

			migrationBuilder.CreateIndex(
				name: "ix_notification_recipient_id_is_read",
				table: "notification",
				columns: new[] { "recipient_id", "is_read" });

			migrationBuilder.CreateIndex(
				name: "ix_engagement_opportunity_id_status",
				table: "engagement",
				columns: new[] { "opportunity_id", "status" });

			migrationBuilder.CreateIndex(
				name: "ix_engagement_volunteer_id_status",
				table: "engagement",
				columns: new[] { "volunteer_id", "status" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_notification_recipient_id_created_on",
				table: "notification");

			migrationBuilder.DropIndex(
				name: "ix_notification_recipient_id_is_read",
				table: "notification");

			migrationBuilder.DropIndex(
				name: "ix_engagement_opportunity_id_status",
				table: "engagement");

			migrationBuilder.DropIndex(
				name: "ix_engagement_volunteer_id_status",
				table: "engagement");
		}
	}
}
