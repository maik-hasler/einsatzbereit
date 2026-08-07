using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddNotificationReadOn : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "read_on",
				table: "notification",
				type: "timestamp with time zone",
				nullable: true);

			// Existing rows already marked read have no way to recover their true
			// read timestamp, so backfill from created_on - a conservative stand-in
			// that keeps them eligible for pruning under the new ReadOn-based
			// retention rule instead of being retained forever (#1725).
			migrationBuilder.Sql(
				"UPDATE notification SET read_on = created_on WHERE is_read = true AND read_on IS NULL;");

			migrationBuilder.CreateIndex(
				name: "ix_notification_is_read_read_on",
				table: "notification",
				columns: new[] { "is_read", "read_on" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_notification_is_read_read_on",
				table: "notification");

			migrationBuilder.DropColumn(
				name: "read_on",
				table: "notification");
		}
	}
}
