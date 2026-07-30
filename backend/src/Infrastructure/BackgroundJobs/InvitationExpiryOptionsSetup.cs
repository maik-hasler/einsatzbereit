using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

internal sealed class InvitationExpiryOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<InvitationExpiryOptions>
{
	public void Configure(InvitationExpiryOptions options) =>
		configuration.GetSection("InvitationExpiry").Bind(options);
}
