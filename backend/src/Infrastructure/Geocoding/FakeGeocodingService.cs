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
	public Task<GeocodingResult> GeocodeAsync(
		string street,
		string houseNumber,
		string zipCode,
		string city,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(GeocodingResult.TransientFailure);

	public Task<IReadOnlyList<CitySuggestion>> SearchCitiesAsync(
		string query,
		CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<CitySuggestion>>([]);
}
