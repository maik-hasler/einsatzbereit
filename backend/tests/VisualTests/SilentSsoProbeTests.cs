using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SilentSsoProbeTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Regression for #1929: a second tab's sessionStorage starts out empty -
	// main.tsx's OIDC userStore is intentionally sessionStorage-backed, scoped
	// per top-level browsing context rather than shared context-wide (see
	// OpportunityApplicationStateTests' Context.NewPageAsync precedent, which
	// documents the same per-tab scoping for a different reason). A public
	// page's header used to render "logged out" - "Anmelden"/"Registrieren" -
	// on a fresh tab regardless of whether the underlying Keycloak SSO session
	// (a real browser cookie, shared context-wide) was still live, since
	// automaticSilentRenew only ever renews an *already-known* session and
	// never discovers one. useSilentSsoProbe now probes once via
	// signinSilent() on mount, picking the live session back up without a
	// full page reload.
	[Test]
	public async Task FreshTab_WithLiveSsoSession_HeaderShowsLoggedIn()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// A real login, not AuthHelper.FastSignInAsync - the fix specifically
		// depends on a live Keycloak SSO session *cookie*, which only a real
		// login through Keycloak's UI sets. FastSignInAsync only ever seeds a
		// token straight into sessionStorage and never visits Keycloak at all.
		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		// A second tab in the same browser context: shares vera's Keycloak
		// session cookie, but starts with none of Page's sessionStorage.
		var freshTab = await Context.NewPageAsync();
		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(freshTab);
		await freshTab.GotoAsync(origin);

		// The probe's hidden-iframe round trip to Keycloak takes a moment -
		// the header is expected to catch up shortly after first paint, not
		// necessarily be correct on the very first frame.
		await Expect(freshTab.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(freshTab.GetByRole(AriaRole.Button, new() { Name = "Sign in" }))
			.Not.ToBeVisibleAsync();
	}
}
