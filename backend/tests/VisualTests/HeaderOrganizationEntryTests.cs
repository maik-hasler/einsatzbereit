using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HeaderOrganizationEntryTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 390;

	private const int MobileHeight = 844;

	private const int DesktopWidth = 1024;

	[Test]
	public async Task MobileMenu_Member_ShowsTheOrgAppAmongThePrimaryDestinations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var banner = Page.GetByRole(AriaRole.Banner);
		await banner.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		var entry = banner.GetByTestId("mobile-nav-organization");
		await Expect(entry).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(entry).ToHaveAttributeAsync("href", $"/app/{pinnedOrgId!.Value}/dashboard");

		await entry.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/app/{pinnedOrgId.Value}/dashboard", new() { Timeout = 15_000 });
	}

	[Test]
	public async Task DesktopHeader_AtTheDesktopBreakpoint_TheEntryFitsWithoutOverflowingTheRow()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(DesktopWidth, 1024);

		var entry = Page.GetByTestId("nav-organization");
		await Expect(entry).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var overflow = await Page.EvaluateAsync<int>(
			"() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
		overflow.Should().BeLessThanOrEqualTo(0,
			"the organization entry must not push the page into horizontal scroll");

		var reference = await Page.GetByTestId("nav-organizations").BoundingBoxAsync();
		var box = await entry.BoundingBoxAsync();
		reference.Should().NotBeNull();
		box.Should().NotBeNull();
		box!.Height.Should().BeLessThan(reference!.Height * 2,
			"a long organization name must truncate, not wrap the entry onto a second line");

		var label = (await entry.InnerTextAsync()).Trim();
		if (label.Length < 20)
			Skip.Test("seed data changed - the resolved organization's name is too short to truncate");

		box.Width.Should().BeGreaterThan(60, "the name must stay readable, not collapse to its first letter");
	}
}
