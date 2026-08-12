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
		// #574: pages that don't call usePageToolbar must not render a stray
		// breadcrumb bar - the home page has no parent to link back to.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(Page.Locator("nav[aria-label='Breadcrumb']")).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task OrganizationProfilePage_BreadcrumbShowsHomeAndOrgName()
	{
		// #574: OrganizationProfilePage had no way back at all - revived
		// breadcrumb must show "Home > {organization name}". #772/#763 had
		// briefly inserted an "Organizations" middle crumb linking to a
		// public directory page - removed along with that directory feature
		// (organizations are now found via the volunteer-opportunity search's
		// keyword field instead of a separate browse page), so the trail is
		// back to a direct "Home > {org name}".
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// #1755 moved the opportunity list (and with it the org links on its
		// cards) off the landing page onto /opportunities.
		await Page.GotoAsync($"{origin}/opportunities");
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// #1708: seed data always publishes opportunities - a non-waiting
		// CountAsync() right after the h1 check above raced the list's
		// opportunity fetch and could silently skip this test instead of failing.
		var orgLink = Page.Locator("a[href*='/organizations/']").First;
		await Expect(orgLink).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var href = await orgLink.GetAttributeAsync("href");
		Skip.When(href is null, "organization link had no href");

		await Page.GotoAsync($"{origin}{href!}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #1755 replaced this page's breadcrumb bar with a PageHeaderBand: the
		// band states the org name as the h1, so the bar restating it directly
		// above was pure duplication.
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
		// #574: the old back link always went to "/#opportunities" regardless of
		// where the user came from. The revived breadcrumb must instead reflect
		// the opportunity's actual organization and title.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// #1755 moved the opportunity list off the landing page onto
		// /opportunities.
		await Page.GotoAsync($"{origin}/opportunities");
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// #1708: seed data always publishes opportunities - a non-waiting
		// CountAsync() right after the h1 check above raced the list's
		// opportunity fetch and could silently skip this test instead of failing.
		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		await Expect(firstCard).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity link had no href");

		await Page.GotoAsync($"{origin}{href!}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #1755: the breadcrumb bar is gone from this page too - the
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
		// #574: back navigation used to only appear in the empty-application state.
		// The revived breadcrumb must be present unconditionally.
		//
		// #751 review follow-up: the breadcrumb must show the specific
		// opportunity being managed - Home > Opportunities > {title}, with
		// "Opportunities" demoted to a link back to the hub - instead of a
		// fixed "Opportunities" label plus a separate context line in the page.
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
		// #751: engagement management moved into the org app as a nested route
		// under /app/:organizationId/dashboard/opportunities/:opportunityId/engagements -
		// the org switcher must stay visible instead of swapping to the public
		// site header/footer. #771 removed the tab bar entirely (aria-current
		// on a tab link no longer applies), so leaving back to the opportunities
		// list now happens via the breadcrumb's "Opportunities" link instead.
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
		// #702: the switcher moved out of the global header into the /app shell,
		// where selecting a different org must preserve whatever tab you're
		// currently on rather than always resetting to the dashboard.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		// Members lives in the page header's section rail (OrgPageHeader.tsx) -
		// the same rail an organizer uses, and unambiguous unlike a bare
		// "member" name match, which the Settings widget's own member-count link
		// also answers to.
		await Page.GetByTestId("org-tab-members").ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard/members"), new() { Timeout = 15_000 });

		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		await switcherBtn.ClickAsync();

		// #1708: wait for the switcher panel to actually render its rows before
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
		// Regression for #783/#787: opportunities/members/settings (and the
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

			// The path should render real content, not the ErrorBoundary fallback.
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
		// #884: dropdown/overlay menus in the Header only closed on outside
		// click - Escape did nothing. useDismissableOverlay fixes this.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var langBtn = Page.Locator("header [data-testid='language-selector-trigger']").First;
		await langBtn.ClickAsync();

		var dropdown = Page.Locator("header [data-testid='language-selector-menu']").First;
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Page.Keyboard.PressAsync("Escape");
		await Expect(dropdown).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Test]
	public async Task HomePage_LanguageSelector_SwitchingLanguage_LazilyLoadsAndAppliesTranslations()
	{
		// #1395: both translation bundles used to be statically imported into the
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

		var langBtn = Page.Locator("header [data-testid='language-selector-trigger']").First;
		await langBtn.ClickAsync();
		var dropdown = Page.Locator("header [data-testid='language-selector-menu']").First;
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
	public async Task MobileMenu_ClosesOnEscape()
	{
		// #884: MobileMenu offered neither outside-click nor Escape dismissal
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

		var mobileLangBtn = Page.Locator("[data-testid='language-selector-trigger']").Last;
		await Expect(mobileLangBtn).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Page.Keyboard.PressAsync("Escape");
		await Expect(mobileLangBtn).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Test]
	public async Task Footer_CtaLink_FromHomePage_NavigatesToOpportunitiesPage()
	{
		// #1031 covered a fragment link ("/#opportunities") that had to scroll
		// the landing page. #1755 gave the list its own route, so the footer
		// CTA is a plain destination link now - what still needs guarding is
		// that it actually lands on the populated list rather than the
		// landing page it used to scroll.
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
		// #884: the account/notification dropdowns (useAccountMenu) only
		// closed on outside click.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var userMenuBtn = Page.GetByRole(AriaRole.Button, new() { Name = "User menu" });
		await userMenuBtn.ClickAsync();

		var profileLink = Page.GetByRole(AriaRole.Link, new() { Name = "My Profile" });
		await Expect(profileLink).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Page.Keyboard.PressAsync("Escape");
		await Expect(profileLink).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}
}
