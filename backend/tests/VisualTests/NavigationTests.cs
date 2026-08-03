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
	public async Task HomePage_LanguageSelector_HasDarkTransparentTheme_OnHero()
	{
		// Regression: LanguageSelector dropdown on the hero section should use the
		// dark (transparent) theme - bg-brand-800 with white text - instead of the
		// white light theme that was shown before the fix (PR #441).
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		// Wait for the hero h1 so React has fully rendered the Header with isTransparent=true.
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var langBtn = Page.Locator("header button[aria-haspopup='listbox']").First;
		await Expect(langBtn).ToBeVisibleAsync();

		// Button must carry transparent Tailwind classes (border-white/30, text-white).
		await Expect(langBtn).ToHaveClassAsync(new Regex("border-white|text-white"));

		// Open the dropdown and verify it uses the dark brand background.
		await langBtn.ClickAsync();
		var dropdown = Page.Locator("header ul[role='listbox']").First;
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Expect(dropdown).ToContainClassAsync("bg-brand-800");
		await Expect(dropdown).ToContainClassAsync("left-0");

		await Page.Keyboard.PressAsync("Escape");
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
	public async Task OrganizationProfilePage_BreadcrumbShowsHomeOrganizationsAndOrgName()
	{
		// #574: OrganizationProfilePage had no way back at all - revived
		// breadcrumb must show "Home > {organization name}".
		// #772 review follow-up (issue #763): the trail must also link back
		// to the organization directory - "Home > Organizations > {org name}",
		// not jump straight from Home to the org.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgLink = Page.Locator("a[href*='/organizations/']").First;
		Skip.When(await orgLink.CountAsync() == 0, "no opportunities/organizations seeded");

		var href = await orgLink.GetAttributeAsync("href");
		Skip.When(href is null, "organization link had no href");

		await Page.GotoAsync($"{origin}{href!}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var breadcrumb = Page.Locator("nav[aria-label='Breadcrumb']");
		await Expect(breadcrumb).ToBeVisibleAsync();

		var homeCrumb = breadcrumb.Locator("a[href='/']");
		await Expect(homeCrumb).ToBeVisibleAsync();

		var organizationsCrumb = breadcrumb.GetByRole(AriaRole.Link, new() { Name = "Organizations" });
		await Expect(organizationsCrumb).ToBeVisibleAsync();
		await Expect(organizationsCrumb).ToHaveAttributeAsync("href", "/organizations");

		var orgName = await Page.Locator("h1").First.InnerTextAsync();
		await Expect(breadcrumb.GetByText(orgName, new() { Exact = true })).ToBeVisibleAsync();
	}

	[Test]
	public async Task HomePage_ShowsOrganizationsTeaser_LinkingToDirectory()
	{
		// #772 review follow-up round 2 (issue #763): a permanent Header nav
		// entry point was judged too heavy a commitment for an unvalidated
		// use case - replaced with a homepage section instead, so the
		// directory stays discoverable without growing the global nav.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var teaserCta = Page.GetByTestId("organizations-teaser-cta");
		await teaserCta.ScrollIntoViewIfNeededAsync();
		await Expect(teaserCta).ToBeVisibleAsync();
		await Expect(teaserCta).ToHaveAttributeAsync("href", "/organizations");

		await teaserCta.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/organizations$"), new() { Timeout = 10_000 });
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_BreadcrumbShowsOrgAndOpportunityTitle()
	{
		// #574: the old back link always went to "/#opportunities" regardless of
		// where the user came from. The revived breadcrumb must instead reflect
		// the opportunity's actual organization and title.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		Skip.When(await firstCard.CountAsync() == 0, "no opportunities seeded");

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity link had no href");

		await Page.GotoAsync($"{origin}{href!}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var breadcrumb = Page.Locator("nav[aria-label='Breadcrumb']");
		await Expect(breadcrumb).ToBeVisibleAsync();
		await Expect(breadcrumb.Locator("a[href='/']")).ToBeVisibleAsync();

		// Middle crumb links to the opportunity's organization, matching the org
		// chip rendered further down the page.
		var orgChipHref = await Page.Locator("a[href*='/organizations/']").First.GetAttributeAsync("href");
		if (orgChipHref is not null)
			await Expect(breadcrumb.Locator($"a[href='{orgChipHref}']")).ToBeVisibleAsync();

		// Last crumb (current page, no link) matches the opportunity title.
		var title = await Page.Locator("h1").First.InnerTextAsync();
		await Expect(breadcrumb.GetByText(title, new() { Exact = true })).ToBeVisibleAsync();
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

		// #771: the tab bar is gone - reach Opportunities via a dashboard widget link.
		await Page.GetByRole(AriaRole.Link, new() { Name = "opportunities" }).First.ClickAsync();
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

		var breadcrumb = Page.Locator("nav[aria-label='Breadcrumb']");
		await Expect(breadcrumb).ToBeVisibleAsync();
		await Expect(breadcrumb.GetByRole(AriaRole.Link, new() { Name = "Opportunities", Exact = true }))
			.ToBeVisibleAsync();
		await Expect(breadcrumb.GetByText(opportunityTitle, new() { Exact = true }))
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

		await Page.GetByRole(AriaRole.Link, new() { Name = "opportunities" }).First.ClickAsync();
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

		var breadcrumb = Page.Locator("nav[aria-label='Breadcrumb']");
		var breadcrumbOpportunitiesLink = breadcrumb.GetByRole(
			AriaRole.Link, new() { Name = "Opportunities", Exact = true });
		await Expect(breadcrumbOpportunitiesLink).ToBeVisibleAsync();

		await breadcrumbOpportunitiesLink.ClickAsync();
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

		// The tab bar is gone (dashboard UX redesign) - reach Members via the
		// Settings widget's member-count link instead (its accessible name is
		// "N member(s)" - #834 made the count grammatically correct German/
		// English plural forms, so match "member" to cover both N=1 and N>1).
		await Page.GetByRole(AriaRole.Link, new() { Name = "member" }).ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard/members"), new() { Timeout = 15_000 });

		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		await switcherBtn.ClickAsync();

		var rowCount = await Page.GetByTestId("org-switch-row").CountAsync();
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
		var breadcrumb = Page.Locator("nav[aria-label='Breadcrumb']");

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
			// above it, at the app root), taking the breadcrumb down with it -
			// so its current-page label is present precisely when the page
			// rendered for real, regardless of whether the org has any
			// opportunities/members/etc. to show.
			await Expect(breadcrumb.Locator("[aria-current='page']"))
				.ToHaveTextAsync(activePageLabel, new() { Timeout = 10_000 });
		}
	}

	[Test]
	public async Task MobileMenu_LanguageSelector_HasDarkTransparentTheme_OnHero()
	{
		// Regression: LanguageSelector inside the mobile menu on the hero section
		// must use the transparent dark theme (PR #441) - white text, dark dropdown.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.SetViewportSizeAsync(390, 844);
		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Open the mobile menu by clicking the button with aria-label matching openMenu.
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

		var mobileLangBtn = Page.Locator("button[aria-haspopup='listbox']").Last;
		await Expect(mobileLangBtn).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Expect(mobileLangBtn).ToHaveClassAsync(new Regex("border-white|text-white"));
	}

	[Test]
	public async Task HomePage_LanguageSelector_ClosesOnEscape()
	{
		// #884: dropdown/overlay menus in the Header only closed on outside
		// click - Escape did nothing. useDismissableOverlay fixes this.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var langBtn = Page.Locator("header button[aria-haspopup='listbox']").First;
		await langBtn.ClickAsync();

		var dropdown = Page.Locator("header ul[role='listbox']").First;
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
			"Volunteering doesn't have to be hard.", new() { Timeout = 15_000 });
		await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "en");

		var langBtn = Page.Locator("header button[aria-haspopup='listbox']").First;
		await langBtn.ClickAsync();
		var dropdown = Page.Locator("header ul[role='listbox']").First;
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await dropdown.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();

		await Expect(heading).ToHaveTextAsync(
			"Ehrenamt muss nicht schwer sein.", new() { Timeout = 10_000 });
		await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "de");

		await langBtn.ClickAsync();
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await dropdown.GetByRole(AriaRole.Button, new() { Name = "English" }).ClickAsync();

		await Expect(heading).ToHaveTextAsync(
			"Volunteering doesn't have to be hard.", new() { Timeout = 10_000 });
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

		var mobileLangBtn = Page.Locator("button[aria-haspopup='listbox']").Last;
		await Expect(mobileLangBtn).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Page.Keyboard.PressAsync("Escape");
		await Expect(mobileLangBtn).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Test]
	public async Task Footer_ParticipateLink_FromHomePage_ScrollsToOpportunitiesSection()
	{
		// #1031: the footer's "Get involved" link used a react-router Link,
		// which navigates via the history API without triggering native
		// browser fragment scrolling - clicking it from the home page did
		// nothing visible. Now a plain <a href="/#opportunities"> like the
		// hero CTA, so the browser handles the scroll itself.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var participateLink = Page.Locator("footer").GetByRole(
			AriaRole.Link, new() { Name = "Get involved" });
		await Expect(participateLink).ToHaveAttributeAsync("href", "/#opportunities");

		await participateLink.ClickAsync();

		await Page.WaitForURLAsync(new Regex(@"/#opportunities$"), new() { Timeout = 10_000 });
		await Expect(Page.Locator("#opportunities")).ToBeInViewportAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task Footer_ParticipateLink_FromAnotherPage_NavigatesHomeAndScrollsToOpportunitiesSection()
	{
		// #1031: from any page other than home, the old Link-based footer
		// link navigated to "/" but stayed scrolled to the top. A plain
		// anchor tag forces a full navigation to "/#opportunities", which
		// the browser scrolls to once the home page has rendered.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend}organizations");
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var participateLink = Page.Locator("footer").GetByRole(
			AriaRole.Link, new() { Name = "Get involved" });
		await Expect(participateLink).ToHaveAttributeAsync("href", "/#opportunities");

		await participateLink.ClickAsync();

		await Page.WaitForURLAsync(new Regex(@"/#opportunities$"), new() { Timeout = 10_000 });
		await Expect(Page.Locator("#opportunities")).ToBeInViewportAsync(new() { Timeout = 10_000 });
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
