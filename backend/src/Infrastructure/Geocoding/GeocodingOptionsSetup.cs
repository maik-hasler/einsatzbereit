using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Geocoding;

internal sealed class GeocodingOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<GeocodingOptions>
{
	public void Configure(GeocodingOptions options) =>
		configuration.GetSection("Geocoding").Bind(options);
}
