using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Issue #775: users with >=1 organization had no way to reach the org
/// dashboard from the mobile burger menu - only the desktop org switcher and
/// the homepage hero CTA linked to it. Adds an "Organization" entry to the
/// mobile menu, gated the same way as the admin-only "Administration" entry
/// (see AdministrationNavLinkTests), resolved via the same
/// active-org-cookie-then-alphabetical logic HomePage already uses.
///
/// Follow-up from PR #777 review: a single link only reached the dashboard
/// tab, forcing an extra tap to get to opportunities/members/settings. The
/// entry is now a collapsible submenu (ORG_TABS, shared with OrgAppLayout's
/// own tab bar) so every org tab is reachable directly from the burger menu.
///
/// The entry was originally labeled "Organization Dashboard"; later
/// consolidated onto the shared nav.organization translation key so mobile
/// matches the desktop avatar dropdown's own org-submenu toggle, which has
/// always just read "Organization" (see AccountControls.tsx).
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrganizationDashboardNavLinkTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 375;
	private const int MobileHeight = 812;

	[Test]
	public async Task MobileMenu_UserWithOrg_ShowsOrganizationSubmenu_AndNavigatesToEachTab()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		// Olaf organizes an org in seed data (see HomePageOrgCtaTests).
		// FastSignInAsync verifies auth by waiting for the desktop "User menu"
		// button, which is CSS-hidden below the md breakpoint - so sign in
		// before shrinking to a mobile viewport, not after.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		// Scope every mobile-menu lookup below to the <header> landmark
		// (implicit ARIA "banner" role). Once we're inside the org app shell,
		// OrgAppLayout's own tab bar (visible at every viewport width, not just
		// desktop - see its "Organization sections" nav) renders links with the
		// exact same names ("Members", "Settings", ...), so an unscoped
		// GetByRole lookup is ambiguous between the two navs.
		var banner = Page.GetByRole(AriaRole.Banner);

		await banner.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		var toggle = banner.GetByRole(AriaRole.Button, new() { Name = "Organization" });
		await Expect(toggle).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Collapsed by default: the org tab links aren't reachable yet.
		// Exact match - the homepage's hero/footer "Find opportunities" and
		// "Browse opportunities" links are still in the DOM behind the mobile
		// menu overlay and would otherwise ambiguously match too (Playwright's
		// default name matching is a case-insensitive substring match).
		var opportunitiesLink = banner.GetByRole(
			AriaRole.Link,
			new() { Name = "Opportunities", Exact = true }
		);
		await Expect(opportunitiesLink).Not.ToBeVisibleAsync();

		await toggle.ClickAsync();
		await Expect(opportunitiesLink).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await opportunitiesLink.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/opportunities"), new() { Timeout = 15_000 });

		// Re-open and confirm the remaining tabs are all reachable too.
		await banner.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });
		await banner.GetByRole(AriaRole.Button, new() { Name = "Organization" })
			.ClickAsync(new() { Timeout = 10_000 });

		await banner.GetByRole(AriaRole.Link, new() { Name = "Members", Exact = true })
			.ClickAsync(new() { Timeout = 10_000 });
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/members"), new() { Timeout = 15_000 });

		await banner.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });
		await banner.GetByRole(AriaRole.Button, new() { Name = "Organization" })
			.ClickAsync(new() { Timeout = 10_000 });

		await banner.GetByRole(AriaRole.Link, new() { Name = "Settings", Exact = true })
			.ClickAsync(new() { Timeout = 10_000 });
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/settings"), new() { Timeout = 15_000 });

		await banner.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });
		await banner.GetByRole(AriaRole.Button, new() { Name = "Organization" })
			.ClickAsync(new() { Timeout = 10_000 });

		await banner.GetByRole(AriaRole.Link, new() { Name = "Dashboard", Exact = true })
			.ClickAsync(new() { Timeout = 10_000 });
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}

	[Test]
	public async Task MobileMenu_UserWithoutOrgs_HasNoOrganizationSubmenu()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		// admin has no organization memberships in seed data.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Administration" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Organization" }))
			.Not.ToBeVisibleAsync();
	}
}
