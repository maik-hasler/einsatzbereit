using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityDetailPageMobileActionRailTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int NarrowViewportWidth = 375;
	private const int NarrowViewportHeight = 812;

	private async Task<string> SeedOpportunityWithMapAsync(string label)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"MobileRail1965 {label} {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"MobileRail1965 {label} {suffix}",
			descriptionDe = "Seeded for #1965 mobile action-rail regression coverage.",
			organizationId,
			isRemote = false,
			street = "Teststrasse",
			houseNumber = "1",
			zipCode = "12345",
			city = "Musterstadt",
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString()!;

		await Page.RouteAsync($"**/v1/volunteer-opportunities/{opportunityId}", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			var response = await route.FetchAsync();
			var body = JsonNode.Parse(await response.TextAsync())!.AsObject();
			body["latitude"] = JsonValue.Create(52.52);
			body["longitude"] = JsonValue.Create(13.405);

			await route.FulfillAsync(new()
			{
				Response = response,
				ContentType = "application/json",
				Body = body.ToJsonString(),
			});
		});

		return opportunityId;
	}

	private async Task AssertRailAboveMapAndNoDesktopDuplicateAsync(ILocator railBlock, string desktopTestId)
	{
		var map = Page.Locator(".leaflet-container");
		await Expect(map).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(railBlock).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var railBox = await railBlock.BoundingBoxAsync();
		var mapBox = await map.BoundingBoxAsync();
		railBox.Should().NotBeNull();
		mapBox.Should().NotBeNull();
		railBox!.Y.Should().BeLessThan(mapBox!.Y,
			"the mobile action rail must sit above the map, matching the priority the same content has "
			+ "in the desktop sticky rail (#1965)");

		await Expect(Page.GetByTestId(desktopTestId)).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task ActionRail_ForAnonymousVisitor_RendersAboveMapOnNarrowViewport()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await SeedOpportunityWithMapAsync("Anonymous");

		await Page.SetViewportSizeAsync(NarrowViewportWidth, NarrowViewportHeight);
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertRailAboveMapAndNoDesktopDuplicateAsync(
			Page.GetByTestId("login-prompt-mobile"), "login-prompt");
	}

	[Test]
	public async Task ActionRail_ForAuthenticatedNonOwner_RendersAboveMapOnNarrowViewport()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await SeedOpportunityWithMapAsync("Vera");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.SetViewportSizeAsync(NarrowViewportWidth, NarrowViewportHeight);
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertRailAboveMapAndNoDesktopDuplicateAsync(
			Page.GetByTestId("signup-cta-mobile"), "signup-cta");
	}

	[Test]
	public async Task ActionRail_ForVolunteerWithExistingEngagement_RendersAboveMapOnNarrowViewport()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await SeedOpportunityWithMapAsync("VeraApplied");

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Seeded for #1948 mobile action-rail regression coverage." });
		applyResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.SetViewportSizeAsync(NarrowViewportWidth, NarrowViewportHeight);
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertRailAboveMapAndNoDesktopDuplicateAsync(
			Page.GetByTestId("application-status-mobile"), "application-status");
	}
}
