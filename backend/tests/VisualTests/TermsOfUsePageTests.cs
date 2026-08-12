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
	public async Task TermsOfUsePage_HasNoActionBar_AndShowsCoreSections()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/terms-of-use");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #1755 replaced this page's breadcrumb action bar with a Home link
		// inside the title band; that link is gone in turn, since the header
		// nav now carries "Home" on every page - see
		// HeaderBreadcrumbSharedImplementationTests.ImprintAndPrivacyPolicyPages_CarryNeitherAnActionBarNorAnInBandHomeLink
		// for the rationale and the cross-page guard.
		await Expect(Page.Locator("header + div nav[aria-label='Breadcrumb']"))
			.ToHaveCountAsync(0);
		await Expect(Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToHaveCountAsync(0);
		await Expect(Page.GetByTestId("nav-home")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Terms of Use", Level = 1 })).ToBeVisibleAsync();
		// Exact = false throughout this file's clause-heading assertions: since
		// #1755 each clause heading carries its own number ("2 Our role as a
		// platform"), because legal text is cited by clause and the number
		// therefore belongs in the heading's accessible name rather than being
		// an aria-hidden decoration. Substring matching keeps these assertions
		// about the clause titles themselves, so inserting a section above one
		// doesn't break an unrelated test.
		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Our role as a platform", Exact = false })).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Suspension and termination", Exact = false })).ToBeVisibleAsync();
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
			new() { Name = "Unsere Rolle als Plattform", Exact = false })).ToBeVisibleAsync();
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

	// #1665: section4Body claimed organizations get a verification badge that
	// confirms we reviewed their identity - no such feature exists anywhere
	// in the product. Pins the sentence's removal in both locales so it can't
	// silently come back before the feature does.
	[Test]
	public async Task TermsOfUsePage_DoesNotDescribeNonExistentVerificationBadge()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/terms-of-use");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Exact = false - see the clause-numbering note higher up this file.
		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Organizations and volunteer opportunities", Exact = false }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByText("verification badge", new() { Exact = false }))
			.Not.ToBeVisibleAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		await Page.GetByRole(AriaRole.Option, new() { Name = "Deutsch" }).ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Organisationen und Einsätze" })).ToBeVisibleAsync();
		await Expect(Page.GetByText("Verifizierungs-Badge", new() { Exact = false }))
			.Not.ToBeVisibleAsync();
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
