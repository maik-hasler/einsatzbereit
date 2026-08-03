using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

internal sealed class CheckInAttemptPruneOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<CheckInAttemptPruneOptions>
{
	public void Configure(CheckInAttemptPruneOptions options) =>
		configuration.GetSection("CheckInAttemptPrune").Bind(options);
}
