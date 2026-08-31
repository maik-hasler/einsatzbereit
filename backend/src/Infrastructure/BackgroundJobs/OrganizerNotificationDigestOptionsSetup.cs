using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

internal sealed class OrganizerNotificationDigestOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<OrganizerNotificationDigestOptions>
{
	public void Configure(OrganizerNotificationDigestOptions options) =>
		configuration.GetSection("OrganizerNotificationDigest").Bind(options);
}
