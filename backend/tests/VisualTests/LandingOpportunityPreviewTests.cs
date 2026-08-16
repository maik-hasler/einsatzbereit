using System.Net.Http.Json;
using System.Text.Json;
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

	/// <summary>
	/// #1914: at 768px the landing preview stayed single-column
	/// (grid-cols-1 until lg) while /opportunities, rendering the same
	/// OpportunityListItem card, already showed a two-column grid at that
	/// width (sm:grid-cols-2). Seeds two fresh published opportunities so the
	/// newest-first preview deterministically has at least two cards, then
	/// checks they land side by side rather than stacked - the same
	/// side-by-side check ListLayoutGridTests uses for /opportunities itself.
	/// </summary>
	[Test]
	public async Task LandingPreview_AtTabletViewport_IsTwoColumnGridLikeOpportunitiesList()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await http.PostAsJsonAsync(
			"/v1/organizations", new { name = $"Visual1914 LandingGrid {Guid.NewGuid():N}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		foreach (var title in new[] { "Tablet Grid Card A", "Tablet Grid Card B" })
		{
			var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				titleDe = title,
				descriptionDe = "Seeded for #1914 landing tablet grid visual test.",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "IndividualContact",
				checkInMethod = "None",
				validUntil = DateTimeOffset.UtcNow.AddDays(30),
				isDraft = false,
			});
			oppResponse.EnsureSuccessStatusCode();
		}

		await Page.SetViewportSizeAsync(768, 1024);
		await Page.GotoAsync(frontend.GetLeftPart(UriPartial.Authority));
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var preview = Page.GetByTestId("landing-latest-opportunities");
		await Expect(preview).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var display = await preview.EvaluateAsync<string>("el => getComputedStyle(el).display");
		display.Should().Be("grid", "the preview must render as a CSS grid at 768px, not a single-column stack");

		var items = preview.Locator("> li");
		(await items.CountAsync()).Should().BeGreaterThanOrEqualTo(2,
			"two opportunities were just seeded and the preview sorts newest first, so both must appear");

		var firstBox = await items.Nth(0).BoundingBoxAsync();
		var secondBox = await items.Nth(1).BoundingBoxAsync();
		firstBox.Should().NotBeNull();
		secondBox.Should().NotBeNull();

		Math.Abs(firstBox!.Y - secondBox!.Y).Should().BeLessThan(2,
			"at 768px the preview must show two columns like /opportunities does at the same viewport, "
			+ "rather than the oversized single column #1914 reported");
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

		// h3, not h2 - the card takes a headingLevel prop and this section
		// passes 3, because its own "These opportunities need people" heading is
		// the h2 these cards sit under. On /opportunities the same card renders
		// an h2, directly below that page's h1. Asserting the level here rather
		// than matching "h2, h3" keeps that distinction pinned.
		var firstCard = preview.Locator("li").First;
		var title = (await firstCard.Locator("h3").InnerTextAsync()).Trim();

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
