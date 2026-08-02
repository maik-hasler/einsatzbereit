using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddCheckInAttempts : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "check_in_attempt",
				columns: table => new
				{
					engagement_id = table.Column<Guid>(type: "uuid", nullable: false),
					failed_attempts = table.Column<int>(type: "integer", nullable: false),
					locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					last_attempt_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_check_in_attempt", x => x.engagement_id);
				});

			migrationBuilder.CreateIndex(
				name: "ix_check_in_attempt_last_attempt_on",
				table: "check_in_attempt",
				column: "last_attempt_on");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "check_in_attempt");
		}
	}
}
