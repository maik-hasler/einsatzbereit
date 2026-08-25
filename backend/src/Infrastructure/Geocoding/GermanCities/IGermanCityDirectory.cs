using Application.Common.Geocoding;

namespace Infrastructure.Geocoding.GermanCities;

internal interface IGermanCityDirectory
{
	IReadOnlyList<CitySuggestion> SearchByPrefix(string query, int limit);
}
