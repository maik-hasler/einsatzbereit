using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddPendingOrganizerDigestItem : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "pending_organizer_digest_item",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					organizer_id = table.Column<Guid>(type: "uuid", nullable: false),
					opportunity_title = table.Column<string>(type: "text", nullable: false),
					volunteer_name = table.Column<string>(type: "text", nullable: false),
					kind = table.Column<string>(type: "text", nullable: false),
					occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					claimed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					digest_sent_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_pending_organizer_digest_item", x => x.id);
				});

			migrationBuilder.CreateIndex(
				name: "ix_pending_organizer_digest_item_digest_sent_on_utc",
				table: "pending_organizer_digest_item",
				column: "digest_sent_on_utc");

			migrationBuilder.CreateIndex(
				name: "ix_pending_organizer_digest_item_organizer_id",
				table: "pending_organizer_digest_item",
				column: "organizer_id",
				filter: "digest_sent_on_utc IS NULL");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "pending_organizer_digest_item");
		}
	}
}
