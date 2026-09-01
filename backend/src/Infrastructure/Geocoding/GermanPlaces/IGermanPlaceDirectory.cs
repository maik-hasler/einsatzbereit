using Application.Common.Geocoding;

namespace Infrastructure.Geocoding.GermanPlaces;

internal interface IGermanPlaceDirectory
{
	IReadOnlyList<CitySuggestion> Search(string query, int limit);
}
