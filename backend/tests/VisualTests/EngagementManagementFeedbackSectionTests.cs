using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1835: the "Feedback" section on the per-opportunity
/// "Manage sign-ups" page (EngagementManagementPage) rendered its heading via
/// PageSectionHeading - the same font-display text-2xl family as the page's
/// own H1 (OrgPageHeader) - and stayed on screen with a permanent
/// "No feedback yet." placeholder even when the opportunity had zero
/// feedback submissions. The fix demotes the heading to a subordinate size
/// and only renders the section once there is feedback (or a load error) to
/// show.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementManagementFeedbackSectionTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EngagementManagementPage_OmitsFeedbackSection_WhenNoFeedbackSubmitted()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (organizationId, opportunityId, _) = await SeedConfirmedEngagementAsync("NoFeedback");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync(
			$"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Feedback", Exact = true }))
			.Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task EngagementManagementPage_ShowsSubordinateFeedbackHeading_WhenFeedbackExists()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (organizationId, opportunityId, engagementId) =
			await SeedConfirmedEngagementAsync("WithFeedback");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");
		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/check-in", null)).EnsureSuccessStatusCode();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		(await veraHttp.PostAsJsonAsync($"/v1/engagements/{engagementId}/feedback", new { rating = 5, comment = "Great!" }))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync(
			$"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var pageTitle = Page.Locator("main").GetByRole(AriaRole.Heading, new() { Level = 1 });
		await Expect(pageTitle).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var feedbackHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Feedback", Exact = true });
		await Expect(feedbackHeading).ToBeVisibleAsync();

		// The bug was purely visual weight (same face/size family as the page's
		// own H1), not structure - assert the rendered heading is actually
		// smaller than the page title rather than just present.
		var titleFontSize = await pageTitle.EvaluateAsync<double>(
			"el => parseFloat(getComputedStyle(el).fontSize)");
		var feedbackFontSize = await feedbackHeading.EvaluateAsync<double>(
			"el => parseFloat(getComputedStyle(el).fontSize)");

		feedbackFontSize.Should().BeLessThan(titleFontSize,
			"the \"Feedback\" section heading must stay visually subordinate to the page's own title (#1835)");
	}

	private async Task<(string OrganizationId, string OpportunityId, string EngagementId)> SeedConfirmedEngagementAsync(string label)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		// Fresh organization rather than olaf's shared seed org - other
		// VisualTests running concurrently in this shared Aspire session can
		// mutate/delete shared orgs (see EngagementManagementCheckInPinTests).
		var orgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations", new { name = $"FeedbackSection {label} Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"FeedbackSection {label} Opportunity {suffix}",
			descriptionDe = "Created by EngagementManagementFeedbackSectionTests",
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
		var opportunityId = opportunity.GetProperty("id").GetString()!;

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = $"{label} application." });
		applyResponse.EnsureSuccessStatusCode();
		var engagement = await applyResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString()!;

		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", null)).EnsureSuccessStatusCode();

		return (organizationId, opportunityId, engagementId);
	}
}
