using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// The header's primary-navigation landmark used to contain no destinations at
/// all - only sign-in/register or the account controls - because the
/// opportunity list lived inside the landing page behind an "#opportunities"
/// fragment. These pin the destinations, on both breakpoints, signed out and
/// signed in.
///
/// "Home" is one of those destinations now: it used to be a link inside every
/// subpage's own PageHeaderBand hero instead, which put site navigation in a
/// per-page surface and repeated it once per page.
/// </summary>
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
		// The two breakpoints render the same primary destinations from two
		// separate arrays (DesktopHeader's LINKS, MobileMenu's PRIMARY_LINKS),
		// so "home" being added to one and not the other is a live failure
		// mode - and the band link this replaces was visible on both.
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

	[Test]
	public async Task HeroSearch_NavigatesToTheListCarryingItsKeyword()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(origin);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// The hero used to write URL params and scroll to an anchor on the same
		// page; it now hands those params to the list's own route.
		await Page.GetByTestId("hero-keyword-input").FillAsync("Tierheim");
		await Page.GetByRole(AriaRole.Button, new() { Name = "Search" }).First.ClickAsync();

		await Page.WaitForURLAsync(
			new System.Text.RegularExpressions.Regex(@"/opportunities\?.*q=Tierheim"),
			new() { Timeout = 15_000 });
	}
}
