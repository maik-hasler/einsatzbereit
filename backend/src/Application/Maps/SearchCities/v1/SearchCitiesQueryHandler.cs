using Application.Common.Geocoding;
using Application.Common.Messaging;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Maps.SearchCities.v1;

internal sealed class SearchCitiesQueryHandler(
	IGeocodingService geocodingService,
	IMemoryCache cache)
	: IQueryHandler<SearchCitiesQuery, IReadOnlyList<CitySuggestion>>
{
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

		if (results.Count > 0)
		{
			// Nominal Size - the shared cache's SizeLimit budget is denominated in the
			// tile bytes OpenStreetMapTileService caches, which dwarf this payload (#2215).
			cache.Set(cacheKey, results, new MemoryCacheEntryOptions
			{
				Size = 1,
				AbsoluteExpirationRelativeToNow = CacheDuration,
			});
		}

		return results;
	}
}
