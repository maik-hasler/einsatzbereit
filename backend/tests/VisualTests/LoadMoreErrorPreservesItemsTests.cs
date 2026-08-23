using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LoadMoreErrorPreservesItemsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int WideViewportWidth = 1440;
	private const int WideViewportHeight = 900;
	private const int PageSize = 9;
	private const int SeedCount = PageSize + 1;

	[Test]
	public async Task OpportunitiesPage_FailedLoadMore_KeepsFirstPageVisible_AndRetrySucceeds()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(WideViewportWidth, WideViewportHeight);

		var olaf = await Fixture.SignInAsync("olaf", "olaf123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olaf.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");

		var tag = $"loadmore1226-{suffix}";

		var orgResponse = await PostJsonWithRetryAsync(http,
			"/v1/organizations", new { name = $"LoadMoreError {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		for (var i = 0; i < SeedCount; i++)
		{
			var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				titleDe = $"LoadMoreError Opportunity {suffix}-{i}",
				descriptionDe = "Seeded by LoadMoreErrorPreservesItemsTests.",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "IndividualContact",
				checkInMethod = "None",
				validUntil = DateTimeOffset.UtcNow.AddDays(30),
				isDraft = false,
				tags = new[] { tag },
			});
			oppResponse.EnsureSuccessStatusCode();
		}

		var shouldFail = true;
		await Page.RouteAsync("**/v1/volunteer-opportunities?PageNumber=2&*", async route =>
		{
			if (!shouldFail)
			{
				await route.ContinueAsync();
				return;
			}

			await route.FulfillAsync(new()
			{
				Status = 500,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.6.1\",\"status\":500}",
			});
		});

		await Page.GotoAsync($"{origin}/opportunities?tag={tag}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var opportunitiesSection = Page.Locator("#opportunities");
		var items = opportunitiesSection.Locator("ul").First.Locator("> li");
		await Expect(items).ToHaveCountAsync(PageSize, new() { Timeout = 15_000 });

		var loadMoreButton = opportunitiesSection.GetByRole(AriaRole.Button, new() { Name = "Load more" });
		await Expect(loadMoreButton).ToBeVisibleAsync();
		await loadMoreButton.ClickAsync();

		var retryButton = opportunitiesSection.GetByRole(AriaRole.Button, new() { Name = "Retry" });
		await Expect(retryButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(opportunitiesSection.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();

		await Expect(items).ToHaveCountAsync(PageSize);

		shouldFail = false;
		await retryButton.ClickAsync();

		await Expect(items).ToHaveCountAsync(SeedCount, new() { Timeout = 10_000 });
		await Expect(retryButton).Not.ToBeVisibleAsync();
		await Expect(loadMoreButton).Not.ToBeVisibleAsync();
	}
}
