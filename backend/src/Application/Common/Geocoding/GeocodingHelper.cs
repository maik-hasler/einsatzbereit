using Domain.Common;
using Domain.Primitives;
using Microsoft.Extensions.Logging;

namespace Application.Common.Geocoding;

internal static class GeocodingHelper
{
	public static async Task<Result<Address>> EnrichAsync(
		Address address,
		IGeocodingService geocodingService,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		GeocodingResult result;
		try
		{
			result = await geocodingService.GeocodeAsync(
				address.Street, address.HouseNumber, address.ZipCode, address.City, cancellationToken);
		}
		catch (Exception ex)
		{
			// An IGeocodingService implementation is expected to translate its own
			// failures into GeocodingOutcome.TransientFailure (see
			// NominatimGeocodingService), but a thrown exception here means we
			// genuinely don't know whether the address is valid - never treat that
			// as a confirmed NotFound.
			logger.LogWarning(
				ex,
				"Geocoding failed unexpectedly for address {Street} {HouseNumber}, {ZipCode} {City}; will retry automatically.",
				address.Street, address.HouseNumber, address.ZipCode, address.City);
			return address;
		}

		// A well-behaved IGeocodingService always returns one of the three named
		// results below, never null - but the interface's return type can't
		// enforce that. Treat a null result the same as an unexpected exception
		// rather than risk crashing (or worse, silently matching NotFound's switch
		// arm through a null check bug) on a misbehaving implementation.
		if (result is null)
		{
			logger.LogWarning(
				"Geocoding service returned no result object for address {Street} {HouseNumber}, {ZipCode} {City}; will retry automatically.",
				address.Street, address.HouseNumber, address.ZipCode, address.City);
			return address;
		}

		switch (result.Outcome)
		{
			case GeocodingOutcome.Found:
				return address.WithCoordinates(result.Coordinates!.Latitude, result.Coordinates.Longitude);

			case GeocodingOutcome.NotFound:
				logger.LogWarning(
					"Geocoding found no match for address {Street} {HouseNumber}, {ZipCode} {City}.",
					address.Street, address.HouseNumber, address.ZipCode, address.City);
				return Result.Failure<Address>(Error.Validation(
					"Address.NotFound",
					"The address could not be located. Please check the street, house number, zip code, and city."));

			default:
				logger.LogWarning(
					"Geocoding temporarily unavailable for address {Street} {HouseNumber}, {ZipCode} {City}; will retry automatically.",
					address.Street, address.HouseNumber, address.ZipCode, address.City);
				return address;
		}
	}
}
