using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ListLayoutGridTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int WideViewportWidth = 1440;
	private const int WideViewportHeight = 900;

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

	private static async Task<string> CreateOrganizationAsync(HttpClient http, string namePrefix)
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var response = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"{namePrefix} {suffix}" });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()!;
	}

	private static async Task<string> CreateOpportunityAsync(
		HttpClient http, string organizationId, string title, bool isDraft)
	{
		var response = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Seeded for #977 list-layout-grid visual test.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft,
		});
		response.EnsureSuccessStatusCode();
		var created = await response.Content.ReadFromJsonAsync<JsonElement>();

		return created.GetProperty("id").GetString()!;
	}

	private async Task AssertGridWithSideBySideItemsAsync(ILocator list, string label)
	{
		await Expect(list).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var display = await list.EvaluateAsync<string>("el => getComputedStyle(el).display");
		display.Should().Be("grid", $"{label}: list container must use a CSS grid, not a single-column stack");

		var items = list.Locator("> li");
		var count = await items.CountAsync();
		if (count < 2)
			return;

		var firstBox = await items.Nth(0).BoundingBoxAsync();
		var secondBox = await items.Nth(1).BoundingBoxAsync();
		firstBox.Should().NotBeNull();
		secondBox.Should().NotBeNull();

		Math.Abs(firstBox!.Y - secondBox!.Y).Should().BeLessThan(2,
			$"{label}: with >= 2 items at a wide viewport, the first two must sit side by side in the same "
			+ "grid row rather than stacked one above the other - this is the concrete symptom #977 reported");
	}

	[Test]
	public async Task HomePage_OpportunitiesList_IsGridWithSideBySideCards()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		await Page.SetViewportSizeAsync(WideViewportWidth, WideViewportHeight);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync(Page);
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var organizationId = await CreateOrganizationAsync(http, "Visual977 HomeGrid");
		await CreateOpportunityAsync(http, organizationId, "Grid Card A", isDraft: false);
		await CreateOpportunityAsync(http, organizationId, "Grid Card B", isDraft: false);

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var list = Page.Locator("#opportunities ul").First;
		await AssertGridWithSideBySideItemsAsync(list, "Homepage opportunities list");
	}

	[Test]
	public async Task OrganizationsDirectory_IsGridWithSideBySideCards()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(WideViewportWidth, WideViewportHeight);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync(Page);
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		await CreateOrganizationAsync(http, "Visual977 OrgGrid A");
		await CreateOrganizationAsync(http, "Visual977 OrgGrid B");

		await Page.GotoAsync($"{origin}/organizations");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var list = Page.Locator("main ul").First;
		await AssertGridWithSideBySideItemsAsync(list, "Organizations directory");
	}

	[Test]
	public async Task OrganizerOpportunitiesList_TwoDrafts_AreGridWithSideBySideCards()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(WideViewportWidth, WideViewportHeight);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync(Page);
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var organizationId = await CreateOrganizationAsync(http, "Visual977 OrgOppGrid");
		await CreateOpportunityAsync(http, organizationId, "Draft Card A", isDraft: true);
		await CreateOpportunityAsync(http, organizationId, "Draft Card B", isDraft: true);

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var draftsList = Page.GetByTestId("drafts-section").Locator("ul").First;
		await AssertGridWithSideBySideItemsAsync(draftsList, "Organizer opportunities list (drafts)");
	}

	[Test]
	public async Task OrganizerOpportunitiesList_SingleDraft_CardDoesNotStretchFullRowWidth()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(WideViewportWidth, WideViewportHeight);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync(Page);
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var organizationId = await CreateOrganizationAsync(http, "Visual977 OrgOppSingle");
		await CreateOpportunityAsync(http, organizationId, "Lone Draft Card", isDraft: true);

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var draftsList = Page.GetByTestId("drafts-section").Locator("ul").First;
		await Expect(draftsList).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var items = draftsList.Locator("> li");
		(await items.CountAsync()).Should().Be(1, "this organization was seeded with exactly one draft");

		var listBox = await draftsList.BoundingBoxAsync();
		var cardBox = await items.First.BoundingBoxAsync();
		listBox.Should().NotBeNull();
		cardBox.Should().NotBeNull();

		cardBox!.Width.Should().BeLessThan(listBox!.Width * 0.6f,
			"a lone grid item must occupy roughly one column's width, not stretch across the whole row "
			+ "the way the pre-fix single-column list did");
	}

	[Test]
	public async Task ProfileEngagementList_TwoEngagements_AreGridWithSideBySideCards()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(WideViewportWidth, WideViewportHeight);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync(Page);
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var organizationId = await CreateOrganizationAsync(http, "Visual977 EngagementGrid");

		foreach (var title in new[] { "Engage Card A", "Engage Card B" })
		{
			var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				titleDe = title,
				descriptionDe = "Seeded for #977 list-layout-grid visual test.",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "ScheduledSlots",
				checkInMethod = "None",
				isDraft = true,
			});
			oppResponse.EnsureSuccessStatusCode();
			var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
			var opportunityId = opportunity.GetProperty("id").GetString();

			var start = DateTimeOffset.UtcNow.AddDays(3);
			var end = start.AddHours(2);
			var slotResponse = await http.PostAsJsonAsync(
				$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
				new { startDateTime = start, endDateTime = end, maxParticipants = 5, recurrenceCount = 1 });
			slotResponse.EnsureSuccessStatusCode();
			var slots = await slotResponse.Content.ReadFromJsonAsync<JsonElement>();
			var timeSlotId = slots[0].GetProperty("id").GetString();

			(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
				.EnsureSuccessStatusCode();

			var engagementResponse = await http.PostAsJsonAsync(
				$"/v1/volunteer-opportunities/{opportunityId}/engagements",
				new { type = "ScheduledSlots", timeSlotId, message = (string?)null });
			engagementResponse.EnsureSuccessStatusCode();
			var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
			var engagementId = engagement.GetProperty("id").GetString();

			(await http.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
				.EnsureSuccessStatusCode();
		}

		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var list = Page.Locator("#activity ul").First;
		await AssertGridWithSideBySideItemsAsync(list, "Profile engagement list");
	}

	[Test]
	public async Task MoreFromOrganization_ThreeOpportunities_AreGridWithSideBySideCards()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(WideViewportWidth, WideViewportHeight);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync(Page);
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var organizationId = await CreateOrganizationAsync(http, "Visual977 MoreFromOrg");
		var firstId = await CreateOpportunityAsync(http, organizationId, "Primary Opportunity", isDraft: false);
		await CreateOpportunityAsync(http, organizationId, "Other Opportunity A", isDraft: false);
		await CreateOpportunityAsync(http, organizationId, "Other Opportunity B", isDraft: false);

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{firstId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var moreSection = Page.GetByTestId("more-from-organization");
		await Expect(moreSection).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var list = moreSection.Locator("ul").First;
		await AssertGridWithSideBySideItemsAsync(list, "\"More from this organization\" list");
	}
}
