using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class NavigationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OrganizationProfilePage_BreadcrumbShowsHomeAndOrgName()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities");
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgLink = Page.Locator("a[href*='/organizations/']").First;
		await Expect(orgLink).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var href = await orgLink.GetAttributeAsync("href");
		Skip.When(href is null, "organization link had no href");

		await Page.GotoAsync($"{origin}{href!}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("nav[aria-label='Breadcrumb']")).ToHaveCountAsync(0);

		var orgName = await Page.Locator("h1").First.InnerTextAsync();
		orgName.Should().NotBeNullOrWhiteSpace();

		await Expect(Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToHaveCountAsync(0);
		await Expect(Page.GetByTestId("nav-home")).ToBeVisibleAsync();
	}

	[Test]
	public async Task EngagementManagementPage_KeepsOrgAppChromeVisible_BreadcrumbReturnsToOpportunities()
	{
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
	public async Task DirectNavigation_ToEachDashboardNestedRoute_RendersRealContent_NotErrorBoundary()
	{
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

			await Expect(Page.Locator("main").GetByRole(AriaRole.Heading, new() { Level = 1 }))
				.ToHaveTextAsync(activePageLabel, new() { Timeout = 10_000 });
		}
	}

	[Test]
	public async Task HomePage_LanguageSelector_ClosesOnEscape()
	{
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
	public async Task HomePage_LanguageSelector_SwitchingLanguage_LazilyLoadsAndAppliesTranslations()
	{
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

		var mobileLangBtn = Page.GetByTestId("language-selector-trigger").Last;
		await Expect(mobileLangBtn).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Page.Keyboard.PressAsync("Escape");
		await Expect(mobileLangBtn).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Test]
	public async Task Footer_CtaLink_FromHomePage_NavigatesToOpportunitiesPage()
	{
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
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var userMenuBtn = Page.GetByRole(AriaRole.Button, new() { Name = "User menu" });
		await userMenuBtn.ClickAsync();

		var profileLink = Page.GetByRole(AriaRole.Link, new() { Name = "My profile" });
		await Expect(profileLink).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Page.Keyboard.PressAsync("Escape");
		await Expect(profileLink).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}
}
