using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1798: the /opportunities filter chips were centred
/// (`justify-center`) inside a full-width row sitting directly above a
/// full-width results grid, so at 1440px the first chip started at x=375
/// while the cards below it started at x=32 - one page with two competing
/// left edges. Fixed by dropping `justify-center`, letting flex's default
/// `flex-start` line the row up with the grid.
///
/// The row is `flex-wrap`, so these tests check the alignment holds for
/// *every* wrapped line, not just the first one, and at the three viewports
/// the issue called out (1440 / 768 / 375).
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityFilterRowAlignmentTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// The viewport the finding was reported at: `max-w-page` is 90rem, so
	// <main> spans the full width here and its `lg:px-8` puts both the filter
	// row and the grid at x=32 - the widest gap the centred row could produce.
	private const int WideViewportWidth = 1440;
	private const int WideViewportHeight = 900;

	// A left edge is either shared or it is not; 2px of slack only absorbs
	// sub-pixel rounding in getBoundingClientRect, and is far below the ~343px
	// offset the centred row produced at 1440.
	private const double MaxLeftEdgeDeltaPx = 2;

	private static async Task<string> GetAccessTokenAsync(IPage page)
	{
		var token = await page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in sessionStorage after login");
		return token!;
	}

	/// <summary>
	/// Seeds one organization with two published opportunities, so the results
	/// grid below the filter row is guaranteed to render cards rather than the
	/// empty state - whatever else the shared test session has left behind.
	/// </summary>
	private static async Task SeedPublishedOpportunitiesAsync(HttpClient http)
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"Visual1798 FilterRow {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		foreach (var title in new[] { "Filter Row Card A", "Filter Row Card B" })
		{
			var response = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				titleDe = title,
				descriptionDe = "Seeded for #1798 filter-row alignment visual test.",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "IndividualContact",
				checkInMethod = "None",
				validUntil = DateTimeOffset.UtcNow.AddDays(30),
				isDraft = false,
			});
			response.EnsureSuccessStatusCode();
		}
	}

	/// <summary>
	/// Groups the filter row's chips into visual lines by their top edge and
	/// returns, per line, how far that line's leftmost chip sits from the
	/// results grid's left edge. Chip boxes and the grid box are read in a
	/// single EvaluateAsync call so nothing can reflow between the two reads
	/// (see VisualTestBase.AssertMaxWidthContentCenteredAsync for the same
	/// pattern).
	/// </summary>
	private async Task<double[]> GetPerLineLeftEdgeDeltasAsync()
	{
		var filterBar = Page.GetByTestId("opportunities-filter-bar");
		await Expect(filterBar).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var grid = Page.Locator("#opportunities ul").First;
		await Expect(grid).ToBeVisibleAsync(new() { Timeout = 15_000 });

		double[] deltas = [];
		await PollUntilAsync(async () =>
		{
			deltas = await filterBar.EvaluateAsync<double[]>(
				"""
				(el, gridSelector) => {
					const gridLeft = document.querySelector(gridSelector).getBoundingClientRect().left;
					const lines = new Map();
					for (const chip of el.children) {
						const box = chip.getBoundingClientRect();
						if (box.width === 0) continue;
						const line = Math.round(box.top);
						lines.set(line, Math.min(lines.get(line) ?? Infinity, box.left));
					}
					return [...lines.entries()]
						.sort((a, b) => a[0] - b[0])
						.map(([, left]) => left - gridLeft);
				}
				""",
				"#opportunities ul");
			return deltas.Length > 0 && deltas.All(d => Math.Abs(d) < MaxLeftEdgeDeltaPx);
		}, () => "filter row lines should all start at the results grid's left edge "
			+ $"(last observed per-line deltas: [{string.Join(", ", deltas.Select(d => $"{d:F0}px"))}], "
			+ $"each must be <{MaxLeftEdgeDeltaPx}px)");

		return deltas;
	}

	private async Task GoToOpportunitiesAsync(int width, int height)
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		// Sign in at the default (desktop) viewport - FastSignInAsync's own
		// success check waits for the "User menu" button, which only exists in
		// the header's desktop nav (`hidden md:flex`); at mobile width it stays
		// hidden behind the hamburger, so signing in at 375px times out (see
		// OrgAppMobileResponsiveTests for the same ordering). Resize only after
		// landing in the app - the alignment under test is decided by the
		// filter row's own layout at the final viewport, not by which width the
		// session was established at.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync(Page);
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
		await SeedPublishedOpportunitiesAsync(http);

		await Page.SetViewportSizeAsync(width, height);
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
	}

	[Test]
	public async Task FilterRow_AtWideViewport_SharesLeftEdgeWithResultsGrid()
	{
		await GoToOpportunitiesAsync(WideViewportWidth, WideViewportHeight);

		var deltas = await GetPerLineLeftEdgeDeltasAsync();
		deltas.Should().NotBeEmpty("the filter row must render at least one line of chips");
	}

	[Test]
	[Arguments(768, 1024, false)]
	[Arguments(375, 812, true)]
	public async Task FilterRow_AtNarrowViewports_EveryLineSharesLeftEdgeWithResultsGrid(
		int width, int height, bool expectWrapping)
	{
		// Narrow viewports are where a wrapped row matters: `justify-center`
		// centred each wrapped line independently, so a short trailing line was
		// offset by a different amount than the first. Wrapping is only asserted
		// at 375, where the six chips cannot possibly fit on one line; at 768 it
		// depends on the translated label widths, which is not this test's
		// subject.
		await GoToOpportunitiesAsync(width, height);

		var deltas = await GetPerLineLeftEdgeDeltasAsync();
		deltas.Should().NotBeEmpty("the filter row must render at least one line of chips");
		if (expectWrapping)
			deltas.Length.Should().BeGreaterThan(1,
				$"the six filter chips cannot fit on one line at {width}px, so the wrapped-line "
				+ "case this test exists for must actually be exercised");
	}
}
