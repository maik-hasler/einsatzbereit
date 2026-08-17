namespace Infrastructure.Geocoding;

internal sealed class GeocodingOptions
{
	public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org";

	public string UserAgent { get; set; } = "Einsatzbereit/1.0 (https://github.com/maik-hasler/einsatzbereit)";

	public int MinRequestIntervalMilliseconds { get; set; } = 1100;

	public int TimeoutSeconds { get; set; } = 5;

	public string DefaultCountry { get; set; } = "Deutschland";

	// ISO 3166-1 alpha-2, lowercase - Nominatim's `countrycodes` free-text search
	// param (unlike the structured `country` field above, which accepts a name).
	// Restricts city-name/postal-code search to Germany so an ambiguous or
	// short query never ranks an international namesake (e.g. Seoul/Bucksport
	// for the "04416"/"04177" postal codes, or Polish villages for "Leip")
	// ahead of - or instead of - the intended German city (#1900).
	public string CitySearchCountryCode { get; set; } = "de";
}
