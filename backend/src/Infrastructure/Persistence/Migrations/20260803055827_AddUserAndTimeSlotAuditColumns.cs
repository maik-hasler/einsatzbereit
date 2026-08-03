using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddUserAndTimeSlotAuditColumns : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "created_on",
				table: "user",
				type: "timestamp with time zone",
				nullable: false,
				defaultValueSql: "now()");

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "modified_on",
				table: "user",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "created_on",
				table: "time_slot",
				type: "timestamp with time zone",
				nullable: false,
				defaultValueSql: "now()");

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "modified_on",
				table: "time_slot",
				type: "timestamp with time zone",
				nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "created_on",
				table: "user");

			migrationBuilder.DropColumn(
				name: "modified_on",
				table: "user");

			migrationBuilder.DropColumn(
				name: "created_on",
				table: "time_slot");

			migrationBuilder.DropColumn(
				name: "modified_on",
				table: "time_slot");
		}
	}
}
