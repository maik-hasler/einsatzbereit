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
/// heading, dropping the header-stacking test that used to live here - but
/// #775/#777 brought the tab bar back (shared ORG_TABS, also reused by the
/// burger menu's org submenu), so its no-wrap-on-mobile coverage stays.
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
	public async Task TabBar_AllFourTabs_StayOnOneRow_NoWrapOnMobile()
	{
		// Log in at the default (desktop) viewport - AuthHelper.LoginAsync looks
		// for the "Sign in" button that only exists in the public header's
		// desktop nav (`hidden md:flex`); at mobile width it lives behind that
		// header's own hamburger instead. Resize only after landing in the app.
		var frontend = Fixture.GetEndpoint("frontend");
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var tabBar = Page.GetByRole(AriaRole.Navigation, new() { Name = "Organization sections" });
		await Expect(tabBar).ToBeVisibleAsync();

		var tabNames = new[] { "Dashboard", "Opportunities", "Settings", "Members" };
		var tabYPositions = new List<float>();
		foreach (var name in tabNames)
		{
			var box = await tabBar.GetByRole(AriaRole.Link, new() { Name = name }).BoundingBoxAsync();
			box.Should().NotBeNull($"could not measure the '{name}' tab");
			tabYPositions.Add(box!.Y);
		}

		// All four tabs must sit on the same visual row (previously `flex
		// gap-6` with no wrapping/scrolling let them run off-screen instead).
		var firstY = tabYPositions[0];
		foreach (var y in tabYPositions.Skip(1))
			Math.Abs(y - firstY).Should().BeLessThan(2, "tabs must stay on a single row, not wrap");

		// The tab row must be configured to scroll horizontally rather than wrap
		// or clip once a translation/tab set overflows the 375px container -
		// checked via the CSS mechanism itself (overflow-x: auto) rather than
		// scrollWidth > clientWidth, since whether today's specific English tab
		// labels happen to overflow at this exact viewport is a font-rendering
		// detail, not the thing this test should be pinned to.
		var overflowX = await tabBar.EvaluateAsync<string>(
			"el => getComputedStyle(el.firstElementChild).overflowX");
		overflowX.Should().Be("auto",
			"the tab row must allow horizontal scrolling so a longer/translated tab set stays reachable");
	}
}
