using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Projects;
using TUnit.Core.Interfaces;

namespace VisualTests;

public class AspireFixture : IAsyncInitializer, IAsyncDisposable
{
    private const string Realm = "einsatzbereit";

    private DistributedApplication _app = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<AppHost>();

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();

        foreach (var name in new[] { "keycloak", "backend", "frontend" })
        {
            await notifications
                .WaitForResourceAsync(name, KnownResourceStates.Running)
                .WaitAsync(TimeSpan.FromMinutes(3));
        }

        await WaitForRealmReadyAsync();
    }

    private async Task WaitForRealmReadyAsync()
    {
        using var client = _app.CreateHttpClient("keycloak");
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync(
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

    public Task WaitForResourceAsync(string resourceName) =>
        _app.Services
            .GetRequiredService<ResourceNotificationService>()
            .WaitForResourceAsync(resourceName, KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(60));

    public Uri GetEndpoint(string resource, string endpointName = "http") =>
        _app.GetEndpoint(resource, endpointName);

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
