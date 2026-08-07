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

			// einsatzbereit#1725: backfill read_on for rows that were already read
			// before this column existed. CreatedOn is the closest available proxy
			// for "when it was read" (matches the pre-fix retention behavior for
			// these rows exactly, rather than either deleting them immediately or
			// granting them an indefinite extension).
			migrationBuilder.Sql("UPDATE notification SET read_on = created_on WHERE is_read = true;");

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
