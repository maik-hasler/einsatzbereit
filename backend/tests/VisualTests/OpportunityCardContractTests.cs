using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityCardContractTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int SlotCapacity = 20;

	private const int MaxLoadMorePages = 12;

	private const string MoreRowsOrNoLoadMoreButton =
		"""
		rowsBefore =>
			document.querySelectorAll("#activity [data-testid='engagement-card']").length > rowsBefore
			|| !document.querySelector("[data-testid='load-more']")
		""";

	[Test]
	public async Task PublicGrid_ADeadlineCard_LooksDifferentFromAStartDateCard()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var keyword = $"CardKinds{Guid.NewGuid():N}";
		using var organizer = await CreateOrganizerClientAsync(keycloak, backend);
		var organizationId = await CreateOrganizationAsync(organizer, keyword);

		await PublishSlotBasedOpportunityAsync(organizer, organizationId, $"{keyword} with a slot");
		await PublishInterestBasedOpportunityAsync(
			organizer, organizationId, $"{keyword} with a deadline", TimeSpan.FromDays(3));

		await Page.GotoAsync($"{origin}/opportunities?q={keyword}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var startLine = Page.Locator("[data-testid='opportunity-date-line'][data-date-kind='start']").First;
		var deadlineLine = Page.Locator("[data-testid='opportunity-date-line'][data-date-kind='deadline']").First;

		await Expect(startLine).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(deadlineLine).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(startLine).ToContainTextAsync("Starts");
		await Expect(deadlineLine).ToContainTextAsync("Express interest by");

		var startColor = await startLine.EvaluateAsync<string>("el => getComputedStyle(el).color");
		var deadlineColor = await deadlineLine.EvaluateAsync<string>("el => getComputedStyle(el).color");
		deadlineColor.Should().NotBe(startColor,
			"a start date and an imminent application deadline are different kinds of fact in the same slot");

		var startGlyph = await startLine.Locator("svg path").First.GetAttributeAsync("d");
		var deadlineGlyph = await deadlineLine.Locator("svg path").First.GetAttributeAsync("d");
		deadlineGlyph.Should().NotBe(startGlyph);
	}

	[Test]
	public async Task PublicGrid_ADistantDeadlineCard_UsesTheSameNeutralToneAsAStartDateCard()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var keyword = $"CardDistantDeadline{Guid.NewGuid():N}";
		using var organizer = await CreateOrganizerClientAsync(keycloak, backend);
		var organizationId = await CreateOrganizationAsync(organizer, keyword);

		await PublishSlotBasedOpportunityAsync(organizer, organizationId, $"{keyword} with a slot");
		await PublishInterestBasedOpportunityAsync(
			organizer, organizationId, $"{keyword} with a distant deadline", TimeSpan.FromDays(90));

		await Page.GotoAsync($"{origin}/opportunities?q={keyword}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var startLine = Page.Locator("[data-testid='opportunity-date-line'][data-date-kind='start']").First;
		var deadlineLine = Page.Locator("[data-testid='opportunity-date-line'][data-date-kind='deadline']").First;

		await Expect(startLine).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(deadlineLine).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(deadlineLine).ToContainTextAsync("Express interest by");

		var startColor = await startLine.EvaluateAsync<string>("el => getComputedStyle(el).color");
		var deadlineColor = await deadlineLine.EvaluateAsync<string>("el => getComputedStyle(el).color");
		deadlineColor.Should().Be(startColor,
			"a deadline months away should not carry the same urgent tone as one about to close");
	}

	[Test]
	public async Task PublicGrid_AnInterestBasedCard_StillStatesItsCapacity()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var keyword = $"CardInterest{Guid.NewGuid():N}";
		using var organizer = await CreateOrganizerClientAsync(keycloak, backend);
		var organizationId = await CreateOrganizationAsync(organizer, keyword);
		await PublishInterestBasedOpportunityAsync(organizer, organizationId, keyword);

		await Page.GotoAsync($"{origin}/opportunities?q={keyword}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var card = Page.Locator("li", new() { HasText = keyword }).First;
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(card.GetByTestId("opportunity-capacity"))
			.ToHaveTextAsync("By expression of interest");
	}

	[Test]
	public async Task OpportunityDetail_StatesTheSameRemainingPlacesAsItsCard_ToAnAnonymousVisitor()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var keyword = $"CardCapacity{Guid.NewGuid():N}";
		using var organizer = await CreateOrganizerClientAsync(keycloak, backend);
		var organizationId = await CreateOrganizationAsync(organizer, keyword);
		var (opportunityId, timeSlotId) =
			await PublishSlotBasedOpportunityAsync(organizer, organizationId, keyword);

		using var volunteer = await CreateVolunteerClientAsync(keycloak, backend);
		(await volunteer.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { type = "ScheduledSlots", timeSlotId, message = (string?)null }))
			.EnsureSuccessStatusCode();

		var expected = $"{SlotCapacity - 1} spots left";

		await Page.GotoAsync($"{origin}/opportunities?q={keyword}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var card = Page.Locator("li", new() { HasText = keyword }).First;
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(card.GetByTestId("opportunity-capacity")).ToHaveTextAsync(expected);

		await card.Locator("a[href*='/volunteer-opportunities/']").First.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/volunteer-opportunities/{opportunityId}",
			new() { Timeout = 15_000 });

		await Expect(Page.GetByTestId("opportunity-capacity")).ToHaveTextAsync(expected);

		(await Page.Locator("main").InnerTextAsync()).Should().NotContain("max. ",
			"one capacity framing across list and detail - free places, not a maximum");
	}

	[Test]
	public async Task PublicGrid_ACardTitle_IsDiscoverablyALink()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var card = Page.Locator("li", new() { Has = Page.GetByTestId("opportunity-date-line") }).First;
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var title = card.Locator("h2, h3").First;
		await card.HoverAsync();
		var decoration = await title.EvaluateAsync<string>(
			"el => getComputedStyle(el).textDecorationLine");
		decoration.Should().Be("underline", "a title that never changes on hover reads as plain text");

		var overflow = await card.EvaluateAsync<string>("el => getComputedStyle(el).overflow");
		overflow.Should().NotBe("hidden",
			"clipping the card's descendants clips the stretched link's focus ring away entirely");
	}

	[Test]
	public async Task MySignUps_ACardTitle_IsDiscoverablyALink()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var card = Page.Locator("#activity [data-testid='engagement-card']")
			.Filter(new() { Has = Page.Locator("a[href*='/volunteer-opportunities/']") })
			.First;
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var title = card.Locator("a[href*='/volunteer-opportunities/']").First;
		await Expect(title).ToBeVisibleAsync();

		await title.HoverAsync();
		var decoration = await title.EvaluateAsync<string>(
			"el => getComputedStyle(el).textDecorationLine");
		decoration.Should().Be("underline", "a link a reader cannot recognize is not an entry point");
	}

	private async Task<ILocator> RevealMySignUpCardAsync(string keyword)
	{
		var cards = Page.Locator("#activity [data-testid='engagement-card']");
		var card = cards.Filter(new() { HasText = keyword }).First;

		var loadMore = Page.GetByTestId("load-more");

		for (var page = 0; page < MaxLoadMorePages; page++)
		{
			if (await card.CountAsync() > 0)
				break;

			if (await loadMore.CountAsync() == 0)
				break;

			var rowsBefore = await cards.CountAsync();

			try
			{
				await loadMore.ClickAsync(new() { Timeout = 10_000 });

				await Page.WaitForFunctionAsync(MoreRowsOrNoLoadMoreButton, rowsBefore,
					new() { Timeout = 15_000 });
			}
			catch (TimeoutException)
			{
				break;
			}
		}

		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });
		return card;
	}

	private static Task<HttpClient> CreateOrganizerClientAsync(Uri keycloak, Uri backend) =>
		CreateClientAsync(keycloak, backend, "olaf", "olaf123");

	private static Task<HttpClient> CreateVolunteerClientAsync(Uri keycloak, Uri backend) =>
		CreateClientAsync(keycloak, backend, "vera", "vera123");

	private static async Task<HttpClient> CreateClientAsync(
		Uri keycloak,
		Uri backend,
		string username,
		string password)
	{
		var token = await AuthHelper.GetTokenAsync(keycloak, username, password);
		var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
		return http;
	}

	private static async Task<string> CreateOrganizationAsync(HttpClient organizer, string keyword)
	{
		var response = await PostJsonWithRetryAsync(organizer, "/v1/organizations", new { name = $"Org {keyword}" });
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("id").GetProperty("value").GetString()
			?? throw new InvalidOperationException("organization id missing");
	}

	private static async Task<(string OpportunityId, string TimeSlotId)> PublishSlotBasedOpportunityAsync(
		HttpClient organizer,
		string organizationId,
		string title)
	{
		var response = await organizer.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by OpportunityCardContractTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
		});
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = body.GetProperty("id").GetString()
			?? throw new InvalidOperationException("opportunity id missing");

		var start = DateTimeOffset.UtcNow.AddDays(21);
		var slotResponse = await organizer.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new
			{
				startDateTime = start,
				endDateTime = start.AddHours(3),
				maxParticipants = SlotCapacity,
				recurrenceCount = 1,
			});
		slotResponse.EnsureSuccessStatusCode();
		var slots = await slotResponse.Content.ReadFromJsonAsync<JsonElement>();
		var timeSlotId = slots[0].GetProperty("id").GetString()
			?? throw new InvalidOperationException("time slot id missing");

		(await organizer.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		return (opportunityId, timeSlotId);
	}

	private static async Task<string> PublishInterestBasedOpportunityAsync(
		HttpClient organizer,
		string organizationId,
		string title,
		TimeSpan? validUntilOffset = null)
	{
		var response = await organizer.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by OpportunityCardContractTests",
			organizationId,
			isRemote = true,
			occurrence = "Recurring",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.Add(validUntilOffset ?? TimeSpan.FromDays(30)),
		});
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("id").GetString()
			?? throw new InvalidOperationException("opportunity id missing");
	}
}
