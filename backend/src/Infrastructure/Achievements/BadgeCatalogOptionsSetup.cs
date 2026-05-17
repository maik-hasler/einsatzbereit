using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Achievements;

internal sealed class BadgeCatalogOptionsSetup(IConfiguration configuration)
	: IConfigureOptions<BadgeCatalogOptions>
{
	public void Configure(BadgeCatalogOptions options) =>
		configuration.GetSection("BadgeCatalog").Bind(options);
}
