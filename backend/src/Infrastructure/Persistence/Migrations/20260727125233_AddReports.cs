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
					content_type = table.Column<string>(type: "text", nullable: false),
					content_id = table.Column<Guid>(type: "uuid", nullable: false),
					reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
					reason = table.Column<string>(type: "text", nullable: false),
					detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
					status = table.Column<string>(type: "text", nullable: false),
					created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_report", x => x.id);
				});

			migrationBuilder.CreateIndex(
				name: "ix_report_content_type_content_id",
				table: "report",
				columns: new[] { "content_type", "content_id" });

			migrationBuilder.CreateIndex(
				name: "ix_report_status",
				table: "report",
				column: "status");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "report");
		}
	}
}
