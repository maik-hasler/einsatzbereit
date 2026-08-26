using Application.Common.Exceptions;
using Application.Common.Geocoding;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.GeocodeVolunteerOpportunityAddress.v1;

internal sealed class GeocodeVolunteerOpportunityAddressHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IGeocodingService geocodingService,
	IMemoryCache cache,
	ILogger<GeocodeVolunteerOpportunityAddressHandler> logger)
	: INotificationHandler<VolunteerOpportunityGeocodingRequestedDomainEvent>
{
	private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

	public async Task Handle(
		VolunteerOpportunityGeocodingRequestedDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			notification.OpportunityId, cancellationToken);

		if (opportunity is null ||
			opportunity.IsRemote ||
			opportunity.Address is null ||
			opportunity.Address.Latitude is not null ||
			opportunity.AddressGeocodingFailed)
			return;

		var address = opportunity.Address;
		var cacheKey = GeocodeCacheKey.For(address.Street, address.HouseNumber, address.ZipCode, address.City);

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

				break;
		}
	}
}
