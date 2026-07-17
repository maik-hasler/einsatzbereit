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
		var btnClass = await langBtn.GetAttributeAsync("class") ?? string.Empty;
		btnClass.Should().MatchRegex(new Regex("border-white|text-white"));

		// Open the dropdown and verify it uses the dark brand background.
		await langBtn.ClickAsync();
		var dropdown = Page.Locator("header ul[role='listbox']").First;
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var dropdownClass = await dropdown.GetAttributeAsync("class") ?? string.Empty;
		dropdownClass.Should().Contain("bg-brand-800");
		dropdownClass.Should().Contain("left-0");

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
	public async Task OrganizationProfilePage_BreadcrumbShowsHomeAndOrgName()
	{
		// #574: OrganizationProfilePage had no way back at all - revived
		// breadcrumb must show "Home > {organization name}".
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgLink = Page.Locator("a[href*='/organizations/']").First;
		if (await orgLink.CountAsync() == 0)
			return; // no opportunities/organizations seeded - skip

		var href = await orgLink.GetAttributeAsync("href");
		if (href is null)
			return;

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var breadcrumb = Page.Locator("nav[aria-label='Breadcrumb']");
		await Expect(breadcrumb).ToBeVisibleAsync();

		var homeCrumb = breadcrumb.Locator("a[href='/']");
		await Expect(homeCrumb).ToBeVisibleAsync();

		var orgName = await Page.Locator("h1").First.InnerTextAsync();
		await Expect(breadcrumb.GetByText(orgName, new() { Exact = true })).ToBeVisibleAsync();
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
		if (await firstCard.CountAsync() == 0)
			return; // no opportunities seeded, skip

		var href = await firstCard.GetAttributeAsync("href");
		if (href is null)
			return;

		await Page.GotoAsync($"{origin}{href}");
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
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		if (!await GoToFirstOrganizationDashboardAsync())
			return; // no org selected in seed - skip

		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Link, new() { Name = "Engagements", Exact = true }).ClickAsync();

		var manageLink = Page.GetByText("Manage engagements").First;
		if (await manageLink.CountAsync() == 0)
			return; // organizer has no opportunities in seed - skip

		await manageLink.ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("nav[aria-label='Breadcrumb']")).ToBeVisibleAsync();
	}

	[Test]
	public async Task OrganizationSwitcher_SelectingAnOrgRow_NavigatesToItsDashboard()
	{
		// #691/#702: selecting an org from the switcher dropdown must navigate
		// to that org's dashboard. #702 merged the row's separate name button
		// and dashboard-link icon button into a single button per row (inside
		// the app shell, selecting a row always means "navigate there"), so
		// this only needs to click a second row's org-dashboard-link button.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The switcher only lives inside the app shell now - get there via
		// /profile's "Your organizations" list first.
		if (!await GoToFirstOrganizationDashboardAsync())
			return; // no org selected in seed - skip

		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		await switcherBtn.First.ClickAsync();
		var dashboardLinks = Page.GetByTestId("org-dashboard-link");
		var rowCount = await dashboardLinks.CountAsync();
		if (rowCount < 2)
			return; // olaf needs at least two orgs in seed to prove navigation follows selection

		// Click a *second* row (not the first/active one) - there is now only
		// one button per row, doing both "select" and "navigate".
		var secondRow = dashboardLinks.Nth(1);
		var secondOrgName = (await secondRow.Locator("span.truncate").InnerTextAsync()).Trim();
		await secondRow.ClickAsync();

		await Page.WaitForURLAsync(
			new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });

		await Expect(switcherBtn.First).ToContainTextAsync(secondOrgName);
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

		if (hamburger is null)
			return; // hamburger not found at this viewport - skip gracefully

		await hamburger.ClickAsync();

		var mobileLangBtn = Page.Locator("button[aria-haspopup='listbox']").Last;
		await Expect(mobileLangBtn).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var mobileBtnClass = await mobileLangBtn.GetAttributeAsync("class") ?? string.Empty;
		mobileBtnClass.Should().MatchRegex(new Regex("border-white|text-white"));
	}
}
