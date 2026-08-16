using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1250: getApiErrorMessage() (frontend/src/lib/apiError.ts)
/// used to prefer the server's raw ProblemDetails.Detail - always English,
/// see ResultFailureExceptionHandler - over the caller-supplied,
/// already-localized fallback string. A German volunteer entering a wrong
/// check-in PIN therefore saw the backend's raw "Invalid PIN." instead of the
/// translated checkIn.invalidPin fallback ("Falsche PIN. Bitte erneut
/// versuchen."). Fixed by mapping the ProblemDetails errorCode extension to
/// an apiError.&lt;errorCode&gt; translation key first, only falling back to
/// the caller's string when no such key exists - server text is never
/// rendered to the user.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LocalizedCheckInPinErrorTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task CheckInModal_ShowsGermanTranslatedInvalidPinMessage_NotRawEnglishServerText()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations", new { name = $"LocalizedPinError Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		const string pin = "482170";
		var oppTitle = $"LocalizedPinError Opportunity {suffix}";
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by LocalizedCheckInPinErrorTests.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "PINCode",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			checkInPin = pin,
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
			new { type = "IndividualContact", message = "Ready to help with LocalizedCheckInPinErrorTests." });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		// Only a Confirmed engagement's "Check in" button renders (ActivitySection.tsx).
		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var row = Page.Locator("li", new() { HasText = oppTitle });

		// This engagement has no time slot (IndividualContact), and
		// EngagementReadRepository.GetByVolunteerAsync orders the "Current &
		// upcoming" scope by time-slot start (entries with none sort last) - so on
		// a shared session where other concurrently-running tests have already
		// given vera their own time-slotted upcoming engagements, this row can
		// land past the first (10-item) page, so page through to it before
		// switching language below (the load state doesn't depend on locale).
		//
		// Wait for the first page before starting: the WaitForLoadStateAsync
		// above can settle before the engagements fetch is even issued, since
		// useLoadMore only requests from an effect after React commits.
		await Expect(Page.Locator("#activity [data-testid='engagement-card']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await LoadMoreUntilVisibleAsync(row);

		// Switch to German only after signing in - FastSignInAsync itself waits on
		// the English "User menu" aria-label (see OrgDashboardWidgetsTests).
		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();

		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await row.GetByRole(AriaRole.Button, new() { Name = "Einchecken" }).ClickAsync();

		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();

		await dialog.Locator("#pin-input").FillAsync("000000");
		await dialog.GetByRole(AriaRole.Button, new() { Name = "Bestätigen" }).ClickAsync();

		await Expect(dialog.GetByText("Falsche PIN. Bitte erneut versuchen."))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		// The bug this guards against: the raw English backend detail text
		// must never be rendered to the user, regardless of locale.
		await Expect(dialog.GetByText("Invalid PIN.")).Not.ToBeVisibleAsync();
	}
}
