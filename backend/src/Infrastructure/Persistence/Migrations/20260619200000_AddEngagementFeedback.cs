using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddEngagementFeedback : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "feedback_comment",
				table: "engagement",
				type: "character varying(500)",
				maxLength: 500,
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "feedback_rating",
				table: "engagement",
				type: "integer",
				nullable: true);

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "feedback_submitted_at",
				table: "engagement",
				type: "timestamp with time zone",
				nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "feedback_comment",
				table: "engagement");

			migrationBuilder.DropColumn(
				name: "feedback_rating",
				table: "engagement");

			migrationBuilder.DropColumn(
				name: "feedback_submitted_at",
				table: "engagement");
		}
	}
}
