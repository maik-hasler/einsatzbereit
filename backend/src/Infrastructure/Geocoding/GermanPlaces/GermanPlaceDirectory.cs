using Application.Common.Geocoding;

namespace Infrastructure.Geocoding.GermanPlaces;

// Nominatim's free-form search only matches complete words, so typing a prefix
// like "Leip" returns nothing even though "Leipzig" is a real city (#2227).
// These bounded, locally embedded gazetteers (GeoNames.org, CC BY 4.0 - see
// README.md) answer a search-as-you-type query without leaving the process,
// which is why the geocoding service asks them before it asks Nominatim.
internal sealed class GermanPlaceDirectory : IGermanPlaceDirectory
{
	private readonly GermanCityIndex _cities = new();
	private readonly GermanPostalCodeIndex _postalCodes = new();

	public IReadOnlyList<CitySuggestion> Search(string query, int limit)
	{
		var trimmed = query.Trim();
		if (trimmed.Length == 0)
			return [];

		// No German city name is all digits, so the two indexes never compete
		// for the same query.
		return trimmed.All(char.IsAsciiDigit)
			? _postalCodes.Search(trimmed, limit)
			: _cities.Search(trimmed, limit);
	}
}
