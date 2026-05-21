using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddUserStreaks : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "user_streak",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					login_streak = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
					last_login_date = table.Column<DateOnly>(type: "date", nullable: true),
					activity_streak = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
					last_active_iso_week = table.Column<int>(type: "integer", nullable: true),
					last_active_iso_year = table.Column<int>(type: "integer", nullable: true),
					created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_user_streak", x => x.id);
				});

			migrationBuilder.CreateIndex(
				name: "ix_user_streak_user_id",
				table: "user_streak",
				column: "user_id",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "user_streak");
		}
	}
}
