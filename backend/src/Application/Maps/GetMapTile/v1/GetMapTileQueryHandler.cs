using Application.Common.Maps;
using Application.Common.Messaging;

namespace Application.Maps.GetMapTile.v1;

internal sealed class GetMapTileQueryHandler(
	IMapTileService mapTileService)
	: IQueryHandler<GetMapTileQuery, MapTile?>
{
	public async ValueTask<MapTile?> Handle(
		GetMapTileQuery request,
		CancellationToken cancellationToken = default) =>
		await mapTileService.GetTileAsync(request.Zoom, request.X, request.Y, cancellationToken);
}
