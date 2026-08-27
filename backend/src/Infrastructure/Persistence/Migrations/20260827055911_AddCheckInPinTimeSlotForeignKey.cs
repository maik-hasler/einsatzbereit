using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddCheckInPinTimeSlotForeignKey : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateIndex(
				name: "ix_volunteer_opportunity_check_in_pin_time_slot_id",
				table: "volunteer_opportunity",
				column: "check_in_pin_time_slot_id");

			migrationBuilder.AddForeignKey(
				name: "fk_volunteer_opportunity_time_slot_check_in_pin_time_slot_id",
				table: "volunteer_opportunity",
				column: "check_in_pin_time_slot_id",
				principalTable: "time_slot",
				principalColumn: "id",
				onDelete: ReferentialAction.SetNull);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "fk_volunteer_opportunity_time_slot_check_in_pin_time_slot_id",
				table: "volunteer_opportunity");

			migrationBuilder.DropIndex(
				name: "ix_volunteer_opportunity_check_in_pin_time_slot_id",
				table: "volunteer_opportunity");
		}
	}
}
