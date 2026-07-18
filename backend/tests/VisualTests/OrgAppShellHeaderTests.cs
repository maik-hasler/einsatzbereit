using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the redesigned org app shell header requested in #742: the
/// plain "Back to Einsatzbereit" text link became the Einsatzbereit logo (linking
/// to the main site), an icon-led breadcrumb (home icon + current subpage label)
/// was added, the org switcher stayed as a separate control, and the tab bar moved
/// out of the <c>&lt;header&gt;</c> into its own landmark directly beneath it.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppShellHeaderTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OrgAppShellHeader_ShowsLogoAndBreadcrumb_WithTabBarBelowHeader()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
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

		// Breadcrumb: home-icon link + the current subpage label ("Settings").
		var breadcrumb = Page.GetByRole(AriaRole.Navigation, new() { Name = "Breadcrumb" });
		await Expect(breadcrumb).ToBeVisibleAsync();
		await Expect(breadcrumb.GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToBeVisibleAsync();
		await Expect(breadcrumb).ToContainTextAsync("Settings");

		// The org switcher remains present as its own separate control.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToBeVisibleAsync();

		// The tab bar is its own landmark and a sibling of <header>, not nested in it.
		var tabBar = Page.GetByRole(AriaRole.Navigation, new() { Name = "Organization sections" });
		await Expect(tabBar).ToBeVisibleAsync();
		(await Page.Locator("header nav[aria-label='Organization sections']").CountAsync())
			.Should().Be(0, "the tab bar must render beneath <header>, not inside it");

		// Active-tab logic is preserved: the Settings tab is marked current.
		await Expect(tabBar.GetByRole(AriaRole.Link, new() { Name = "Settings" }))
			.ToHaveAttributeAsync("aria-current", "page");

		// Clicking the logo navigates to the main site.
		await logoLink.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 15_000 });
	}
}
