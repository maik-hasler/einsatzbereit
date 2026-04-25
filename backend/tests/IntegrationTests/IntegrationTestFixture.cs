using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
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
    private const string Realm = "einsatzbereit";
    private const string FrontendClientId = "frontend";
    private const string BackendClientId = "backend";
    private const string BackendClientSecret = "backend-secret";

    private DistributedApplication _app = null!;
    private Respawner _respawner = null!;
    private string _connectionString = null!;
    private HttpClient _keycloakClient = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<AppHost>();

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();

        await notifications
            .WaitForResourceAsync("keycloak", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(180));

        await notifications
            .WaitForResourceAsync("backend", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(180));

        _keycloakClient = _app.CreateHttpClient("keycloak");

        await WaitForRealmReadyAsync();

        _connectionString = await _app.GetConnectionStringAsync("einsatzbereit")
            ?? throw new InvalidOperationException("Connection string 'einsatzbereit' not found.");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
        });

        await _respawner.ResetAsync(conn);
        await ResetKeycloakOrganizationsAsync();
    }

    public async ValueTask DisposeAsync() =>
        await _app.DisposeAsync();

    public HttpClient CreateHttpClient() =>
        _app.CreateHttpClient("backend");

    public async Task<string> GetAccessTokenAsync(string username, string password)
    {
        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", FrontendClientId),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
            new KeyValuePair<string, string>("scope", "openid"),
        ]);

        var response = await _keycloakClient.PostAsync(
            $"/realms/{Realm}/protocol/openid-connect/token", content);

        await EnsureSuccessAsync(response);

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>()
            ?? throw new InvalidOperationException("Keycloak returned no token.");

        return token.AccessToken;
    }

    public async Task ResetDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    public async Task ResetAsync()
    {
        await ResetDatabaseAsync();
        await ResetKeycloakOrganizationsAsync();
    }

    public async Task ResetKeycloakOrganizationsAsync()
    {
        var adminToken = await GetAdminTokenAsync();

        using var listRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/admin/realms/{Realm}/organizations?max=1000");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var listResponse = await _keycloakClient.SendAsync(listRequest);
        await EnsureSuccessAsync(listResponse);

        var orgs = await listResponse.Content.ReadFromJsonAsync<List<KeycloakOrganization>>()
            ?? [];

        foreach (var org in orgs)
        {
            using var deleteRequest = new HttpRequestMessage(
                HttpMethod.Delete, $"/admin/realms/{Realm}/organizations/{org.Id}");
            deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var deleteResponse = await _keycloakClient.SendAsync(deleteRequest);
            await EnsureSuccessAsync(deleteResponse);
        }
    }

    public Task WaitForResourceAsync(string resourceName) =>
        _app.Services
            .GetRequiredService<ResourceNotificationService>()
            .WaitForResourceAsync(resourceName, KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(60));

    private async Task<string> GetAdminTokenAsync()
    {
        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", BackendClientId),
            new KeyValuePair<string, string>("client_secret", BackendClientSecret),
        ]);

        var response = await _keycloakClient.PostAsync(
            $"/realms/{Realm}/protocol/openid-connect/token", content);

        await EnsureSuccessAsync(response);

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>()
            ?? throw new InvalidOperationException("Keycloak returned no admin token.");

        return token.AccessToken;
    }

    private async Task WaitForRealmReadyAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await _keycloakClient.GetAsync(
                    $"/realms/{Realm}/.well-known/openid-configuration");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
            }
            await Task.Delay(1000);
        }

        throw new TimeoutException($"Keycloak realm '{Realm}' did not become ready in time.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Keycloak {(int)response.StatusCode} for {response.RequestMessage?.Method} " +
            $"{response.RequestMessage?.RequestUri}: {body}");
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record KeycloakOrganization(string Id, string Name);
}
