namespace Application.Common.Geocoding;

public sealed record GeoCoordinates(double Latitude, double Longitude);

public interface IGeocodingService
{
	Task<GeoCoordinates?> GeocodeAsync(
		string street,
		string houseNumber,
		string zipCode,
		string city,
		CancellationToken cancellationToken = default);
}
