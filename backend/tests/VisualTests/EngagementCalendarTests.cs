using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementCalendarTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	/// <summary>
	/// Regression for #572: a Confirmed engagement with a time slot must show
	/// an "Add to Calendar" menu in "My Sign-ups" with Google Calendar,
	/// Apple Calendar (webcal), and .ics download links scoped to that one
	/// engagement - not the old opportunity-level file download.
	/// </summary>
	[Test]
	public async Task ConfirmedEngagementWithTimeSlot_ShowsAddToCalendarMenu_WithScopedLinks()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in sessionStorage after login");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"VisualCal {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"VisualCal Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by EngagementCalendarTests",
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

		var start = DateTimeOffset.UtcNow.AddDays(3);
		var end = start.AddHours(2);
		var slotResponse = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = end, maxParticipants = 5, recurrenceCount = 1 });
		slotResponse.EnsureSuccessStatusCode();
		var slots = await slotResponse.Content.ReadFromJsonAsync<JsonElement>();
		var timeSlotId = slots[0].GetProperty("id").GetString();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		var engagementResponse = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { type = "ScheduledSlots", timeSlotId, message = (string?)null });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		(await http.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var row = Page.Locator("li", new() { HasText = oppTitle });
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var calendarButton = row.GetByRole(AriaRole.Button, new() { Name = "Add to Calendar" });
		await Expect(calendarButton).ToBeVisibleAsync();
		await calendarButton.ClickAsync();

		var googleHref = await row.GetByRole(AriaRole.Link, new() { Name = "Google Calendar" }).GetAttributeAsync("href");
		googleHref.Should().Contain("calendar.google.com");
		googleHref.Should().Contain(Uri.EscapeDataString(oppTitle).Replace("%20", "+"));

		var appleHref = await row.GetByRole(AriaRole.Link, new() { Name = "Apple Calendar" }).GetAttributeAsync("href");
		appleHref.Should().StartWith("webcal://");
		appleHref.Should().Contain($"/engagements/{engagementId}/calendar");

		var downloadHref = await row.GetByRole(AriaRole.Link, new() { Name = "Download .ics" }).GetAttributeAsync("href");
		downloadHref.Should().Be($"{backend.GetLeftPart(UriPartial.Authority)}/v1/engagements/{engagementId}/calendar");

		var icsResponse = await http.GetAsync(downloadHref);
		icsResponse.EnsureSuccessStatusCode();
		icsResponse.Content.Headers.ContentType!.MediaType.Should().Be("text/calendar");
		icsResponse.Content.Headers.ContentDisposition!.FileName.Should().Be($"engagement-{engagementId}.ics");

		var icsBody = await icsResponse.Content.ReadAsStringAsync();
		icsBody.Should().Contain("BEGIN:VCALENDAR");
		icsBody.Should().Contain($"UID:{engagementId}@einsatzbereit");
	}
}
