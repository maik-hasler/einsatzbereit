using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Keycloak;

internal sealed class KeycloakOptionsSetup(
    IConfiguration configuration)
    : IConfigureOptions<KeycloakOptions>
{
    public void Configure(KeycloakOptions options) =>
        configuration.GetSection("Keycloak").Bind(options);
}
