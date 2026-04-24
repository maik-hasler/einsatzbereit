using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

public class IntegrationTestFixture
    : IAsyncInitializer,
    IAsyncDisposable
{
    private DistributedApplication _app = null!;
    
   // private Respawner _respawner = null!;

    public Task InitializeAsync()
    {
        /*var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<AppHost>();

        appHost.Services.ConfigureHttpClientDefaults(http =>
            http.AddStandardResilienceHandler());

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        await _app.WaitForResourceAsync("backend",
            targetState: "Running",
            timeout: TimeSpan.FromSeconds(120));

        _respawner = await CreateRespawnerAsync();*/
        
        return Task.CompletedTask;
    }

    public HttpClient CreateHttpClient()
        => _app.CreateHttpClient("backend");

    public Task<string> GetAccessTokenAsync(string username, string password) => Task.FromResult("");
    
    /*public HttpClient CreateHttpClient() => _app.CreateHttpClient("backend");

    public async Task ResetDatabaseAsync()
    {
        var cs = await _app.GetConnectionStringAsync("einsatzbereit");
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task<string> GetAccessTokenAsync(string username, string password)
    {
        var keycloakBase = _app.GetEndpoint("keycloak", "http").ToString().TrimEnd('/');
        var tokenUrl = $"{keycloakBase}/realms/einsatzbereit/protocol/openid-connect/token";

        using var http = new HttpClient();
        var response = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
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

    private async Task<Respawner> CreateRespawnerAsync()
    {
        var cs = await _app.GetConnectionStringAsync("einsatzbereit");
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        return await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"]
        });
    }
    */

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
