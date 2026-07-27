using System.Globalization;
using System.Net.Http.Headers;
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
	private const int MaxCitySuggestions = 6;
	private const int MinCitySearchQueryLength = 2;

	private static readonly SemaphoreSlim Throttle = new(1, 1);
	private static DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

	private readonly GeocodingOptions _options = options.Value;

	public async Task<GeocodingResult> GeocodeAsync(
		string street,
		string houseNumber,
		string zipCode,
		string city,
		CancellationToken cancellationToken = default)
	{
		var requestUri = BuildRequestUri(street, houseNumber, zipCode, city);

		try
		{
			return await ThrottledAsync(
				async () =>
				{
					var results = await httpClient.GetFromJsonAsync<IReadOnlyList<NominatimResult>>(requestUri, cancellationToken);

					var first = results?.FirstOrDefault();
					if (first is null)
						return GeocodingResult.NotFound;

					if (!double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
						!double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
					{
						logger.LogWarning("Nominatim returned unparsable coordinates for {RequestUri}.", requestUri);
						return GeocodingResult.TransientFailure;
					}

					return GeocodingResult.Found(new GeoCoordinates(latitude, longitude));
				},
				cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Nominatim geocoding request failed for {RequestUri}.", requestUri);
			return GeocodingResult.TransientFailure;
		}
	}

	public async Task<IReadOnlyList<CitySuggestion>> SearchCitiesAsync(
		string query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < MinCitySearchQueryLength)
			return [];

		var requestUri = BuildCitySearchRequestUri(query);

		try
		{
			return await ThrottledAsync<IReadOnlyList<CitySuggestion>>(
				async () =>
				{
					using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
					request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("de"));
					request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));

					using var response = await httpClient.SendAsync(request, cancellationToken);
					if (!response.IsSuccessStatusCode)
						return [];

					var results = await response.Content.ReadFromJsonAsync<IReadOnlyList<NominatimCityResult>>(
						cancellationToken: cancellationToken);

					return ToSuggestions(results);
				},
				cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Nominatim city search failed for {RequestUri}.", requestUri);
			return [];
		}
	}

	// Nominatim's usage policy caps combined traffic from this app at one request
	// per second, regardless of which method triggers the call - the throttle and
	// _lastRequest timestamp are shared (static) across GeocodeAsync and
	// SearchCitiesAsync so both stay under that single limit together.
	private async Task<T> ThrottledAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
	{
		await Throttle.WaitAsync(cancellationToken);

		try
		{
			var sinceLast = DateTimeOffset.UtcNow - _lastRequest;
			var minInterval = TimeSpan.FromMilliseconds(_options.MinRequestIntervalMilliseconds);
			if (sinceLast < minInterval)
				await Task.Delay(minInterval - sinceLast, cancellationToken);

			var result = await action();

			_lastRequest = DateTimeOffset.UtcNow;

			return result;
		}
		finally
		{
			Throttle.Release();
		}
	}

	private static IReadOnlyList<CitySuggestion> ToSuggestions(IReadOnlyList<NominatimCityResult>? results)
	{
		var suggestions = new List<CitySuggestion>();
		if (results is null)
			return suggestions;

		foreach (var result in results)
		{
			var label = result.Address?.City ?? result.Address?.Town ?? result.Address?.Village ?? result.Address?.Municipality;
			if (string.IsNullOrEmpty(label))
				continue;

			if (!double.TryParse(result.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude))
				continue;

			if (!double.TryParse(result.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
				continue;

			if (suggestions.Any(s => s.Label == label))
				continue;

			suggestions.Add(new CitySuggestion(label, latitude, longitude));

			if (suggestions.Count >= MaxCitySuggestions)
				break;
		}

		return suggestions;
	}

	private static string BuildCitySearchRequestUri(string query)
	{
		var queryString = string.Join(
			'&',
			"format=json",
			"addressdetails=1",
			"featuretype=city",
			$"q={Uri.EscapeDataString(query.Trim())}",
			$"limit={MaxCitySuggestions}");

		return $"search?{queryString}";
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

	private sealed record NominatimCityResult(
		[property: JsonPropertyName("lat")] string? Lat,
		[property: JsonPropertyName("lon")] string? Lon,
		[property: JsonPropertyName("address")] NominatimAddress? Address);

	private sealed record NominatimAddress(
		[property: JsonPropertyName("city")] string? City,
		[property: JsonPropertyName("town")] string? Town,
		[property: JsonPropertyName("village")] string? Village,
		[property: JsonPropertyName("municipality")] string? Municipality);
}
