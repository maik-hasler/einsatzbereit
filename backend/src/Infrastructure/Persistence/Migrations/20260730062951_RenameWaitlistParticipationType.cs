using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class RenameWaitlistParticipationType : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("UPDATE volunteer_opportunity SET participation_type = 'ScheduledSlots' WHERE participation_type = 'Waitlist';");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("UPDATE volunteer_opportunity SET participation_type = 'Waitlist' WHERE participation_type = 'ScheduledSlots';");
		}
	}
}
