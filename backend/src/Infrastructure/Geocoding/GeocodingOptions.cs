namespace Infrastructure.Geocoding;

internal sealed class GeocodingOptions
{
	public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org";

	public string UserAgent { get; set; } = "Einsatzbereit/1.0 (https://github.com/maik-hasler/einsatzbereit)";

	public int MinRequestIntervalMilliseconds { get; set; } = 1100;

	public int TimeoutSeconds { get; set; } = 5;

	public string DefaultCountry { get; set; } = "Deutschland";
}
