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

	private DistributedApplication _app = null!;
	private string _connectionString = null!;
	private HttpClient _keycloakClient = null!;

	// Vera's Keycloak user id, resolved once at boot (see CaptureBaselineSnapshotAsync).
	// ResetAsync scopes every mutation to this id specifically - see ResetAsync's
	// own doc comment for why nothing here touches organization/opportunity/
	// engagement/invitation/layout rows at large, only vera's own.
	private Guid _veraUserId;

	// Captured once, right after the backend's own startup
	// (ASPNETCORE_ENVIRONMENT=Development, forced by AppHost.cs even under
	// "Testing") has already run ApplicationDbContextInitializer.MigrateAsync +
	// SeedAsync - i.e. before any test has created a throwaway org. Each
	// organizer's alphabetically-first organized org, matching activeOrg.ts's
	// resolveActiveOrg tie-break exactly. AuthHelper.FastSignInAsync uses this
	// to pin the active-org cookie to a real, stable org id instead of letting
	// the frontend's own alphabetical fallback pick whatever throwaway org some
	// concurrently-running test happens to have created first under the same
	// shared account. Capturing it here means no name-based filter is ever
	// needed to tell a seed org apart from one a test created - there simply
	// aren't any others yet.
	private Dictionary<Guid, Guid> _pinnedOrganizerOrgByUserId = null!;

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

	// Restores vera's own account state for tests that need it deterministic
	// (#1316) - opt in via [Before(Test)] fixture.ResetAsync() plus a keyed
	// [NotInParallel("visualtests-db")] on the class.
	//
	// An earlier version of this method also restored the organization/
	// organization_invitation/organization_dashboard_layout tables to a
	// baseline snapshot (delete every row outside a captured set of ids).
	// That is unsafe at any scope wider than "vera's own rows": two of those
	// three tables have zero seed rows, so "delete everything outside the
	// baseline" degenerates into an unconditional DELETE FROM table - wiping
	// whatever any other, concurrently running test (most of the suite is
	// NOT keyed into "visualtests-db" and keeps running while this executes)
	// had just created there, e.g. OrgDashboardCustomizeTests' saved
	// dashboard layouts. None of the 3 classes that call this actually assert
	// anything about those tables' contents - AuthHelper's pinned-org
	// navigation already makes which *org* every test lands on deterministic,
	// independent of this reset. The only state that's genuinely global,
	// shared, and something these classes depend on is vera's own Organizer-
	// role membership/Keycloak role (she must never appear to organize
	// anything), so that's the only thing restored here - scoped to her user
	// id specifically, never touching another user's rows or any shared
	// table at large.
	public async Task ResetAsync()
	{
		await ResetVeraOrganizerMembershipAsync();
		await ResetVeraOrganisatorRoleAsync();
	}

	public Guid? GetPinnedOrganizerOrganizationId(Guid userId) =>
		_pinnedOrganizerOrgByUserId.TryGetValue(userId, out var organizationId) ? organizationId : null;

	// Live re-query of userId's alphabetically-first Organizer org, matching
	// activeOrg.ts's resolveActiveOrg fallback - unlike GetPinnedOrganizerOrganizationId
	// above (a one-time snapshot from fixture boot, valid only because no other
	// test had created any orgs yet), this re-runs the same query against
	// current state. OrganizationDashboardNavLinkTests's deliberately-unpinned
	// resolution-order test needs this: AchievementsTests (see its own doc
	// comment) permanently adds two more Organizer orgs for olaf with no
	// cleanup, sorting ahead of the seeded one, the instant it runs anywhere
	// in the same suite run - comparing against the frozen boot-time snapshot
	// instead of current state made that test racy against test ordering, not
	// concurrency.
	public async Task<Guid?> GetCurrentFirstOrganizerOrganizationIdAsync(Guid userId)
	{
		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();
		var current = await ReadPinnedOrganizerOrgsAsync(conn);
		return current.TryGetValue(userId, out var organizationId) ? organizationId : null;
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

		// Keycloak-side membership alone isn't enough: command handlers like
		// ChangeMemberRoleCommandHandler resolve membership via the local
		// organization_membership table (dbContext.GetMembershipAsync), not
		// Keycloak, so this escape hatch must seed that row too or those
		// handlers 404 on a member this call just added.
		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync(cancellationToken);
		await using var cmd = new NpgsqlCommand(
			"""
			INSERT INTO organization_membership (id, organization_id, user_id, role, created_on)
			VALUES (@id, @organizationId, @userId, @role, now())
			ON CONFLICT (organization_id, user_id) DO NOTHING
			""", conn);
		cmd.Parameters.AddWithValue("id", Guid.CreateVersion7());
		cmd.Parameters.AddWithValue("organizationId", organizationId);
		cmd.Parameters.AddWithValue("userId", userId);
		cmd.Parameters.AddWithValue("role", "Member");
		await cmd.ExecuteNonQueryAsync(cancellationToken);
	}

	// Olaf's well-known seed user id (ApplicationDbContextInitializer.OlafId,
	// an internal constant in a different assembly - duplicated here rather
	// than exposed cross-assembly since it's just a fixed literal). Exposed
	// publicly (not just used internally) so callers needing his id directly -
	// e.g. GetCurrentFirstOrganizerOrganizationIdAsync above - don't need a
	// throwaway SignInAsync call just to decode it back off a token.
	public static Guid OlafId { get; } = new("00000000-0000-0000-0000-000000000001");

	private async Task CaptureBaselineSnapshotAsync()
	{
		// Resolve vera's Keycloak user id once - discard the token, this call
		// exists only to decode "sub" off it (see SignInAsync).
		var vera = await SignInAsync("vera", "vera123");
		_veraUserId = vera.UserId;

		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();

		_pinnedOrganizerOrgByUserId = await ReadPinnedOrganizerOrgsAsync(conn);

		// ApplicationDbContextInitializer.SeedAsync wraps its Keycloak-dependent
		// org/membership seeding in a catch-and-log-only block with no retry
		// (a transient Keycloak hiccup during backend startup silently leaves
		// olaf's Organizer memberships un-seeded, no exception surfaced anywhere)
		// - if that happened, every downstream test that dereferences
		// FastSignInAsync's pinned-org id for olaf would fail with a nullref
		// deep into the run, minutes later, with no obvious connection back to
		// this. Fail loudly here instead, at fixture boot, with a message that
		// actually points at the cause.
		if (!_pinnedOrganizerOrgByUserId.ContainsKey(OlafId))
			throw new InvalidOperationException(
				"Seed data has no Organizer-role organization membership for olaf "
				+ $"(user id {OlafId}). ApplicationDbContextInitializer.SeedAsync likely "
				+ "failed partway through (it swallows exceptions from its Keycloak-dependent "
				+ "seed calls and logs rather than rethrowing) - check the backend resource's "
				+ "startup logs for \"An exception occurred while seeding the database\".");
	}

	// Ordered by name so the first row seen per user is exactly the org
	// resolveActiveOrg (activeOrg.ts) would fall back to for that user on a
	// clean database - no name filter needed since nothing but seed data
	// exists at the point this runs.
	private static async Task<Dictionary<Guid, Guid>> ReadPinnedOrganizerOrgsAsync(NpgsqlConnection conn)
	{
		var pinned = new Dictionary<Guid, Guid>();
		await using var cmd = new NpgsqlCommand(
			"""
			SELECT m.user_id, o.id
			FROM organization_membership AS m
			JOIN organization AS o ON o.id = m.organization_id
			WHERE m.role = 'Organizer' AND NOT o.is_deleted
			ORDER BY m.user_id, o.name
			""", conn);
		await using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			var userId = reader.GetGuid(0);
			if (!pinned.ContainsKey(userId))
				pinned[userId] = reader.GetGuid(1);
		}
		return pinned;
	}

	// Vera never organizes anything in seed data (SeedOrg1Async/SeedOrg2Async
	// only ever assign olaf as Organizer) - remove any Organizer-role
	// membership row a test granted her, in any organization. Scoped to her
	// user id only via the WHERE clause, so this can never touch another
	// user's membership row or race a concurrently running test that isn't
	// touching vera's own account.
	private async Task ResetVeraOrganizerMembershipAsync()
	{
		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();
		await using var cmd = new NpgsqlCommand(
			"""
			DELETE FROM organization_membership
			WHERE user_id = @userId AND role = 'Organizer'
			""", conn);
		cmd.Parameters.AddWithValue("userId", _veraUserId);
		await cmd.ExecuteNonQueryAsync();
	}

	// Some tests grant vera the realm-level "organisator" role by having her
	// create an organization (CreateOrganization assigns it to the creator).
	// That role is global and outlives any per-organization cleanup, leaking
	// into later tests in the shared session and breaking assumptions that
	// vera is not an organizer. Revoking a role mapping a user doesn't
	// currently hold is a no-op in Keycloak's admin API, so this is safe to
	// call unconditionally rather than checking first.
	private async Task ResetVeraOrganisatorRoleAsync()
	{
		var adminToken = await GetAdminTokenAsync();

		using var roleRequest = new HttpRequestMessage(
			HttpMethod.Get, $"/admin/realms/{Realm}/roles/{OrganisatorRole}");
		roleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var roleResponse = await _keycloakClient.SendAsync(roleRequest);
		await EnsureSuccessAsync(roleResponse);

		var organisatorRole = await roleResponse.Content.ReadFromJsonAsync<KeycloakRole>()
			?? throw new InvalidOperationException("Keycloak role 'organisator' not found.");

		using var deleteRequest = new HttpRequestMessage(
			HttpMethod.Delete, $"/admin/realms/{Realm}/users/{_veraUserId}/role-mappings/realm")
		{
			Content = JsonContent.Create(new[] { new { id = organisatorRole.Id, name = organisatorRole.Name } }),
		};
		deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var deleteResponse = await _keycloakClient.SendAsync(deleteRequest);
		await EnsureSuccessAsync(deleteResponse);
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
	// into the browser's sessionStorage instead of driving Keycloak's login form.
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
		// callers to look it up separately) since both #825-style regression
		// tests and AuthHelper.FastSignInAsync's active-org pin need the
		// signed-in user's own Guid.
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

	private sealed record KeycloakRole(string Id, string Name);
}

public sealed record KeycloakSession(
	string AccessToken,
	string IdToken,
	string? RefreshToken,
	int ExpiresIn,
	string TokenType,
	string Authority,
	Guid UserId);
