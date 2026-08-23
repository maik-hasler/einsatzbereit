namespace Application.Common.Geocoding;

internal static class GeocodeCacheKey
{
	public static string For(string street, string houseNumber, string zipCode, string city) =>
		$"geocode:{zipCode}|{street}|{houseNumber}|{city}".ToLowerInvariant();
}
