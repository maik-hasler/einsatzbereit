using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

internal sealed class ReportRetentionOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<ReportRetentionOptions>
{
	public void Configure(ReportRetentionOptions options) =>
		configuration.GetSection("ReportRetention").Bind(options);
}
