using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the redesigned org app shell requested in #742 (and refined
/// in the #744 review): the plain "Back to Einsatzbereit" text link became the
/// Einsatzbereit logo (linking to the main site), and an icon-led breadcrumb
/// (home icon + current subpage label) occupies the action bar directly
/// beneath the header. #771 went further and removed the section tabs
/// entirely (they had briefly lived in the main content area per #744) -
/// dashboard widgets are now the only navigation into a subpage, so this
/// class no longer asserts anything about a tab bar's existence or location.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppShellHeaderTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OrgAppShell_LogoInHeader_BreadcrumbInActionBar()
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

		// #771: no section-tabs nav exists anywhere in the shell anymore.
		(await Page.Locator("nav[aria-label='Organization sections']").CountAsync())
			.Should().Be(0, "the tab bar was removed entirely - dashboard widgets replace it");

		// Clicking the logo navigates to the main site.
		await logoLink.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 15_000 });
	}
}
