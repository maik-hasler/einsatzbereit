using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Persistence.Options;

internal class ConnectionStringOptionsSetup(
    IConfiguration configuration)
    : IConfigureOptions<ConnectionStringOptions>
{
    private const string SectionName = "ConnectionStrings";

    public void Configure(
        ConnectionStringOptions options)
    {
        configuration
            .GetSection(SectionName)
            .Bind(options);
    }
}