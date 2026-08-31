using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgDashboardWidgetsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ToDoWidget_ShowsTheError_AndNoQueue_WhenTheSignUpsFailToLoad()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add(
			"Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"Visual1780 Error {Guid.NewGuid():N}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		await Page.RouteAsync($"**/v1/organizations/{organizationId}/engagements*", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			await route.FulfillAsync(new()
			{
				Status = 500,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.6.1\",\"status\":500}",
			});
		});

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		var todoWidget = Page.GetByTestId("widget-tile-ToDo");
		await Expect(todoWidget.GetByText("Couldn't load the sign-ups waiting for you."))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		// A failed queue must not read as an empty one: "Nothing waiting" over a
		// request that never landed tells an organizer their inbox is clear when
		// nobody knows whether it is.
		await Expect(todoWidget.GetByTestId("todo-widget-resolved")).ToHaveCountAsync(0);
		await Expect(todoWidget.GetByRole(AriaRole.Button, new() { Name = "Confirm" }))
			.ToHaveCountAsync(0);
	}

	[Test]
	public async Task CalendarWidget_MobileViewport_ToolbarButtonsAndAgendaColumnStayReachable()
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
		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"Visual812 {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"Visual812 Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by CalendarWidget mobile overflow test",
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

		var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(10), TimeSpan.Zero);
		var end = start.AddHours(2);
		(await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = end, maxParticipants = 5, recurrenceCount = 1 }))
			.EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		var calendarWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Calendar", Exact = true }),
		});
		await Expect(calendarWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(390, 844);

		var viewGroup = calendarWidget.Locator(".rbc-btn-group").Last;
		var agendaButton = viewGroup.GetByRole(AriaRole.Button, new() { Name = "Agenda", Exact = true });

		await agendaButton.ScrollIntoViewIfNeededAsync();
		await Expect(agendaButton).ToBeVisibleAsync();
		await agendaButton.ClickAsync();

		var eventHeader = calendarWidget.Locator(".rbc-agenda-table thead th", new() { HasText = "Event" });
		await Expect(eventHeader).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var eventHeaderWidth = 0d;
		await PollUntilAsync(async () =>
		{
			eventHeaderWidth = await eventHeader.EvaluateAsync<double>(
				"el => el.getBoundingClientRect().width");
			return eventHeaderWidth > 80;
		}, () => "the EVENT column should stay legibly wide (the Agenda table scrolls "
			+ "horizontally instead) rather than being squeezed down to a couple "
			+ $"of characters to fit the narrow viewport (last observed width: {eventHeaderWidth}px)");
		await Expect(calendarWidget.GetByText(oppTitle)).ToBeVisibleAsync();

		var toolbarLabel = calendarWidget.Locator(".rbc-toolbar-label");
		var labelBeforeNext = await toolbarLabel.InnerTextAsync();

		var navGroup = calendarWidget.Locator(".rbc-btn-group").First;
		var nextButton = navGroup.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true });
		await nextButton.ScrollIntoViewIfNeededAsync();
		await Expect(nextButton).ToBeVisibleAsync();
		await nextButton.ClickAsync();
		await Expect(toolbarLabel).Not.ToHaveTextAsync(labelBeforeNext);

		var dayButton = viewGroup.GetByRole(AriaRole.Button, new() { Name = "Day", Exact = true });
		await dayButton.ScrollIntoViewIfNeededAsync();
		await Expect(dayButton).ToBeVisibleAsync();
		await dayButton.ClickAsync();
		await Expect(calendarWidget.Locator(".rbc-time-view")).ToBeVisibleAsync();
	}

	[Test]
	public async Task CalendarWidget_SelectEventAndSaveColor_RecoloredEventSurvivesReload()
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
		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"Visual1397 {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"Visual1397 Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by CalendarWidget color-save test",
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

		var start = DateTimeOffset.UtcNow.AddHours(1);
		var end = start.AddHours(2);
		(await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = end, maxParticipants = 5, recurrenceCount = 1 }))
			.EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		var calendarWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Calendar", Exact = true }),
		});
		await Expect(calendarWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var calendarEvent = calendarWidget.Locator(".rbc-event").First;
		await Expect(calendarEvent).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(calendarEvent).ToHaveCSSAsync("background-color", "rgb(34, 105, 71)");

		await calendarEvent.ClickAsync();
		var colorDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(colorDialog).ToBeVisibleAsync();
		await Expect(colorDialog).ToContainTextAsync(oppTitle);

		var colorInput = Page.Locator("#event-color-picker");
		await Expect(colorInput).ToHaveValueAsync("#226947");

		const string newColor = "#3366cc";
		await colorInput.FillAsync(newColor);
		await Expect(Page.GetByText(newColor)).ToBeVisibleAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
		await Expect(colorDialog).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Expect(calendarEvent).ToHaveCSSAsync("background-color", "rgb(51, 102, 204)");

		await Page.ReloadAsync();
		await Expect(calendarWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var reloadedEvent = calendarWidget.Locator(".rbc-event").First;
		await Expect(reloadedEvent).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(reloadedEvent).ToHaveCSSAsync("background-color", "rgb(51, 102, 204)");
	}

	[Test]
	public async Task UpcomingOpportunitiesWidget_ListsAPublishedOpportunity_WithItsNextSlotTime()
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
		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"Visual Upcoming {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"Visual Upcoming Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by the Upcoming Opportunities widget test",
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
		(await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = end, maxParticipants = 5, recurrenceCount = 1 }))
			.EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		var upcomingWidget = Page.GetByTestId("widget-tile-UpcomingOpportunities");
		await Expect(upcomingWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(upcomingWidget.GetByRole(AriaRole.Link, new() { Name = oppTitle }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(upcomingWidget).ToContainTextAsync(start.ToString("yyyy"));
		await Expect(upcomingWidget).ToContainTextAsync("0/5 places");
		await Expect(upcomingWidget.GetByText("This widget couldn't be displayed"))
			.ToHaveCountAsync(0);
	}
}
