using Application.Common.Geocoding;

namespace Infrastructure.Geocoding;

internal sealed class FakeGeocodingService : IGeocodingService
{
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
