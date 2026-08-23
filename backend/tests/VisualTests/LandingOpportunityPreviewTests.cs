using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LandingOpportunityPreviewTests(AspireFixture fixture) : VisualTestBase(fixture)
{
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
	public async Task LandingPreview_AtTabletViewport_IsTwoColumnGridLikeOpportunitiesList()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http,
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

		var firstCard = preview.Locator("li").First;
		var title = (await firstCard.Locator("h3").InnerTextAsync()).Trim();

		await firstCard.Locator("a[href*='/volunteer-opportunities/']").First.ClickAsync();

		await Page.WaitForURLAsync(
			new System.Text.RegularExpressions.Regex(@"/volunteer-opportunities/[0-9a-f-]+"),
			new() { Timeout = 15_000 });
		await Expect(Page.Locator("h1").First).ToContainTextAsync(title);
	}
}
