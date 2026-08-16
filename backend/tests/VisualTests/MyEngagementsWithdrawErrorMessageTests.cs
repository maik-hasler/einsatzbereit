using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1918: WithdrawEngagementErrorMessageTests already covers
/// VolunteerOpportunityDetailPage.tsx's withdraw dialog reacting to the
/// server's specific errorCode (fixed under #1849), but
/// MyEngagementsPage/ActivitySection.tsx's own handleWithdrawConfirm - the
/// /my-signups page's withdraw flow the bug was actually filed against - was
/// never switched over to getApiErrorMessage() at the same time, and kept
/// checking `err instanceof Error`. That is never true for the parsed
/// ProblemDetails object the NSwag client throws, so every withdrawal
/// failure on this page, regardless of cause, fell back to the generic
/// "Could not withdraw your sign-up. Please try again." - looping the same
/// unhelpful message on a retry that could only ever fail the same way.
///
/// Reproduced here the same way as WithdrawEngagementErrorMessageTests: the
/// engagement is withdrawn out-of-band while the confirm dialog is open, so
/// confirming it hits Engagement.AlreadyTerminated (409 Conflict).
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MyEngagementsWithdrawErrorMessageTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MySignupsPage_ShowsSpecificTerminatedMessage_NotGenericWithdrawFallback()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations", new { name = $"MyEngagementsWithdrawErrorMessage Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"MyEngagementsWithdrawErrorMessage Opportunity {suffix}",
			description = "Created by MyEngagementsWithdrawErrorMessageTests.",
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
		var opportunityId = opportunity.GetProperty("id").GetString();

		var veraToken = (await Fixture.SignInAsync("vera", "vera123")).AccessToken;
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraToken}");
		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Ready to help with MyEngagementsWithdrawErrorMessageTests." });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Pending engagements already show the Withdraw button on this page
		// (unlike the detail page's application-status card, which only shows
		// it once Confirmed), so no confirm step is needed here.
		var card = Page.Locator($"[data-engagement-id='{engagementId}']");

		// This is a shared Aspire session (~50 VisualTests classes running
		// concurrently) - vera can have other no-time-slot engagements ahead of
		// this one on the "Current & upcoming" scope's page-1, so page through
		// to it the same way MyEngagementsTests does rather than assuming it
		// lands on the first page.
		await Expect(Page.Locator("#activity [data-testid='engagement-card']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await LoadMoreUntilVisibleAsync(card);
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await card.GetByRole(AriaRole.Button, new() { Name = "Withdraw" }).ClickAsync();
		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();

		// Withdraw out-of-band while the dialog sits open - the page's own list
		// state still shows the sign-up as active, so confirming below sends a
		// withdraw request the server can no longer honour.
		(await veraHttp.PostAsync($"/v1/engagements/{engagementId}/withdraw", content: null))
			.EnsureSuccessStatusCode();

		await dialog.GetByRole(AriaRole.Button, new() { Name = "Yes, withdraw" }).ClickAsync();

		var errorBanner = dialog.GetByRole(AriaRole.Alert);
		await Expect(errorBanner).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(errorBanner).ToHaveTextAsync("Sign-up is already terminated.");
	}
}
