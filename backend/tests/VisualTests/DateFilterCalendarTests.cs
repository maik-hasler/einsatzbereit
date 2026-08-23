using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;
using TUnit.Core;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class DateFilterCalendarTests(AspireFixture fixture) : VisualTestBase(fixture)
{
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

		var slotStart = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(10), TimeSpan.Zero).AddHours(12);
		var organizationId = await CreateOrganizationAsync(http, "Visual1779 DateFilter");
		await CreateOpportunityWithSlotAsync(http, organizationId, "Marked day opportunity", slotStart);

		await OpenDateFilterAsync(frontend);

		var markedDate = Iso(slotStart.UtcDateTime);
		var markedCell = Page.Locator($"[data-date='{markedDate}']");
		if (await markedCell.CountAsync() == 0)
		{
			await Page.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
		}

		await Expect(markedCell).ToHaveAttributeAsync("data-marked", "true", new() { Timeout = 15_000 });

		await Expect(markedCell).ToHaveAttributeAsync("aria-label", new Regex("1 opportunity"),
			new() { Timeout = 15_000 });

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
