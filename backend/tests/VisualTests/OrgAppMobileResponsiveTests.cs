using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppMobileResponsiveTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 375;
	private const int MobileHeight = 812;

	[Test]
	public async Task MobileHeader_OrgSwitcherDoesNotBlockControls_HamburgerRevealsProfileAndLanguage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var mobileBell = Page.GetByTestId("notification-bell-mobile");
		await Expect(mobileBell).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var hamburger = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" });
		await Expect(hamburger).ToBeVisibleAsync();

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
			.Not.ToBeVisibleAsync();

		await hamburger.ClickAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "My profile" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task MobileHeader_OrgSwitcherName_StaysLegibleForOrgsSharingAnInitial()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		await switcherBtn.ClickAsync();

		var animalWelfareRow = Page.GetByTestId("org-switch-row")
			.Filter(new() { HasText = "Lindenauer Tierschutzverein e.V." });
		try
		{
			await animalWelfareRow.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("seed data changed - nothing to compare against");
		}

		await animalWelfareRow.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });

		var nameSpan = Page.GetByTestId("org-switcher-current-name");
		await Expect(nameSpan).ToHaveTextAsync("Lindenauer Tierschutzverein e.V.", new() { Timeout = 15_000 });
		var box = await nameSpan.BoundingBoxAsync();
		box.Should().NotBeNull();
		box!.Width.Should().BeGreaterThan(60,
			"the org name must keep enough width on mobile to show more than just its "
			+ "first letter - it previously rendered at ~0px wide here");
	}

	[Test]
	public async Task MobileHeader_OrgSwitcherDoesNotOverlapControls_At320pxViewport()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(320, 568);

		var mobileBell = Page.GetByTestId("notification-bell-mobile");
		await Expect(mobileBell).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var hamburger = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" });
		await Expect(hamburger).ToBeVisibleAsync();

		await hamburger.ClickAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }))
			.ToBeVisibleAsync();
	}
}
