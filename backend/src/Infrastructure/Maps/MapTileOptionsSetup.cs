using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Maps;

internal sealed class MapTileOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<MapTileOptions>
{
	public void Configure(MapTileOptions options) =>
		configuration.GetSection("MapTiles").Bind(options);
}
