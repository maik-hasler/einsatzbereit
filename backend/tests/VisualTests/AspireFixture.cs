using System.Net.Http.Json;
using System.Text.Json.Serialization;
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

	// ROPC-enabled test-only client (see keycloak/AGENTS.md) - same one
	// IntegrationTestFixture.GetAccessTokenAsync already uses. Its protocol
	// mappers (roles, realm-name, backend-audience) are documented as
	// identical to the real "frontend" client's, which is what makes minting
	// through it here a valid stand-in for a real browser login - see
	// JwtAudienceTests, which keeps a real login specifically to guard that
	// the two clients' mappers don't drift apart.
	private const string FrontendTestClientId = "frontend-test";

	private DistributedApplication _app = null!;
	private string _connectionString = null!;

	public async Task InitializeAsync()
	{
		// DistributedApplicationTestingBuilder.CreateAsync<AppHost>() defaults the
		// AppHost's own hosting environment to "Development", not "Testing" -
		// passing --environment explicitly is required for AppHost.cs's isTestEnv
		// gate (skipping the Postgres data volume, pointing Geocoding__BaseUrl at
		// an unroutable address for #975) to actually activate during test runs.
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<AppHost>(["--environment", "Testing"]);

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

	// Mints a real Keycloak token via direct grant (ROPC), bypassing the
	// interactive login UI - see AuthHelper.FastSignInAsync, which seeds this
	// into the browser's localStorage instead of driving Keycloak's login form.
	public async Task<KeycloakSession> SignInAsync(string username, string password)
	{
		using var client = _app.CreateHttpClient("keycloak");

		var content = new FormUrlEncodedContent([
			new KeyValuePair<string, string>("grant_type", "password"),
			new KeyValuePair<string, string>("client_id", FrontendTestClientId),
			new KeyValuePair<string, string>("username", username),
			new KeyValuePair<string, string>("password", password),
			new KeyValuePair<string, string>("scope", "openid"),
		]);

		var response = await client.PostAsync(
			$"/realms/{Realm}/protocol/openid-connect/token", content);
		response.EnsureSuccessStatusCode();

		var token = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>()
			?? throw new InvalidOperationException("Keycloak returned no token.");

		// Must match VITE_KEYCLOAK_AUTHORITY_URL exactly (AppHost.cs builds it the
		// same way: "{keycloakEndpoint}/realms/{realm}") since it becomes part of
		// oidc-client-ts's storage key. GetEndpoint(...).ToString() carries a
		// trailing slash (System.Uri's default for a bare authority) that Aspire's
		// EndpointReference interpolation does not, so it must be trimmed first
		// or the concatenation produces a double slash and a key that never matches.
		var authority = $"{GetEndpoint("keycloak").ToString().TrimEnd('/')}/realms/{Realm}";

		return new KeycloakSession(
			token.AccessToken, token.IdToken, token.RefreshToken, token.ExpiresIn, token.TokenType, authority);
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

	private sealed record KeycloakTokenResponse(
		[property: JsonPropertyName("access_token")] string AccessToken,
		[property: JsonPropertyName("id_token")] string IdToken,
		[property: JsonPropertyName("refresh_token")] string? RefreshToken,
		[property: JsonPropertyName("expires_in")] int ExpiresIn,
		[property: JsonPropertyName("token_type")] string TokenType);
}

public sealed record KeycloakSession(
	string AccessToken,
	string IdToken,
	string? RefreshToken,
	int ExpiresIn,
	string TokenType,
	string Authority);
