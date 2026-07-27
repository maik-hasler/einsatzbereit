using Application.Common.Maps;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Maps;

internal sealed class OpenStreetMapTileService(
	HttpClient httpClient,
	IMemoryCache cache,
	IOptions<MapTileOptions> options,
	ILogger<OpenStreetMapTileService> logger)
	: IMapTileService
{
	private const string ContentType = "image/png";

	private readonly MapTileOptions _options = options.Value;

	public async Task<MapTile?> GetTileAsync(
		int zoom,
		int x,
		int y,
		CancellationToken cancellationToken = default)
	{
		if (!IsValidTile(zoom, x, y))
			return null;

		var cacheKey = $"tile:{zoom}/{x}/{y}";
		if (cache.TryGetValue(cacheKey, out byte[]? cached) && cached is not null)
			return new MapTile(cached, ContentType);

		try
		{
			var response = await httpClient.GetAsync($"{zoom}/{x}/{y}.png", cancellationToken);

			if (!response.IsSuccessStatusCode)
				return null;

			var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

			cache.Set(cacheKey, content, TimeSpan.FromMinutes(_options.CacheDurationMinutes));

			return new MapTile(content, ContentType);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "OpenStreetMap tile request failed for {Zoom}/{X}/{Y}.", zoom, x, y);
			return null;
		}
	}

	private bool IsValidTile(int zoom, int x, int y)
	{
		if (zoom < 0 || zoom > _options.MaxZoom)
			return false;

		var tilesPerAxis = 1 << zoom;
		return x >= 0 && x < tilesPerAxis && y >= 0 && y < tilesPerAxis;
	}
}
