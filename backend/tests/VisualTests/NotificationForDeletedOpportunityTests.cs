using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// The browser half of #655: an EngagementCreated notification for a
/// since-deleted opportunity has no resolvable actionUrl, so following the
/// notification lands on a 404 - EngagementManagementPage must render the
/// existing NotFoundPage there rather than a raw error string.
///
/// The read-model assertions behind it (relatedTitle and actionUrl going
/// null for #655, and #2073's OpportunityDeleted notification keeping its
/// snapshotted title through the same deletion) moved to
/// <c>IntegrationTests/NotificationTests.cs</c> in einsatzbereit#2148,
/// beside the happy-path deep-link test they are the negative twin of.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class NotificationForDeletedOpportunityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EngagementManagementPage_RendersNotFoundPage_ForDeletedOpportunity()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId, _) = await CreateIndividualContactOpportunityAsync(keycloak, backend, "NotifDeletedUi");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var deleteResponse = await olafHttp.DeleteAsync($"/v1/volunteer-opportunities/{opportunityId}");
		deleteResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	private static async Task<(string OpportunityId, string OrganizationId, string Title)> CreateIndividualContactOpportunityAsync(Uri keycloak, Uri backend, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");
		var title = $"NotifDeletedOpp {label} {suffix}";

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");

		// Create a fresh organization rather than reusing olaf's shared seed
		// org - other VisualTests running concurrently in this shared Aspire
		// session can mutate/delete shared orgs, which made GET
		// /v1/organizations intermittently race to an empty list here.
		var createOrgResponse = await PostJsonWithRetryAsync(http,
			"/v1/organizations",
			new { name = $"NotifDeletedOpp Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		// CreateOrganizationEndpoint returns the raw domain Organization
		// aggregate (unlike GetOrganizations, which projects to a DTO), so
		// its strongly-typed OrganizationId record struct serializes as a
		// nested { "value": "<guid>" } object rather than a plain string.
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by NotificationForDeletedOpportunityTests",
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
		return (opportunity.GetProperty("id").GetString()!, organizationId, title);
	}
}
