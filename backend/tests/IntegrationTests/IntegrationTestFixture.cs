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
	private const string FrontendClientId = "frontend-test";
	private const string BackendClientId = "backend";
	private const string BackendClientSecret = "backend-secret";
	private const string OrganisatorRole = "organisator";
	private const string DefaultUserRole = "user";
	private const string BaselineOrganisator = "olaf";

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
			.WaitAsync(TimeSpan.FromMinutes(5));

		await notifications
			.WaitForResourceAsync("backend", KnownResourceStates.Running)
			.WaitAsync(TimeSpan.FromMinutes(5));

		_keycloakClient = _app.CreateHttpClient("keycloak");

		var backendClient = _app.CreateHttpClient("backend", "http");
		await WaitForBackendReadyAsync(backendClient);

		await WaitForRealmReadyAsync();

		_connectionString = await _app.GetConnectionStringAsync("einsatzbereit")
			?? throw new InvalidOperationException("Connection string 'einsatzbereit' not found.");

		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();

		_respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
		{
			DbAdapter = DbAdapter.Postgres,
			SchemasToInclude = ["public"],
			TablesToIgnore = ["__EFMigrationsHistory"],
		});

		await _respawner.ResetAsync(conn);
		await ResetKeycloakOrganizationsAsync();
		await ResetKeycloakOrganisatorRolesAsync();
	}

	public async ValueTask DisposeAsync() =>
		await _app.DisposeAsync();

	public HttpClient CreateHttpClient() =>
		_app.CreateHttpClient("backend", "http");

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

	// Test-only helper for asserting hard-deletes/anonymization at the DB level,
	// e.g. proving a notification or user row no longer exists after an account
	// deletion (#829) - there's no API to observe another user's rows directly.
	// Column names are trusted call-site literals, not user input, so building
	// the SQL string is fine here even though it wouldn't be for production code.
	public async Task<int> CountRowsWhereAsync(string table, string column, Guid value)
	{
		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();
		await using var cmd = new NpgsqlCommand(
			$"SELECT COUNT(*) FROM \"{table}\" WHERE {column} = @value", conn);
		cmd.Parameters.AddWithValue("value", value);
		var count = await cmd.ExecuteScalarAsync();
		return Convert.ToInt32(count);
	}

	public async Task ResetAsync()
	{
		await ResetDatabaseAsync();
		await ResetKeycloakOrganizationsAsync();
		await ResetKeycloakOrganisatorRolesAsync();
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

	// Some tests grant a user the realm-level "organisator" role by creating an
	// organization (CreateOrganization assigns it to the creator). That role is
	// global and survives ResetKeycloakOrganizationsAsync, which only deletes
	// organizations - so it leaks into later tests in the shared session and
	// breaks assumptions that, for example, vera is not an organisator. Revoke it
	// from every non-baseline user between tests to restore the imported baseline.
	public async Task ResetKeycloakOrganisatorRolesAsync()
	{
		var adminToken = await GetAdminTokenAsync();

		using var roleRequest = new HttpRequestMessage(
			HttpMethod.Get, $"/admin/realms/{Realm}/roles/{OrganisatorRole}");
		roleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var roleResponse = await _keycloakClient.SendAsync(roleRequest);
		await EnsureSuccessAsync(roleResponse);

		var organisatorRole = await roleResponse.Content.ReadFromJsonAsync<KeycloakRole>()
			?? throw new InvalidOperationException("Keycloak role 'organisator' not found.");

		using var usersRequest = new HttpRequestMessage(
			HttpMethod.Get, $"/admin/realms/{Realm}/roles/{OrganisatorRole}/users?max=1000");
		usersRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var usersResponse = await _keycloakClient.SendAsync(usersRequest);
		await EnsureSuccessAsync(usersResponse);

		var users = await usersResponse.Content.ReadFromJsonAsync<List<KeycloakUser>>() ?? [];

		foreach (var user in users)
		{
			if (string.Equals(user.Username, BaselineOrganisator, StringComparison.OrdinalIgnoreCase))
				continue;

			using var deleteRequest = new HttpRequestMessage(
				HttpMethod.Delete, $"/admin/realms/{Realm}/users/{user.Id}/role-mappings/realm")
			{
				Content = JsonContent.Create(new[] { new { id = organisatorRole.Id, name = organisatorRole.Name } }),
			};
			deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

			var deleteResponse = await _keycloakClient.SendAsync(deleteRequest);
			await EnsureSuccessAsync(deleteResponse);
		}
	}

	// Creates a brand-new, disposable Keycloak user with the realm's baseline
	// "user" role. This fixture is shared PerTestSession, so tests that need to
	// destructively mutate an account (e.g. deleting it, #829) must never touch
	// the shared vera/olaf/admin seed users - they should operate on a
	// throwaway account like this one instead. No cleanup is needed even if a
	// test fails mid-way: unlike the shared seed accounts, a leftover ephemeral
	// user cannot affect other tests' assumptions since nothing else
	// references it by name.
	public async Task<(Guid UserId, string Username, string Password)> CreateEphemeralUserAsync(
		CancellationToken cancellationToken = default)
	{
		var username = $"itest-{Guid.NewGuid():N}";
		const string password = "TestPass1";

		var adminToken = await GetAdminTokenAsync();

		using var createRequest = new HttpRequestMessage(
			HttpMethod.Post, $"/admin/realms/{Realm}/users")
		{
			Content = JsonContent.Create(new
			{
				username,
				enabled = true,
				emailVerified = true,
				email = $"{username}@example.com",
				credentials = new[]
				{
					new { type = "password", value = password, temporary = false },
				},
			}),
		};
		createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var createResponse = await _keycloakClient.SendAsync(createRequest, cancellationToken);
		await EnsureSuccessAsync(createResponse);

		var location = createResponse.Headers.Location
			?? throw new InvalidOperationException("Keycloak did not return a Location header for the new user.");
		var userId = location.Segments[^1];

		using var roleRequest = new HttpRequestMessage(
			HttpMethod.Get, $"/admin/realms/{Realm}/roles/{DefaultUserRole}");
		roleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var roleResponse = await _keycloakClient.SendAsync(roleRequest, cancellationToken);
		await EnsureSuccessAsync(roleResponse);

		var userRole = await roleResponse.Content.ReadFromJsonAsync<KeycloakRole>(cancellationToken)
			?? throw new InvalidOperationException("Keycloak role 'user' not found.");

		using var assignRequest = new HttpRequestMessage(
			HttpMethod.Post, $"/admin/realms/{Realm}/users/{userId}/role-mappings/realm")
		{
			Content = JsonContent.Create(new[] { new { id = userRole.Id, name = userRole.Name } }),
		};
		assignRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var assignResponse = await _keycloakClient.SendAsync(assignRequest, cancellationToken);
		await EnsureSuccessAsync(assignResponse);

		return (Guid.Parse(userId), username, password);
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

	private async Task WaitForRealmReadyAsync()
	{
		var deadline = DateTime.UtcNow.AddSeconds(300);
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

	private sealed record KeycloakRole(string Id, string Name);

	private sealed record KeycloakUser(string Id, string Username);
}
