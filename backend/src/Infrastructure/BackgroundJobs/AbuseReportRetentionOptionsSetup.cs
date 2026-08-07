using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

internal sealed class AbuseReportRetentionOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<AbuseReportRetentionOptions>
{
	public void Configure(AbuseReportRetentionOptions options) =>
		configuration.GetSection("AbuseReportRetention").Bind(options);
}
