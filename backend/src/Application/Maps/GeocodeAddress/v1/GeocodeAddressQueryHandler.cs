using Application.Common.Geocoding;
using Application.Common.Messaging;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Maps.GeocodeAddress.v1;

internal sealed class GeocodeAddressQueryHandler(
	IGeocodingService geocodingService,
	IMemoryCache cache)
	: IQueryHandler<GeocodeAddressQuery, GeocodingResult>
{
	private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

	public async ValueTask<GeocodingResult> Handle(
		GeocodeAddressQuery request,
		CancellationToken cancellationToken = default)
	{
		var address = request.Address;
		var cacheKey = GeocodeCacheKey.For(address.Street, address.HouseNumber, address.ZipCode, address.City);

		if (cache.TryGetValue(cacheKey, out GeocodingResult? cached) && cached is not null)
			return cached;

		var result = await geocodingService.GeocodeAsync(
			address.Street, address.HouseNumber, address.ZipCode, address.City, cancellationToken);

		if (result.Outcome != GeocodingOutcome.TransientFailure)
		{
			// Nominal Size - the shared cache's SizeLimit budget is denominated in the
			// tile bytes OpenStreetMapTileService caches, which dwarf this payload (#2215).
			cache.Set(cacheKey, result, new MemoryCacheEntryOptions
			{
				Size = 1,
				AbsoluteExpirationRelativeToNow = CacheDuration,
			});
		}

		return result;
	}
}
