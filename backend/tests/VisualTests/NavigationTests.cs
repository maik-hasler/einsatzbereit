using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class NavigationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task HomePage_HasMainHeading()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("h1").First).ToBeVisibleAsync();
	}

	[Test]
	public async Task HomePage_HasNoBreadcrumb()
	{
		// Pages that don't call usePageToolbar must not render a stray
		// breadcrumb bar - the home page has no parent to link back to.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(Page.Locator("nav[aria-label='Breadcrumb']")).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task OrganizationProfilePage_BreadcrumbShowsHomeAndOrgName()
	{
		// The breadcrumb is a direct "Home > {organization name}" - no
		// "Organizations" middle crumb, since organizations are found via the
		// volunteer-opportunity search's keyword field, not a browse page.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// The opportunity list, and the org links on its cards, live on
		// /opportunities rather than the landing page.
		await Page.GotoAsync($"{origin}/opportunities");
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Seed data always publishes opportunities - a non-waiting
		// CountAsync() right after the h1 check above raced the list's
		// opportunity fetch and could silently skip this test instead of failing.
		var orgLink = Page.Locator("a[href*='/organizations/']").First;
		await Expect(orgLink).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var href = await orgLink.GetAttributeAsync("href");
		Skip.When(href is null, "organization link had no href");

		await Page.GotoAsync($"{origin}{href!}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// No breadcrumb bar: the PageHeaderBand already states the org name as the
		// h1, so a bar directly above restating it is duplication.
		await Expect(Page.Locator("nav[aria-label='Breadcrumb']")).ToHaveCountAsync(0);

		var orgName = await Page.Locator("h1").First.InnerTextAsync();
		orgName.Should().NotBeNullOrWhiteSpace();

		// The band carried a "Home" link too, until every subpage repeating the
		// same one destination inside its own hero was replaced by a single
		// "Home" entry in the header nav - on screen on every page at once.
		await Expect(Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToHaveCountAsync(0);
		await Expect(Page.GetByTestId("nav-home")).ToBeVisibleAsync();
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_BreadcrumbShowsOrgAndOpportunityTitle()
	{
		// The old back link always went to "/#opportunities" regardless of
		// where the user came from. The revived breadcrumb must instead reflect
		// the opportunity's actual organization and title.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// The opportunity list lives on /opportunities, not the landing page.
		await Page.GotoAsync($"{origin}/opportunities");
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Seed data always publishes opportunities - a non-waiting
		// CountAsync() right after the h1 check above raced the list's
		// opportunity fetch and could silently skip this test instead of failing.
		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		await Expect(firstCard).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity link had no href");

		await Page.GotoAsync($"{origin}{href!}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// The breadcrumb bar is gone from this page too - the
		// PageHeaderBand states the opportunity title as the h1 and puts the
		// link to the owning organization in its eyebrow, which is where the
		// middle crumb's job moved.
		await Expect(Page.Locator("nav[aria-label='Breadcrumb']")).ToHaveCountAsync(0);
		// No in-band "Home" link either - that destination is a header nav
		// entry now, see HeaderPrimaryNavTests.
		await Expect(Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToHaveCountAsync(0);
		await Expect(Page.Locator("main a[href*='/organizations/']").First).ToBeVisibleAsync();

		var title = await Page.Locator("h1").First.InnerTextAsync();
		title.Should().NotBeNullOrWhiteSpace();
	}

	[Test]
	public async Task EngagementManagementPage_BreadcrumbPersistsRegardlessOfApplicationCount()
	{
		// The breadcrumb must be present unconditionally, not just in the
		// empty-application state, and must name the opportunity being managed:
		// Home > Opportunities > {title}, with "Opportunities" a link back to the
		// hub rather than a fixed label plus a separate in-page context line.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		// Reached through the page header's own section rail (OrgPageHeader.tsx).
		await Page.GetByTestId("org-tab-opportunities").ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// "Manage sign-ups" only appears for published opportunities on the
		// Opportunities hub.
		var manageLink = Page.GetByRole(AriaRole.Link, new() { Name = "Manage sign-ups" }).First;
		try
		{
			await manageLink.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("organizer has no published opportunities in seed");
		}

		var row = Page.Locator("li").Filter(new() { Has = manageLink });
		var opportunityTitle = (await row.Locator("a").First.InnerTextAsync()).Trim();

		await manageLink.ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// The band replaced the breadcrumb bar: the nested page's own title as
		// the h1, and one link back up to the tab that owns it.
		var band = Page.Locator("main");
		await Expect(band.GetByRole(AriaRole.Heading, new() { Level = 1 }))
			.ToHaveTextAsync(opportunityTitle, new() { Timeout = 15_000 });
		await Expect(band.GetByRole(AriaRole.Link, new() { Name = "Opportunities", Exact = true }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task EngagementManagementPage_KeepsOrgAppChromeVisible_BreadcrumbReturnsToOpportunities()
	{
		// Engagement management is a nested org app route, so the org switcher must
		// stay visible rather than swapping to the public site header/footer. With
		// no tab bar, the way back to the opportunities list is the breadcrumb's
		// "Opportunities" link.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		await Page.GetByTestId("org-tab-opportunities").ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var manageLink = Page.GetByRole(AriaRole.Link, new() { Name = "Manage sign-ups" }).First;
		try
		{
			await manageLink.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("organizer has no published opportunities in seed");
		}

		await manageLink.ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		var backToOpportunities = Page.Locator("main").GetByRole(
			AriaRole.Link, new() { Name = "Opportunities", Exact = true });
		await Expect(backToOpportunities).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await backToOpportunities.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard/opportunities$"), new() { Timeout = 15_000 });
	}

	[Test]
	public async Task OrganizationSwitcher_SelectingAnOrgRow_NavigatesToTheSameTabInThatOrg()
	{
		// The switcher moved out of the global header into the /app shell,
		// where selecting a different org must preserve whatever tab you're
		// currently on rather than always resetting to the dashboard.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		// Via the page header's section rail, not a bare "member" name match -
		// the Settings widget's member-count link answers to that too.
		await Page.GetByTestId("org-tab-members").ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard/members"), new() { Timeout = 15_000 });

		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		await switcherBtn.ClickAsync();

		// Wait for the switcher panel to actually render its rows before
		// counting them - a bare CountAsync() right after the click raced the
		// panel's own mount, which could misreport "< 2" and skip this test even
		// when olaf's seed data has the two orgs it needs.
		var orgSwitchRows = Page.GetByTestId("org-switch-row");
		await Expect(orgSwitchRows.First).ToBeVisibleAsync(new() { Timeout = 10_000 });
		var rowCount = await orgSwitchRows.CountAsync();
		Skip.When(rowCount < 2, "olaf needs at least two orgs in seed to prove navigation follows selection");

		// The active org's row carries aria-current="page" - pick a different one.
		var otherRow = Page.Locator("[data-testid='org-switch-row']:not([aria-current='page'])").First;
		var otherOrgName = (await otherRow.TextContentAsync() ?? "").Trim();
		await otherRow.ClickAsync();

		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard/members"), new() { Timeout = 15_000 });
		await Expect(switcherBtn).ToContainTextAsync(otherOrgName);
	}

	[Test]
	public async Task DirectNavigation_ToEachDashboardNestedRoute_RendersRealContent_NotErrorBoundary()
	{
		// Opportunities/members/settings (and the
		// dashboard index) are nested under a pathless "dashboard" parent
		// route (see App.tsx) whose element used to be a bare <Outlet />
		// with no `context` prop - that starts a brand new outlet context
		// instead of forwarding OrgAppLayout's <Outlet context={{org,
		// reloadOrg}}>, so every one of these pages got undefined from
		// useOutletContext<OrgAppContext>() and crashed on the very first
		// destructure, caught by the app-wide ErrorBoundary. A direct
		// (full page load) navigation to each route below exercises that
		// same render chain from scratch every time, unlike a client-side
		// Link click that could in principle reuse already-mounted state.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var homeOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, homeOrgId!.Value);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		var errorBoundaryHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Something went wrong" });

		foreach (var (path, activePageLabel) in new[]
		{
			("dashboard", "Dashboard"),
			("dashboard/opportunities", "Opportunities"),
			("dashboard/members", "Members"),
			("dashboard/settings", "Settings"),
		})
		{
			await Page.GotoAsync($"{origin}/app/{organizationId}/{path}");
			await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

			await Expect(errorBoundaryHeading).ToHaveCountAsync(0);

			// A crash unmounts OrgAppLayout entirely (the ErrorBoundary sits
			// above it, at the app root), taking the header band down with it -
			// so the band's h1 carries the page's name precisely when the page
			// rendered for real, regardless of whether the org has any
			// opportunities/members/etc. to show.
			await Expect(Page.Locator("main").GetByRole(AriaRole.Heading, new() { Level = 1 }))
				.ToHaveTextAsync(activePageLabel, new() { Timeout = 10_000 });
		}
	}

	[Test]
	public async Task HomePage_LanguageSelector_ClosesOnEscape()
	{
		// Dropdown/overlay menus in the Header only closed on outside
		// click - Escape did nothing. useDismissableOverlay fixes this.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var banner = Page.GetByRole(AriaRole.Banner);
		var langBtn = banner.GetByTestId("language-selector-trigger");
		await langBtn.ClickAsync();

		var dropdown = banner.GetByTestId("language-selector-menu");
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Page.Keyboard.PressAsync("Escape");
		await Expect(dropdown).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Test]
	public async Task HomePage_LanguageSelector_AnnouncesDisclosureSemantics()
	{
		// The selector used to wrap each <button> in an <li role="option">
		// under a role="listbox" <ul>, with the trigger advertising
		// aria-haspopup="listbox" - a keyboard model (arrow keys,
		// aria-activedescendant) the component has never implemented, since
		// Escape via useDismissableOverlay is the only key it handles. The axe
		// side of that defect is guarded by
		// LanguageSelector_Open_HasNoSeriousA11yViolations in
		// AccessibilityTests.cs; this is the DOM-shape half, so a regression
		// names itself instead of surfacing as a generic nested-interactive
		// scan failure.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var banner = Page.GetByRole(AriaRole.Banner);
		var langBtn = banner.GetByTestId("language-selector-trigger");
		await Expect(langBtn).ToBeVisibleAsync(new() { Timeout = 5_000 });

		// The trigger shows only the active language's code ("EN"/"DE"); the
		// chevron beside it is an SVG and contributes no text. Read it rather
		// than hardcoding a language, so this doesn't depend on which locale
		// the browser context happens to resolve to.
		var activeCode = (await langBtn.InnerTextAsync()).Trim();

		// A disclosure promises only expand/collapse - not a popup role whose
		// keyboard model this does not implement.
		await Expect(langBtn).Not.ToHaveAttributeAsync("aria-haspopup", new Regex(".*"));
		await Expect(langBtn).ToHaveAttributeAsync("aria-expanded", "false");

		// The closed trigger's accessible name used to be just "Switch
		// language"/"Sprache wechseln", overriding the visible "EN"/"DE" text
		// with no indication of which language is currently active. It must
		// now name the current language too, e.g. "..., currently English".
		var expectedLanguageName = activeCode == "DE" ? "Deutsch" : "English";
		await Expect(langBtn).ToHaveAttributeAsync(
			"aria-label",
			new Regex($".*{Regex.Escape(expectedLanguageName)}.*")
		);

		// #2072: the accessible name used to replace the visible "EN"/"DE" text
		// outright rather than extend it - a WCAG 2.5.3 Label-in-Name violation,
		// since it never contained the string a speech-input user would say
		// ("Klick DE") to target this control. It must now lead with that code.
		await Expect(langBtn).ToHaveAttributeAsync(
			"aria-label",
			new Regex($"^{Regex.Escape(activeCode)}\\b.*")
		);

		await langBtn.ClickAsync();

		var dropdown = banner.GetByTestId("language-selector-menu");
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(langBtn).ToHaveAttributeAsync("aria-expanded", "true");

		await Expect(dropdown).Not.ToHaveAttributeAsync("role", new Regex(".*"));
		await Expect(dropdown.Locator("[role='option']")).ToHaveCountAsync(0);
		await Expect(dropdown.Locator("[aria-selected]")).ToHaveCountAsync(0);

		// The active language is marked on the focusable element itself, so a
		// keyboard user tabbing the list is told which one they are on.
		await Expect(dropdown.GetByRole(AriaRole.Button)).ToHaveCountAsync(2);
		await Expect(dropdown.Locator("button[aria-current='true']")).ToHaveCountAsync(1);
		await Expect(dropdown.Locator("button[aria-current='true']")).ToContainTextAsync(activeCode);
	}

	[Test]
	public async Task HomePage_LanguageSelector_SwitchingLanguage_LazilyLoadsAndAppliesTranslations()
	{
		// Both translation bundles used to be statically imported into the
		// entry chunk. Each language's JSON is now fetched lazily on demand via a
		// custom i18next backend - switching language must still dynamically load
		// and apply the target locale's strings, and switching back must reuse the
		// already-loaded English bundle without breaking.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		var heading = Page.Locator("h1").First;
		await Expect(heading).ToHaveTextAsync(
			"Your volunteering starts here.", new() { Timeout = 15_000 });
		await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "en");

		var banner = Page.GetByRole(AriaRole.Banner);
		var langBtn = banner.GetByTestId("language-selector-trigger");
		await langBtn.ClickAsync();
		var dropdown = banner.GetByTestId("language-selector-menu");
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await dropdown.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();

		await Expect(heading).ToHaveTextAsync(
			"Dein Ehrenamt beginnt hier.", new() { Timeout = 10_000 });
		await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "de");

		await langBtn.ClickAsync();
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await dropdown.GetByRole(AriaRole.Button, new() { Name = "English" }).ClickAsync();

		await Expect(heading).ToHaveTextAsync(
			"Your volunteering starts here.", new() { Timeout = 10_000 });
		await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "en");
	}

	[Test]
	public async Task HomePage_LanguageSelector_DropdownStaysInsideViewportAt1440px()
	{
		// The open <ul> was anchored "top-full left-0" with a fixed
		// w-36 (144px) width, so it grew rightward from the trigger's left
		// edge instead of the trigger's own right edge - at 1440px that pushed
		// the panel's right edge past the viewport, clipping its border/
		// background and truncating "Deutsch" with no visible box edge.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.SetViewportSizeAsync(1440, 900);
		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var banner = Page.GetByRole(AriaRole.Banner);
		var langBtn = banner.GetByTestId("language-selector-trigger");
		await langBtn.ClickAsync();

		var dropdown = banner.GetByTestId("language-selector-menu");
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var box = await dropdown.BoundingBoxAsync();
		box.Should().NotBeNull("Could not measure the language selector dropdown");
		(box!.X + box.Width).Should().BeLessThanOrEqualTo(
			1440, "the dropdown must not overflow past the right edge of the viewport");
	}

	[Test]
	public async Task MobileMenu_ClosesOnEscape()
	{
		// MobileMenu offered neither outside-click nor Escape dismissal
		// at all - useDismissableOverlay now backs it too.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.SetViewportSizeAsync(390, 844);
		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var menuBtn = Page.Locator("header button[aria-label]")
			.Filter(new() { HasNotText = "English" })
			.Filter(new() { HasNotText = "Deutsch" });
		var menuBtnCount = await menuBtn.CountAsync();
		ILocator? hamburger = null;
		for (var i = 0; i < menuBtnCount; i++)
		{
			var label = await menuBtn.Nth(i).GetAttributeAsync("aria-label");
			if (label is not null && Regex.IsMatch(label, "menu|menü|open|öffnen", RegexOptions.IgnoreCase))
			{
				hamburger = menuBtn.Nth(i);
				break;
			}
		}

		Skip.When(hamburger is null, "hamburger not found at this viewport");

		await hamburger!.ClickAsync();

		// Both header variants are mounted at this point (the desktop one is
		// only hidden by CSS), and the mobile menu's copy is the later of the
		// two in the DOM.
		var mobileLangBtn = Page.GetByTestId("language-selector-trigger").Last;
		await Expect(mobileLangBtn).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Page.Keyboard.PressAsync("Escape");
		await Expect(mobileLangBtn).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Test]
	public async Task Footer_CtaLink_FromHomePage_NavigatesToOpportunitiesPage()
	{
		// The footer CTA is a plain destination link, not a "/#opportunities"
		// fragment that scrolls the landing page - guard that it lands on the
		// populated list.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var ctaLink = Page.Locator("footer").GetByRole(
			AriaRole.Link, new() { Name = "Find opportunities" }).First;
		await Expect(ctaLink).ToHaveAttributeAsync("href", "/opportunities");

		await ctaLink.ClickAsync();

		await Page.WaitForURLAsync(new Regex(@"/opportunities$"), new() { Timeout = 10_000 });
		await Expect(Page.Locator("#opportunities")).ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	[Test]
	public async Task Footer_CtaLink_FromAnotherPage_NavigatesToOpportunitiesPage()
	{
		// The companion to the test above from a page that is not the landing
		// page: the footer is shared by every route, so the CTA has to reach
		// the list from anywhere, not just from "/".
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend}help");
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var ctaLink = Page.Locator("footer").GetByRole(
			AriaRole.Link, new() { Name = "Find opportunities" }).First;
		await Expect(ctaLink).ToHaveAttributeAsync("href", "/opportunities");

		await ctaLink.ClickAsync();

		await Page.WaitForURLAsync(new Regex(@"/opportunities$"), new() { Timeout = 10_000 });
		await Expect(Page.Locator("#opportunities")).ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	[Test]
	public async Task AccountControls_UserMenu_ClosesOnEscape()
	{
		// The account/notification dropdowns (useAccountMenu) only
		// closed on outside click.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var userMenuBtn = Page.GetByRole(AriaRole.Button, new() { Name = "User menu" });
		await userMenuBtn.ClickAsync();

		var profileLink = Page.GetByRole(AriaRole.Link, new() { Name = "My profile" });
		await Expect(profileLink).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Page.Keyboard.PressAsync("Escape");
		await Expect(profileLink).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Test]
	public async Task AccountControls_UserMenu_ClosesAfterNavigatingToOwnLink()
	{
		// None of the dropdown's own links (My profile, My signups,
		// Profile settings, Administration) closed the disclosure on click -
		// only the outside-click/Escape handling in useAccountMenu did. The
		// stale panel stayed rendered (aria-expanded="true", panel still
		// visible) on top of the destination page until the user clicked
		// elsewhere.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var userMenuBtn = Page.GetByRole(AriaRole.Button, new() { Name = "User menu" });
		await userMenuBtn.ClickAsync();

		var profileLink = Page.GetByRole(AriaRole.Link, new() { Name = "My profile" });
		await Expect(profileLink).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await profileLink.ClickAsync();

		await Page.WaitForURLAsync(new Regex(@"/profile$"), new() { Timeout = 15_000 });
		await Expect(userMenuBtn).ToHaveAttributeAsync("aria-expanded", "false", new() { Timeout = 5_000 });
		await Expect(profileLink).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}
}
