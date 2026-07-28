using System.Net.Http.Json;
using System.Text.Json;
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
		// #809: two org names sharing both a first letter and a common prefix
		// used to collapse the switcher's name span to almost nothing on phone
		// widths (the brand wordmark plus the mobile bell/hamburger left it no
		// room), rendering as just "F.." for both - indistinguishable. Fixed by
		// cropping the header wordmark to its icon mark on mobile whenever the org
		// switcher is present (frees the width the name needs) plus a min-width
		// floor on the name span itself (OrganizationSwitcher.tsx).
		//
		// Seed two throwaway orgs sharing a prefix here instead of relying on
		// olaf's original seed orgs ("Fairview Red Cross" / "Fairview Animal
		// Welfare Association") still being present: across the full shared
		// VisualTests session olaf accumulates dozens of throwaway orgs from
		// other test classes, and the switcher's org list (unpaginated, sorted
		// by name) is not guaranteed to still contain those two specific names
		// by the time this test runs - only that whichever org this test itself
		// creates will be there.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");
		var secondOrgName = $"MobileSwitcherShared Beta {suffix}";

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		(await http.PostAsJsonAsync(
			"/v1/organizations", new { name = $"MobileSwitcherShared Alpha {suffix}" }))
			.EnsureSuccessStatusCode();
		(await http.PostAsJsonAsync("/v1/organizations", new { name = secondOrgName }))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		await switcherBtn.ClickAsync();

		var targetRow = Page.GetByTestId("org-switch-row").Filter(new() { HasText = secondOrgName });
		await Expect(targetRow).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await targetRow.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });

		// The name span's rendered box, not its (untruncated-by-CSS) text content,
		// is what actually regresses - assert on the box width so a truncated
		// render is caught even though the DOM text is always the full name.
		// The switcher re-fetches orgs on navigation and briefly renders a
		// loading skeleton with no name span at all, so wait for the new org's
		// name to actually be showing before measuring - otherwise BoundingBoxAsync
		// races the skeleton and returns null.
		var nameSpan = Page.GetByTestId("org-switcher-current-name");
		await Expect(nameSpan).ToHaveTextAsync(secondOrgName, new() { Timeout = 15_000 });
		var box = await nameSpan.BoundingBoxAsync();
		box.Should().NotBeNull();
		box!.Width.Should().BeGreaterThan(60,
			"the org name must keep enough width on mobile to show more than just its "
			+ "first letter - it previously rendered at ~0px wide here");
	}
}
