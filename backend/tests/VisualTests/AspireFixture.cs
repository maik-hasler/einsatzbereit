using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Projects;
using TUnit.Core.Interfaces;

namespace VisualTests;

public class AspireFixture : IAsyncInitializer, IAsyncDisposable
{
	private const string Realm = "einsatzbereit";

	private DistributedApplication _app = null!;
	private string _connectionString = null!;

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
				.WaitAsync(TimeSpan.FromMinutes(5));
		}

		await WaitForRealmReadyAsync();
		await WaitForBackendReadyAsync();

		_connectionString = await _app.GetConnectionStringAsync("einsatzbereit")
			?? throw new InvalidOperationException("Connection string 'einsatzbereit' not found.");
	}

	// Test-only escape hatch to simulate an opportunity row removed without
	// going through the command handler that cancels its engagements first -
	// e.g. data predating that cancellation safeguard (#703).
	public async Task DeleteOpportunityRowDirectlyAsync(Guid opportunityId)
	{
		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();
		await using var cmd = new NpgsqlCommand(
			"DELETE FROM volunteer_opportunity WHERE id = @id", conn);
		cmd.Parameters.AddWithValue("id", opportunityId);
		await cmd.ExecuteNonQueryAsync();
	}

	private async Task WaitForBackendReadyAsync()
	{
		var backendEndpoint = _app.GetEndpoint("backend", "http");
		using var client = new HttpClient
		{
			BaseAddress = backendEndpoint,
			Timeout = TimeSpan.FromSeconds(5)
		};
		var deadline = DateTime.UtcNow.AddSeconds(120);
		while (DateTime.UtcNow < deadline)
		{
			try
			{
				var response = await client.GetAsync("/health");
				if (response.IsSuccessStatusCode)
					return;
			}
			catch (Exception)
			{
			}
			await Task.Delay(1000);
		}

		throw new TimeoutException("Backend did not become healthy in time.");
	}

	private async Task WaitForRealmReadyAsync()
	{
		using var client = _app.CreateHttpClient("keycloak");
		var deadline = DateTime.UtcNow.AddSeconds(300);
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
