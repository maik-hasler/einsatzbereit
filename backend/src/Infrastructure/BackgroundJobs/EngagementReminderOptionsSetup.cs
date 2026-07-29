using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

internal sealed class EngagementReminderOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<EngagementReminderOptions>
{
	public void Configure(EngagementReminderOptions options) =>
		configuration.GetSection("EngagementReminder").Bind(options);
}
