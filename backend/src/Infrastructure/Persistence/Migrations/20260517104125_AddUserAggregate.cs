using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddUserAggregate : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "user",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					bio = table.Column<string>(type: "text", nullable: true),
					skills = table.Column<string>(type: "text", nullable: false),
					languages = table.Column<string>(type: "text", nullable: false),
					preferred_contact = table.Column<string>(type: "text", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_user", x => x.id);
				});
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "user");
		}
	}
}
