using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for #750: the org app header had no responsive breakpoints at
/// all, so controls overlapped or got squeezed below the 768px `md`
/// breakpoint. Per the #755 follow-up review, the org app header is no longer
/// a bespoke duplicate - it's the same shared <c>Header.tsx</c> component the
/// public site uses, just grown an optional org-switcher slot, so its mobile
/// behavior (bell/hamburger always visible, avatar/profile/sign-out/language
/// collapsed behind the hamburger) is identical to the public site's and
/// already covered by <c>MobileHeaderTests</c>. What's specific to the org
/// app here is that the org switcher's own name must not overflow onto the
/// bell/hamburger. #771 removed the tab bar and the per-page org-name
/// heading; #775/#777 brought the tab bar back for a time (shared ORG_TABS,
/// also reused by the burger menu's org submenu), but the dashboard UX
/// redesign removed it again in favor of the dashboard's own widget links
/// plus the burger menu's org submenu as the sole way to reach opportunities/
/// members/settings - see <c>OrganizationDashboardNavLinkTests</c> for that
/// submenu's own coverage.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppMobileResponsiveTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 375;
	private const int MobileHeight = 812;

	[Test]
	public async Task MobileHeader_OrgSwitcherDoesNotBlockControls_HamburgerRevealsProfileAndLanguage()
	{
		// Log in at the default (desktop) viewport - AuthHelper.LoginAsync looks
		// for the "Sign in" button that only exists in the public header's
		// desktop nav (`hidden md:flex`); at mobile width it lives behind that
		// header's own hamburger instead. Resize only after landing in the app.
		var frontend = Fixture.GetEndpoint("frontend");
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		// The mobile bell and hamburger must stay visible and clickable - the
		// org-switcher's flex-1 wrapper sits directly to their left, and a long
		// org name previously overflowed onto them (fixed in
		// OrganizationSwitcher.tsx by making the button/name span shrink with
		// min-w-0/flex-1 instead of growing past the available space).
		var mobileBell = Page.GetByTestId("notification-bell-mobile");
		await Expect(mobileBell).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var hamburger = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" });
		await Expect(hamburger).ToBeVisibleAsync();

		// Same shared component as the public site: no persistent avatar/"User
		// menu" button on mobile - it only appears inside the opened hamburger.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
			.Not.ToBeVisibleAsync();

		// A click that lands here (rather than timing out on an intercepting
		// element) proves the org switcher isn't overlapping the hamburger.
		await hamburger.ClickAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "My Profile" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task MobileHeader_OrgSwitcherName_StaysLegibleForOrgsSharingAnInitial()
	{
		// #809: olaf organizes "Fairview Red Cross" and "Fairview Animal Welfare
		// Association" - two names sharing both a first letter and a "Fairview "
		// prefix. The switcher's name span used to collapse to almost nothing on
		// phone widths (the brand wordmark plus the mobile bell/hamburger left it
		// no room), rendering as just "F.." for both - indistinguishable. Fixed by
		// cropping the header wordmark to its icon mark on mobile whenever the org
		// switcher is present (frees the width the name needs) plus a min-width
		// floor on the name span itself (OrganizationSwitcher.tsx).
		var frontend = Fixture.GetEndpoint("frontend");
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		await switcherBtn.ClickAsync();

		var animalWelfareRow = Page.GetByTestId("org-switch-row")
			.Filter(new() { HasText = "Fairview Animal Welfare Association" });
		if (await animalWelfareRow.CountAsync() == 0)
			return; // seed data changed - nothing to compare against

		await animalWelfareRow.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });

		// The name span's rendered box, not its (untruncated-by-CSS) text content,
		// is what actually regresses - assert on the box width so a truncated
		// render is caught even though the DOM text is always the full name.
		// The switcher re-fetches orgs on navigation and briefly renders a
		// loading skeleton with no name span at all, so wait for the new org's
		// name to actually be showing before measuring - otherwise BoundingBoxAsync
		// races the skeleton and returns null.
		var nameSpan = Page.GetByTestId("org-switcher-current-name");
		await Expect(nameSpan).ToHaveTextAsync("Fairview Animal Welfare Association", new() { Timeout = 15_000 });
		var box = await nameSpan.BoundingBoxAsync();
		box.Should().NotBeNull();
		box!.Width.Should().BeGreaterThan(60,
			"the org name must keep enough width on mobile to show more than just its "
			+ "first letter - it previously rendered at ~0px wide here");
	}
}
