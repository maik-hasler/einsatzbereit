using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// The one session-expiry case that needs a real browser: a 401 on an
/// authenticated request has to end on Keycloak's own
/// <c>/protocol/openid-connect/auth</c> page.
///
/// The four cases that asserted what the *app* does about an expiry - the
/// toast, its hold before the redirect, the coalescing of several concurrent
/// 401s, and staying quiet for a visitor who was never signed in - moved to
/// <c>frontend/src/hooks/useSessionExpiryHandler.test.tsx</c> in
/// einsatzbereit#2148. Each of them intercepted every authenticated GET with
/// a 401 purely to reach one call to <c>sessionExpiryBus</c>.
/// </summary>
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
