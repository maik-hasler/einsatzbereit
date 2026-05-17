using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

internal sealed class SmtpOptionsSetup(
	IConfiguration configuration)
	: IConfigureOptions<SmtpOptions>
{
	public void Configure(SmtpOptions options) =>
		configuration.GetSection("Smtp").Bind(options);
}
