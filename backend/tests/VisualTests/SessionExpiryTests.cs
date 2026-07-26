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
		await Expect(sessionExpiredToast).ToBeVisibleAsync(new() { Timeout = 10_000 });
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
