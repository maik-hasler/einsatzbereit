using Application.Common.Geocoding;
using Application.Common.Messaging;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Maps.SearchCities.v1;

internal sealed class SearchCitiesQueryHandler(
	IGeocodingService geocodingService,
	IMemoryCache cache)
	: IQueryHandler<SearchCitiesQuery, IReadOnlyList<CitySuggestion>>
{
	// City name-to-coordinates mappings are effectively static, so a repeated
	// query never needs to reach Nominatim (and its shared one-request-per-
	// second throttle, see NominatimGeocodingService) again within this window.
	private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

	public async ValueTask<IReadOnlyList<CitySuggestion>> Handle(
		SearchCitiesQuery request,
		CancellationToken cancellationToken = default)
	{
		// Keyed by language too - Nominatim returns different exonyms per
		// requested language (e.g. "Munich" vs "Munchen"), so a German result
		// must never be served from cache to an English-requesting caller.
		var cacheKey = $"city-search:{request.Language}:{request.Query.Trim().ToLowerInvariant()}";

		if (cache.TryGetValue(cacheKey, out IReadOnlyList<CitySuggestion>? cached) && cached is not null)
			return cached;

		var results = await geocodingService.SearchCitiesAsync(request.Query, request.Language, cancellationToken);

		// Don't cache an empty result - it's indistinguishable here from a
		// transient geocoding failure, and caching that would turn a temporary
		// hiccup into a day-long false "no such city" for every visitor.
		if (results.Count > 0)
			cache.Set(cacheKey, results, CacheDuration);

		return results;
	}
}
