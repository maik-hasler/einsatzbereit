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
		await page.GotoAsync(frontendUrl.ToString());

		await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).First.ClickAsync();

		await page.WaitForURLAsync("**/realms/einsatzbereit/**");

		await page.Locator("#username").FillAsync(username);
		await page.Locator("#password").FillAsync(password);
		await page.Locator("#kc-login").ClickAsync();

		await page.WaitForURLAsync($"{frontendUrl.GetLeftPart(UriPartial.Authority)}/", new()
		{
			Timeout = 30_000,
		});
	}

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
	/// </summary>
	public static async Task FastSignInAsync(
		IPage page, AspireFixture fixture, Uri frontendUrl, string username, string password)
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
			refresh_token = session.RefreshToken,
			token_type = session.TokenType,
			scope = "openid",
			profile,
			expires_at = expiresAt,
		});
		var storageKey = $"oidc.user:{session.Authority}:{FrontendClientId}";

		// Pin which organization the org-app entry points resolve to, rather
		// than letting activeOrg.ts's "first alphabetically" fallback pick one
		// out of whatever throwaway orgs other tests have created under this
		// same shared account - see AspireFixture.GetSeededOrganizerOrganizationIdAsync
		// for the full failure mode. Seeding the cookie here (rather than in
		// GoToOrgAppDashboardAsync) is what makes it take effect: it has to be
		// in place before the SPA resolves the CTA's href, which happens on
		// the very first render after this method's GotoAsync below.
		var activeOrgCookieScript = string.Empty;
		if (profile.TryGetProperty("sub", out var sub)
			&& sub.GetString() is { } userId
			&& await fixture.GetSeededOrganizerOrganizationIdAsync(userId) is { } organizationId)
		{
			var cookie = $"active-org={organizationId}; path=/; SameSite=Lax";
			activeOrgCookieScript = $"document.cookie = {JsonSerializer.Serialize(cookie)};";
		}

		// AddInitScriptAsync (not EvaluateAsync after navigation) so this runs
		// before the SPA's own bundle - by the time React/oidc-client-ts mounts,
		// the "user" is already in storage and no anonymous render happens first.
		await page.AddInitScriptAsync(
			$"window.localStorage.setItem({JsonSerializer.Serialize(storageKey)}, "
			+ $"{JsonSerializer.Serialize(storageValue)}); {activeOrgCookieScript}");

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
	}

	private static JsonElement DecodeJwtPayload(string jwt)
	{
		var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
		payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
		var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
		return JsonDocument.Parse(json).RootElement.Clone();
	}

	/// <summary>
	/// Gets a logged-in user into the org app shell at
	/// /app/{organizationId}/dashboard, as a *precondition* for tests that are
	/// about the org app rather than about how you get there.
	///
	/// Navigates straight to the organization <see cref="FastSignInAsync"/>
	/// pinned in the active-org cookie, rather than waiting on the home page's
	/// "Organization overview" hero CTA. That CTA only renders once
	/// GET /v1/organizations has resolved *and* produced a non-empty list (see
	/// resolveOrgAppPath in activeOrg.ts), and HomePage discards the fetch's
	/// error - so a single failed or slow org-list request leaves the hero
	/// showing the fallback button with no retry, and every caller of this
	/// helper then burns its full timeout waiting for a link that will never
	/// appear. That was a recurring source of 30s timeouts here
	/// (OrgDashboardPage_*_AsOlaf, EngagementManagementPage_AsOlaf, ...) which
	/// successive timeout bumps (15s -> 25s -> 30s) never fixed, because the
	/// wait was not actually short - the link was absent.
	///
	/// The CTA itself stays under real coverage in HomePageOrgCtaTests, which
	/// is where that behaviour belongs; re-exercising it as an incidental
	/// precondition in ~28 other tests only ever bought flakiness.
	/// </summary>
	public static async Task GoToOrgAppDashboardAsync(IPage page, Uri frontendUrl)
	{
		var origin = frontendUrl.GetLeftPart(UriPartial.Authority);

		// Defensive: resolves instantly if the caller is already there (the
		// common case, right after LoginAsync), but also makes this helper
		// safe to call from elsewhere.
		await page.WaitForURLAsync($"{origin}/", new() { Timeout = 15_000 });

		var cookies = await page.Context.CookiesAsync();
		var activeOrgId = cookies.FirstOrDefault(c => c.Name == "active-org")?.Value;

		if (!string.IsNullOrEmpty(activeOrgId))
		{
			await page.GotoAsync($"{origin}/app/{Uri.UnescapeDataString(activeOrgId)}/dashboard");
		}
		else
		{
			// No pinned org (e.g. after a real LoginAsync, which doesn't seed
			// the cookie) - fall back to the hero CTA.
			var cta = page.GetByRole(AriaRole.Link, new() { Name = "Organization overview" });
			await cta.First.WaitForAsync(new() { Timeout = 30_000 });
			await cta.First.ClickAsync();
		}

		await page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}
}
