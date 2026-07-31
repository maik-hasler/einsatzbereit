using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

internal sealed class AutomaticCheckInOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<AutomaticCheckInOptions>
{
	public void Configure(AutomaticCheckInOptions options) =>
		configuration.GetSection("AutomaticCheckIn").Bind(options);
}
