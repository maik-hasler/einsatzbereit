using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1079: the platform had no Terms of Use page, no route, and no acceptance
/// step anywhere - these tests pin the new /terms-of-use route, its footer
/// link, its breadcrumb bar, and its EN/DE content so the page can't silently
/// regress or go missing again.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class TermsOfUsePageTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task TermsOfUsePage_ShowsBreadcrumbBar_AndCoreSections()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/terms-of-use");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var actionBar = Page.Locator("header + div nav[aria-label='Breadcrumb']");
		await Expect(actionBar).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(actionBar.GetByText("Terms of Use", new() { Exact = true }))
			.ToBeVisibleAsync();

		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Terms of Use", Level = 1 })).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Our role as a platform" })).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Suspension and termination" })).ToBeVisibleAsync();
		await Expect(Page.GetByText("at your own risk", new() { Exact = false }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task TermsOfUsePage_ShowsGermanContent_WhenLanguageSwitched()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/terms-of-use");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		await Page.GetByRole(AriaRole.Option, new() { Name = "Deutsch" }).ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Nutzungsbedingungen", Level = 1 })).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Unsere Rolle als Plattform" })).ToBeVisibleAsync();
		await Expect(Page.GetByText("auf eigenes Risiko", new() { Exact = false }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task TermsOfUsePage_CrossLinksToContactPrivacyAndImprint()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/terms-of-use");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Scoped to <main> - the footer on every page also links /contact,
		// /privacy-policy, and /imprint, which would otherwise make these
		// locators match two elements each (Playwright strict mode).
		var main = Page.Locator("main");
		await Expect(main.Locator("a[href='/contact']")).ToBeVisibleAsync();
		await Expect(main.Locator("a[href='/privacy-policy']")).ToBeVisibleAsync();
		await Expect(main.Locator("a[href='/imprint']")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Footer_LegalLinks_IncludeTermsOfUse()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var footer = Page.Locator("footer");
		await Expect(footer.Locator("a[href='/terms-of-use']")).ToBeVisibleAsync();
		await Expect(footer.GetByText("Terms of Use", new() { Exact = true }))
			.ToBeVisibleAsync();
	}

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
