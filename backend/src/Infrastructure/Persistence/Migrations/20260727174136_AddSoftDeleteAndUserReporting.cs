using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddSoftDeleteAndUserReporting : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "deleted_on",
				table: "volunteer_opportunity",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<bool>(
				name: "is_deleted",
				table: "volunteer_opportunity",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "deleted_on",
				table: "user",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<bool>(
				name: "is_deleted",
				table: "user",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "deleted_on",
				table: "organization",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<bool>(
				name: "is_deleted",
				table: "organization",
				type: "boolean",
				nullable: false,
				defaultValue: false);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "deleted_on",
				table: "volunteer_opportunity");

			migrationBuilder.DropColumn(
				name: "is_deleted",
				table: "volunteer_opportunity");

			migrationBuilder.DropColumn(
				name: "deleted_on",
				table: "user");

			migrationBuilder.DropColumn(
				name: "is_deleted",
				table: "user");

			migrationBuilder.DropColumn(
				name: "deleted_on",
				table: "organization");

			migrationBuilder.DropColumn(
				name: "is_deleted",
				table: "organization");
		}
	}
}
