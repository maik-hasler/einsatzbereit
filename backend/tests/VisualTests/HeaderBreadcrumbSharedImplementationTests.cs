using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for #758: the org app shell's icon-led breadcrumb action bar
/// moved out of OrgAppLayout.tsx into the shared Header.tsx component, which
/// both the org app shell and the public site (via AppLayout.tsx +
/// usePageToolbar) now render through. Public-site pages previously used a
/// separate mechanism (ToolbarContext/Breadcrumb.tsx) that rendered plain-text
/// chips inside &lt;main&gt;, with no icon and no Home entry beneath &lt;header&gt;
/// specifically - these tests pin the new, shared behaviour.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HeaderBreadcrumbSharedImplementationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ProfilePage_ActionBar_RendersDirectlyBeneathHeader_IconLed()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// The action bar sits immediately after </header> as a sibling - the
		// same placement the org app shell already used - not inside <main>
		// where the old Breadcrumb.tsx/ToolbarContext mechanism rendered it.
		var actionBar = Page.Locator("header + div nav[aria-label='Breadcrumb']");
		await Expect(actionBar).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Home crumb is icon-led (aria-label only, no visible "Home" text),
		// matching the org app shell's style instead of the old plain-text chip.
		var homeLink = actionBar.GetByRole(AriaRole.Link, new() { Name = "Home" });
		await Expect(homeLink).ToBeVisibleAsync();
		await Expect(homeLink).ToHaveAttributeAsync("href", "/");
		// the home crumb should be icon-only (aria-label='Home'), not a visible text link
		await Expect(homeLink).ToHaveTextAsync("");
		await Expect(homeLink.Locator("svg")).ToBeVisibleAsync();

		await Expect(actionBar.GetByText("Profile", new() { Exact = true })).ToBeVisibleAsync();
	}

	[Test]
	public async Task OrgAppShell_ActionBar_StillSitsImmediatelyAfterHeader_NoRegression()
	{
		// #758 acceptance criterion: the org app shell's action bar must behave
		// exactly as before now that it shares Header.tsx's implementation
		// instead of its own copy - home icon + current tab label, directly
		// beneath <header>, with the org switcher remaining a separate control.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardViaCtaAsync(Page, frontend);

		var actionBar = Page.Locator("header + div nav[aria-label='Breadcrumb']");
		await Expect(actionBar).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(actionBar.GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToBeVisibleAsync();

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task ImprintAndPrivacyPolicyPages_ShowActionBar_AtTheirNewEnglishSlugs()
	{
		// Follow-up to #758: the legal pages were missed in the initial rollout
		// (still on ToolbarContext.tsx, no action bar) and used German slugs
		// (/impressum, /datenschutz) while every other route is English -
		// renamed to /imprint and /privacy-policy, with the action bar now
		// shared via Header.tsx like every other subpage.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/imprint");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		var imprintBar = Page.Locator("header + div nav[aria-label='Breadcrumb']");
		await Expect(imprintBar).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(imprintBar.GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToBeVisibleAsync();
		await Expect(imprintBar.GetByText("Imprint", new() { Exact = true }))
			.ToBeVisibleAsync();

		await Page.GotoAsync($"{origin}/privacy-policy");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		var privacyBar = Page.Locator("header + div nav[aria-label='Breadcrumb']");
		await Expect(privacyBar).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(privacyBar.GetByText("Privacy Policy", new() { Exact = true }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task OldGermanSlugs_AreRemoved_404sInstead()
	{
		// The old /impressum and /datenschutz routes are removed outright (no
		// redirect kept) - visiting them now falls through to the catch-all
		// NotFoundPage, same as any other unknown path.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/impressum");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Back to home" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.GotoAsync($"{origin}/datenschutz");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Back to home" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task Footer_LegalLinks_PointDirectlyAtNewEnglishSlugs()
	{
		// The footer itself should link straight to the new slugs, not rely on
		// the legacy redirect for its own internal navigation.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var footer = Page.Locator("footer");
		await Expect(footer.Locator("a[href='/imprint']")).ToBeVisibleAsync();
		await Expect(footer.Locator("a[href='/privacy-policy']")).ToBeVisibleAsync();
	}
}
