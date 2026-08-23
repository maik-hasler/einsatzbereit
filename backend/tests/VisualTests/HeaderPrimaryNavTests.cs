using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HeaderPrimaryNavTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 390;

	private const int MobileHeight = 844;

	[Test]
	public async Task MobileMenu_Anonymous_CarriesTheSameDestinations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);
		await Page.GotoAsync(origin);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).ClickAsync();

		var link = Page.GetByTestId("mobile-nav-findOpportunities");
		await Expect(link).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await link.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/opportunities", new() { Timeout = 15_000 });
	}

	[Test]
	public async Task MobileMenu_OnASubpage_CarriesTheSameWayHome()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);
		await Page.GotoAsync($"{origin}/help");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToHaveCountAsync(0);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).ClickAsync();

		var link = Page.GetByTestId("mobile-nav-home");
		await Expect(link).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await link.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 15_000 });
	}
}
