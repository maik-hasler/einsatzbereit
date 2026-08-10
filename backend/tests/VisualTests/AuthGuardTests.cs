using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AuthGuardTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MyEngagements_Anonymous_RedirectsToKeycloak()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		// Navigation chain: /my-signups -> ProtectedRoute triggers signinRedirect()
		// -> Keycloak /protocol/openid-connect/auth. Don't wait on individual URL -
		// race-prone with frame detachment. Wait on Keycloak login form element instead.
		try
		{
			await Page.GotoAsync($"{frontend}my-signups", new() { WaitUntil = WaitUntilState.Commit });
		}
		catch (PlaywrightException)
		{
			// GotoAsync may abort if the JS-driven redirect kicks in before commit completes.
			// The redirect itself is what we're testing for - ignore the abort and verify below.
		}

		await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 30_000 });
		await Expect(Page.Locator("#password")).ToBeVisibleAsync();
		await Expect(Page).ToHaveURLAsync(new Regex(@"/realms/einsatzbereit/protocol/openid-connect/auth"));
	}

	// Kept as a real, uncompromised login (not AuthHelper.FastSignInAsync) on
	// purpose: this is the one test whose entire job is proving the interactive
	// Keycloak round trip - submit credentials, redirect, /callback, authenticated
	// state - still works end to end. Every other test's real login was converted
	// to token injection for speed; this one (plus JwtAudienceTests, which guards
	// the frontend/frontend-test protocol-mapper parity that injection relies on)
	// is what keeps that round trip under actual test coverage instead of just
	// incidental setup.
	[Test]
	public async Task SignIn_WithValidCredentials_ReachesAuthenticatedHomePage()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }))
			.Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task Header_Anonymous_ShowsSignInButton()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).First)
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task Header_Anonymous_RegisterButton_RedirectsToKeycloakRegistrationEndpoint()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).First.ClickAsync();

		// Distinct from Header_Anonymous_ShowsSignInButton: "Register" must land on
		// Keycloak's registration form, not the login form the "Sign in" button uses.
		await Expect(Page).ToHaveURLAsync(
			new Regex(@"/realms/einsatzbereit/protocol/openid-connect/registrations"));
		await Expect(Page.Locator("#kc-register-form")).ToBeVisibleAsync(new() { Timeout = 30_000 });
	}
}
