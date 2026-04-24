using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Projects;
using Respawn;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

public class IntegrationTestFixture
    : IAsyncInitializer,
    IAsyncDisposable
{
    private DistributedApplication _app = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<AppHost>();

        var frontend = appHost.Resources.FirstOrDefault(r => r.Name == "frontend");
        if (frontend is not null)
            appHost.Resources.Remove(frontend);

        appHost.Services.ConfigureHttpClientDefaults(http =>
            http.AddStandardResilienceHandler());

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        await _app.Services
            .GetRequiredService<ResourceNotificationService>()
            .WaitForResourceAsync("backend", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(120));
    }
    
    public async ValueTask DisposeAsync() =>
        await _app.DisposeAsync();

    public HttpClient CreateHttpClient() =>
        _app.CreateHttpClient("backend");

    public Task<string> GetAccessTokenAsync(string username, string password) => Task.FromResult("");

    /*public async Task<string> GetAccessTokenAsync(string username, string password)
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
    */

    public Task WaitForResouceAsync(string resourceName)
    {
        return _app.Services
            .GetRequiredService<ResourceNotificationService>()
            .WaitForResourceAsync(resourceName, KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(60));
    }
}
