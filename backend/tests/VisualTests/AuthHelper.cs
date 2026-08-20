using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

public static class AuthHelper
{
	// Must match VITE_KEYCLOAK_CLIENT_ID (frontend/.env.development) - the client
	// the SPA itself is configured with, not the frontend-test client used to
	// mint the token in FastSignInAsync. This is what oidc-client-ts's storage
	// key is keyed on, regardless of which client actually issued the token.
	private const string FrontendClientId = "frontend";

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
