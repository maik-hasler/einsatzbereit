using System.Diagnostics;
using System.Globalization;
using System.Net;
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

	// Also referenced by dotnet.yml/publish.yml's "Report Aspire stack startup
	// cost" step - renaming it here means renaming it there.
	public const string BootTimingFileName = "aspire-boot-seconds.txt";

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

	// Each organizer's alphabetically-first organized org, matching
	// activeOrg.ts's resolveActiveOrg tie-break. Captured once after the
	// backend has run MigrateAsync + SeedAsync but before any test has created
	// a throwaway org, so no name-based filter is needed to tell a seed org
	// apart from a test's - there are no others yet.
	//
	// AuthHelper.FastSignInAsync pins the active-org cookie to this, rather
	// than letting the frontend's alphabetical fallback pick whichever
	// throwaway org a concurrent test created first under the same account.
	private Dictionary<Guid, Guid> _pinnedOrganizerOrgByUserId = null!;

	public async Task InitializeAsync()
	{
		// Sharding trades one Aspire stack for one per shard (einsatzbereit#2145),
		// so this boot is now a fixed cost paid N times rather than once - it is
		// what decides how many shards are still worth adding. Timing it here puts
		// the number in every visual-tests job log instead of leaving it to be
		// re-derived from step timestamps and TUnit's "[slow] still running"
		// lines, which is all that was available when the shard count was chosen.
		var bootStartedAt = Stopwatch.GetTimestamp();

		// DistributedApplicationTestingBuilder.CreateAsync<AppHost>() defaults the
		// AppHost's own hosting environment to "Development", not "Testing", so
		// --environment must be passed explicitly for AppHost.cs's isTestEnv gate
		// (skipping the Postgres data volume, pointing Geocoding__BaseUrl at an
		// unroutable address) to activate during test runs.
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
		await WarmKeycloakLoginPageAsync();
		await WaitForBackendReadyAsync();

		_connectionString = await _app.GetConnectionStringAsync("einsatzbereit")
			?? throw new InvalidOperationException("Connection string 'einsatzbereit' not found.");

		_keycloakClient = _app.CreateHttpClient("keycloak");

		await CaptureBaselineSnapshotAsync();

		// Written to a file rather than only Console.WriteLine because this runs
		// inside the first test that pulls the fixture in, and TUnit attributes
		// console output to that test's context - on a green run nothing would
		// ever print it. dotnet.yml/publish.yml read this file back into the job
		// summary after the run (see their "Report Aspire stack startup cost"
		// steps), so the number is in the log whether the shard passed or failed.
		var bootSeconds = Stopwatch.GetElapsedTime(bootStartedAt).TotalSeconds;
		Console.WriteLine($"[aspire-fixture] stack ready in {bootSeconds:F1}s");
		try
		{
			await File.WriteAllTextAsync(
				Path.Combine(AppContext.BaseDirectory, BootTimingFileName),
				bootSeconds.ToString("F1", CultureInfo.InvariantCulture));
		}
		catch (IOException)
		{
			// A timing breadcrumb is never worth failing a whole shard's boot over.
		}
	}

	// Restores vera's own account state for tests that need it deterministic -
	// opt in via [Before(Test)] fixture.ResetAsync() plus a keyed
	// [NotInParallel("visualtests-db")] on the class.
	//
	// Deliberately scoped to vera's rows and nothing wider. Resetting the
	// organization/invitation/dashboard_layout tables would be unsafe: two have
	// zero seed rows, so "delete everything outside the baseline" degenerates
	// into an unconditional DELETE FROM, wiping what a concurrent test just
	// created - most of the suite is not keyed into "visualtests-db" and keeps
	// running through this. The one shared thing callers depend on is vera's
	// organization membership and Keycloak organisator role: she must never
	// appear to organize or belong to anything.
	public async Task ResetAsync()
	{
		await ResetVeraOrganizationMembershipAsync();
		await ResetVeraOrganisatorRoleAsync();
	}

	public Guid? GetPinnedOrganizerOrganizationId(Guid userId) =>
		_pinnedOrganizerOrgByUserId.TryGetValue(userId, out var organizationId) ? organizationId : null;

	// Live re-query of userId's alphabetically-first Organizer org, matching
	// activeOrg.ts's resolveActiveOrg fallback - unlike
	// GetPinnedOrganizerOrganizationId above, which is a boot-time snapshot.
	// OrganizationDashboardNavLinkTests's deliberately-unpinned resolution-order
	// test needs current state: other classes permanently add Organizer orgs
	// for olaf with no cleanup, sorting ahead of the seeded one, which makes
	// the frozen snapshot racy against test ordering.
	public async Task<Guid?> GetCurrentFirstOrganizerOrganizationIdAsync(Guid userId)
	{
		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();
		var current = await ReadPinnedOrganizerOrgsAsync(conn);
		return current.TryGetValue(userId, out var organizationId) ? organizationId : null;
	}

	// Test-only escape hatch replicating what the now-removed admin-only
	// AddMember endpoint did: add a user to a Keycloak organization as a
	// plain member, without granting the Organizer role. Accepting an
	// invitation grants Organizer too, so it's the only way left to
	// reconstruct a plain-member-only state for regression tests.
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

	// Olaf's seed user id, duplicated from
	// ApplicationDbContextInitializer.OlafId rather than exposed cross-assembly
	// since it is a fixed literal. Public so callers can use his id directly
	// instead of a throwaway SignInAsync just to decode it off a token.
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

		// Program.cs's Development branch (which this AppHost forces the backend
		// into) logs a SeedAsync failure rather than rethrowing, so a transient
		// Keycloak hiccup at startup can leave olaf's Organizer memberships
		// un-seeded with no exception anywhere. Every test dereferencing
		// FastSignInAsync's pinned-org id would then nullref minutes later with no
		// obvious connection back here - fail loudly at fixture boot instead.
		if (!_pinnedOrganizerOrgByUserId.ContainsKey(OlafId))
			throw new InvalidOperationException(
				"Seed data has no Organizer-role organization membership for olaf "
				+ $"(user id {OlafId}). ApplicationDbContextInitializer.SeedAsync likely "
				+ "failed partway through and Program.cs's Development branch logged "
				+ "rather than rethrowing - check the backend resource's startup logs "
				+ "for \"An exception occurred while seeding the database\".");
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

	// Vera never belongs to anything in seed data (SeedOrg1Async/SeedOrg2Async
	// only assign olaf as Organizer) - remove any membership row a test granted
	// her, in any organization and at any role. Not just role='Organizer':
	// GetOrganizationsQueryHandler returns Member-only orgs too, so a leftover
	// Member row breaks "vera organizes/belongs to nothing" just as well.
	// Scoped to her user id via the WHERE clause, so this can never touch
	// another user's row or race a test not touching vera's account.
	private async Task ResetVeraOrganizationMembershipAsync()
	{
		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();
		await using var cmd = new NpgsqlCommand(
			"""
			DELETE FROM organization_membership
			WHERE user_id = @userId
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
		var response = await PostTokenRequestWithRetryAsync(
			_keycloakClient,
			$"/realms/{Realm}/protocol/openid-connect/token",
			() => new FormUrlEncodedContent([
				new KeyValuePair<string, string>("grant_type", "client_credentials"),
				new KeyValuePair<string, string>("client_id", BackendClientId),
				new KeyValuePair<string, string>("client_secret", BackendClientSecret),
			]),
			cancellationToken);

		await EnsureSuccessAsync(response);

		var token = await response.Content.ReadFromJsonAsync<AdminTokenResponse>(cancellationToken: cancellationToken)
			?? throw new InvalidOperationException("Keycloak returned no admin token.");

		return token.AccessToken;
	}

	// Delegates to AuthHelper so the whole suite shares one retry policy rather
	// than this copy plus the ~29 hand-rolled ones the test classes carried
	// (einsatzbereit#2147). Kept as a thin local name so the call sites above
	// read the same as before.
	private static Task<HttpResponseMessage> PostTokenRequestWithRetryAsync(
		HttpClient client, string requestUri, Func<FormUrlEncodedContent> contentFactory,
		CancellationToken cancellationToken = default)
		=> AuthHelper.PostTokenRequestWithRetryAsync(client, requestUri, contentFactory, cancellationToken);

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

		var response = await PostTokenRequestWithRetryAsync(
			client,
			$"/realms/{Realm}/protocol/openid-connect/token",
			() => new FormUrlEncodedContent([
				new KeyValuePair<string, string>("grant_type", "password"),
				new KeyValuePair<string, string>("client_id", FrontendTestClientId),
				new KeyValuePair<string, string>("username", username),
				new KeyValuePair<string, string>("password", password),
				new KeyValuePair<string, string>("scope", "openid"),
			]));
		await EnsureSuccessAsync(response);

		var token = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>()
			?? throw new InvalidOperationException("Keycloak returned no token.");

		// Must match VITE_KEYCLOAK_AUTHORITY_URL exactly (AppHost.cs builds it the
		// same way: "{keycloakEndpoint}/realms/{realm}") since it becomes part of
		// oidc-client-ts's storage key. GetEndpoint(...).ToString() carries a
		// trailing slash (System.Uri's default for a bare authority) that Aspire's
		// EndpointReference interpolation does not, so it must be trimmed first
		// or the concatenation produces a double slash and a key that never matches.
		var authority = $"{GetEndpoint("keycloak").ToString().TrimEnd('/')}/realms/{Realm}";

		// "sub" is the Keycloak user id - decoded here rather than making
		// callers look it up, since AuthHelper.FastSignInAsync's active-org pin
		// and the membership-guard tests both need the signed-in user's Guid.
		var userId = Guid.Parse(
			AuthHelper.DecodeJwtPayload(token.IdToken).GetProperty("sub").GetString()!);

		return new KeycloakSession(
			token.AccessToken, token.IdToken, token.RefreshToken, token.ExpiresIn, token.TokenType, authority, userId);
	}

	/// <summary>
	/// Creates a throwaway Keycloak user for tests that need a page only a
	/// required action reaches (<c>login-update-password.ftl</c>,
	/// <c>login-update-profile.ftl</c>, <c>login-verify-email.ftl</c>) - the realm
	/// has <c>verifyEmail</c> on, so Keycloak defers the password to
	/// UPDATE_PASSWORD rather than collecting it at registration. Reaching those
	/// from a seeded account would pin a required action to it for the session.
	///
	/// Always pair with <see cref="DeleteUserAsync"/> in a finally: one realm is
	/// shared suite-wide, and an abandoned user carrying UPDATE_PASSWORD breaks a
	/// later, unrelated login test.
	///
	/// <paramref name="password"/> must satisfy the realm policy
	/// (<c>upperCase(1)</c>, <c>length(8)</c>). <paramref name="attributes"/>
	/// seeds user attributes (e.g. <c>{"locale": ["de"]}</c>); null omits it.
	/// </summary>
	public async Task<Guid> CreateThrowawayUserAsync(
		string username, string password, bool emailVerified, string[] requiredActions,
		IReadOnlyDictionary<string, string[]>? attributes = null,
		CancellationToken cancellationToken = default)
	{
		var adminToken = await GetAdminTokenAsync(cancellationToken);

		using var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/realms/{Realm}/users")
		{
			Content = JsonContent.Create(new
			{
				username,
				email = $"{username}@example.invalid",
				emailVerified,
				enabled = true,
				requiredActions,
				attributes,
				credentials = new[] { new { type = "password", value = password, temporary = false } },
			}),
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var response = await _keycloakClient.SendAsync(request, cancellationToken);
		await EnsureSuccessAsync(response);

		// Keycloak returns the new user's id only in the Location header.
		var location = response.Headers.Location?.ToString()
			?? throw new InvalidOperationException("Keycloak returned no Location header for the created user.");
		return Guid.Parse(location[(location.LastIndexOf('/') + 1)..]);
	}

	/// <summary>Removes a user created by <see cref="CreateThrowawayUserAsync"/>.</summary>
	public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		var adminToken = await GetAdminTokenAsync(cancellationToken);

		using var request = new HttpRequestMessage(
			HttpMethod.Delete, $"/admin/realms/{Realm}/users/{userId}");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var response = await _keycloakClient.SendAsync(request, cancellationToken);
		// A 404 means some earlier cleanup already removed it - not a failure
		// worth blowing up a test's finally block over.
		if (response.StatusCode == HttpStatusCode.NotFound)
			return;
		await EnsureSuccessAsync(response);
	}

	// Test-only escape hatch to simulate an opportunity row removed without
	// going through the command handler that cancels its engagements first -
	// e.g. data predating that cancellation safeguard.
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

	/// <summary>
	/// Renders the realm's login page once, during boot.
	///
	/// <see cref="WaitForRealmReadyAsync"/> proves the realm imported, but it only
	/// ever fetches JSON (<c>.well-known/openid-configuration</c>) - no template is
	/// involved. The first request that actually renders a page is what pays for
	/// compiling this repo's custom Freemarker login theme (<c>keycloak/themes/
	/// einsatzbereit</c>, set realm-wide as <c>loginTheme</c>); Keycloak caches it
	/// only afterwards.
	///
	/// Unsharded, that cost was paid once per 542-test session and landed on
	/// whichever test happened to run first - in practice almost always one of
	/// AccessibilityTests' 90, which carry their own [Retry]. Sharded
	/// (einsatzbereit#2145) every shard boots its own Keycloak, so the cost recurs
	/// per shard and lands on whichever classes that shard happens to start with -
	/// and for a shard holding the real-login classes that means AuthHelper's
	/// login budget absorbs it on top of ordinary CPU contention. Paying it here
	/// moves it inside this initializer's own generous budget instead.
	///
	/// Best-effort by design: if this fails, the first real login is merely as
	/// slow as it was before, which is not worth failing a whole shard's boot for.
	/// </summary>
	private async Task WarmKeycloakLoginPageAsync()
	{
		try
		{
			using var client = _app.CreateHttpClient("keycloak");

			// redirect_uri only has to satisfy the frontend client's
			// "http://localhost:*" (AppHost.cs relaxes it to that for local runs) -
			// nothing ever follows it, so the port is arbitrary.
			using var response = await client.GetAsync(
				$"/realms/{Realm}/protocol/openid-connect/auth"
				+ "?client_id=frontend&response_type=code&scope=openid"
				+ "&redirect_uri=http%3A%2F%2Flocalhost%3A1%2Fcallback");

			response.EnsureSuccessStatusCode();
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			Console.WriteLine($"[aspire-fixture] login-page warm-up skipped: {ex.Message}");
		}
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
