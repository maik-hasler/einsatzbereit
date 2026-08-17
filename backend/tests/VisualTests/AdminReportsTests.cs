using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for the admin Reports panel returning a 500: AdminReportReadRepository
/// filtered VolunteerOpportunitiesQuery/OrganizationsQuery/UsersQuery with
/// <c>x.Id.Value</c> inside a still-queryable Where/Select. EF Core cannot invert
/// the strongly-typed ID's non-trivial "from provider" conversion
/// (<c>guid => VolunteerOpportunityId.Create(guid).GetValueOrThrow()</c>) to
/// translate <c>.Id.Value</c> back to the raw column, so every load of the admin
/// Reports section threw at query-translation time - even with zero reports in
/// the database, since translation depends on the expression shape, not the
/// captured list's runtime contents. Fixed by converting the captured Guid lists
/// to strongly-typed IDs first and comparing with <c>idVOs.Contains(x.Id)</c>,
/// matching the established pattern elsewhere (e.g. NotificationReadRepository).
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AdminReportsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task AdministrationPage_ReportsSection_ListsFlaggedOpportunityWithoutError()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations",
			new { name = $"Admin Reports Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var title = $"Flagged Opportunity {suffix}";
		var opportunityResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Regression coverage for the admin reports 500.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		opportunityResponse.EnsureSuccessStatusCode();
		var opportunity = await opportunityResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var veraToken = (await Fixture.SignInAsync("vera", "vera123")).AccessToken;
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraToken}");
		var reportResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/reports",
			new { reason = "Spam", details = $"Regression coverage {suffix}." });
		reportResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Page.GotoAsync($"{origin}/administration/reports");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByText("Failed to load reports.")).Not.ToBeVisibleAsync();

		var row = Page.Locator("li").Filter(new() { HasTextString = title });
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(row.GetByText("Opportunity", new() { Exact = true })).ToBeVisibleAsync();
		await Expect(row.GetByText("Active", new() { Exact = true })).ToBeVisibleAsync();
	}
}
