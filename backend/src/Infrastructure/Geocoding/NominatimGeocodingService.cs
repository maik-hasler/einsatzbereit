using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.Common.Geocoding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Geocoding;

internal sealed class NominatimGeocodingService(
	HttpClient httpClient,
	IOptions<GeocodingOptions> options,
	ILogger<NominatimGeocodingService> logger)
	: IGeocodingService
{
	private static readonly SemaphoreSlim Throttle = new(1, 1);
	private static DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

	private readonly GeocodingOptions _options = options.Value;

	public async Task<GeoCoordinates?> GeocodeAsync(
		string street,
		string houseNumber,
		string zipCode,
		string city,
		CancellationToken cancellationToken = default)
	{
		var requestUri = BuildRequestUri(street, houseNumber, zipCode, city);

		await Throttle.WaitAsync(cancellationToken);

		try
		{
			var sinceLast = DateTimeOffset.UtcNow - _lastRequest;
			var minInterval = TimeSpan.FromMilliseconds(_options.MinRequestIntervalMilliseconds);
			if (sinceLast < minInterval)
				await Task.Delay(minInterval - sinceLast, cancellationToken);

			var results = await httpClient.GetFromJsonAsync<IReadOnlyList<NominatimResult>>(requestUri, cancellationToken);

			_lastRequest = DateTimeOffset.UtcNow;

			var first = results?.FirstOrDefault();
			if (first is null)
				return null;

			if (!double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
				!double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
				return null;

			return new GeoCoordinates(latitude, longitude);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Nominatim geocoding request failed for {RequestUri}.", requestUri);
			return null;
		}
		finally
		{
			Throttle.Release();
		}
	}

	private string BuildRequestUri(string street, string houseNumber, string zipCode, string city)
	{
		var query = new Dictionary<string, string?>
		{
			["street"] = $"{houseNumber} {street}".Trim(),
			["postalcode"] = zipCode,
			["city"] = city,
			["country"] = _options.DefaultCountry,
			["format"] = "jsonv2",
			["limit"] = "1",
		};

		var queryString = string.Join('&', query
			.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
			.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));

		return $"search?{queryString}";
	}

	private sealed record NominatimResult(
		[property: JsonPropertyName("lat")] string? Lat,
		[property: JsonPropertyName("lon")] string? Lon);
}
