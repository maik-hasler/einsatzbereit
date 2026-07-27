using Application.Common.Maps;
using Application.Common.Messaging;

namespace Application.Maps.GetMapTile.v1;

public sealed record GetMapTileQuery(int Zoom, int X, int Y) : IQuery<MapTile?>;
