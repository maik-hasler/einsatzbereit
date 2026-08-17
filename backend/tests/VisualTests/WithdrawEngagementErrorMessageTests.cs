using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1849: WithdrawEngagementEndpoint used to catch every
/// ResultFailureException locally and collapse it into a bare
/// Results.Problem(ex.Error.Description, statusCode: 400) - losing the
/// errorCode extension ResultFailureExceptionHandler normally adds, and
/// forcing every failure (even a Forbidden ownership check) onto 400.
/// handleWithdrawConfirm (VolunteerOpportunityDetailPage.tsx) then checked
/// `err instanceof Error`, which is never true for the parsed ProblemDetails
/// object the NSwag client throws - so every withdrawal failure, regardless
/// of cause, showed the generic "Error withdrawing" fallback instead of the
/// server's actual rejection reason. Fixed by letting the endpoint's
/// failures propagate to the global exception handler (matching
/// CancelEngagementEndpoint's pattern) and switching the page to
/// getApiErrorMessage(), the same mechanism #1250 fixed for the check-in PIN
/// dialog (see LocalizedCheckInPinErrorTests).
///
/// Reproduced here via a stale-dialog double withdraw: the engagement is
/// withdrawn out-of-band while the confirm dialog is open, so the UI's own
/// withdraw call - unaware of that change - hits Engagement.AlreadyTerminated
/// (409 Conflict) once confirmed.
///
/// Also covers #1950: once that terminal error is showing, ConfirmDialog used
/// to leave its original "Keep"/"Yes, withdraw" pair active, inviting a
/// second attempt guaranteed to fail the same way. The dialog should instead
/// swap to a single "Understood" acknowledgement that just dismisses it.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class WithdrawEngagementErrorMessageTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task DetailPage_ShowsSpecificTerminatedMessage_NotGenericWithdrawFallback()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations", new { name = $"WithdrawErrorMessage Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"WithdrawErrorMessage Opportunity {suffix}",
			descriptionDe = "Created by WithdrawEngagementErrorMessageTests.",
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
			new { message = "Ready to help with WithdrawEngagementErrorMessageTests." });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		// Only a Confirmed sign-up shows the withdraw button on this page's
		// application-status card (VolunteerOpportunityDetailPage.tsx).
		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Withdraw" }).ClickAsync();
		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();

		// Withdraw out-of-band while the dialog sits open - the page's own state
		// still thinks the sign-up is Confirmed, so confirming below sends a
		// withdraw request the server can no longer honour.
		(await veraHttp.PostAsync($"/v1/engagements/{engagementId}/withdraw", content: null))
			.EnsureSuccessStatusCode();

		await dialog.GetByRole(AriaRole.Button, new() { Name = "Yes, withdraw" }).ClickAsync();

		var errorBanner = dialog.GetByRole(AriaRole.Alert);
		await Expect(errorBanner).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(errorBanner).ToHaveTextAsync("Sign-up is already terminated.");

		await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Yes, withdraw" }))
			.Not.ToBeVisibleAsync();
		await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Keep" }))
			.Not.ToBeVisibleAsync();

		var understoodButton = dialog.GetByRole(AriaRole.Button, new() { Name = "Understood" });
		await Expect(understoodButton).ToBeVisibleAsync();
		await understoodButton.ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();
	}
}
