using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

// Aliased rather than a plain `using System.Net`, which would make Cookie
// ambiguous against Microsoft.Playwright.Cookie further down this file.
using HttpStatusCode = System.Net.HttpStatusCode;

namespace VisualTests;

public static class AuthHelper
{
	// Must match VITE_KEYCLOAK_CLIENT_ID (frontend/.env.development) - the client
	// the SPA itself is configured with, not the frontend-test client used to
	// mint the token in FastSignInAsync. This is what oidc-client-ts's storage
	// key is keyed on, regardless of which client actually issued the token.
	private const string FrontendClientId = "frontend";

	// The realm and the two clients every direct token request in this suite uses.
	// These were duplicated as private consts across ~25 test classes, each with
	// its own copy of the token request below - which is exactly how those copies
	// ended up bypassing the retry AspireFixture already had (einsatzbereit#2147).
	private const string Realm = "einsatzbereit";
	private const string FrontendTestClientId = "frontend-test";
	private const string BackendClientId = "backend";
	private const string BackendClientSecret = "backend-secret";

	/// <summary>
	/// Mints a user access token straight from Keycloak, no browser involved.
	///
	/// This is the single implementation for the whole suite. Every test class
	/// that needs one used to carry its own private copy - 25 of them, all
	/// semantically identical, none retrying - so a transient 500 from Keycloak's
	/// token endpoint failed an unrelated test outright. AspireFixture had solved
	/// this for its own sign-ins already; the copies simply never went through it.
	/// </summary>
	public static async Task<string> GetTokenAsync(Uri keycloak, string username, string password)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		using var response = await PostTokenRequestWithRetryAsync(
			http,
			$"/realms/{Realm}/protocol/openid-connect/token",
			() => new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "password",
				["client_id"] = FrontendTestClientId,
				["username"] = username,
				["password"] = password,
				["scope"] = "openid",
			}));
		response.EnsureSuccessStatusCode();

		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("access_token").GetString()
			?? throw new InvalidOperationException("Keycloak returned no access_token.");
	}

	/// <summary>
	/// Mints a service-account token for the <c>backend</c> client, for tests that
	/// drive Keycloak's admin API directly. Same retry story as
	/// <see cref="GetTokenAsync"/> - four classes carried their own unprotected copy.
	/// </summary>
	public static async Task<string> GetAdminTokenAsync(Uri keycloak)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		using var response = await PostTokenRequestWithRetryAsync(
			http,
			$"/realms/{Realm}/protocol/openid-connect/token",
			() => new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "client_credentials",
				["client_id"] = BackendClientId,
				["client_secret"] = BackendClientSecret,
			}));
		response.EnsureSuccessStatusCode();

		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("access_token").GetString()
			?? throw new InvalidOperationException("Keycloak returned no access_token.");
	}

	/// <summary>
	/// The one retry policy for every Keycloak token request the suite makes.
	/// AspireFixture delegates to this too, so widening the budget here widens it
	/// everywhere rather than in one of several near-identical copies.
	///
	/// Every sign-in and every admin-token mint hits the token endpoint, hundreds
	/// of times per run, and under that load it occasionally answers with a
	/// transient 500 no request here caused. Never retries a 4xx (wrong
	/// credentials, bad client config) - that is a real failure, not a blip.
	///
	/// The budget is four attempts over ~3.5 s (0.5 s, 1 s, 2 s). Three attempts
	/// over ~1.5 s was not enough: einsatzbereit#2147 observed a run exhaust the
	/// old budget against a shard's cold stack, and sharding (#2145) means every
	/// shard now has its own cold window instead of one per suite.
	/// </summary>
	public static async Task<HttpResponseMessage> PostTokenRequestWithRetryAsync(
		HttpClient client, string requestUri, Func<FormUrlEncodedContent> contentFactory,
		CancellationToken cancellationToken = default)
	{
		const int maxAttempts = 4;
		HttpResponseMessage response;
		for (var attempt = 1; ; attempt++)
		{
			using var content = contentFactory();
			response = await client.PostAsync(requestUri, content, cancellationToken);
			if (response.StatusCode < HttpStatusCode.InternalServerError || attempt >= maxAttempts)
				break;

			response.Dispose();
			await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1)), cancellationToken);
		}

		return response;
	}

	/// <summary>
	/// Drives the real Keycloak login UI, retrying the round trip once.
	///
	/// The retry is here rather than on the calling test on purpose. The budget
	/// below used to lean on "callers that still time out carry their own
	/// [Retry(2)]", which is not true - of the 15 classes that drive a real login,
	/// only AccessibilityTests does, and einsatzbereit#2145's sharding made that
	/// gap bite: every shard now boots its own Keycloak, so more classes run
	/// against a cold stack than when one 542-test session amortised that window
	/// across the whole suite.
	///
	/// Retrying at this level keeps #1321's distinction intact. What gets a second
	/// chance is a browser round trip through an external identity provider -
	/// genuinely non-deterministic infrastructure - and never a product assertion
	/// in the test body, which is what the blanket [assembly: Retry(2)] #1321
	/// removed was masking.
	/// </summary>
	public static async Task LoginAsync(IPage page, Uri frontendUrl, string username, string password)
	{
		await AllowKeycloakCrossOriginRequestsAsync(page);

		try
		{
			await DriveLoginAsync(page, frontendUrl, username, password);
			return;
		}
		// Both types are needed, for the reason VisualTestBase's LoadMore helper
		// documents: there is no Microsoft.Playwright.TimeoutException in this
		// version, so a Playwright timeout surfaces as a *System*.TimeoutException
		// which does not derive from PlaywrightException. Catching only the latter
		// would miss the timeout this retry exists for.
		catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
		{
			// Fall through to the single retry below.
		}

		// The first attempt can have succeeded at Keycloak and only lost the race
		// on the client render, which leaves a live session and no "Sign in"
		// button for the retry to click. Check for the authenticated shell before
		// assuming the form is still there.
		await page.GotoAsync(frontendUrl.ToString());
		try
		{
			await page.GetByRole(AriaRole.Button, new() { Name = "User menu" })
				.WaitForAsync(new() { Timeout = 15_000 });
			return;
		}
		catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
		{
			// Genuinely not signed in - drive the form again.
		}

		await DriveLoginAsync(page, frontendUrl, username, password);
	}

	private static async Task DriveLoginAsync(IPage page, Uri frontendUrl, string username, string password)
	{
		await page.GotoAsync(frontendUrl.ToString());

		await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).First.ClickAsync();

		// Wait on Keycloak's login form element, not the URL - WaitForURLAsync
		// races the frame's own navigation/detachment during the redirect and
		// becomes intermittently flaky under CPU contention.
		await page.Locator("#username").WaitForAsync(new() { Timeout = 30_000 });

		await page.Locator("#username").FillAsync(username);
		await page.Locator("#password").FillAsync(password);
		await page.Locator("#kc-login").ClickAsync();

		// Same reasoning for the return leg: WaitForURLAsync races the callback
		// redirect chain (Keycloak -> /callback -> code exchange -> history.replace
		// to "/") and intermittently times out even though sign-in succeeded. The
		// "User menu" button - the same authenticated-render signal FastSignInAsync
		// waits on - is independent of which frame navigation Playwright observes.
		// 45s rather than 30s because this is the only caller driving the real
		// Keycloak round trip rather than seeding a token, so it is the most
		// exposed to AssemblyParallelLimit.cs's documented CPU contention. Do not
		// raise it further - LoginAsync above retries the whole round trip once,
		// which covers a cold stack better than a longer single wait would, and
		// without making every genuine failure take proportionally longer.
		await page.GetByRole(AriaRole.Button, new() { Name = "User menu" })
			.WaitForAsync(new() { Timeout = 45_000 });
	}

	/// <summary>
	/// Strips <c>X-Forwarded-For</c> (seeded context-wide by
	/// <see cref="VisualTestBase.ContextOptions"/>) from requests crossing into
	/// Keycloak, whose CORS preflight does not allow it: oidc-client-ts's
	/// discovery fetch then fails silently, <c>signinRedirect()</c> never
	/// navigates, and the click just sits there until the caller's own wait for a
	/// Keycloak-only locator times out. Page-level rather than a context-level
	/// <c>"**/*"</c> route - see <see cref="VisualTestBase.ContextOptions"/>.
	/// </summary>
	public static Task AllowKeycloakCrossOriginRequestsAsync(IPage page) =>
		page.RouteAsync("**/realms/**", async route =>
		{
			var headers = new Dictionary<string, string>(route.Request.Headers, StringComparer.OrdinalIgnoreCase);
			headers.Remove("X-Forwarded-For");
			await route.ContinueAsync(new() { Headers = headers });
		});

	/// <summary>
	/// Signs in without the Keycloak login UI: mints a token via
	/// <see cref="AspireFixture.SignInAsync"/> and seeds it into sessionStorage in
	/// oidc-client-ts's storage shape (see User.toStorageString and
	/// UserManager._userStoreKey), so the SPA boots authenticated with no redirect.
	/// Keep at least one real <see cref="LoginAsync"/> test per meaningfully
	/// different path, so the round trip and the frontend/frontend-test
	/// protocol-mapper parity it relies on stay covered.
	///
	/// Returns <paramref name="username"/>'s seeded organizer org id (a
	/// fixture-boot snapshot), or null for a non-organizer. With
	/// <paramref name="pinActiveOrg"/> (default true) that id also goes into the
	/// "active-org" cookie, so callers bypass resolveActiveOrg's alphabetical
	/// fallback - which a concurrent test's throwaway org could win. Pass false
	/// only for a test whose subject is that fallback order.
	/// </summary>
	public static async Task<Guid?> FastSignInAsync(
		IPage page, AspireFixture fixture, Uri frontendUrl, string username, string password,
		bool pinActiveOrg = true)
	{
		var session = await fixture.SignInAsync(username, password);
		// Unfiltered id_token claims (oidc-client-ts's real flow strips protocol-only
		// ones via filterProtocolClaims - iss/aud/azp/exp/etc). Harmless here since the
		// app only ever reads name/preferred_username/roles/locale/sub/email off profile.
		var profile = DecodeJwtPayload(session.IdToken);
		var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + session.ExpiresIn;

		var storageValue = JsonSerializer.Serialize(new
		{
			id_token = session.IdToken,
			session_state = (string?)null,
			access_token = session.AccessToken,
			// session.RefreshToken was minted for the frontend-test client, not
			// the frontend client oidc-client-ts believes it's holding (see
			// FrontendClientId above) - Keycloak rejects a silent-renew with it.
			// Omitting it means automaticSilentRenew (main.tsx) never tries;
			// AppHost.cs's accessTokenLifespan bump gives these sessions enough
			// runway that they never need one.
			refresh_token = (string?)null,
			token_type = session.TokenType,
			scope = "openid",
			profile,
			expires_at = expiresAt,
		});
		var storageKey = $"oidc.user:{session.Authority}:{FrontendClientId}";

		// AddInitScriptAsync (not EvaluateAsync after navigation) so this runs
		// before the SPA's own bundle - by the time React/oidc-client-ts mounts,
		// the "user" is already in storage and no anonymous render happens first.
		// sessionStorage, not localStorage (main.tsx) - an init script re-runs on
		// every document navigation in this page/tab, so it stays seeded across
		// GotoAsync calls the same way it would with localStorage.
		await page.AddInitScriptAsync(
			$"window.sessionStorage.setItem({JsonSerializer.Serialize(storageKey)}, "
			+ $"{JsonSerializer.Serialize(storageValue)});");

		// Computed regardless of pinActiveOrg - unpinned callers still need the
		// id the alphabetical fallback *should* resolve to, to assert against.
		var pinnedOrganizationId = fixture.GetPinnedOrganizerOrganizationId(session.UserId);

		// A one-time Context.AddCookiesAsync rather than the init script above:
		// an init script re-runs on every document navigation, which would
		// clobber OrgAppLayout.tsx's own setActiveOrgId after a real in-app
		// navigation. Setting it once still seeds it before the first GotoAsync.
		if (pinActiveOrg && pinnedOrganizationId is { } organizationId)
		{
			await page.Context.AddCookiesAsync([
				new Cookie
				{
					Name = "active-org",
					Value = organizationId.ToString(),
					Url = frontendUrl.ToString(),
				},
			]);
		}

		await page.GotoAsync(frontendUrl.ToString());

		try
		{
			await page.GetByRole(AriaRole.Button, new() { Name = "User menu" })
				.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException ex)
		{
			throw new InvalidOperationException(
				"FastSignInAsync did not authenticate the SPA - oidc-client-ts's "
				+ "storage key/shape may have drifted from what's hardcoded here. "
				+ "Confirm with AuthHelper.LoginAsync, then update FastSignInAsync "
				+ "(see User.toStorageString in oidc-client-ts's source).", ex);
		}

		return pinnedOrganizationId;
	}

	internal static JsonElement DecodeJwtPayload(string jwt)
	{
		var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
		payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
		var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
		return JsonDocument.Parse(json).RootElement.Clone();
	}

	/// <summary>
	/// Gets a logged-in user into the org app shell as a *precondition*. Pass the
	/// id <see cref="FastSignInAsync"/> pinned; LoginAsync-based callers have none
	/// and use the CTA overload below. Navigates straight to the dashboard URL
	/// rather than the home page's "Organization overview" CTA, which only renders
	/// once GET /v1/organizations resolves non-empty (resolveOrgAppPath in
	/// activeOrg.ts) and has no retry, so waiting on it can time out.
	/// </summary>
	public static async Task GoToOrgAppDashboardAsync(IPage page, Uri frontendUrl, Guid organizationId)
	{
		var origin = frontendUrl.GetLeftPart(UriPartial.Authority);
		await page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		await page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}

	/// <summary>
	/// Gets a logged-in user into the org app shell by clicking the home page's
	/// "Organization overview" hero CTA - for <see cref="LoginAsync"/>-based
	/// callers, which have no pinned active-org id. The CTA's own behaviour
	/// stays covered in HomePageOrgCtaTests; re-exercising it here only bought
	/// flakiness.
	/// </summary>
	public static async Task GoToOrgAppDashboardViaCtaAsync(IPage page, Uri frontendUrl)
	{
		// Resolves instantly if the caller is already there (the common case
		// right after LoginAsync), but keeps this helper safe to call elsewhere.
		await page.WaitForURLAsync($"{frontendUrl.GetLeftPart(UriPartial.Authority)}/", new() { Timeout = 15_000 });
		var cta = page.GetByRole(AriaRole.Link, new() { Name = "Organization overview" });
		await cta.First.WaitForAsync(new() { Timeout = 45_000 });
		await cta.First.ClickAsync();

		await page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}
}
