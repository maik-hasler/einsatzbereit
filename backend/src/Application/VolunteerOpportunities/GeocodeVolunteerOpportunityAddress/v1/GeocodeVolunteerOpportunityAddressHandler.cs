using Application.Common.Exceptions;
using Application.Common.Geocoding;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Common;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.GeocodeVolunteerOpportunityAddress.v1;

// Reacts to VolunteerOpportunityGeocodingRequestedDomainEvent (raised by
// VolunteerOpportunity.Create/Relocate) via the transactional-outbox pipeline
// (see backend/AGENTS.md's "Domain events" section) - dispatch happens in its
// own scope well after the triggering command's transaction has committed, so
// the Nominatim call (and its up-to-1.1s process-wide throttle, see
// NominatimGeocodingService) no longer runs while a DB transaction is open
// (#1388).
internal sealed class GeocodeVolunteerOpportunityAddressHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IGeocodingService geocodingService,
	IMemoryCache cache,
	ILogger<GeocodeVolunteerOpportunityAddressHandler> logger)
	: INotificationHandler<VolunteerOpportunityGeocodingRequestedDomainEvent>
{
	// Repeated identical addresses (e.g. several opportunities at the same
	// venue) resolve from cache instead of each waiting out Nominatim's shared
	// one-request-per-second throttle (mirrors SearchCitiesQueryHandler).
	private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

	public async Task Handle(
		VolunteerOpportunityGeocodingRequestedDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			notification.OpportunityId, cancellationToken);

		// By the time this dispatches, the opportunity may have been deleted,
		// gone remote, had its address changed again (a newer event supersedes
		// this one), or already been resolved by an earlier attempt - nothing to
		// do in any of those cases.
		if (opportunity is null ||
			opportunity.IsRemote ||
			opportunity.Address is null ||
			opportunity.Address.Latitude is not null ||
			opportunity.AddressGeocodingFailed)
			return;

		var address = opportunity.Address;
		var cacheKey = BuildCacheKey(address);

		if (!cache.TryGetValue(cacheKey, out GeocodingResult? result) || result is null)
		{
			try
			{
				result = await geocodingService.GeocodeAsync(
					address.Street, address.HouseNumber, address.ZipCode, address.City, cancellationToken);
			}
			catch (Exception ex)
			{
				logger.LogWarning(
					ex,
					"Geocoding failed unexpectedly for volunteer opportunity {OpportunityId}; GeocodingRetryJob will retry later.",
					opportunity.Id.Value);
				return;
			}

			// Don't cache a TransientFailure - a temporary Nominatim hiccup would
			// otherwise become a day-long false negative for every other
			// opportunity sharing this address (mirrors SearchCitiesQueryHandler).
			if (result.Outcome != GeocodingOutcome.TransientFailure)
				cache.Set(cacheKey, result, CacheDuration);
		}

		switch (result.Outcome)
		{
			case GeocodingOutcome.Found:
				var resolved = address.WithCoordinates(
					result.Coordinates!.Latitude, result.Coordinates.Longitude).GetValueOrThrow();
				opportunity.ApplyGeocodingResult(resolved);
				await unitOfWork.SaveChangesAsync(cancellationToken);
				break;

			case GeocodingOutcome.NotFound:
				logger.LogWarning(
					"Geocoding found no match for volunteer opportunity {OpportunityId}'s address; will not retry.",
					opportunity.Id.Value);
				opportunity.MarkAddressGeocodingFailed();
				await unitOfWork.SaveChangesAsync(cancellationToken);
				break;

			default:
				// Leave coordinates null - GeocodingRetryJob backstops transient
				// failures hourly. Deliberately not throwing here: OutboxProcessorJob
				// retries a message whose handler threw on its very next 5s poll,
				// which would hammer Nominatim far harder than the hourly job does.
				break;
		}
	}

	private static string BuildCacheKey(Address address) =>
		$"geocode:{address.ZipCode}|{address.Street}|{address.HouseNumber}|{address.City}".ToLowerInvariant();
}
