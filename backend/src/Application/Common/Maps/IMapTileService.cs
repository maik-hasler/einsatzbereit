namespace Application.Common.Maps;

public sealed record MapTile(byte[] Content, string ContentType);

public interface IMapTileService
{
	Task<MapTile?> GetTileAsync(
		int zoom,
		int x,
		int y,
		CancellationToken cancellationToken = default);
}
