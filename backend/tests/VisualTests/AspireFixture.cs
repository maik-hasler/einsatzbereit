using System.Net.Http.Headers;
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

	private const string BackendClientId = "backend";
	private const string BackendClientSecret = "backend-secret";
	private const string OrganisatorRole = "organisator";

	// Matches ApplicationDbContextInitializer.SeedAsync, which assigns olaf
	// (not vera) as Organizer of both seed organizations - the one user
	// ResetKeycloakOrganisatorRolesAsync must never revoke the role from.
	private const string BaselineOrganisator = "olaf";

	private DistributedApplication _app = null!;
	private string _connectionString = null!;
	private HttpClient _keycloakClient = null!;

	// Snapshotted once in InitializeAsync, right after the backend's own
	// startup (ASPNETCORE_ENVIRONMENT=Development, forced by AppHost.cs even
	// under "Testing") has already run ApplicationDbContextInitializer.MigrateAsync
	// + SeedAsync. ResetAsync (#1316) restores each table to exactly this set of
	// row ids instead of wiping it - VisualTests, unlike IntegrationTests, has
	// ~180 tests that assume the two seed organizations/opportunities/vera's
	// seed engagements stay present for the whole shared session, so a blanket
	// Respawn-style wipe would destroy state other, unrelated tests still need.
	private HashSet<Guid> _baselineOrganizationIds = null!;
	private HashSet<Guid> _baselineOrganizationMembershipIds = null!;
	private HashSet<Guid> _baselineOrganizationInvitationIds = null!;
	private HashSet<Guid> _baselineOrganizationDashboardLayoutIds = null!;
	private HashSet<Guid> _baselineVolunteerOpportunityIds = null!;
	private HashSet<Guid> _baselineEngagementIds = null!;

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

		_keycloakClient = _app.CreateHttpClient("keycloak");

		await CaptureBaselineSnapshotAsync();
	}

	// Restores the shared session to its baseline for tests that need
	// deterministic organization/membership/role state (#1316) - opt in via
	// [Before(Test)] fixture.ResetAsync() plus a bare [NotInParallel] on the
	// class, since this mutates state every other concurrently running
	// VisualTest could also be reading or writing.
	public async Task ResetAsync()
	{
		await ResetDatabaseToBaselineAsync();
		await ResetKeycloakOrganizationsToBaselineAsync();
		await ResetKeycloakOrganisatorRolesAsync();
	}

	// Test-only escape hatch replicating what the now-removed admin-only
	// AddMember endpoint did (#810): add a user to a Keycloak organization as a
	// plain member, without granting the Organizer role. Accepting an
	// invitation grants Organizer too (#826), so it's the only way left to
	// reconstruct a plain-member-only state for regression tests (#825).
	public async Task AddPlainMemberDirectlyAsync(
		Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
	{
		var adminToken = await GetAdminTokenAsync(cancellationToken);

		using var request = new HttpRequestMessage(
			HttpMethod.Post, $"/admin/realms/{Realm}/organizations/{organizationId}/members")
		{
			Content = JsonContent.Create(userId.ToString()),
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var response = await _keycloakClient.SendAsync(request, cancellationToken);
		await EnsureSuccessAsync(response);
	}

	private async Task CaptureBaselineSnapshotAsync()
	{
		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();

		_baselineOrganizationIds = await ReadIdsAsync(conn, "organization");
		_baselineOrganizationMembershipIds = await ReadIdsAsync(conn, "organization_membership");
		_baselineOrganizationInvitationIds = await ReadIdsAsync(conn, "organization_invitation");
		_baselineOrganizationDashboardLayoutIds = await ReadIdsAsync(conn, "organization_dashboard_layout");
		_baselineVolunteerOpportunityIds = await ReadIdsAsync(conn, "volunteer_opportunity");
		_baselineEngagementIds = await ReadIdsAsync(conn, "engagement");
	}

	private static async Task<HashSet<Guid>> ReadIdsAsync(NpgsqlConnection conn, string table)
	{
		var ids = new HashSet<Guid>();
		// Table name is a trusted call-site literal, not user input (see
		// IntegrationTestFixture.CountRowsWhereAsync for the same precedent).
		await using var cmd = new NpgsqlCommand($"SELECT id FROM \"{table}\"", conn);
		await using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			ids.Add(reader.GetGuid(0));
		return ids;
	}

	// Deletes every row outside the baseline snapshot from each table that
	// #1316's affected tests can pollute. No Respawn package (which would
	// truncate whole tables, wiping the seed data other tests still depend
	// on) - and no FK constraints exist between these tables (verified
	// against the EF migrations; only time_slot -> volunteer_opportunity
	// cascades), so per-table baseline deletes are safe in any order.
	private async Task ResetDatabaseToBaselineAsync()
	{
		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();

		await DeleteRowsOutsideBaselineAsync(conn, "organization_membership", _baselineOrganizationMembershipIds);
		await DeleteRowsOutsideBaselineAsync(conn, "organization_invitation", _baselineOrganizationInvitationIds);
		await DeleteRowsOutsideBaselineAsync(
			conn, "organization_dashboard_layout", _baselineOrganizationDashboardLayoutIds);
		await DeleteRowsOutsideBaselineAsync(conn, "engagement", _baselineEngagementIds);
		await DeleteRowsOutsideBaselineAsync(conn, "volunteer_opportunity", _baselineVolunteerOpportunityIds);
		await DeleteRowsOutsideBaselineAsync(conn, "organization", _baselineOrganizationIds);
	}

	private static async Task DeleteRowsOutsideBaselineAsync(
		NpgsqlConnection conn, string table, HashSet<Guid> baselineIds)
	{
		var sql = baselineIds.Count == 0
			? $"DELETE FROM \"{table}\""
			: $"DELETE FROM \"{table}\" WHERE id <> ALL(@baselineIds)";
		await using var cmd = new NpgsqlCommand(sql, conn);
		if (baselineIds.Count > 0)
			cmd.Parameters.AddWithValue("baselineIds", baselineIds.ToArray());
		await cmd.ExecuteNonQueryAsync();
	}

	// Mirrors IntegrationTestFixture.ResetKeycloakOrganizationsAsync, but
	// restores to the baseline snapshot instead of deleting every
	// organization - VisualTests' two seed organizations must survive.
	private async Task ResetKeycloakOrganizationsToBaselineAsync()
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
			if (_baselineOrganizationIds.Contains(Guid.Parse(org.Id)))
				continue;

			using var deleteRequest = new HttpRequestMessage(
				HttpMethod.Delete, $"/admin/realms/{Realm}/organizations/{org.Id}");
			deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

			var deleteResponse = await _keycloakClient.SendAsync(deleteRequest);
			await EnsureSuccessAsync(deleteResponse);
		}
	}

	// Ported from IntegrationTestFixture.ResetKeycloakOrganisatorRolesAsync:
	// some tests grant a user the realm-level "organisator" role by creating
	// an organization (CreateOrganization assigns it to the creator). That
	// role is global and outlives any per-organization cleanup, leaking into
	// later tests in the shared session and breaking assumptions that, for
	// example, vera is not an organizer. Revoke it from every non-baseline
	// user to restore the imported baseline.
	private async Task ResetKeycloakOrganisatorRolesAsync()
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

	private async Task<string> GetAdminTokenAsync(CancellationToken cancellationToken = default)
	{
		var content = new FormUrlEncodedContent([
			new KeyValuePair<string, string>("grant_type", "client_credentials"),
			new KeyValuePair<string, string>("client_id", BackendClientId),
			new KeyValuePair<string, string>("client_secret", BackendClientSecret),
		]);

		var response = await _keycloakClient.PostAsync(
			$"/realms/{Realm}/protocol/openid-connect/token", content, cancellationToken);

		await EnsureSuccessAsync(response);

		var token = await response.Content.ReadFromJsonAsync<AdminTokenResponse>(cancellationToken: cancellationToken)
			?? throw new InvalidOperationException("Keycloak returned no admin token.");

		return token.AccessToken;
	}

	private static async Task EnsureSuccessAsync(HttpResponseMessage response)
	{
		if (response.IsSuccessStatusCode) return;

		var body = await response.Content.ReadAsStringAsync();
		throw new HttpRequestException(
			$"Keycloak {(int)response.StatusCode} for {response.RequestMessage?.Method} "
			+ $"{response.RequestMessage?.RequestUri}: {body}");
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

		// "sub" is the Keycloak user id - decoded here (rather than requiring
		// callers to look it up separately) since #825-style regression tests
		// need the signed-in user's own Guid to call escape hatches like
		// AddPlainMemberDirectlyAsync.
		var userId = Guid.Parse(
			AuthHelper.DecodeJwtPayload(token.IdToken).GetProperty("sub").GetString()!);

		return new KeycloakSession(
			token.AccessToken, token.IdToken, token.RefreshToken, token.ExpiresIn, token.TokenType, authority, userId);
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

	private sealed record AdminTokenResponse(
		[property: JsonPropertyName("access_token")] string AccessToken);

	private sealed record KeycloakOrganization(string Id, string Name);

	private sealed record KeycloakRole(string Id, string Name);

	private sealed record KeycloakUser(string Id, string Username);
}

public sealed record KeycloakSession(
	string AccessToken,
	string IdToken,
	string? RefreshToken,
	int ExpiresIn,
	string TokenType,
	string Authority,
	Guid UserId);
