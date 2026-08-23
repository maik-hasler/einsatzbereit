namespace Application.Common.Geocoding;

public sealed record GeoCoordinates(double Latitude, double Longitude);

public sealed record CitySuggestion(string Label, double Latitude, double Longitude);

public enum GeocodingOutcome
{
	Found,

	NotFound,

	TransientFailure,
}

public sealed record GeocodingResult(GeocodingOutcome Outcome, GeoCoordinates? Coordinates)
{
	public static GeocodingResult Found(GeoCoordinates coordinates) => new(GeocodingOutcome.Found, coordinates);

	public static readonly GeocodingResult NotFound = new(GeocodingOutcome.NotFound, null);

	public static readonly GeocodingResult TransientFailure = new(GeocodingOutcome.TransientFailure, null);
}

public interface IGeocodingService
{
	Task<GeocodingResult> GeocodeAsync(
		string street,
		string houseNumber,
		string zipCode,
		string city,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<CitySuggestion>> SearchCitiesAsync(
		string query,
		string language,
		CancellationToken cancellationToken = default);
}
