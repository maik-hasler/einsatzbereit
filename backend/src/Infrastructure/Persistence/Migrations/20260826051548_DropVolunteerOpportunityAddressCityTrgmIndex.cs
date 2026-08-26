using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class DropVolunteerOpportunityAddressCityTrgmIndex : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// #2211: the city filter this index served has since moved to geocoded
			// radius search, so it's now dead weight that costs every fresh
			// deployment a pg_trgm CREATE EXTENSION privilege it has no other use
			// for. IF EXISTS makes this a no-op on a chain that never created it.
			migrationBuilder.Sql("DROP INDEX IF EXISTS ix_volunteer_opportunity_address_city_trgm;");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Deliberately not reversible: recreating the index would require
			// re-running CREATE EXTENSION pg_trgm, the exact privilege requirement
			// this migration exists to remove (#2211).
		}
	}
}
