using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Respawn;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationTests;

public sealed class IntegrationTestFixture
    : IAsyncLifetime
{
    private KeycloakContainer _keycloak = null!;
    private PostgreSqlContainer _postgres = null!;
    private Respawner _respawner = null!;

    private WebApplicationFactory<Program> _factory = null!;

    public WebApplicationFactory<Program> Factory => _factory;

    public HttpClient CreateClient() => _factory.CreateClient();

    public string KeycloakBaseAddress => _keycloak.GetBaseAddress();

    public async ValueTask InitializeAsync()
    {
        var realmPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
            "keycloak", "realms", "einsatzbereit-realm.json"));

        _postgres = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("einsatzbereit")
            .WithUsername("einsatzbereit")
            .WithPassword("einsatzbereit")
            .Build();

        _keycloak = new KeycloakBuilder("quay.io/keycloak/keycloak:26.5.6")
            .WithResourceMapping(realmPath, "/opt/keycloak/data/import")
            .WithCommand("--import-realm")
            .Build();

        await Task.WhenAll(_postgres.StartAsync(), _keycloak.StartAsync());

        var authority = $"{_keycloak.GetBaseAddress()}realms/einsatzbereit";

        // Force-load DatabaseMigrations assembly so EF can discover migrations at host startup
        _ = typeof(DatabaseMigrations.Migrations.Initial);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:Authority"] = authority,
                        ["ConnectionStrings:Database"] = _postgres.GetConnectionString(),
                        ["Keycloak:BaseUrl"] = _keycloak.GetBaseAddress().TrimEnd('/'),
                        ["Keycloak:Realm"] = "einsatzbereit",
                        ["Keycloak:ClientId"] = "backend",
                        ["Keycloak:ClientSecret"] = "backend-secret"
                    });
                });
                builder.UseEnvironment("Development");
            });

        // Trigger host startup — Program.cs runs MigrateAsync in Development
        _ = _factory.Services;

        _respawner = await CreateRespawnerAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await _respawner.ResetAsync(connection);
    }

    private async Task<Respawner> CreateRespawnerAsync()
    {
        await using var connection = await OpenConnectionAsync();
        return await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"]
        });
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        return connection;
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
