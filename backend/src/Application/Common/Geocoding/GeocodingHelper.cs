using Application.Common.Exceptions;
using Domain.Common;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.Common.Geocoding;

internal static class GeocodingHelper
{
	public static async Task<Address> EnrichAsync(
		Address address,
		IGeocodingService geocodingService,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		try
		{
			var coordinates = await geocodingService.GeocodeAsync(
				address.Street, address.HouseNumber, address.ZipCode, address.City, cancellationToken);

			if (coordinates is not null)
				return address.WithCoordinates(coordinates.Latitude, coordinates.Longitude).GetValueOrThrow();

			logger.LogWarning(
				"Geocoding returned no result for address {Street} {HouseNumber}, {ZipCode} {City}.",
				address.Street, address.HouseNumber, address.ZipCode, address.City);
		}
		catch (Exception ex)
		{
			logger.LogWarning(
				ex,
				"Geocoding failed for address {Street} {HouseNumber}, {ZipCode} {City}.",
				address.Street, address.HouseNumber, address.ZipCode, address.City);
		}

		return address;
	}
}
