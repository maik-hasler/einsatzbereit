using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1777: the same slots on an opportunity card meant different things - or
/// nothing - across the surfaces that show them.
///
/// - The public grid's line under the title was either a start date or an
///   application deadline, rendered with identical classes and the same
///   calendar icon, so only its label said which. The capacity chip appeared
///   on some cards and not others.
/// - The organizer list dropped the sign-up count entirely for any opportunity
///   with no time slots.
/// - /my-signups put the volunteer's own application message in the region
///   where the card beside it stated its date.
/// - The detail page framed capacity as a per-slot maximum where the card that
///   linked to it had said free places, and never showed the remaining places
///   to an anonymous visitor at all.
///
/// The shared cause of the two capacity faults is one backend value:
/// `VolunteerOpportunitySummary.TotalMaxParticipants` is tri-state (null =
/// unlimited, 0 = no time slots, &gt; 0 = capped) and every surface handled
/// only two of the three. These pin the contract on each surface, since
/// nothing else fails when a card silently states nothing.
///
/// The grid cases build their own opportunities and reach them through the
/// list's `?q=` keyword filter rather than asserting against seed data on page
/// one: this suite runs in parallel against one shared stack, and other tests
/// publish opportunities that would otherwise push the expected cards out of
/// the first page.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityCardContractTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int SlotCapacity = 20;

	/// <summary>
	/// Upper bound on "Load more" clicks when hunting for a card on
	/// /my-signups - see <see cref="RevealMySignUpCardAsync"/>. Ten sign-ups
	/// per page, so this covers 120 of vera's engagements.
	/// </summary>
	private const int MaxLoadMorePages = 12;

	/// <summary>
	/// Resolves once a "Load more" click has visibly landed: either the list
	/// grew past the row count passed in, or the button is no longer rendered
	/// (last page reached, or the load failed and LoadMoreError took its
	/// place). See <see cref="RevealMySignUpCardAsync"/>.
	/// </summary>
	private const string MoreRowsOrNoLoadMoreButton =
		"""
		rowsBefore =>
			document.querySelectorAll("#activity [data-testid='engagement-card']").length > rowsBefore
			|| !document.querySelector("[data-testid='load-more']")
		""";

	/// <summary>
	/// The acceptance criterion the previous code deliberately failed: a
	/// deadline card and a start-date card have to be distinguishable without
	/// reading the label. Asserted on the rendered colour and glyph rather than
	/// on class names - what matters is that the eye can separate them. Uses a
	/// deadline inside the imminent window (#2088's amber only applies there -
	/// see the next test for a deadline outside it).
	/// </summary>
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

		// A different glyph too, so the distinction survives for a reader who
		// cannot separate the two tones.
		var startGlyph = await startLine.Locator("svg path").First.GetAttributeAsync("d");
		var deadlineGlyph = await deadlineLine.Locator("svg path").First.GetAttributeAsync("d");
		deadlineGlyph.Should().NotBe(startGlyph);
	}

	/// <summary>
	/// #2088: every deadline used to render in the same amber warning tone
	/// regardless of how far away it was, including ones months out - which
	/// diluted the warning for a deadline that was actually close. A deadline
	/// outside the imminent window now falls back to the same neutral tone a
	/// start date uses.
	/// </summary>
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

	/// <summary>
	/// An interest-based opportunity has no time slots, so its
	/// TotalMaxParticipants is 0 - the value that used to fall through both
	/// branches of the chip logic and render nothing.
	/// </summary>
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

	/// <summary>
	/// The card and the page it links to have to state the same capacity, and
	/// the number has to be there for the anonymous visitor who makes up most
	/// of the detail page's traffic - "N spots left" used to be gated on
	/// `isAuthenticated &amp;&amp; !isOwner &amp;&amp; !cue &amp;&amp; !isDraft`,
	/// leaving a signed-out reader with the per-slot maximum instead.
	/// </summary>
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

		// Same number, same framing, and visible without signing in.
		await Expect(Page.GetByTestId("opportunity-capacity")).ToHaveTextAsync(expected);

		// The per-slot line moved to the same free-places framing, so the
		// parenthetical maximum the review saw is gone from the page entirely.
		(await Page.Locator("main").InnerTextAsync()).Should().NotContain("max. ",
			"one capacity framing across list and detail - free places, not a maximum");
	}

	/// <summary>
	/// AC: hovering or tabbing to a card title has to show it is a link. The
	/// grid's title is not focusable itself - the stretched link covering the
	/// card is - so hover is asserted on the title and the keyboard half is
	/// asserted as the card no longer clipping that link's focus ring, which is
	/// what made tabbing through the grid move an invisible focus.
	/// </summary>
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

		// global.css's shared focus ring (#992) draws at outline-offset 2px,
		// entirely outside the stretched link's box - which is the card's box.
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

		// The first card that actually links somewhere, not simply the first
		// card: an engagement whose opportunity was deleted renders its title as
		// a plain span on purpose, and ordering decides which lands first.
		var card = Page.Locator("#activity [data-testid='engagement-card']")
			.Filter(new() { Has = Page.Locator("a[href*='/volunteer-opportunities/']") })
			.First;
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var title = card.Locator("a[href*='/volunteer-opportunities/']").First;
		await Expect(title).ToBeVisibleAsync();

		// It carried no underline and exactly the classes of the plain-text
		// fallback span used for a deleted opportunity, so a real route read as
		// static text.
		await title.HoverAsync();
		var decoration = await title.EvaluateAsync<string>(
			"el => getComputedStyle(el).textDecorationLine");
		decoration.Should().Be("underline", "a link a reader cannot recognize is not an entry point");
	}

	/// <summary>
	/// Pages /my-signups until the card whose title contains <paramref name="keyword"/>
	/// is on screen.
	///
	/// It is not on the first page. "Current &amp; Upcoming" orders by the
	/// shift's own start time and sorts entries that have no time slot last -
	/// they have no shift to sort by (see
	/// <c>EngagementReadRepository.GetByVolunteerAsync</c>) - so an
	/// interest-based sign-up lands at the very end of a list that grows all
	/// suite long, since every test here shares the one `vera` account and the
	/// page shows ten at a time.
	/// </summary>
	private async Task<ILocator> RevealMySignUpCardAsync(string keyword)
	{
		var cards = Page.Locator("#activity [data-testid='engagement-card']");
		var card = cards.Filter(new() { HasText = keyword }).First;
		// The test id, not the accessible name: the button's label flips to
		// "Loading…" while a page is in flight, so a name-based locator matches
		// nothing mid-load (see LoadMoreButton.tsx's own note).
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
				// The CountAsync above is a sample, not a lease: the button can
				// leave the DOM between it and this click (last page landed, or
				// LoadMoreError swapped in). Both mean "nothing left to page
				// through", which CountAsync() == 0 already treats as stop - so a
				// short timeout here, not the 30s default that would fail the test
				// instead of reaching its assertion.
				await loadMore.ClickAsync(new() { Timeout = 10_000 });

				// Not NetworkIdle: that samples the whole page and can return
				// before this click's request is even dispatched. Wait on what the
				// loop depends on - more rows, or the button gone.
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

	/// <summary>Published, one capped time slot - a card whose date line is a start date.</summary>
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

	/// <summary>
	/// Published, no time slots, with an application deadline - the
	/// TotalMaxParticipants == 0 case, and a card whose date line is a deadline.
	/// </summary>
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
