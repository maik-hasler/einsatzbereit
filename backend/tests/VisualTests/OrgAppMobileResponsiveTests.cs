using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for #750: the org app header, tab bar, and opportunity
/// overview had no responsive breakpoints at all, so controls overlapped or
/// got squeezed below the 768px `md` breakpoint. The header now collapses the
/// language selector behind a hamburger button (matching the public site's
/// <c>Header.tsx</c> pattern) while keeping the notification bell and user
/// menu always visible; the tab bar scrolls horizontally instead of
/// wrapping/cutting off; and page headers plus opportunity rows stack
/// vertically instead of squeezing side by side.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppMobileResponsiveTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 375;
	private const int MobileHeight = 812;

	[Test]
	public async Task MobileHeader_LanguageSelectorHiddenUntilHamburgerOpened_BellAndAvatarAlwaysVisible()
	{
		// Log in at the default (desktop) viewport - AuthHelper.LoginAsync looks
		// for the "Sign in" button that only exists in the public header's
		// desktop nav (`hidden md:flex`); at mobile width it lives behind that
		// header's own hamburger instead. Resize only after landing in the app.
		var frontend = Fixture.GetEndpoint("frontend");
		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		// Bell + avatar (AccountControls) render at every viewport width.
		await Expect(Page.GetByTestId("notification-bell")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
			.ToBeVisibleAsync();

		var hamburger = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" });
		await Expect(hamburger).ToBeVisibleAsync();

		// Collapses behind the hamburger below md instead of squeezing into the header row.
		var languageSelector = Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" });
		await Expect(languageSelector).Not.ToBeVisibleAsync();

		// Opening the hamburger reveals it.
		await hamburger.ClickAsync();
		await Expect(languageSelector).ToBeVisibleAsync();
	}

	[Test]
	public async Task TabBar_AllFourTabs_StayOnOneRow_NoWrapOnMobile()
	{
		// Log in at the default (desktop) viewport - AuthHelper.LoginAsync looks
		// for the "Sign in" button that only exists in the public header's
		// desktop nav (`hidden md:flex`); at mobile width it lives behind that
		// header's own hamburger instead. Resize only after landing in the app.
		var frontend = Fixture.GetEndpoint("frontend");
		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var tabBar = Page.GetByRole(AriaRole.Navigation, new() { Name = "Organization sections" });
		await Expect(tabBar).ToBeVisibleAsync();

		var tabNames = new[] { "Calendar", "Opportunities", "Members", "Settings" };
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

		// The tab bar itself must be horizontally scrollable so an overflowing
		// tab set is reachable rather than clipped.
		var overflows = await tabBar.EvaluateAsync<bool>(
			"el => el.firstElementChild.scrollWidth > el.firstElementChild.clientWidth");
		overflows.Should().BeTrue(
			"the tab row should overflow its container width at 375px so overflow-x scrolling is meaningful");
	}

	[Test]
	public async Task OpportunitiesPageHeader_StacksTitleAboveButton_OnMobile()
	{
		// Log in at the default (desktop) viewport - AuthHelper.LoginAsync looks
		// for the "Sign in" button that only exists in the public header's
		// desktop nav (`hidden md:flex`); at mobile width it lives behind that
		// header's own hamburger instead. Resize only after landing in the app.
		var frontend = Fixture.GetEndpoint("frontend");
		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		await Page.GetByRole(AriaRole.Navigation, new() { Name = "Organization sections" })
			.GetByRole(AriaRole.Link, new() { Name = "Opportunities" })
			.ClickAsync();
		await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"/app/[^/]+/opportunities"));

		var heading = Page.GetByRole(AriaRole.Heading, new() { Level = 1 });
		var createButton = Page.GetByTestId("create-opportunity-btn");
		await Expect(heading).ToBeVisibleAsync();
		await Expect(createButton).ToBeVisibleAsync();

		var headingBox = await heading.BoundingBoxAsync();
		var buttonBox = await createButton.BoundingBoxAsync();
		headingBox.Should().NotBeNull();
		buttonBox.Should().NotBeNull();

		buttonBox!.Y.Should().BeGreaterThan(
			headingBox!.Y + headingBox.Height - 1,
			"the create-opportunity button should stack below the org name on mobile instead of squeezing beside it");

		// Full-width touch target on mobile.
		buttonBox.Width.Should().BeGreaterThan(
			headingBox.Width,
			"the button should span the available width when stacked, not stay pill-sized");
	}
}
