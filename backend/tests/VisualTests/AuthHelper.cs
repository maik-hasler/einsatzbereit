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

		// AddInitScriptAsync (not EvaluateAsync after navigation) so this runs
		// before the SPA's own bundle - by the time React/oidc-client-ts mounts,
		// the "user" is already in storage and no anonymous render happens first.
		await page.AddInitScriptAsync(
			$"window.localStorage.setItem({JsonSerializer.Serialize(storageKey)}, "
			+ $"{JsonSerializer.Serialize(storageValue)});");

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
	/// Navigates a logged-in user (assumed to be on the home page, as they are
	/// right after <see cref="LoginAsync"/> or <see cref="FastSignInAsync"/>) into
	/// the org app shell by clicking the "Organization overview" hero CTA, which
	/// resolves directly to /app/{organizationId}/dashboard - the /app intermediate
	/// picker page no longer exists (#747).
	/// </summary>
	public static async Task GoToOrgAppDashboardAsync(IPage page, Uri frontendUrl)
	{
		// Defensive: resolves instantly if the caller is already there (the
		// common case, right after LoginAsync), but also makes this helper
		// safe to call from elsewhere.
		await page.WaitForURLAsync($"{frontendUrl.GetLeftPart(UriPartial.Authority)}/", new() { Timeout = 15_000 });

		// 45s (bumped again from 30s) rather than the usual 15s: this CTA only
		// renders once GET /v1/organizations resolves for the signed-in user
		// (see resolveOrgAppPath in activeOrg.ts) - on a contended shared CI
		// stack (~61+ VisualTests classes hitting one Aspire-hosted
		// backend/DB per session, see AssemblyRetryPolicy.cs) that round trip
		// can occasionally run long even though nothing is actually broken.
		// 25s was previously not enough - #794 added two more concurrent
		// AccessibilityTests methods and tipped
		// CreateVolunteerOpportunityModal_HasNoSeriousA11yViolations/
		// OrgDashboardPage_AddWidgetModal_AsOlaf_HasNoSeriousA11yViolations
		// over the edge even with AssemblyRetryPolicy's retries; 30s then
		// stopped being enough once AssemblyParallelLimit.cs's global
		// ParallelLimiter<VisualTestsParallelLimit> (added alongside two more
		// new test classes, AdminReportsTests and FocusVisibleRingTests) still
		// leaves up to Environment.ProcessorCount CPU-heavy Chromium/axe-core
		// scans running at once - this call's own three attempts (one plus
		// AssemblyRetryPolicy's two retries) all timed out identically under
		// that sustained load, not a one-off blip.
		var cta = page.GetByRole(AriaRole.Link, new() { Name = "Organization overview" });
		await cta.First.WaitForAsync(new() { Timeout = 45_000 });
		await cta.First.ClickAsync();

		await page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}
}
