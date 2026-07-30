using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddTimeSlotSeries : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
				name: "recurrence_count",
				table: "time_slot",
				type: "integer",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "recurrence_frequency",
				table: "time_slot",
				type: "character varying(20)",
				maxLength: 20,
				nullable: true);

			migrationBuilder.AddColumn<Guid>(
				name: "series_id",
				table: "time_slot",
				type: "uuid",
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "ix_time_slot_series_id",
				table: "time_slot",
				column: "series_id");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_time_slot_series_id",
				table: "time_slot");

			migrationBuilder.DropColumn(
				name: "recurrence_count",
				table: "time_slot");

			migrationBuilder.DropColumn(
				name: "recurrence_frequency",
				table: "time_slot");

			migrationBuilder.DropColumn(
				name: "series_id",
				table: "time_slot");
		}
	}
}
