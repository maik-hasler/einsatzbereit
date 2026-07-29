using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the redesigned org app shell requested in #742 (and refined
/// in the #744 review): the plain "Back to Einsatzbereit" text link became the
/// Einsatzbereit logo (linking to the main site), and an icon-led breadcrumb
/// (home icon + current subpage label) occupies the action bar directly
/// beneath the header, separate from the section-tabs nav (see
/// OrgAppMobileResponsiveTests for that nav's own coverage - #771 briefly
/// removed it, #775/#777 brought it back for the mobile burger submenu).
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppShellHeaderTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OrgAppShell_LogoInHeader_BreadcrumbInActionBar()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		// Land on a known subpage so the breadcrumb has a stable current-page label.
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/settings");
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
		// The breadcrumb moved out of the header into the action bar.
		await Expect(Page.Locator("header nav[aria-label='Breadcrumb']")).ToHaveCountAsync(0);

		// The org switcher remains present in the header as its own separate control.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToBeVisibleAsync();

		// Clicking the logo navigates to the main site.
		await logoLink.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 15_000 });
	}
}
