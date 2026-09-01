using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityDetailOfflineTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private async Task<string> CreatePublishedOpportunityAsync(string label)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http,
			"/v1/organizations", new { name = $"DetailOffline2065 {label} {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"DetailOffline2065 {label} {suffix}",
			descriptionDe = "Seeded for #2065 detail-page offline-state regression coverage.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		return opportunity.GetProperty("id").GetString()!;
	}

	[Test]
	public async Task OpportunityDetail_WhileOffline_ShowsOfflineState_NotAGenericError()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var opportunityId = await CreatePublishedOpportunityAsync("Generic");

		await Page.AddInitScriptAsync(
			"Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => false });");
		await Page.RouteAsync($"**/v1/volunteer-opportunities/{opportunityId}", route =>
			route.AbortAsync("internetdisconnected"));

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are offline" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByText("Error:", new() { Exact = false })).Not.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Try again" })).ToBeVisibleAsync();
	}

	[Test]
	public async Task OpportunityDetail_ManualRetry_SucceedsWithoutAnOnlineEvent()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var opportunityId = await CreatePublishedOpportunityAsync("ManualRetry");

		await Page.AddInitScriptAsync(
			"Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => false });");

		var shouldFail = true;
		await Page.RouteAsync($"**/v1/volunteer-opportunities/{opportunityId}", async route =>
		{
			if (!shouldFail)
			{
				await route.ContinueAsync();
				return;
			}
			await route.AbortAsync("internetdisconnected");
		});

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");

		var retryButton = Page.GetByRole(AriaRole.Button, new() { Name = "Try again" });
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are offline" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(retryButton).ToBeVisibleAsync();

		shouldFail = false;
		await retryButton.ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are offline" }))
			.Not.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByTestId("opportunity-action-panel")).ToBeVisibleAsync();
	}
}
