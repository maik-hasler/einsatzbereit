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
/// bell/hamburger. (#771 removed the tab bar and the per-page org-name
/// heading entirely, so the mobile tab-bar-no-wrap and header-stacking tests
/// that used to live here no longer have anything to assert on.)
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

}
