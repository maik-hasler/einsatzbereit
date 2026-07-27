using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddReports : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "report",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					target_type = table.Column<string>(type: "text", nullable: false),
					target_id = table.Column<Guid>(type: "uuid", nullable: false),
					reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
					reason = table.Column<string>(type: "text", nullable: false),
					details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
					status = table.Column<string>(type: "text", nullable: false),
					resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
					resolved_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_report", x => x.id);
				});

			migrationBuilder.CreateIndex(
				name: "ix_report_status",
				table: "report",
				column: "status");

			migrationBuilder.CreateIndex(
				name: "ix_report_target_type_target_id",
				table: "report",
				columns: new[] { "target_type", "target_id" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "report");
		}
	}
}
