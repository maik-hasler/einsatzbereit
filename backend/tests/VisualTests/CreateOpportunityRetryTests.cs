using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CreateOpportunityRetryTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task PublishScheduledSlots_TimeSlotFailureMidPublish_RetryUpdatesSameDraftInsteadOfDuplicating()
	{
		// Regression for #1227: the create-then-publish flow is create draft ->
		// upload banner -> create N time slots -> publish. If a time slot
		// creation fails partway through, the draft opportunity is already
		// persisted, incomplete. Retrying used to call createVolunteerOpportunity
		// again, producing a second, duplicate draft opportunity. The retry must
		// now reuse the first attempt's opportunity id (updating it instead of
		// re-creating it) and only create the time slots still missing.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var uniqueTitle = $"Retry Dedup Test {Guid.NewGuid().ToString("N")[..8]}";

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		var organizationId = pinnedOrgId!.Value;
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, organizationId);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });

		await Page.Locator("#opportunity-title").FillAsync(uniqueTitle);
		await Page.Locator("#opportunity-description").FillAsync(
			"Regression test for the #1227 retry-duplicate-draft bug.");

		// Step 2: remote, to skip address fields.
		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
		await Page.Locator("#opportunity-remote").CheckAsync();

		// Step 3: Scheduled slots, so publishing requires time slots and goes
		// through the create-draft-then-publish path this bug lives in.
		await Page.GetByTestId("wizard-stepper-3").ClickAsync();
		await Page.Locator("label:has(input[name='participationType'][value='ScheduledSlots'])").ClickAsync();

		// Step 4: add two time slots up front - the first CreateTimeSlot call
		// is made to fail below, and the second must still end up created
		// after the retry succeeds.
		await Page.GetByTestId("wizard-stepper-4").ClickAsync();
		var step4 = Page.GetByTestId("wizard-step-4");

		async Task AddSlotAsync(int daysFromNow)
		{
			var start = DateTimeOffset.UtcNow.AddDays(daysFromNow);
			var end = start.AddHours(2);
			await step4.Locator("#slot-start").FillAsync(start.ToString("yyyy-MM-ddTHH:mm"));
			await step4.Locator("#slot-end").FillAsync(end.ToString("yyyy-MM-ddTHH:mm"));
			await step4.GetByRole(AriaRole.Button, new() { Name = "Add", Exact = true }).ClickAsync();
		}

		await AddSlotAsync(7);
		await AddSlotAsync(14);
		await Expect(step4.Locator("ul li")).ToHaveCountAsync(2);

		// Fail exactly the first CreateTimeSlot call (whichever slot the
		// frontend happens to send first) with a plain 500 - everything else,
		// including the earlier CreateVolunteerOpportunity call and every
		// later request, passes through untouched.
		var timeSlotCallCount = 0;
		await Page.RouteAsync("**/volunteer-opportunities/*/time-slots", async route =>
		{
			if (route.Request.Method != "POST")
			{
				await route.ContinueAsync();
				return;
			}

			timeSlotCallCount++;
			if (timeSlotCallCount != 1)
			{
				await route.ContinueAsync();
				return;
			}

			// Cross-origin in this test environment (see NotificationTests for the
			// same note)
			// - a fulfilled response still needs CORS headers or the browser
			// rejects it before the app's own error handling runs.
			await route.FulfillAsync(new()
			{
				Status = 500,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"about:blank\",\"status\":500,\"title\":\"Internal Server Error\"}",
			});
		});

		await Page.GetByTestId("modal-submit").ClickAsync();

		// The mocked failure surfaces as the generic fallback error (no
		// errorCode on the mocked body) and the dialog stays open for retry.
		var errorBanner = Page.GetByRole(AriaRole.Alert)
			.Filter(new() { HasTextString = "Unknown error" });
		await Expect(errorBanner).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.Locator("[role='dialog']")).ToBeVisibleAsync();

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		async Task<List<JsonElement>> GetOpportunitiesByTitleAsync(string status)
		{
			var response = await http.GetAsync(
				$"/v1/organizations/{organizationId}/opportunities?status={status}&pageNumber=1&pageSize=100");
			response.EnsureSuccessStatusCode();
			var body = await response.Content.ReadFromJsonAsync<JsonElement>();
			return body.GetProperty("items").EnumerateArray()
				.Where(o => o.GetProperty("titleDe").GetString() == uniqueTitle)
				.ToList();
		}

		// Exactly one draft exists after the failed attempt - the create call
		// itself succeeded before the mocked time-slot failure, but no second
		// draft was created by that failure.
		var draftsAfterFailure = await GetOpportunitiesByTitleAsync("Draft");
		draftsAfterFailure.Should().HaveCount(1,
			"the failed time-slot creation must leave exactly the one draft created before it failed");
		var opportunityId = draftsAfterFailure[0].GetProperty("id").GetString();

		// Retry: same button, same form state. All requests now succeed,
		// including the previously-failing time slot.
		await Page.GetByTestId("modal-submit").ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 30_000 });

		var draftsAfterRetry = await GetOpportunitiesByTitleAsync("Draft");
		draftsAfterRetry.Should().BeEmpty(
			"the retried publish must move the original draft to Published, not leave a second draft behind");

		var publishedAfterRetry = await GetOpportunitiesByTitleAsync("Published");
		publishedAfterRetry.Should().HaveCount(1,
			"retrying publish must reuse the original draft's id instead of creating a duplicate opportunity");
		publishedAfterRetry[0].GetProperty("id").GetString().Should().Be(opportunityId,
			"the published opportunity must be the same one created on the first attempt");

		var detailsResponse = await http.GetAsync($"/v1/volunteer-opportunities/{opportunityId}");
		detailsResponse.EnsureSuccessStatusCode();
		var details = await detailsResponse.Content.ReadFromJsonAsync<JsonElement>();
		details.GetProperty("timeSlots").GetArrayLength().Should().Be(2,
			"both time slots must exist exactly once - the one that failed on the first attempt, "
			+ "created on retry, plus the one that never failed");
	}
}
