using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

internal sealed class NotificationRetentionOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<NotificationRetentionOptions>
{
	public void Configure(NotificationRetentionOptions options) =>
		configuration.GetSection("NotificationRetention").Bind(options);
}
