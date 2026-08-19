using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #2075 (issue finding F19 from PR #2039): after signing up,
/// the only signal a volunteer got was an amber "Pending" chip - nothing
/// explained what "pending" means, who resolves it, or whether to show up
/// while it's still pending on the day. A one-sentence explanation now sits
/// next to that chip, reused verbatim on both surfaces that show it: the
/// opportunity detail page's own sidebar status card and the /my-signups
/// engagement card. It disappears once the organizer confirms the sign-up,
/// since the chip itself already reads unambiguously at that point.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class PendingSignUpExplanationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const string ExplanationText =
		"The organization is reviewing your sign-up. You'll get a message once it's confirmed.";

	[Test]
	public async Task PendingExplanation_IsShown_OnOpportunityDetailPageAndMySignUps()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var organizerHttp = new HttpClient { BaseAddress = backend };
		organizerHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(organizerHttp, "/v1/organizations", new { name = $"PendingExplain Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"PendingExplain Opportunity {suffix}";
		var oppResponse = await organizerHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by PendingSignUpExplanationTests",
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
		using var volunteerHttp = new HttpClient { BaseAddress = backend };
		volunteerHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraToken}");

		var engagementResponse = await volunteerHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Applying via PendingSignUpExplanationTests." });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		var statusCard = Page.GetByTestId("application-status");
		await Expect(statusCard).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(statusCard.GetByText("Pending")).ToBeVisibleAsync();
		await Expect(statusCard.GetByText(ExplanationText)).ToBeVisibleAsync();

		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		// This is a shared Aspire session (~50 VisualTests classes running
		// concurrently) - IndividualContact engagements have no time slot, and
		// EngagementReadRepository.GetByVolunteerAsync orders the "Current &
		// upcoming" scope by time-slot start (slot-less entries sort last), so
		// vera's other concurrently-created, time-slotted engagements can push
		// this card past the first (10-item) page. Page through to it by its
		// stable data-engagement-id the same way MyEngagementsTests does,
		// instead of assuming it lands on page 1.
		var card = Page.Locator($"[data-engagement-id='{engagementId}']");
		await Expect(Page.Locator("#activity [data-testid='engagement-card']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await LoadMoreUntilVisibleAsync(card);
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(card.GetByText("Pending")).ToBeVisibleAsync();
		await Expect(card.GetByText(ExplanationText)).ToBeVisibleAsync();
	}

	[Test]
	public async Task PendingExplanation_IsHidden_OnceConfirmed()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var organizerHttp = new HttpClient { BaseAddress = backend };
		organizerHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(organizerHttp, "/v1/organizations", new { name = $"PendingExplainConfirmed Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"PendingExplainConfirmed Opportunity {suffix}";
		var oppResponse = await organizerHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by PendingSignUpExplanationTests",
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
		using var volunteerHttp = new HttpClient { BaseAddress = backend };
		volunteerHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraToken}");

		var engagementResponse = await volunteerHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Applying via PendingSignUpExplanationTests." });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		(await organizerHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		var statusCard = Page.GetByTestId("application-status");
		await Expect(statusCard).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(statusCard.GetByText("Confirmed")).ToBeVisibleAsync();
		await Expect(statusCard.GetByText(ExplanationText)).ToHaveCountAsync(0);

		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		// See PendingExplanation_IsShown_OnOpportunityDetailPageAndMySignUps
		// above for why this pages through by data-engagement-id rather than
		// assuming the card lands on page 1.
		var card = Page.Locator($"[data-engagement-id='{engagementId}']");
		await Expect(Page.Locator("#activity [data-testid='engagement-card']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await LoadMoreUntilVisibleAsync(card);
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(card.GetByText("Confirmed")).ToBeVisibleAsync();
		await Expect(card.GetByText(ExplanationText)).ToHaveCountAsync(0);
	}
}
