namespace Application.Common.Geocoding;

// Shared by GeocodeAddressQueryHandler (synchronous, create-time lookups) and
// GeocodeVolunteerOpportunityAddressHandler (async outbox retries) so both
// geocoding-result caches key identical addresses the same way.
internal static class GeocodeCacheKey
{
	public static string For(string street, string houseNumber, string zipCode, string city) =>
		$"geocode:{zipCode}|{street}|{houseNumber}|{city}".ToLowerInvariant();
}
