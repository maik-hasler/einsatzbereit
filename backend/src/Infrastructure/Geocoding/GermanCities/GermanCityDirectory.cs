using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common.Geocoding;

namespace Infrastructure.Geocoding.GermanCities;

// Nominatim's free-form search only matches complete words, so typing a
// prefix like "Leip" returns nothing even though "Leipzig" is a real city
// (#2227). This bounded, locally embedded gazetteer (GeoNames.org, CC BY
// 4.0, German places with population >= 5000) lets the search-as-you-type
// experience resolve a prefix without depending on the upstream geocoder
// supporting one.
internal sealed class GermanCityDirectory : IGermanCityDirectory
{
	private readonly IReadOnlyList<GermanCityEntry> _cities;

	public GermanCityDirectory() => _cities = LoadCities();

	public IReadOnlyList<CitySuggestion> SearchByPrefix(string query, int limit)
	{
		var normalizedQuery = GermanCityNameNormalizer.Normalize(query.Trim());
		if (normalizedQuery.Length == 0)
			return [];

		return _cities
			.Where(city => city.NormalizedName.StartsWith(normalizedQuery, StringComparison.Ordinal))
			.OrderByDescending(city => city.Population)
			.ThenBy(city => city.Name, StringComparer.Ordinal)
			.Take(limit)
			.Select(city => new CitySuggestion(city.Name, city.Latitude, city.Longitude))
			.ToList();
	}

	private static IReadOnlyList<GermanCityEntry> LoadCities()
	{
		const string resourceName = "Infrastructure.Geocoding.GermanCities.german-cities.json";

		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
			?? throw new InvalidOperationException($"Embedded German city directory resource '{resourceName}' was not found.");

		var records = JsonSerializer.Deserialize<IReadOnlyList<GermanCityRecord>>(stream)
			?? throw new InvalidOperationException($"Embedded German city directory resource '{resourceName}' is empty or invalid.");

		return records
			.Select(record => new GermanCityEntry(
				record.Name,
				GermanCityNameNormalizer.Normalize(record.Name),
				record.Latitude,
				record.Longitude,
				record.Population))
			.ToList();
	}

	private sealed record GermanCityEntry(
		string Name,
		string NormalizedName,
		double Latitude,
		double Longitude,
		long Population);

	private sealed record GermanCityRecord(
		[property: JsonPropertyName("n")] string Name,
		[property: JsonPropertyName("lat")] double Latitude,
		[property: JsonPropertyName("lon")] double Longitude,
		[property: JsonPropertyName("p")] long Population);
}
