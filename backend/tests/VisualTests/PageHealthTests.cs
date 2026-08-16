using Microsoft.Playwright;

namespace VisualTests;

// einsatzbereit#997: the 2026-07-25 live-staging audit swept 16 page loads
// across anonymous/vera/olaf/admin sessions and found zero console errors
// and zero failed (>= 400) network responses - a positive finding worth
// locking in as a regression guard, since nothing else in this suite watches
// for either. Scoped to the home page rather than replaying all 16 - that
// sweep was a one-time manual audit, not a suite this is meant to reproduce
// wholesale, and every other test in this project already exercises its own
// page's happy path without a console/network assertion of its own.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class PageHealthTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task HomePage_HasNoConsoleErrorsOrFailedRequests()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		// #1929: the home page now probes for a live Keycloak SSO session on
		// mount even when anonymous (useSilentSsoProbe) - it genuinely crosses
		// into Keycloak now, same as the anonymous-redirect tests in
		// AuthGuardTests/HomePageOrgCtaTests, so it needs the same
		// X-Forwarded-For strip (see AllowKeycloakCrossOriginRequestsAsync's
		// own doc comment) or the probe's discovery fetch trips Keycloak's
		// CORS preflight and this test's own console-error assertion below
		// fails on that, not on a real regression.
		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		var consoleErrors = new List<string>();
		var failedResponses = new List<string>();

		Page.Console += (_, msg) =>
		{
			if (msg.Type == "error")
				consoleErrors.Add(msg.Text);
		};
		Page.Response += (_, response) =>
		{
			// Chromium probes /favicon.ico by default regardless of the SVG
			// <link rel="icon"> this app actually serves - a browser quirk
			// unrelated to app correctness, not a real failed request.
			if (response.Status >= 400 && !response.Url.EndsWith("/favicon.ico"))
				failedResponses.Add($"{response.Status} {response.Url}");
		};

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		if (consoleErrors.Count > 0)
			throw new Exception(
				$"Home page logged {consoleErrors.Count} console error(s):\n"
				+ string.Join("\n", consoleErrors));

		if (failedResponses.Count > 0)
			throw new Exception(
				$"Home page had {failedResponses.Count} failed request(s):\n"
				+ string.Join("\n", failedResponses));
	}
}
