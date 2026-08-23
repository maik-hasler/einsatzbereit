using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityFilterRowAlignmentTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int WideViewportWidth = 1440;
	private const int WideViewportHeight = 900;

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
		await GoToOpportunitiesAsync(width, height);

		var deltas = await GetPerLineLeftEdgeDeltasAsync();
		deltas.Should().NotBeEmpty("the filter row must render at least one line of chips");
		if (expectWrapping)
			deltas.Length.Should().BeGreaterThan(1,
				$"the six filter chips cannot fit on one line at {width}px, so the wrapped-line "
				+ "case this test exists for must actually be exercised");
	}
}
