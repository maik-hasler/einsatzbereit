using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1778: /opportunities rendered its result count only into an
/// sr-only paragraph (measured 1x1px on live staging), so a sighted user got no
/// count at all - not on load, not after filtering, and no sense of how much was
/// still behind "Load more". The strings themselves already existed and were
/// already correctly worded in both locales; they simply never reached the screen.
///
/// The fix makes that same node visible rather than adding a second one, so these
/// tests assert both halves: the count is genuinely painted (a real box, not the
/// 1x1 clipped rect Tailwind's sr-only produces - the exact shape the issue
/// measured) and it still carries the role="status" that announces it on every
/// filter change. The one deliberate exception is an empty list, where it stays
/// sr-only rather than printing "0 opportunities found." directly above
/// EmptyState's "No opportunities found." - covered by the last assertions here.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityResultCountTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// useVolunteerOpportunitiesData's computePageSize pages in 9 at xl (>=1280px,
	// 3 cols x 3 rows). Pinned explicitly rather than inherited from PageTest's
	// default viewport, same as LoadMoreErrorPreservesItemsTests.
	private const int WideViewportWidth = 1440;
	private const int WideViewportHeight = 900;
	private const int PageSize = 9;

	private const string CountSelector = "[data-testid='opportunities-result-count']";

	/// <summary>
	/// Seeds one throwaway organization with an opportunity per title, all
	/// carrying <paramref name="tag"/> so <c>/opportunities?tag=...</c> shows
	/// exactly these and nothing else - the count under test has to be
	/// deterministic while ~50 other classes concurrently seed their own
	/// opportunities into the shared-session database (same tag-scoping pattern
	/// as LoadMoreErrorPreservesItemsTests and ListLayoutGridTests).
	/// </summary>
	private static async Task SeedTaggedOpportunitiesAsync(
		HttpClient http, string tag, IReadOnlyList<string> titles)
	{
		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"ResultCount {tag}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		foreach (var title in titles)
		{
			var response = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				titleDe = title,
				descriptionDe = "Seeded by OpportunityResultCountTests.",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "IndividualContact",
				checkInMethod = "None",
				validUntil = DateTimeOffset.UtcNow.AddDays(30),
				isDraft = false,
				tags = new[] { tag },
			});
			response.EnsureSuccessStatusCode();
		}
	}

	/// <summary>
	/// Asserts the count paragraph reads <paramref name="expected"/> and is
	/// actually on screen: a real text box, not the 1x1 clipped rect sr-only
	/// leaves behind. A plain <c>ToBeVisibleAsync</c> would pass either way -
	/// sr-only hides via clipping rather than display:none, so Playwright
	/// considers it visible (see LiveRegionTests' note on the same trap).
	/// </summary>
	private async Task AssertCountRenderedAsync(string expected)
	{
		var count = Page.Locator(CountSelector);
		await Expect(count).ToHaveTextAsync(expected, new() { Timeout = 15_000 });

		var box = await count.BoundingBoxAsync();
		box.Should().NotBeNull();
		box!.Height.Should().BeGreaterThan(8f,
			$"\"{expected}\" must render at its natural line height, not be clipped into the "
			+ "1x1 box sr-only produces - that clip is exactly what #1778 measured on staging");
		box.Width.Should().BeGreaterThan(8f,
			$"\"{expected}\" must render at its natural width, not be clipped to 1px by sr-only");
	}

	[Test]
	public async Task OpportunitiesPage_ResultCount_IsVisibleOnLoad_AndUpdatesWhenLoadMoreAddsAPage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(WideViewportWidth, WideViewportHeight);

		var olaf = await Fixture.SignInAsync("olaf", "olaf123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olaf.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var tag = $"count1778-{suffix}";

		// One more than a page, so the first render is the "loaded, more
		// available" wording and the second is the settled total.
		var titles = new List<string>();
		for (var i = 0; i < PageSize + 1; i++)
			titles.Add($"ResultCount Opportunity {suffix}-{i}");
		await SeedTaggedOpportunitiesAsync(http, tag, titles);

		await Page.GotoAsync($"{origin}/opportunities?tag={tag}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertCountRenderedAsync($"{PageSize} opportunities loaded, more available.");

		// Making it visible must not cost the announcement: the issue's own
		// scoping note turns on this node still being an implicit live region.
		await Expect(Page.Locator($"{CountSelector}[role='status']")).ToBeAttachedAsync();

		// data-testid, not the accessible name: LoadMoreButton swaps its label
		// for the loading one on the same element (see VisualTestBase's
		// LoadMoreUntilVisibleAsync).
		await Page.Locator("#opportunities [data-testid='load-more']").ClickAsync();

		await AssertCountRenderedAsync($"{PageSize + 1} opportunities found.");
	}

	[Test]
	public async Task OpportunitiesPage_ResultCount_FollowsTheKeywordFilter_AndStaysSrOnlyWhenTheListIsEmpty()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(WideViewportWidth, WideViewportHeight);

		var olaf = await Fixture.SignInAsync("olaf", "olaf123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olaf.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var tag = $"count1778f-{suffix}";
		// Long enough to appear in exactly one seeded title and in no other
		// title, description or organization name the keyword filter searches
		// (VolunteerOpportunityReadRepository matches all three).
		var uniqueKeyword = $"Solitaire{suffix}";

		await SeedTaggedOpportunitiesAsync(http, tag, [
			$"ResultCount {uniqueKeyword}",
			$"ResultCount Companion {suffix}-1",
			$"ResultCount Companion {suffix}-2",
		]);

		await Page.GotoAsync($"{origin}/opportunities?tag={tag}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertCountRenderedAsync("3 opportunities found.");

		// Driven through the page's own search box rather than a hand-written
		// URL: "updates as filters change" is the half of #1778 a static
		// first-render assertion can't prove.
		var keywordInput = Page.GetByTestId("opportunities-keyword-input");
		await keywordInput.FillAsync(uniqueKeyword);
		await keywordInput.PressAsync("Enter");

		// Singular wording included on purpose - resultCount_one existed but had
		// never been rendered anywhere a sighted user could reach it.
		await AssertCountRenderedAsync("1 opportunity found.");

		await keywordInput.FillAsync($"nomatch{suffix}");
		await keywordInput.PressAsync("Enter");

		var emptyState = Page.Locator("#opportunities")
			.GetByText("No opportunities found.", new() { Exact = true });
		await Expect(emptyState).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The empty list is the one case the count deliberately stays hidden:
		// "0 opportunities found." printed right above EmptyState's own "No
		// opportunities found." is duplication on screen, while a screen reader
		// still needs the zero to know the filter landed.
		var count = Page.Locator(CountSelector);
		await Expect(count).ToHaveTextAsync("0 opportunities found.");
		var box = await count.BoundingBoxAsync();
		box.Should().NotBeNull();
		box!.Height.Should().BeLessThan(8f,
			"with no results the count must fall back to sr-only rather than repeat the empty state on screen");
	}
}
