using Application.Common.Geocoding;
using Infrastructure.Geocoding.GermanPlaces;

namespace Infrastructure.Geocoding;

// GeocodeAsync is faked as a permanent transient failure (see the
// GeocodingRetryJob) so local dev/tests never depend on a live Nominatim
// call. City search has no such retry path and no reason to depend on
// Nominatim at all - it serves straight from the same bounded local
// directory the real service asks first (#2227).
internal sealed class FakeGeocodingService(IGermanPlaceDirectory placeDirectory) : IGeocodingService
{
	private const int MaxCitySuggestions = 6;

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
		CancellationToken cancellationToken = default) =>
		Task.FromResult(placeDirectory.Search(query, MaxCitySuggestions));
}
