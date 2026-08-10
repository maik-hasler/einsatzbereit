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
	public async Task AccountPages_ReplaceActionBar_WithBandHomeLinkAndQuickActions()
	{
		// #758 made /profile the canonical example of the shared action bar.
		// #1755 gave the three account pages (/profile, /my-engagements,
		// /profile/settings) the same PageHeaderBand the legal pages use, and a
		// band page renders no action bar - the title would otherwise be stated
		// twice with a grey strip cutting through the treatment.
		//
		// The bar itself is unchanged and still covered on the org app shell by
		// OrgAppShell_ActionBar_StillSitsImmediatelyAfterHeader_NoRegression
		// below. What this pins instead is that the two things the bar carried
		// for /profile survived the move: the way back home, and the Edit
		// quick action.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My Profile", Level = 1 }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.Locator("header + div nav[aria-label='Breadcrumb']"))
			.ToHaveCountAsync(0);

		var homeLink = Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Home" });
		await Expect(homeLink).ToBeVisibleAsync();
		await Expect(homeLink).ToHaveAttributeAsync("href", "/");

		// useEditModeQuickActions publishes through QuickActionsContext, which
		// PageHeaderBand now reads in the action bar's place - same key and the
		// same data-testid, so the edit-mode tests keep working unchanged.
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync();

		// The sub-nav is what ties the three pages together now that each has
		// its own band; all three must offer all three destinations.
		var subNav = Page.Locator("main nav[aria-label]").First;
		foreach (var tab in new[] { "Profile", "Activity", "Settings" })
			await Expect(subNav.GetByRole(AriaRole.Link, new() { Name = tab })).ToBeVisibleAsync();
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
	public async Task ImprintAndPrivacyPolicyPages_ReplaceActionBar_WithInBandHomeLink()
	{
		// Follow-up to #758: the legal pages were missed in the initial rollout
		// (still on ToolbarContext.tsx, no action bar) and used German slugs
		// (/impressum, /datenschutz) while every other route is English -
		// renamed to /imprint and /privacy-policy, with the action bar then
		// shared via Header.tsx like every other subpage.
		//
		// #1755 dropped the action bar from exactly these pages again. They now
		// open with a full-bleed PageHeaderBand that states the page title in
		// 72px display type, so a grey strip immediately above it repeating
		// that same title was pure duplication - and it drew a hard white line
		// through the middle of the band treatment. The Home link #758 added
		// still exists, it just lives inside the band now. The slugs, and the
		// action bar on every *other* subpage, are unchanged - see
		// ProfilePage_ActionBar_RendersDirectlyBeneathHeader_IconLed above.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		foreach (var (path, title) in new[]
		{
			("/imprint", "Imprint"),
			("/privacy-policy", "Privacy Policy"),
		})
		{
			await Page.GotoAsync($"{origin}{path}");
			await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

			await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = title, Level = 1 }))
				.ToBeVisibleAsync(new() { Timeout = 15_000 });
			await Expect(Page.Locator("header + div nav[aria-label='Breadcrumb']"))
				.ToHaveCountAsync(0);
			await Expect(Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Home" }))
				.ToBeVisibleAsync();
		}
	}

	[Test]
	public async Task PageHeaderBand_MakesHeaderTransparent_UntilScrolledPastTheBand()
	{
		// #1755: the band runs up *behind* the sticky header (negative
		// --header-height margin), so the header has to drop its own white
		// background and switch its controls to on-dark variants while that's
		// true - otherwise a white bar sits across the top of a dark band.
		// Once scrolled past, there is white page underneath again and the
		// header has to take its background back, or white-on-dark controls
		// would be left sitting on white.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await Page.GotoAsync($"{origin}/terms-of-use");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var header = Page.Locator("header");
		await Expect(header).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(header).ToContainClassAsync("bg-transparent");

		await Page.EvaluateAsync("() => window.scrollTo(0, 600)");
		// The header cross-fades over 300ms (transition-all duration-300).
		await Expect(header).ToContainClassAsync("bg-white/95", new() { Timeout = 5_000 });

		// Leaving the page has to release the transparency again - the overlay
		// flag is refcounted precisely because React mounts the incoming route
		// before unmounting the outgoing one.
		await Page.GotoAsync($"{origin}/imprint");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(header).ToContainClassAsync("bg-transparent");

		await Page.GotoAsync($"{origin}/");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(header).ToContainClassAsync("bg-white");
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
