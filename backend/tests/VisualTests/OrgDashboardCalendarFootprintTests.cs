using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgDashboardCalendarFootprintTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task DefaultLayout_GivesTheCalendarAProportionateFootprint_AndLeavesNoGapBelowIt()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual CalFootprint {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		var calendar = Page.GetByTestId("widget-tile-Calendar");
		await Expect(calendar).ToBeVisibleAsync(new() { Timeout = 15_000 });

		(await calendar.EvaluateAsync<string>("el => el.style.gridRow"))
			.Should().Be("4 / span 4", "the Calendar defaults to 4 rows, not the 6 it shipped with");
		(await Page.GetByTestId("widget-tile-Settings").EvaluateAsync<string>("el => el.style.gridRow"))
			.Should().Be("8 / span 1",
				"shrinking the Calendar must pull Settings up with it, not leave three empty rows");

		var calendarBox = await calendar.BoundingBoxAsync();
		calendarBox.Should().NotBeNull();
		calendarBox!.Height.Should().BeLessThan(700,
			"the Calendar must stay close to its own content/floor height, not balloon back toward the "
				+ "~900px footprint #1795 fixed");

		foreach (var testId in new[] { "CreateOpportunity", "ToDo", "UpcomingOpportunities" })
		{
			var box = await Page.GetByTestId($"widget-tile-{testId}").BoundingBoxAsync();
			box.Should().NotBeNull();
			(box!.Y + box.Height).Should().BeLessThan(900,
				$"the {testId} widget must be fully above the fold at 1440x900");
		}

		await DeleteOrganizationAsync(backend, organizationId);
	}

	[Test]
	public async Task SavedLayout_KeepsItsOwnCalendarHeight_AndIsNotResetToTheNewDefault()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual CalSavedLayout {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var moveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Calendar" });
		await moveButton.FocusAsync();
		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();
		await Page.Keyboard.PressAsync("Enter");
		for (var i = 0; i < 7; i++)
			await Page.Keyboard.PressAsync("ArrowRight");
		for (var i = 0; i < 4; i++)
			await Page.Keyboard.PressAsync("ArrowDown");
		await Page.Keyboard.PressAsync("Enter");

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var calendar = Page.GetByTestId("widget-tile-Calendar");
		await Expect(calendar).ToBeVisibleAsync(new() { Timeout = 15_000 });

		string? gridRow = null;
		await PollUntilAsync(async () =>
		{
			gridRow = await calendar.EvaluateAsync<string>("el => el.style.gridRow");
			return gridRow == "4 / span 5";
		}, () => "a saved layout must keep its own Calendar height rather than being reset to the "
			+ $"default 4 rows (last observed: \"{gridRow}\")", timeoutMs: 10_000);

		await DeleteOrganizationAsync(backend, organizationId);
	}

	[Test]
	public async Task CustomizedMediumWidthCalendar_WithNoEventsInTheDefaultWeek_FallsBackToAgenda()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual CalEmptyWeek {Guid.NewGuid():N}");

		using (var http = await CreateAuthenticatedHttpClientAsync(backend))
		{
			var response = await http.PutAsJsonAsync(
				$"/v1/organizations/{organizationId}/dashboard/layout",
				new
				{
					widgets = new[]
					{
						new { widgetKey = "Calendar", x = 1, y = 1, width = 5, height = 4 },
					},
				});
			response.EnsureSuccessStatusCode();
		}

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		var calendarWidget = Page.GetByTestId("widget-tile-Calendar");
		await Expect(calendarWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(calendarWidget.Locator(".rbc-time-view")).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(calendarWidget.Locator(".rbc-agenda-view")).ToBeVisibleAsync();
		await Expect(calendarWidget.GetByText("No events in this range.")).ToBeVisibleAsync();

		await DeleteOrganizationAsync(backend, organizationId);
	}

	[Test]
	public async Task AgendaView_AtMobileWidth_FitsTheCardInsteadOfScrollingSideways()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");

		var organizationId = await CreateOrganizationAsync($"Visual CalAgendaFade {Guid.NewGuid():N}");

		using (var http = await CreateAuthenticatedHttpClientAsync(backend))
		{
			var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				titleDe = "Agenda Fade Opportunity",
				descriptionDe = "Seeded for calendar agenda scroll-fade coverage.",
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

			var slotStart = DateTimeOffset.UtcNow.AddDays(2);
			var slotResponse = await http.PostAsJsonAsync(
				$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
				new
				{
					startDateTime = slotStart,
					endDateTime = slotStart.AddHours(2),
					maxParticipants = 5,
					recurrenceCount = 1,
				});
			slotResponse.EnsureSuccessStatusCode();

			var publishResponse = await http.PostAsync(
				$"/v1/volunteer-opportunities/{opportunityId}/publish", null);
			publishResponse.EnsureSuccessStatusCode();
		}

		// The agenda view only defaults to the mobile "compact" widget size when the
		// dashboard mounts under 1024px - resizing after a desktop-width load does not
		// retroactively switch the calendar into agenda view.
		await Page.SetViewportSizeAsync(375, 812);
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, Guid.Parse(organizationId));

		var calendarWidget = Page.GetByTestId("widget-tile-Calendar");
		await Expect(calendarWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var agendaView = calendarWidget.Locator(".rbc-agenda-view");
		await Expect(agendaView).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// This used to assert the opposite - that the table overflowed and the fade
		// advertised the scroll. #2321 reclassified that as the defect: the table's
		// 30rem floor was wider than the card, so DATE and TIME took their fixed
		// widths first and the EVENT column - the only one whose content matters -
		// was left with ~40px, sliced mid-word, header cell reading "TERM". The
		// floor is now min(30rem, 100%), so the table fits the card and the event
		// title truncates with a real ellipsis instead of hiding behind a scroll.
		var overflows = await agendaView.EvaluateAsync<bool>(
			"el => el.scrollWidth > el.clientWidth + 1");
		overflows.Should().BeFalse(
			"the agenda table must fall back to the card's own width at 375px rather than "
			+ "putting its only meaningful column behind a horizontal scroll");

		var eventCellFits = await agendaView.EvaluateAsync<bool>(@"el => {
			const view = el.getBoundingClientRect();
			return [...el.querySelectorAll('.rbc-agenda-event-cell')]
				.every(cell => cell.getBoundingClientRect().right <= view.right + 1);
		}");
		eventCellFits.Should().BeTrue(
			"every event cell must end inside the agenda box - a cell reaching past it is "
			+ "the clipped-mid-word rendering again");

		// Deliberately not asserted here: how much of the card the event column ends up
		// with. The narrow-viewport padding/nowrap rule that widens it is worth pinning,
		// but every threshold that separates it from the unfixed rendering sits within a
		// few points of the line once the agenda renders in English (37% vs 30%), and the
		// date/time strings depend on the runner's timezone - a guard that brittle costs
		// more in false reds than it catches.

		// The fade is driven by real overflow, so with nothing to scroll it must stay
		// hidden rather than advertising a scroll that does not exist.
		var fadeRight = calendarWidget.GetByTestId("calendar-agenda-fade-right");
		await Expect(fadeRight).ToHaveCSSAsync("opacity", "0");

		await DeleteOrganizationAsync(backend, organizationId);
	}

	private async Task<string> CreateOrganizationAsync(string name)
	{
		var backend = Fixture.GetEndpoint("backend");
		using var http = await CreateAuthenticatedHttpClientAsync(backend);
		var response = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()!;
	}

	private async Task DeleteOrganizationAsync(Uri backend, string organizationId)
	{
		using var http = await CreateAuthenticatedHttpClientAsync(backend);
		await http.DeleteAsync($"/v1/organizations/{organizationId}");
	}

	private async Task<HttpClient> CreateAuthenticatedHttpClientAsync(Uri backend)
	{
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

		var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
		return http;
	}
}
