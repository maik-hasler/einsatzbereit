using Application.Common.Geocoding;

namespace Infrastructure.Geocoding;

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
		string language,
		CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<CitySuggestion>>([]);
}
