using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1965: on the 375px reflow, the opportunity detail page's
/// sticky rail (deadline / application status / sign-up CTA / login prompt)
/// dropped to single-column and landed after the at-a-glance summary, the
/// category tags, the full-width Leaflet map (~250px tall) and the
/// organisation contact card - roughly 700px of scrolling before the page's
/// one conversion point was reachable, even though the same content sits at
/// the very top of the page on desktop next to the reading column.
///
/// Fixed by rendering the rail's content a second time (VolunteerOpportunity
/// DetailPage.tsx's `renderActionRail`, testid-suffixed "-mobile") right
/// after the at-a-glance meta row and before the map, visible only below
/// `lg`; the original sticky `<aside>` now hides below `lg` in turn so the
/// two copies are never both visible at the same viewport.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityDetailPageMobileActionRailTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int NarrowViewportWidth = 375;
	private const int NarrowViewportHeight = 812;

	/// <summary>
	/// Seeds a published, non-remote, IndividualContact opportunity (so both
	/// the deadline-carrying blocks and the map are eligible to render), then
	/// patches coordinates into the detail fetch - VisualTests always runs
	/// against FakeGeocodingService (AppHost.cs), which never returns real
	/// coordinates for a seeded address, so the map would never render
	/// otherwise (same technique as SingleMarkerMapStaticTests.cs).
	/// </summary>
	private async Task<string> SeedOpportunityWithMapAsync(string label)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"MobileRail1965 {label} {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"MobileRail1965 {label} {suffix}",
			description = "Seeded for #1965 mobile action-rail regression coverage.",
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

	/// <summary>
	/// Asserts <paramref name="railBlock"/> is visible, sits above the map
	/// (smaller y than the map's top edge) and that the desktop-only copy
	/// with testid <paramref name="desktopTestId"/> is not visible at the
	/// current (narrow) viewport - the two copies must never both be on
	/// screen at once.
	/// </summary>
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
}
