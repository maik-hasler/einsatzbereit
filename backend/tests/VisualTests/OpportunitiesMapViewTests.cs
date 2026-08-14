using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1851: /opportunities gained a list/map toggle so a volunteer can see every
/// on-site result in the current filter as pins instead of opening each one
/// individually - the multi-pin scope #110 originally described but PR #183
/// only partially delivered (a single-marker detail-page map).
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunitiesMapViewTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OpportunitiesPage_MapViewToggle_SwitchesBetweenListAndMap()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var listButton = Page.GetByTestId("opportunities-view-list");
		var mapButton = Page.GetByTestId("opportunities-view-map");
		await Expect(listButton).ToHaveAttributeAsync("aria-pressed", "true");
		await Expect(mapButton).ToHaveAttributeAsync("aria-pressed", "false");
		await Expect(Page.GetByTestId("opportunities-filter-bar")).ToBeVisibleAsync();

		await mapButton.ClickAsync();

		await Expect(mapButton).ToHaveAttributeAsync("aria-pressed", "true");
		await Expect(listButton).ToHaveAttributeAsync("aria-pressed", "false");
		await Expect(Page).ToHaveURLAsync(new Regex("view=map"));

		await listButton.ClickAsync();

		await Expect(listButton).ToHaveAttributeAsync("aria-pressed", "true");
		await Expect(mapButton).ToHaveAttributeAsync("aria-pressed", "false");
		await Expect(Page).Not.ToHaveURLAsync(new Regex("view=map"));
	}

	[Test]
	public async Task OpportunitiesPage_MapView_ShowsPinForOnSiteOpportunity()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var title = $"MapView Pin Test {suffix}";
		var tag = $"mapview1851-{suffix}";

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Map View Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title,
			description = "Created by OpportunitiesPage_MapView_ShowsPinForOnSiteOpportunity",
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
			tags = new[] { tag },
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		// VisualTests always runs against FakeGeocodingService (AppHost.cs's
		// Geocoding__UseFakeService override), which reports TransientFailure so
		// no seeded opportunity here ever gets real coordinates from the normal
		// write path - the map would never render a pin otherwise. Patch
		// coordinates into the list response the map view actually reads,
		// mirroring SingleMarkerMapTouchScrollTests.cs's identical patch of the
		// single-opportunity detail response. The "?*" glob (not a bare
		// "/volunteer-opportunities" suffix) is what keeps this from also
		// matching the GET .../volunteer-opportunities/{id} detail endpoint -
		// same pattern LoadingStateTests.cs already uses for this list route.
		await Page.RouteAsync("**/v1/volunteer-opportunities?*", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			var response = await route.FetchAsync();
			var body = JsonNode.Parse(await response.TextAsync())!.AsObject();
			foreach (var item in body["items"]!.AsArray())
			{
				if (item!["id"]!.GetValue<string>() == opportunityId)
				{
					item["latitude"] = JsonValue.Create(52.52);
					item["longitude"] = JsonValue.Create(13.405);
				}
			}

			await route.FulfillAsync(new()
			{
				Response = response,
				ContentType = "application/json",
				Body = body.ToJsonString(),
			});
		});

		// Scoped to this one opportunity via its unique tag (same tag-scoping
		// pattern as OpportunityResultCountTests/LoadMoreErrorPreservesItemsTests/
		// ListLayoutGridTests) so the map page's single 100-item fetch doesn't
		// depend on where this test's freshly created opportunity lands relative
		// to whatever else the shared test session has seeded.
		await Page.GotoAsync($"{origin}/opportunities?tag={tag}&view=map");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator(".leaflet-container")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Leaflet gives the marker's focus stop role="button" with icon.title as
		// its accessible name (same mechanism SingleMarkerMap.tsx relies on, #1681).
		var marker = Page.GetByRole(AriaRole.Button, new() { Name = title });
		await Expect(marker).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await marker.ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "View details" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task OpportunitiesPage_MapView_NoOnSiteMatches_ShowsEmptyState()
	{
		// Filtering to remote-only opportunities is the simplest way to
		// reliably reach zero on-site pins without depending on which of the
		// shared test session's seeded opportunities already have coordinates.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities?isRemote=true&view=map");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByText("No on-site opportunities found."))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
	}
}
