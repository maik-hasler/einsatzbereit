using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddAchievements : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "achievement",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					type = table.Column<string>(type: "text", nullable: false),
					name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
					unlocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_achievement", x => x.id);
				});

			migrationBuilder.CreateIndex(
				name: "ix_achievement_user_id",
				table: "achievement",
				column: "user_id");

			migrationBuilder.CreateIndex(
				name: "ix_achievement_user_id_type",
				table: "achievement",
				columns: new[] { "user_id", "type" },
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "achievement");
		}
	}
}
