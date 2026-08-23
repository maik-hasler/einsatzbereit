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
			cache.Set(cacheKey, result, CacheDuration);

		return result;
	}
}
