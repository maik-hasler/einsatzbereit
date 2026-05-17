using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddCategoryAndTags : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "category",
				table: "volunteer_opportunity",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<List<string>>(
				name: "tags",
				table: "volunteer_opportunity",
				type: "text[]",
				nullable: false);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "category",
				table: "volunteer_opportunity");

			migrationBuilder.DropColumn(
				name: "tags",
				table: "volunteer_opportunity");
		}
	}
}
