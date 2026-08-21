using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;
using TUnit.Core;

namespace VisualTests;

/// <summary>
/// Regression tests for #1779: every day button in the /opportunities date filter
/// was enabled, so picking a day in the past was fully available and answered with
/// a silently empty list, and nothing on the grid distinguished a day that has
/// opportunities from one that has none.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class DateFilterCalendarTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// The grid's idea of "today" comes from the browser's clock; the expectations
	// below are computed on the runner. Left unpinned the two are the same date
	// only by luck of the runner's local zone - they disagree outright for the
	// hours either side of midnight, which is exactly when this class used to
	// fail. Pinning the context to UTC and computing in UTC below makes both
	// sides read the same calendar day at every instant of the day.
	public override BrowserNewContextOptions ContextOptions(TestContext testContext)
	{
		var options = base.ContextOptions(testContext);
		options.TimezoneId = "UTC";
		return options;
	}

	private async Task OpenDateFilterAsync(Uri frontend)
	{
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Date", Exact = true }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Grid)).ToBeVisibleAsync();
	}

	[Test]
	public async Task DateFilter_MarksTheDayASeededOpportunityRunsOn_AndLeavesEmptyDaysUnmarked()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync(Page);
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		// Midday UTC, so the day the browser resolves this slot to is the same
		// calendar day the seeding code computed for any plausible runner timezone.
		var slotStart = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(10), TimeSpan.Zero).AddHours(12);
		var organizationId = await CreateOrganizationAsync(http, "Visual1779 DateFilter");
		await CreateOpportunityWithSlotAsync(http, organizationId, "Marked day opportunity", slotStart);

		await OpenDateFilterAsync(frontend);

		var markedDate = Iso(slotStart.UtcDateTime);
		var markedCell = Page.Locator($"[data-date='{markedDate}']");
		if (await markedCell.CountAsync() == 0)
		{
			// Ten days ahead can land in next month; the grid shows one month at a time.
			await Page.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
		}

		await Expect(markedCell).ToHaveAttributeAsync("data-marked", "true", new() { Timeout = 15_000 });
		// The mark is not only a dot: the day's accessible name says how much is on it,
		// so a screen-reader user gets the same signal a sighted one does.
		await Expect(markedCell).ToHaveAttributeAsync("aria-label", new Regex("1 opportunity"),
			new() { Timeout = 15_000 });

		// The day before it has nothing on it - the mark has to distinguish days,
		// not simply appear on all of them.
		var neighbourDate = Iso(slotStart.UtcDateTime.AddDays(-1));
		var neighbourCell = Page.Locator($"[data-date='{neighbourDate}']");
		if (await neighbourCell.CountAsync() > 0)
		{
			await Expect(neighbourCell).Not.ToHaveAttributeAsync("data-marked", "true");
		}
	}

	private static string Iso(DateTime day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

	private static async Task<string> GetAccessTokenAsync(IPage page)
	{
		var token = await page.EvaluateAsync<string?>(@"() => {
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
		return token!;
	}

	private static async Task<string> CreateOrganizationAsync(HttpClient http, string namePrefix)
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var response = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"{namePrefix} {suffix}" });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()!;
	}

	private static async Task CreateOpportunityWithSlotAsync(
		HttpClient http, string organizationId, string title, DateTimeOffset slotStart)
	{
		// A ScheduledSlots opportunity can't be published before it has a time slot,
		// so it goes draft -> slot -> publish.
		var createResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Seeded for #1779 date-filter availability marks.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
		});
		createResponse.EnsureSuccessStatusCode();
		var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = created.GetProperty("id").GetString()!;

		var slotResponse = await http.PostAsJsonAsync($"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
		{
			startDateTime = slotStart,
			endDateTime = slotStart.AddHours(2),
			maxParticipants = 10,
			recurrenceCount = 1,
		});
		slotResponse.EnsureSuccessStatusCode();

		var publishResponse = await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", null);
		publishResponse.EnsureSuccessStatusCode();
	}
}
