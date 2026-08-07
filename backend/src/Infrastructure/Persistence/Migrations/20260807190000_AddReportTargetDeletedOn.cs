using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddReportTargetDeletedOn : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "target_deleted_on",
				table: "report",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "ix_report_target_deleted_on",
				table: "report",
				column: "target_deleted_on");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_report_target_deleted_on",
				table: "report");

			migrationBuilder.DropColumn(
				name: "target_deleted_on",
				table: "report");
		}
	}
}
