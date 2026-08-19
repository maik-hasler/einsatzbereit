using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1226: a failed "load more" request used to feed the same
/// `error` state as an initial-load failure, and every list page hid its
/// already-rendered rows whenever that state was set - so one failed page-2+
/// fetch wiped every row the user had already scrolled through, with no
/// recovery short of a full reload. `useLoadMore` now tracks a load-more
/// failure separately (`loadMoreError`) from the initial-load failure
/// (`error`), and callers render it as an inline retry affordance next to the
/// still-visible list instead of replacing that list with a full-page error
/// banner.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LoadMoreErrorPreservesItemsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// The opportunities grid's page size is viewport-responsive (see
	// useVolunteerOpportunitiesData.ts's computePageSize: xl >= 1280px is 3
	// cols x 3 rows = 9, so a fully-loaded page is always a whole number of
	// rows). Viewport is pinned explicitly below rather than relying on
	// PageTest's default so this stays correct if that default ever changes.
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
		// Scopes the homepage list to exactly the opportunities seeded below,
		// regardless of whatever other VisualTests are concurrently seeding
		// their own data in this shared-session database (see AvatarAndLogoDisplayTests
		// and ListLayoutGridTests for the same tag-scoping pattern).
		var tag = $"loadmore1226-{suffix}";

		var orgResponse = await PostJsonWithRetryAsync(http,
			"/v1/organizations", new { name = $"LoadMoreError {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		// One more than a single page, so page 1 (10 items) renders fully and
		// "load more" is available to fetch the 11th on page 2.
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

			// Cross-origin in this test environment - a fulfilled response still
			// needs CORS headers or fetch() rejects before the app's own
			// error-handling code (and thus loadMoreError) ever runs, same as
			// NotificationTests/SessionExpiryTests.
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

		// The whole point of #1226: the already-rendered first page must still
		// be there after the load-more failure, not replaced by the error.
		await Expect(items).ToHaveCountAsync(PageSize);

		shouldFail = false;
		await retryButton.ClickAsync();

		await Expect(items).ToHaveCountAsync(SeedCount, new() { Timeout = 10_000 });
		await Expect(retryButton).Not.ToBeVisibleAsync();
		await Expect(loadMoreButton).Not.ToBeVisibleAsync();
	}
}
