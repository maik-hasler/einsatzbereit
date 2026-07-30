using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Common;

internal sealed class ApiOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<ApiOptions>
{
	public void Configure(ApiOptions options) =>
		configuration.GetSection("Api").Bind(options);
}
