using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class RetryVolunteerOpportunityCityLevelGeocoding : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Data-only: address_geocoding_failed is a "do not retry" tombstone, and
			// NominatimGeocodingService used to set it for any address whose street and house
			// number OSM could not match - even when the city itself was perfectly locatable.
			// Those rows keep null coordinates forever, so every radius search skips them while
			// their cards still show a city map pin (#2319). The service now falls back to
			// postcode/city granularity, so clear the tombstone on the rows that never got
			// coordinates and let GeocodingRetryJob pick them up again.
			migrationBuilder.Sql("""
				UPDATE volunteer_opportunity
				SET address_geocoding_failed = FALSE
				WHERE address_geocoding_failed
					AND NOT is_remote
					AND address_city IS NOT NULL
					AND address_latitude IS NULL;
				""");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// The tombstone carried no record of when or why it was set, so there is nothing
			// to restore - a re-run of the retry job re-derives it for anything still unmatched.
		}
	}
}
