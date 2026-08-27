using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Projects;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

// A separate, minimal Aspire boot from IntegrationTestFixture's - AppHost.cs pins the shared
// fixture's backend to ASPNETCORE_ENVIRONMENT=Development for all of its ~70 test classes, and
// that value is fixed for the lifetime of one Aspire boot. Testing the non-Development branch
// of Program.cs (RequiredConfigurationValidator, Database:MigrateOnStartup, Database:SeedOnStartup,
// HSTS) genuinely needs its own instance rather than a flag on the shared one (#2204).
public class ProductionEnvironmentFixture
	: IAsyncInitializer,
	IAsyncDisposable
{
	private DistributedApplication _app = null!;

	public async Task InitializeAsync()
	{
		var appHost = await DistributedApplicationTestingBuilder
			.CreateAsync<AppHost>([
				"--environment", "Testing",
				"--Testing:BackendAspNetCoreEnvironment=Production",
				"--Database:MigrateOnStartup=true",
				"--Database:SeedOnStartup=true",
				// This Aspire test network's Keycloak is always plain HTTP - no TLS
				// termination in front of it, unlike a real deployment's Authority. Without
				// this, JwtBearerHandler throws "MetadataAddress or Authority must use
				// HTTPS" on every request (including anonymous ones, since authentication
				// runs before endpoint routing), which crashed the whole app before it
				// could answer /alive. See Program.cs's comment on RequireHttpsMetadata.
				"--Authentication:RequireHttpsMetadata=false",
			]);

		_app = await appHost.BuildAsync();
		await _app.StartAsync();

		var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();

		await notifications
			.WaitForResourceAsync("keycloak", KnownResourceStates.Running)
			.WaitAsync(TimeSpan.FromMinutes(5));

		await notifications
			.WaitForResourceAsync("backend", KnownResourceStates.Running)
			.WaitAsync(TimeSpan.FromMinutes(5));

		await WaitForBackendReadyAsync(CreateHttpClient());
	}

	public async ValueTask DisposeAsync() =>
		await _app.DisposeAsync();

	public HttpClient CreateHttpClient() =>
		_app.CreateHttpClient("backend", "http");

	private static async Task WaitForBackendReadyAsync(HttpClient client)
	{
		var deadline = DateTime.UtcNow.AddSeconds(300);
		while (DateTime.UtcNow < deadline)
		{
			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				var response = await client.GetAsync("/alive", cts.Token);
				if (response.IsSuccessStatusCode)
					return;
			}
			catch (Exception) { }
			await Task.Delay(1000);
		}
		throw new TimeoutException("Backend did not become ready in time.");
	}
}
