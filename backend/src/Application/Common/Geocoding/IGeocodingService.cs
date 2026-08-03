namespace Application.Common.Geocoding;

public sealed record GeoCoordinates(double Latitude, double Longitude);

public sealed record CitySuggestion(string Label, double Latitude, double Longitude);

public enum GeocodingOutcome
{
	/// <summary>A coordinate match was found.</summary>
	Found,

	/// <summary>The geocoding provider confirmed no match exists for this address; retrying the same query won't help.</summary>
	NotFound,

	/// <summary>The geocoding provider could not be reached or answered unexpectedly (timeout, rate limit, outage); worth retrying later.</summary>
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
