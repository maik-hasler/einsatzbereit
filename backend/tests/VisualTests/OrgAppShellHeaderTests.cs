using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the redesigned org app shell requested in #742 (and refined
/// in the #744 review): the plain "Back to Einsatzbereit" text link became the
/// Einsatzbereit logo (linking to the main site); the section tabs were removed
/// from the header entirely and relocated into the org page's main content area;
/// and an icon-led breadcrumb (home icon + current subpage label) now occupies
/// the action bar directly beneath the header, where the tab bar used to sit.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppShellHeaderTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OrgAppShell_LogoInHeader_BreadcrumbInActionBar_TabsInMainContent()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		// Land on a known subpage so the breadcrumb has a stable current-page label.
		await Page.GotoAsync($"{origin}/app/{organizationId}/settings");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Logo replaces the old text link and sits top-left in the header.
		var logoLink = Page.Locator("header a[href='/']");
		await Expect(logoLink.Locator("img")).ToBeVisibleAsync();
		await Expect(Page.GetByText("Back to Einsatzbereit")).Not.ToBeVisibleAsync();

		// Breadcrumb: home-icon link + the current subpage label ("Settings"). It
		// lives in the action bar beneath the header, NOT inside <header> itself.
		var breadcrumb = Page.GetByRole(AriaRole.Navigation, new() { Name = "Breadcrumb" });
		await Expect(breadcrumb).ToBeVisibleAsync();
		await Expect(breadcrumb.GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToBeVisibleAsync();
		await Expect(breadcrumb).ToContainTextAsync("Settings");
		(await Page.Locator("header nav[aria-label='Breadcrumb']").CountAsync())
			.Should().Be(0, "the breadcrumb moved out of the header into the action bar");

		// The org switcher remains present in the header as its own separate control.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToBeVisibleAsync();

		// The section tabs are gone from the header/action bar and now render inside
		// the org page's main content area.
		var tabBar = Page.GetByRole(AriaRole.Navigation, new() { Name = "Organization sections" });
		await Expect(tabBar).ToBeVisibleAsync();
		(await Page.Locator("header nav[aria-label='Organization sections']").CountAsync())
			.Should().Be(0, "the tabs must not remain in the header");
		(await Page.Locator("main nav[aria-label='Organization sections']").CountAsync())
			.Should().Be(1, "the tabs moved into the org page's main content area");

		// Active-tab logic is preserved: the Settings tab is marked current.
		await Expect(tabBar.GetByRole(AriaRole.Link, new() { Name = "Settings" }))
			.ToHaveAttributeAsync("aria-current", "page");

		// Clicking the logo navigates to the main site.
		await logoLink.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 15_000 });
	}
}
