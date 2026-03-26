using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationTests;

public sealed class IntegrationTestFixture
    : IAsyncLifetime
{
    private KeycloakContainer _keycloak = null!;
    private PostgreSqlContainer _postgres = null!;

    private WebApplicationFactory<Program> _factory = null!;

    public HttpClient CreateClient() => _factory.CreateClient();

    public async ValueTask InitializeAsync()
    {
        var realmPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
            "keycloak", "realms", "einsatzbereit-realm.json"));

        _postgres = new PostgreSqlBuilder()
            .WithDatabase("einsatzbereit")
            .WithUsername("einsatzbereit")
            .WithPassword("einsatzbereit")
            .Build();

        _keycloak = new KeycloakBuilder()
            .WithResourceMapping(realmPath, "/opt/keycloak/data/import")
            .WithCommand("--import-realm")
            .Build();

        await Task.WhenAll(_postgres.StartAsync(), _keycloak.StartAsync());

        var authority = $"{_keycloak.GetBaseAddress()}realms/einsatzbereit";

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:Authority"] = authority,
                        ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString()
                    });
                });
                builder.UseEnvironment("Development");
            });
    }

    public async Task<string> GetAccessTokenAsync(string username, string password)
    {
        using var http = new HttpClient();
        var tokenUrl = $"{_keycloak.GetBaseAddress()}realms/einsatzbereit/protocol/openid-connect/token";

        var response = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "frontend",
            ["username"] = username,
            ["password"] = password
        }));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()!;
    }

    #pragma warning disable CA1816 // No finalizer needed
    public async ValueTask DisposeAsync()
    #pragma warning restore CA1816
    {
        await _factory.DisposeAsync();
        await _keycloak.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
