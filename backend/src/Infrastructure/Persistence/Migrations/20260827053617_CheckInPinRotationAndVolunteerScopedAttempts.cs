using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class CheckInPinRotationAndVolunteerScopedAttempts : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropPrimaryKey(
				name: "pk_check_in_attempt",
				table: "check_in_attempt");

			migrationBuilder.RenameColumn(
				name: "engagement_id",
				table: "check_in_attempt",
				newName: "opportunity_id");

			migrationBuilder.AddColumn<Guid>(
				name: "check_in_pin_time_slot_id",
				table: "volunteer_opportunity",
				type: "uuid",
				nullable: true);

			migrationBuilder.AddColumn<Guid>(
				name: "volunteer_id",
				table: "check_in_attempt",
				type: "uuid",
				nullable: false,
				defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

			migrationBuilder.AddPrimaryKey(
				name: "pk_check_in_attempt",
				table: "check_in_attempt",
				columns: new[] { "volunteer_id", "opportunity_id" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropPrimaryKey(
				name: "pk_check_in_attempt",
				table: "check_in_attempt");

			migrationBuilder.DropColumn(
				name: "check_in_pin_time_slot_id",
				table: "volunteer_opportunity");

			migrationBuilder.DropColumn(
				name: "volunteer_id",
				table: "check_in_attempt");

			migrationBuilder.RenameColumn(
				name: "opportunity_id",
				table: "check_in_attempt",
				newName: "engagement_id");

			migrationBuilder.AddPrimaryKey(
				name: "pk_check_in_attempt",
				table: "check_in_attempt",
				column: "engagement_id");
		}
	}
}
