using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #675: "My profile -> Engagements" split into "Current &amp;
/// upcoming" (default) and "Past" tabs, each paginated, instead of one
/// unbounded flat list.
///
/// Which engagement lands in which tab is decided server-side, by
/// <c>EngagementReadRepository.GetByVolunteerAsync(upcoming: true|false)</c> -
/// the tab only re-fetches. #2148 moved the bucketing cases down to
/// <c>IntegrationTests/EngagementReadRepositoryTests.cs</c> accordingly
/// (Pending vs Withdrawn, and the #1855 checked-in rule, which that file
/// already covered). What is left here is the one claim that is genuinely
/// about the rendered card.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MyEngagementsScopeTabsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	/// <summary>
	/// Regression for #2070: a Withdrawn engagement whose opportunity still has
	/// a future "express interest by" deadline (the common shape for an
	/// IndividualContact opportunity - it stays open for other volunteers long
	/// after this one withdrew) used to keep showing that future-dated deadline
	/// on its card in the "Past" scope, contradicting the scope's own label.
	/// Once terminal (Cancelled/Withdrawn), the deadline is no longer
	/// actionable for this engagement, so the card should drop it and rely on
	/// the status chip instead.
	/// </summary>
	[Test]
	public async Task EngagementsTab_PastScope_HidesFutureApplyByDeadline_ForWithdrawnEngagement()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "ScopeTabsWithdrawnFuture");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var engagementId = await ApplyAsync(veraHttp, opportunityId, "Withdrawing right away.");
		var withdrawResponse = await veraHttp.PostAsync($"/v1/engagements/{engagementId}/withdraw", content: null);
		withdrawResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.Locator("[data-testid='engagements-scope-past']").ClickAsync();

		var card = Page.Locator($"[data-engagement-id='{engagementId}']");
		await LoadMoreUntilVisibleAsync(card);
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Exact match: the opportunity's own fixture title ("ScopeTabsWithdrawnFuture")
		// contains "Withdrawn" as a substring too, so a non-exact GetByText here
		// is a strict-mode violation - it resolves to both the title link and
		// the actual "Withdrawn" status badge this assertion means to check.
		await Expect(card.GetByText("Withdrawn", new() { Exact = true })).ToBeVisibleAsync();
		await Expect(card.GetByText("Express interest by")).Not.ToBeVisibleAsync();
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

	private static async Task<string> CreateIndividualContactOpportunityAsync(Uri keycloak, Uri backend, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");

		// Create a fresh organization rather than reusing olaf's shared seed
		// org - other VisualTests running concurrently in this shared Aspire
		// session can mutate/delete shared orgs (see EngagementReactivationTests).
		var createOrgResponse = await PostJsonWithRetryAsync(http,
			"/v1/organizations",
			new { name = $"MyEngagementsScopeTabs Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"{label} {suffix}",
			descriptionDe = "Created by MyEngagementsScopeTabsTests",
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
}
