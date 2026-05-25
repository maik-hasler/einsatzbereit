using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class FixAchievementUniqueIndex : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_achievement_user_id_type",
				table: "achievement");

			migrationBuilder.CreateIndex(
				name: "ix_achievement_user_id_name",
				table: "achievement",
				columns: new[] { "user_id", "name" },
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_achievement_user_id_name",
				table: "achievement");

			migrationBuilder.CreateIndex(
				name: "ix_achievement_user_id_type",
				table: "achievement",
				columns: new[] { "user_id", "type" },
				unique: true);
		}
	}
}
