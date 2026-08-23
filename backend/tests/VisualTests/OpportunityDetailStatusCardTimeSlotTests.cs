using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityDetailStatusCardTimeSlotTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ApplicationStatusCard_ShowsRegisteredTimeSlot_ForMultiSlotOpportunity()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var organizerHttp = new HttpClient { BaseAddress = backend };
		organizerHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(organizerHttp, "/v1/organizations", new { name = $"StatusCardSlot Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"StatusCardSlot Opportunity {suffix}";
		var oppResponse = await organizerHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by OpportunityDetailStatusCardTimeSlotTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var firstStart = DateTimeOffset.UtcNow.AddDays(5);
		(await organizerHttp.PostAsJsonAsync($"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
		{
			startDateTime = firstStart,
			endDateTime = firstStart.AddHours(2),
			maxParticipants = 5,
			recurrenceCount = 1,
		})).EnsureSuccessStatusCode();

		var secondStart = firstStart.AddDays(7);
		var secondSlotResponse = await organizerHttp.PostAsJsonAsync($"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
		{
			startDateTime = secondStart,
			endDateTime = secondStart.AddHours(4),
			maxParticipants = 5,
			recurrenceCount = 1,
		});
		secondSlotResponse.EnsureSuccessStatusCode();
		var secondSlots = await secondSlotResponse.Content.ReadFromJsonAsync<JsonElement>();
		var secondTimeSlotId = secondSlots[0].GetProperty("id").GetString();

		(await organizerHttp.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		var veraToken = (await Fixture.SignInAsync("vera", "vera123")).AccessToken;
		using var volunteerHttp = new HttpClient { BaseAddress = backend };
		volunteerHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraToken}");

		var engagementResponse = await volunteerHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { type = "ScheduledSlots", timeSlotId = secondTimeSlotId, message = (string?)null });
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
		await Expect(statusCard.GetByText("Your sign-up")).ToBeVisibleAsync();
		await Expect(statusCard.GetByText("Confirmed")).ToBeVisibleAsync();

		await Expect(statusCard.GetByText("Scheduled:")).ToBeVisibleAsync();
	}
}
