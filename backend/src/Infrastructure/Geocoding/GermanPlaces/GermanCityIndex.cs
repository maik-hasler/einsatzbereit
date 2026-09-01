using System.Text.Json.Serialization;
using Application.Common.Geocoding;

namespace Infrastructure.Geocoding.GermanPlaces;

// Matches what people actually type into a location field. A name that starts
// with the query is the obvious match, but German city names carry qualifiers
// people skip - nobody types "Bad Homburg" or "Frankfurt (Oder)" from the
// front, they type "Homburg" and "Oder" - so a word inside the name counts as
// a match too, just ranked below a leading one. Matching a bare substring is
// deliberately not a tier: "eipzig" is noise, not a way anyone searches.
internal sealed class GermanCityIndex
{
	private static readonly char[] NameSeparators = [' ', '-', '/', '(', ')', ',', '.'];

	private const int ExactMatch = 0;
	private const int NamePrefixMatch = 1;
	private const int WordPrefixMatch = 2;
	private const int NoMatch = int.MaxValue;

	private readonly IReadOnlyList<GermanCityEntry> _cities;

	public GermanCityIndex() => _cities = LoadCities();

	public IReadOnlyList<CitySuggestion> Search(string query, int limit)
	{
		var normalizedQuery = GermanPlaceNameNormalizer.Normalize(query.Trim());
		if (normalizedQuery.Length == 0)
			return [];

		return _cities
			.Select(city => (City: city, Rank: RankOf(city, normalizedQuery)))
			.Where(match => match.Rank != NoMatch)
			.OrderBy(match => match.Rank)
			.ThenByDescending(match => match.City.Population)
			.ThenBy(match => match.City.Name, StringComparer.Ordinal)
			.Take(limit)
			.Select(match => new CitySuggestion(match.City.Name, match.City.Latitude, match.City.Longitude))
			.ToList();
	}

	private static int RankOf(GermanCityEntry city, string normalizedQuery)
	{
		if (city.NormalizedName.Equals(normalizedQuery, StringComparison.Ordinal))
			return ExactMatch;

		if (city.NormalizedName.StartsWith(normalizedQuery, StringComparison.Ordinal))
			return NamePrefixMatch;

		if (city.NormalizedWords.Any(word => word.StartsWith(normalizedQuery, StringComparison.Ordinal)))
			return WordPrefixMatch;

		return NoMatch;
	}

	private static IReadOnlyList<GermanCityEntry> LoadCities() =>
		EmbeddedJson.Load<GermanCityRecord>("Infrastructure.Geocoding.GermanPlaces.german-cities.json")
			.Select(record =>
			{
				var normalizedName = GermanPlaceNameNormalizer.Normalize(record.Name);
				return new GermanCityEntry(
					record.Name,
					normalizedName,
					normalizedName.Split(NameSeparators, StringSplitOptions.RemoveEmptyEntries),
					record.Latitude,
					record.Longitude,
					record.Population);
			})
			.ToList();

	private sealed record GermanCityEntry(
		string Name,
		string NormalizedName,
		IReadOnlyList<string> NormalizedWords,
		double Latitude,
		double Longitude,
		long Population);

	private sealed record GermanCityRecord(
		[property: JsonPropertyName("n")] string Name,
		[property: JsonPropertyName("lat")] double Latitude,
		[property: JsonPropertyName("lon")] double Longitude,
		[property: JsonPropertyName("p")] long Population);
}
