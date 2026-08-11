using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Coverage for einsatzbereit#1038: a published opportunity previously had no
/// exit besides hard delete, which silently mass-cancelled every active
/// engagement with no audit trail and no volunteer notification. Unpublish()
/// and Cancel(reason) give the organizer a reversible take-down and a
/// terminal cancellation respectively - both cascade-cancel active
/// engagements, but asynchronously via the outbox (see
/// VolunteerOpportunityUnpublishedDomainEventHandler /
/// VolunteerOpportunityCancelledDomainEventHandler), so these tests poll
/// rather than assert immediately after the UI action returns.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityUnpublishCancelTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task Unpublish_MovesToUnpublishedSection_CascadeCancelsEngagement_AndCanBeRepublished()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var (opportunityId, organizationId, title) = await CreatePublishedOpportunityAsync(olafHttp, "Unpublish Flow");
		var engagementId = await ApplyAsVeraAsync(backend, opportunityId);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var publishedSection = Page.GetByTestId("published-section");
		var row = publishedSection.Locator("li", new() { HasText = title });
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await OpportunityRowHelper.ClickActionAsync(row, "opportunity-unpublish");
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Yes, unpublish" }).ClickAsync();

		var unpublishedSection = Page.GetByTestId("unpublished-section");
		var unpublishedRow = unpublishedSection.Locator("li", new() { HasText = title });
		await Expect(unpublishedRow).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(unpublishedRow.GetByTestId("opportunity-status-badge")).ToHaveTextAsync("Unpublished");

		// The outbox job polls every 5s (appsettings.json Outbox:PollIntervalSeconds) -
		// give it real headroom rather than asserting immediately.
		await PollUntilAsync(
			async () => await GetEngagementStatusAsync(olafHttp, opportunityId, engagementId) == "Cancelled",
			() => $"Expected engagement {engagementId} to be cascade-cancelled after unpublishing opportunity {opportunityId}.",
			timeoutMs: 20_000);

		// Unpublished is reversible - Publish() works again from this state.
		await unpublishedRow.GetByTestId("opportunity-publish").ClickAsync();
		await Expect(publishedSection.Locator("li", new() { HasText = title })).ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	[Test]
	public async Task Cancel_MovesToCancelledSection_CascadeCancelsEngagementWithReason_AndIsTerminal()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var (opportunityId, organizationId, title) = await CreatePublishedOpportunityAsync(olafHttp, "Cancel Flow");
		var engagementId = await ApplyAsVeraAsync(backend, opportunityId);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var publishedSection = Page.GetByTestId("published-section");
		var row = publishedSection.Locator("li", new() { HasText = title });
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await OpportunityRowHelper.ClickActionAsync(row, "opportunity-cancel");
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
		await Page.Locator("#cancel-opportunity-reason").FillAsync("Venue is no longer available");
		await Page.GetByRole(AriaRole.Button, new() { Name = "Yes, cancel opportunity" }).ClickAsync();

		var cancelledSection = Page.GetByTestId("cancelled-section");
		var cancelledRow = cancelledSection.Locator("li", new() { HasText = title });
		await Expect(cancelledRow).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(cancelledRow.GetByTestId("opportunity-status-badge")).ToHaveTextAsync("Cancelled");

		// Terminal - Cancelled offers no Edit/Publish/Unpublish/Cancel, only
		// Delete. Publish is the one primary action rendered on the card
		// itself; the rest live in the row's overflow menu, so open it before
		// asserting on what it does and does not contain.
		await Expect(cancelledRow.GetByTestId("opportunity-publish")).Not.ToBeVisibleAsync();
		await OpportunityRowHelper.OpenActionsAsync(cancelledRow);
		await Expect(cancelledRow.GetByTestId("opportunity-delete")).ToBeVisibleAsync();
		await Expect(cancelledRow.GetByTestId("opportunity-edit")).Not.ToBeVisibleAsync();
		await Expect(cancelledRow.GetByTestId("opportunity-unpublish")).Not.ToBeVisibleAsync();
		await Expect(cancelledRow.GetByTestId("opportunity-cancel")).Not.ToBeVisibleAsync();
		await Page.Keyboard.PressAsync("Escape");

		await PollUntilAsync(
			async () =>
			{
				var (status, reason) = await GetEngagementStatusAndReasonAsync(olafHttp, opportunityId, engagementId);
				return status == "Cancelled" && reason == "Opportunity was cancelled: Venue is no longer available";
			},
			() => $"Expected engagement {engagementId} to be cascade-cancelled with the organizer's reason after cancelling opportunity {opportunityId}.",
			timeoutMs: 20_000);
	}

	private static async Task<(string OpportunityId, string OrganizationId, string Title)> CreatePublishedOpportunityAsync(
		HttpClient olafHttp, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		var orgResponse = await olafHttp.PostAsJsonAsync("/v1/organizations", new { name = $"{label} Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var title = $"{label} {suffix}";
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title,
			description = $"Created by {nameof(OpportunityUnpublishCancelTests)}",
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

	private async Task<string> ApplyAsVeraAsync(Uri backend, string opportunityId)
	{
		var veraToken = (await Fixture.SignInAsync("vera", "vera123")).AccessToken;
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraToken}");

		var response = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Please let me help." });
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("id").GetString()!;
	}

	private static async Task<string?> GetEngagementStatusAsync(HttpClient olafHttp, string opportunityId, string engagementId)
	{
		var (status, _) = await GetEngagementStatusAndReasonAsync(olafHttp, opportunityId, engagementId);
		return status;
	}

	private static async Task<(string? Status, string? CancellationReason)> GetEngagementStatusAndReasonAsync(
		HttpClient olafHttp, string opportunityId, string engagementId)
	{
		var response = await olafHttp.GetAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements?pageNumber=1&pageSize=50");
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		foreach (var item in body.GetProperty("items").EnumerateArray())
		{
			if (item.GetProperty("id").GetString() == engagementId)
			{
				var reason = item.TryGetProperty("cancellationReason", out var r) ? r.GetString() : null;
				return (item.GetProperty("status").GetString(), reason);
			}
		}
		return (null, null);
	}
}
