using System.Text.Json.Serialization;
using Application.Common.Geocoding;

namespace Infrastructure.Geocoding.GermanPlaces;

// The location field advertises "city or postal code", so "26129" has to
// resolve to Oldenburg. Postal codes are their own lookup: a prefix of digits
// narrows to the codes starting with it, in ascending order, because nothing
// about a postal code ranks one match above another.
internal sealed class GermanPostalCodeIndex
{
	private readonly IReadOnlyList<GermanPostalCodeEntry> _postalCodes;

	public GermanPostalCodeIndex() => _postalCodes = LoadPostalCodes();

	public IReadOnlyList<CitySuggestion> Search(string query, int limit)
	{
		var prefix = query.Trim();
		if (prefix.Length == 0)
			return [];

		return _postalCodes
			.Where(entry => entry.PostalCode.StartsWith(prefix, StringComparison.Ordinal))
			.Take(limit)
			// The code is part of the label so the suggestion visibly answers what
			// was typed - "26129" alone would look like an unrelated city.
			.Select(entry => new CitySuggestion($"{entry.PostalCode} {entry.PlaceName}", entry.Latitude, entry.Longitude))
			.ToList();
	}

	private static IReadOnlyList<GermanPostalCodeEntry> LoadPostalCodes() =>
		EmbeddedJson.Load<GermanPostalCodeEntry>("Infrastructure.Geocoding.GermanPlaces.german-postal-codes.json")
			.OrderBy(entry => entry.PostalCode, StringComparer.Ordinal)
			.ToList();

	private sealed record GermanPostalCodeEntry(
		[property: JsonPropertyName("z")] string PostalCode,
		[property: JsonPropertyName("n")] string PlaceName,
		[property: JsonPropertyName("lat")] double Latitude,
		[property: JsonPropertyName("lon")] double Longitude);
}
