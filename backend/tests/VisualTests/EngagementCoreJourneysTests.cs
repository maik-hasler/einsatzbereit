using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1326: the volunteer's and organizer's two most common state
/// transitions - confirming an application, withdrawing from one - were only ever
/// exercised at the API layer in this suite. Every setup step here still goes
/// through the API (the established convention in this file's siblings), but the
/// state transition itself under test is a real button click, so a frontend
/// regression in the confirm button's payload, its optimistic update, or its
/// error handling would actually fail a test instead of reaching production
/// unnoticed. Signing up itself already has E2E coverage elsewhere
/// (OpportunityApplicationStateTests.cs), so it isn't repeated here.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementCoreJourneysTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EngagementManagementPage_ConfirmButton_MovesApplicationToConfirmed()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId) = await CreateIndividualContactOpportunityAsync(keycloak, backend, "ConfirmJourney");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");
		await ApplyAsync(veraHttp, opportunityId, "Please let me help.");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var row = Page.Locator("li", new() { HasText = "Vera Volunteer" });
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(row.GetByText("Pending")).ToBeVisibleAsync();

		await row.GetByRole(AriaRole.Button, new() { Name = "Confirm" }).ClickAsync();

		await Expect(row.GetByText("Confirmed")).ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	[Test]
	public async Task ProfileActivitySection_WithdrawButton_MovesEngagementFromUpcomingToPastScope()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, _) = await CreateIndividualContactOpportunityAsync(keycloak, backend, "WithdrawJourney");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");
		var engagementId = await ApplyAsync(veraHttp, opportunityId, "Please let me help.");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		var confirmResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null);
		confirmResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var card = Page.Locator($"[data-engagement-id='{engagementId}']");

		// This engagement has no time slot (IndividualContact), and
		// EngagementReadRepository.GetByVolunteerAsync orders the "Current &
		// upcoming" scope by time-slot start (entries with none sort last) - so on
		// a shared session where other concurrently-running tests have already
		// given vera their own time-slotted upcoming engagements, this card can
		// land past the first (10-item) page, so page through to it.
		//
		// Wait for the first page before starting: the WaitForLoadStateAsync
		// above can settle before the engagements fetch is even issued, since
		// useLoadMore only requests from an effect after React commits.
		await Expect(Page.Locator("#activity [data-testid='engagement-card']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await LoadMoreUntilVisibleAsync(card);

		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await card.GetByRole(AriaRole.Button, new() { Name = "Withdraw" }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Yes, withdraw" }).ClickAsync();

		// The card must disappear from Upcoming - a withdrawn engagement is no
		// longer Pending/Confirmed, so the button vanishing alone would already
		// be true here whether or not the row actually moved anywhere.
		await Expect(card).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.Locator("[data-testid='engagements-scope-past']").ClickAsync();

		var pastCard = Page.Locator($"[data-engagement-id='{engagementId}']");
		await Expect(pastCard).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(pastCard.GetByText("Withdrawn")).ToBeVisibleAsync();
	}

	private static async Task<string> ApplyAsync(HttpClient http, string opportunityId, string message)
	{
		var response = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message });
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("id").GetString()!;
	}

	private static async Task<(string OpportunityId, string OrganizationId)> CreateIndividualContactOpportunityAsync(
		Uri keycloak, Uri backend, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");

		// Create a fresh organization rather than reusing olaf's shared seed
		// org - other VisualTests running concurrently in this shared Aspire
		// session can mutate/delete shared orgs (see EngagementCancellationReasonTests.cs).
		var createOrgResponse = await PostJsonWithRetryAsync(http,
			"/v1/organizations",
			new { name = $"{label} Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"{label} {suffix}",
			descriptionDe = "Created by EngagementCoreJourneysTests",
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
		return (opportunity.GetProperty("id").GetString()!, organizationId);
	}
}
