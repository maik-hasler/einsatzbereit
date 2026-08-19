using Application.Common.Geocoding;

namespace Infrastructure.Geocoding;

// Wired in place of NominatimGeocodingService for IntegrationTests/VisualTests
// (see AppHost.cs's "Geocoding__UseFakeService" override) so those runs never
// make a real network call - deterministic, instant, and doesn't depend on
// HttpClient resilience/retry timing. Always reports TransientFailure (never
// Found or NotFound) so an opportunity always saves with null coordinates,
// matching what these tests already expect, and never trips the #975 hard
// validation error for a placeholder test address.
internal sealed class FakeGeocodingService : IGeocodingService
{
	// #1930: the real bug this fixture exists for is Nominatim data, not
	// anything this backend computes - a query can genuinely resolve to a
	// place whose name is character-for-character what was typed (see
	// NominatimGeocodingService.ToSuggestions), which the frontend must not
	// render identically to an unambiguous result. Only this exact query
	// (obviously synthetic - not a real place) exercises that fixture
	// deterministically; every other query still returns no results, which
	// is what CityOnlyDeepLinkLocationFilterTests relies on.
	// The other result's label deliberately contains the query as a
	// non-prefix substring (not just anything unrelated) - since #2046,
	// filterByLabelMatch drops any suggestion whose label doesn't contain
	// the typed query at all, so a genuinely unrelated "other" fixture like
	// the old "Zzztestwalde" would itself get filtered out, collapsing this
	// test's two-result scenario down to one before it could ever run.
	internal const string ExactMatchFixtureQuery = "Zzztestdorf";
	internal const string ExactMatchFixtureOtherResult = "Neu-Zzztestdorf";

	public Task<GeocodingResult> GeocodeAsync(
		string street,
		string houseNumber,
		string zipCode,
		string city,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(GeocodingResult.TransientFailure);

	public Task<IReadOnlyList<CitySuggestion>> SearchCitiesAsync(
		string query,
		string language,
		CancellationToken cancellationToken = default)
	{
		if (string.Equals(query.Trim(), ExactMatchFixtureQuery, StringComparison.OrdinalIgnoreCase))
		{
			return Task.FromResult<IReadOnlyList<CitySuggestion>>([
				new CitySuggestion(ExactMatchFixtureQuery, 51.0, 10.0),
				new CitySuggestion(ExactMatchFixtureOtherResult, 51.1, 10.1),
			]);
		}

		return Task.FromResult<IReadOnlyList<CitySuggestion>>([]);
	}
}
