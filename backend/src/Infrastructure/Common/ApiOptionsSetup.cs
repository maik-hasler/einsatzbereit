using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Common;

internal sealed class ApiOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<ApiOptions>
{
	public void Configure(ApiOptions options)
	{
		configuration.GetSection("Api").Bind(options);

		var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
		if (origins.Length > 0)
			options.FrontendBaseUrl = origins[0].TrimEnd('/');
	}
}
