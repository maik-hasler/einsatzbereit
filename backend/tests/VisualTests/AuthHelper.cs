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

	public static async Task LoginAsync(IPage page, Uri frontendUrl, string username, string password)
	{
		await AllowKeycloakCrossOriginRequestsAsync(page);
		await page.GotoAsync(frontendUrl.ToString());

		await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).First.ClickAsync();

		// Wait on Keycloak's login form element, not the URL - WaitForURLAsync
		// races the frame's own navigation/detachment during the redirect and
		// becomes intermittently flaky under CPU contention (see
		// AuthGuardTests.MyEngagements_Anonymous_RedirectsToKeycloak, which hit
		// the same redirect and independently arrived at the same strategy -
		// wait on the form element itself, not the URL - via
		// Expect(...).ToBeVisibleAsync() rather than this raw WaitForAsync).
		await page.Locator("#username").WaitForAsync(new() { Timeout = 30_000 });

		await page.Locator("#username").FillAsync(username);
		await page.Locator("#password").FillAsync(password);
		await page.Locator("#kc-login").ClickAsync();

		await page.WaitForURLAsync($"{frontendUrl.GetLeftPart(UriPartial.Authority)}/", new()
		{
			Timeout = 30_000,
		});
	}

	/// <summary>
	/// Strips the <c>X-Forwarded-For</c> header (seeded context-wide by
	/// <see cref="VisualTestBase.ContextOptions"/> for rate-limit isolation) from
	/// any request that crosses into Keycloak. Keycloak's CORS preflight does not
	/// list it in <c>Access-Control-Allow-Headers</c>, so oidc-client-ts's
	/// discovery/authorization fetch fails silently and <c>signinRedirect()</c>
	/// never navigates - the "Sign in"/"Register" click (or ProtectedRoute's
	/// auto-redirect for an anonymous visitor) just sits on the current page
	/// until the caller's own wait for a Keycloak-only locator times out.
	///
	/// Scoped to a page-level route matching only <c>/realms/</c> paths (Keycloak's
	/// own), not a context-level <c>"**/*"</c> handler - see
	/// <see cref="VisualTestBase.ContextOptions"/>'s doc comment for why a
	/// context-wide route is reserved for every one of this suite's 209 tests, not
	/// just the ones that actually cross into Keycloak from the browser (this one,
	/// plus the anonymous-redirect tests in AuthGuardTests/HomePageOrgCtaTests that
	/// call this directly instead of going through LoginAsync).
	/// </summary>
	public static Task AllowKeycloakCrossOriginRequestsAsync(IPage page) =>
		page.RouteAsync("**/realms/**", async route =>
		{
			var headers = new Dictionary<string, string>(route.Request.Headers, StringComparer.OrdinalIgnoreCase);
			headers.Remove("X-Forwarded-For");
			await route.ContinueAsync(new() { Headers = headers });
		});

	/// <summary>
	/// Signs in without touching Keycloak's login UI: mints a real token via
	/// <see cref="AspireFixture.SignInAsync"/> (direct grant, frontend-test client)
	/// and seeds it into localStorage in oidc-client-ts's own storage shape
	/// (verified against the installed oidc-client-ts package - see User.toStorageString
	/// and UserManager._userStoreKey), so the SPA boots already authenticated with
	/// no redirect round trip.
	///
	/// This is a faster drop-in for <see cref="LoginAsync"/> for tests that only need
	/// an authenticated session as a precondition. Keep at least one real
	/// <see cref="LoginAsync"/>-based test per meaningfully different path (this repo
	/// keeps one generic one plus JwtAudienceTests, which specifically guards the
	/// frontend/frontend-test protocol-mapper parity this method's realism depends
	/// on) so the actual login round trip - and that parity - stay under real,
	/// non-bypassed test coverage.
	///
	/// If oidc-client-ts's storage format ever changes (a version bump), this fails
	/// loudly here rather than as a confusing downstream locator failure in whatever
	/// the calling test actually checks.
	///
	/// Always returns <paramref name="username"/>'s seeded organizer org id
	/// (per AspireFixture.GetPinnedOrganizerOrganizationId, captured once
	/// before any test had a chance to create a throwaway org), or null for a
	/// non-organizer account. When <paramref name="pinActiveOrg"/> is true
	/// (the default), that id is also written into the "active-org" cookie,
	/// so callers can reach the org app deterministically via the id-based
	/// overload of <see cref="GoToOrgAppDashboardAsync(IPage, Uri, Guid)"/>
	/// instead of resolveActiveOrg's alphabetical fallback - which, without
	/// this pin, a throwaway org created by some concurrently running test
	/// could win by sorting ahead of the seeded ones. Pass false for the one
	/// test whose actual subject is that fallback order
	/// (OrganizationDashboardNavLinkTests) - it re-queries the alphabetically-
	/// first org fresh (AspireFixture.GetCurrentFirstOrganizerOrganizationIdAsync)
	/// right before asserting instead of trusting this method's own returned id
	/// (a snapshot from fixture boot, stale the instant any other test creates
	/// an org for this user), and nothing is written to the cookie jar to force
	/// the pin, so the real, unpinned resolution stays under coverage.
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
			// FrontendClientId above) - Keycloak rejects a silent-renew attempt
			// with it. Omitting it entirely means automaticSilentRenew
			// (main.tsx) never tries, instead of trying and failing partway
			// through a long-running test (see AppHost.cs's accessTokenLifespan
			// bump, which gives fast-signed-in sessions enough runway not to
			// need a renewal at all).
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
		await page.AddInitScriptAsync(
			$"window.localStorage.setItem({JsonSerializer.Serialize(storageKey)}, "
			+ $"{JsonSerializer.Serialize(storageValue)});");

		// Computed regardless of pinActiveOrg so callers that deliberately stay
		// unpinned (e.g. OrganizationDashboardNavLinkTests's resolution-order
		// test) still get back the id the frontend's own alphabetical fallback
		// *should* resolve to, to assert against - only the cookie write below
		// is conditional.
		var pinnedOrganizationId = fixture.GetPinnedOrganizerOrganizationId(session.UserId);

		// A one-time Context.AddCookiesAsync (not embedded in the init script
		// above) - an init script re-runs on every subsequent document
		// navigation in this context, which would otherwise clobber
		// OrgAppLayout.tsx's own setActiveOrgId call after a real in-app
		// navigation. Setting it once here still seeds it before the first
		// GotoAsync below, and the app is free to update it normally afterward.
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
	/// Gets a logged-in user into the org app shell at
	/// /app/{organizationId}/dashboard, as a *precondition* for tests that are
	/// about the org app rather than about how you get there. Pass the id
	/// <see cref="FastSignInAsync"/> already pinned for this user - a caller
	/// with no such id fails immediately here rather than silently falling
	/// back to a CTA that may never render (see the CTA-based overload below
	/// for LoginAsync-based callers, which have no pinned id).
	///
	/// Navigates straight to the dashboard URL rather than waiting on the home
	/// page's "Organization overview" hero CTA. That CTA only renders once
	/// GET /v1/organizations has resolved *and* produced a non-empty list (see
	/// resolveOrgAppPath in activeOrg.ts) - a single failed or slow org-list
	/// request left the hero showing the fallback button with no retry, and
	/// every caller of the old CTA-clicking helper then burned its full 30s
	/// timeout waiting for a link that would never appear. Successive timeout
	/// bumps here (15s -> 25s -> 30s) never fixed that, because the wait was
	/// not actually short - the link was absent.
	/// </summary>
	public static async Task GoToOrgAppDashboardAsync(IPage page, Uri frontendUrl, Guid organizationId)
	{
		var origin = frontendUrl.GetLeftPart(UriPartial.Authority);
		await page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		await page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}

	/// <summary>
	/// Gets a logged-in user into the org app shell by clicking the home
	/// page's "Organization overview" hero CTA - for <see cref="LoginAsync"/>-based
	/// callers, which have no pinned active-org id to navigate to directly.
	/// The CTA itself stays under real coverage in HomePageOrgCtaTests, which
	/// is where that behaviour belongs; re-exercising it as an incidental
	/// precondition elsewhere only ever bought flakiness.
	/// </summary>
	public static async Task GoToOrgAppDashboardViaCtaAsync(IPage page, Uri frontendUrl)
	{
		// Defensive: resolves instantly if the caller is already there (the
		// common case, right after LoginAsync), but also makes this helper
		// safe to call from elsewhere.
		await page.WaitForURLAsync($"{frontendUrl.GetLeftPart(UriPartial.Authority)}/", new() { Timeout = 15_000 });

		var cta = page.GetByRole(AriaRole.Link, new() { Name = "Organization overview" });
		await cta.First.WaitForAsync(new() { Timeout = 30_000 });
		await cta.First.ClickAsync();

		await page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}
}
