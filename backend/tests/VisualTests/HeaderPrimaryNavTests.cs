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
	public async Task DesktopHeader_Anonymous_LinksToTheOpportunityList()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(origin);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var link = Page.GetByTestId("nav-findOpportunities");
		await Expect(link).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await link.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/opportunities", new() { Timeout = 15_000 });

		// The list itself, not just the route - the band states the page name,
		// so the list must not restate it (showHeading={false}).
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Find opportunities");
		await Expect(Page.GetByTestId("opportunities-keyword-input"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	[Test]
	public async Task DesktopHeader_SignedIn_StillCarriesPrimaryDestinations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The regression this guards: signed in, the nav previously collapsed to
		// the account avatar and the language selector, leaving the account
		// dropdown as a volunteer's only route to the opportunity list.
		await Expect(Page.GetByTestId("nav-findOpportunities")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("nav-help")).ToBeVisibleAsync();
	}

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
	public async Task DesktopHeader_OnASubpage_CarriesTheWayHome_TheBandDoesNot()
	{
		// Every subpage used to open with its own "back to the home page" link
		// inside PageHeaderBand's hero - the same single destination restated
		// per page, in the one place a visitor does not look for site
		// navigation, and next to nothing else that navigates. It is a site
		// destination, so it is a nav entry now: one "Home" link beside the
		// other primary ones, on screen from every page at once.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/help");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1 }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToHaveCountAsync(0);

		var link = Page.GetByTestId("nav-home");
		await Expect(link).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(link).ToHaveAttributeAsync("href", "/");

		await link.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 15_000 });
		await Expect(Page.GetByTestId("hero-keyword-input"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
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
