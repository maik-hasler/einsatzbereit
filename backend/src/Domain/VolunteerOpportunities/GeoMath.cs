namespace Domain.VolunteerOpportunities;

public readonly record struct GeoBoundingBox(double South, double North, double West, double East);

public static class GeoMath
{
	private const double EarthRadiusKm = 6371.0;

	public static double DistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
	{
		var dLat = ToRadians(latitude2 - latitude1);
		var dLon = ToRadians(longitude2 - longitude1);

		var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
			Math.Cos(ToRadians(latitude1)) * Math.Cos(ToRadians(latitude2)) *
			Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

		var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

		return EarthRadiusKm * c;
	}

	public static GeoBoundingBox BoundingBoxFor(double centerLatitude, double centerLongitude, double radiusKm)
	{
		var latDelta = radiusKm / 111.0;
		var lonDelta = radiusKm / (111.0 * Math.Cos(ToRadians(centerLatitude)));

		return new GeoBoundingBox(
			South: Math.Max(centerLatitude - latDelta, -90),
			North: Math.Min(centerLatitude + latDelta, 90),
			West: Math.Max(centerLongitude - lonDelta, -180),
			East: Math.Min(centerLongitude + lonDelta, 180));
	}

	private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
