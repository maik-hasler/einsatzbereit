using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1757 gave the opportunity list its own /opportunities route and left the
/// landing page with no trace of real inventory: a hero promising "find an
/// opportunity that fits you", and then straight into a pitch at
/// organizations. These pin the three-card preview that answers the hero -
/// that it renders seeded opportunities, that it stays a preview rather than
/// growing back into the grid, that it sits ahead of the organization band so
/// the page stays volunteer-facing until it changes audience, and that both
/// its link and its cards are real entry points.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LandingOpportunityPreviewTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int PreviewCount = 3;

	[Test]
	public async Task LandingPage_RendersSeededOpportunitiesAsAPreview()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.GetLeftPart(UriPartial.Authority));
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var preview = Page.GetByTestId("landing-latest-opportunities");
		await Expect(preview).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// A preview, not the list: /opportunities is what paginates. The lower
		// bound matters as much as the upper one - the section removes itself
		// when the fetch comes back empty, so an assertion that only capped the
		// count would still pass against a landing page showing nothing.
		var cardCount = await preview.Locator("li").CountAsync();
		cardCount.Should().BeGreaterThan(0, "the seed data publishes opportunities");
		cardCount.Should().BeLessThanOrEqualTo(PreviewCount,
			"the landing page shows a preview, not the paginated grid");
	}

	[Test]
	public async Task LandingPage_PlacesThePreviewAheadOfTheOrganizationBand()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.GetLeftPart(UriPartial.Authority));
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var preview = Page.GetByTestId("landing-latest-opportunities");
		await Expect(preview).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var previewBox = await preview.BoundingBoxAsync();
		var orgBandBox = await Page.Locator("#for-organizations").BoundingBoxAsync();

		previewBox.Should().NotBeNull();
		orgBandBox.Should().NotBeNull();
		previewBox!.Y.Should().BeLessThan(orgBandBox!.Y,
			"a volunteer reaching the organization pitch before any opportunity is the ordering #1757 left behind");
	}

	[Test]
	public async Task PreviewLink_LeadsToTheFullOpportunityList()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(origin);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var link = Page.GetByTestId("landing-all-opportunities-link");
		await Expect(link).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(link).ToHaveTextAsync("Browse all opportunities");

		await link.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/opportunities", new() { Timeout = 15_000 });
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Find opportunities");
	}

	[Test]
	public async Task PreviewCard_OpensTheOpportunityItNames()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(origin);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var preview = Page.GetByTestId("landing-latest-opportunities");
		await Expect(preview).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var firstCard = preview.Locator("li").First;
		var title = (await firstCard.Locator("h2").InnerTextAsync()).Trim();

		// The stretched-link pattern the card uses (an absolutely positioned
		// <a> covering the <li>) is easy to break with a later z-index change,
		// and a preview whose cards are not clickable is decoration.
		await firstCard.Locator("a[href*='/volunteer-opportunities/']").First.ClickAsync();

		await Page.WaitForURLAsync(
			new System.Text.RegularExpressions.Regex(@"/volunteer-opportunities/[0-9a-f-]+"),
			new() { Timeout = 15_000 });
		await Expect(Page.Locator("h1").First).ToContainTextAsync(title);
	}
}
