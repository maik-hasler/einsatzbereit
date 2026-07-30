using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SessionExpiryTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Regression for #1219: handleErrorResponse used to silently discard every
	// 401 ("if (response.status === 401) { return; }"), leaving a
	// logged-in-looking UI - avatar, notification bell still rendered - where
	// every authenticated API call quietly failed. A 401 on a request that
	// carried a bearer token now means the Keycloak session behind it is no
	// longer valid, so the app must send the user back through sign-in instead
	// of pretending nothing happened.
	[Test]
	public async Task AuthenticatedRequest_Returns401_RedirectsToKeycloakSignIn()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		await MockAllV1GetRequestsAsUnauthorizedAsync();

		// Force the header + home page effects to remount and re-fire their
		// concurrent authenticated requests against the mocked 401 above.
		await Page.ReloadAsync();

		// Same tolerant wait as AuthGuardTests.MyEngagements_Anonymous_RedirectsToKeycloak -
		// wait on the Keycloak login form rather than an exact URL, which is race-prone
		// against the JS-driven redirect.
		await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 30_000 });
		await Expect(Page).ToHaveURLAsync(new Regex(@"/realms/einsatzbereit/protocol/openid-connect/auth"));
	}

	[Test]
	public async Task AuthenticatedRequest_Returns401_ShowsSessionExpiredToast()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		// Registered before the abort-specific route below, so that one (added
		// later, matching the same origin) still wins for the /auth navigation
		// itself - this only needs to unblock oidc-client-ts's earlier discovery
		// fetch so signinRedirect() gets far enough to attempt that navigation
		// at all.
		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		await MockAllV1GetRequestsAsUnauthorizedAsync();

		// Hold the sign-in redirect at the network layer instead of letting it
		// navigate away: aborting a cross-origin top-level navigation before any
		// response arrives makes Chromium cancel it and stay on the current
		// document, rather than swapping documents. That keeps the SPA (and its
		// toast) on screen long enough to assert, instead of racing the redirect.
		await Page.RouteAsync(
			"**/realms/einsatzbereit/protocol/openid-connect/auth**",
			route => route.AbortAsync());

		await Page.ReloadAsync();

		var sessionExpiredToast = Page.GetByRole(AriaRole.Alert)
			.Filter(new() { HasTextString = "session has expired" });
		// Same tolerant timeout as the redirect test above (30s, not the
		// previous 10s): #1449 made i18n resource loading async (its own
		// fetched chunk, gating the whole app behind a root Suspense
		// boundary), so a cold Page.ReloadAsync() now has one more
		// network-bound step before anything - including this toast - can
		// render at all. This budget no longer races the toast's own 5s
		// auto-dismiss (ToastContext.tsx) - AppHost sets
		// VITE_TOAST_LIFETIME_MS=0 for test runs, so a render that lands late
		// under CI contention can't have its toast disappear before this
		// assertion's next poll catches it.
		await Expect(sessionExpiredToast).ToBeVisibleAsync(new() { Timeout = 30_000 });
	}

	// Edge case: an anonymous visitor's public, tokenless requests (e.g. the
	// home page's opportunity listing) can also 401 (auth required, none
	// given) - that is just "not logged in", not a session expiring, and must
	// not trigger the toast/redirect above. Guarded by handleErrorResponse's
	// hadAccessToken check in api-instance.ts.
	[Test]
	public async Task AnonymousRequest_Returns401_DoesNotShowToastOrRedirect()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await MockAllV1GetRequestsAsUnauthorizedAsync();

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var sessionExpiredToast = Page.GetByRole(AriaRole.Alert)
			.Filter(new() { HasTextString = "session has expired" });
		await Expect(sessionExpiredToast).Not.ToBeVisibleAsync();

		Page.Url.Should().StartWith(frontend.ToString());
	}

	// Regression: a Keycloak session that naturally lapsed (browser closed,
	// reopened later - no explicit sign-out) leaves an already-expired user
	// object sitting in localStorage. automaticSilentRenew (main.tsx) fires a
	// renewal attempt for it immediately on mount regardless of which page is
	// open, and that attempt fails right away (no live Keycloak SSO session
	// behind it, since this session was minted out-of-band here rather than
	// via a real browser login). useSessionExpiryHandler used to treat any
	// addSilentRenewError as "your live session just expired" and force a
	// signinRedirect - even though the visitor was never actually
	// authenticated on this page, which is meant to work for anonymous
	// visitors. Also covers VolunteerOpportunityDetailPage.tsx separately
	// gating its organiser-only getOrganizations() call on isAuthenticated,
	// not just on the (still-present-but-stale) profile roles.
	[Test]
	public async Task StaleExpiredSession_AnonymousVisitor_OpportunityDetailPage_StaysOnPage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		// Without this, oidc-client-ts's silent-renew discovery fetch gets
		// blocked by the context-wide X-Forwarded-For header (see this
		// method's own doc comment) before it ever reaches Keycloak, so a
		// still-broken handleExpiry would never actually attempt the redirect
		// this test needs to be able to observe - it would pass for the wrong
		// reason instead of exercising the real regression.
		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await http.PostAsJsonAsync(
			"/v1/organizations", new { name = $"Stale Session Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"Stale Session Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Seeded for the stale-session regression coverage.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opp = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opp.GetProperty("id").GetString();

		// Olaf is an organiser (has the "organisator" role), same as the real
		// scenario this guards - a stale session whose cached profile still
		// carries organiser claims. expires_at is set well in the past, unlike
		// AuthHelper.FastSignInAsync's session (always in the future) - that
		// helper covers the "still logged in" case already exercised
		// elsewhere in this file.
		var session = await Fixture.SignInAsync("olaf", "olaf123");
		var profile = AuthHelper.DecodeJwtPayload(session.IdToken);
		var storageValue = JsonSerializer.Serialize(new
		{
			id_token = session.IdToken,
			session_state = (string?)null,
			access_token = session.AccessToken,
			refresh_token = (string?)null,
			token_type = session.TokenType,
			scope = "openid",
			profile,
			expires_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600,
		});
		// Must match VITE_KEYCLOAK_CLIENT_ID (frontend/.env.development) - see
		// AuthHelper.FrontendClientId's own doc comment for why.
		var storageKey = $"oidc.user:{session.Authority}:frontend";

		await Page.AddInitScriptAsync(
			$"window.localStorage.setItem({JsonSerializer.Serialize(storageKey)}, "
			+ $"{JsonSerializer.Serialize(storageValue)});");

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Expect(Page.Locator("h1").First).ToHaveTextAsync(oppTitle, new() { Timeout = 15_000 });

		// The anonymous sign-in CTA renders - the page must not treat this
		// visitor as authenticated just because a stale profile is present.
		await Expect(Page.GetByTestId("opportunity-signin")).ToBeVisibleAsync();

		// Give automaticSilentRenew's background failure and the (old) 2s
		// redirect timer time to fire - generous margin over that 2s under a
		// contended CI runner - then confirm we're still on the SPA rather
		// than having been bounced to Keycloak's login page.
		await Page.WaitForTimeoutAsync(5000);
		Page.Url.Should().StartWith(origin);
	}

	private async Task MockAllV1GetRequestsAsUnauthorizedAsync()
	{
		await Page.RouteAsync("**/v1/**", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			// The frontend and backend are cross-origin in this test environment,
			// so a mocked response still needs an Access-Control-Allow-Origin
			// header - the browser enforces CORS on fulfilled responses just as
			// it would on a real one, and without it fetch() rejects before the
			// app's response-handling code (and thus the toast/redirect) ever runs.
			await route.FulfillAsync(new()
			{
				Status = 401,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.2\",\"status\":401}",
			});
		});
	}
}
