using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

internal sealed class SearchAlertDigestOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<SearchAlertDigestOptions>
{
	public void Configure(SearchAlertDigestOptions options) =>
		configuration.GetSection("SearchAlertDigest").Bind(options);
}
