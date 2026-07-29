using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

internal sealed class OutboxOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<OutboxOptions>
{
	public void Configure(OutboxOptions options) =>
		configuration.GetSection("Outbox").Bind(options);
}
