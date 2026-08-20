using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1079: the platform had no Terms of Use page, no route, and no acceptance
/// step anywhere.
///
/// The page half of that - the route, its EN/DE clause content, its footer
/// link and the absence of an action bar - moved to
/// <c>frontend/src/pages/TermsOfUsePage.test.tsx</c> in einsatzbereit#2148,
/// since none of it needs a browser. What is left is the acceptance step,
/// which lives in Keycloak's own registration form and can only be seen by
/// driving the real one.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class TermsOfUsePageTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// #1079: the acceptance step lives in the registration form itself (a
	// "registration-terms-and-conditions" execution on Keycloak's built-in
	// "registration" flow, requirement REQUIRED) rather than a realm-wide
	// required action with defaultAction=true - the latter would attach to
	// every newly created Keycloak user, including the ad-hoc accounts other
	// integration/visual tests create via the admin API, breaking their
	// ROPC token grant with "Account is not fully set up". Stops short of
	// submitting the form (like Header_Anonymous_RegisterButton_Redirects...
	// in AuthGuardTests.cs) so this doesn't leave a dangling Keycloak user
	// behind with no cleanup mechanism.
	[Test]
	public async Task KeycloakRegistrationForm_RequiresAcceptingTermsOfUse()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).First.ClickAsync();

		await Expect(Page.Locator("#kc-register-form")).ToBeVisibleAsync(new() { Timeout = 30_000 });
		await Expect(Page.Locator("#kc-registration-terms-text")).ToBeVisibleAsync();
		await Expect(Page.Locator("#termsAccepted")).ToBeVisibleAsync();
	}
}
