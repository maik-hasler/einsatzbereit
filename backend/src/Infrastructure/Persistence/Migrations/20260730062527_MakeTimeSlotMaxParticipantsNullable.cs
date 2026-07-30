using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class MakeTimeSlotMaxParticipantsNullable : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AlterColumn<int>(
				name: "max_participants",
				table: "time_slot",
				type: "integer",
				nullable: true,
				oldClrType: typeof(int),
				oldType: "integer");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AlterColumn<int>(
				name: "max_participants",
				table: "time_slot",
				type: "integer",
				nullable: false,
				defaultValue: 0,
				oldClrType: typeof(int),
				oldType: "integer",
				oldNullable: true);
		}
	}
}
