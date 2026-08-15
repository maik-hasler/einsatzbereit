using Application.Common.Geocoding;
using Application.Common.Messaging;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Maps.GeocodeAddress.v1;

// Resolves an address to coordinates synchronously, in the caller's own
// request - unlike GeocodeVolunteerOpportunityAddressHandler (the transactional-
// outbox reaction used to retry a previously-failed/pending attempt out of
// band), this is the first attempt, called directly from an endpoint before
// it dispatches its create/update command, so a bad address (NotFound) can be
// rejected immediately instead of silently saving with null coordinates and
// only surfacing the problem later (#1963's underlying cause, addressed from
// the create side here).
internal sealed class GeocodeAddressQueryHandler(
	IGeocodingService geocodingService,
	IMemoryCache cache)
	: IQueryHandler<GeocodeAddressQuery, GeocodingResult>
{
	// Mirrors GeocodeVolunteerOpportunityAddressHandler's cache duration -
	// repeated identical addresses (e.g. several opportunities at the same
	// venue) resolve from cache instead of each waiting out Nominatim's shared
	// one-request-per-second throttle.
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

		// Don't cache a TransientFailure - a temporary Nominatim hiccup would
		// otherwise become a day-long false negative for every other address
		// lookup sharing this address (mirrors SearchCitiesQueryHandler).
		if (result.Outcome != GeocodingOutcome.TransientFailure)
			cache.Set(cacheKey, result, CacheDuration);

		return result;
	}
}
