using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Issue #775: users with >=1 organization had no way to reach the org
/// dashboard from the mobile burger menu - only the desktop org switcher and
/// the homepage hero CTA linked to it. Adds an "Organization Dashboard" entry
/// to the mobile menu, gated the same way as the admin-only "Administration"
/// entry (see AdministrationNavLinkTests), resolved via the same
/// active-org-cookie-then-alphabetical logic HomePage already uses.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrganizationDashboardNavLinkTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 375;
	private const int MobileHeight = 812;

	[Test]
	public async Task MobileMenu_UserWithOrg_ShowsOrganizationDashboardLink_AndNavigatesToItsDashboard()
	{
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var frontend = Fixture.GetEndpoint("frontend");

		// Olaf organizes an org in seed data (see HomePageOrgCtaTests).
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		var link = Page.GetByRole(AriaRole.Link, new() { Name = "Organization Dashboard" });
		await Expect(link).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await link.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}

	[Test]
	public async Task MobileMenu_UserWithoutOrgs_HasNoOrganizationDashboardLink()
	{
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var frontend = Fixture.GetEndpoint("frontend");

		// admin has no organization memberships in seed data.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Administration" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Organization Dashboard" }))
			.Not.ToBeVisibleAsync();
	}
}
