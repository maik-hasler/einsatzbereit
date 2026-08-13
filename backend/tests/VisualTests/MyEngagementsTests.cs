using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MyEngagementsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MyEngagementsPage_ShowsOrganizationNameLinks_ForVerasEngagements()
	{
		// Regression: org name and org link were missing from engagement cards
		// before PR #475 added OrganizationId/OrganizationName to EngagementSummary.
		//
		// #1334: this used to assert against vera's *ambient* engagement cards
		// (seed data, plus whatever throwaway engagements other concurrently
		// running test classes happened to have created for her in this shared
		// session), guarded by two stacked "return if empty" checks - so its
		// assertions only ran when seed data happened to be present and no
		// other test had already polluted the card count, and it never proved
		// anything about a card it could actually identify. Creates and owns
		// its own organization/opportunity/engagement instead (the pattern
		// EngagementReactivationTests.cs already uses), and scopes every
		// assertion to that specific card via data-engagement-id, so this is
		// deterministic regardless of what else runs in this shared session.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgName = $"MyEngagements Org {suffix}";
		var orgResponse = await olafHttp.PostAsJsonAsync("/v1/organizations", new { name = orgName });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		// CreateOrganizationEndpoint returns the raw domain Organization
		// aggregate, whose strongly-typed OrganizationId record struct
		// serializes as a nested { "value": "<guid>" } object.
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"MyEngagements Opportunity {suffix}",
			description = "Created by MyEngagementsTests.",
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
		var opportunityId = opportunity.GetProperty("id").GetString();

		var veraToken = (await Fixture.SignInAsync("vera", "vera123")).AccessToken;
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraToken}");
		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Signing up via MyEngagementsTests." });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("h1").First).ToBeVisibleAsync();

		var card = Page.Locator($"[data-engagement-id='{engagementId}']");

		// IndividualContact engagements have no time slot, and
		// EngagementReadRepository.GetByVolunteerAsync orders the "Current &
		// upcoming" scope by time-slot start (entries with none sort last) - so on
		// a shared session where other concurrently-running tests have already
		// given vera their own time-slotted upcoming engagements, this card can
		// land past the first (10-item) page instead of being visible immediately,
		// so page through to it. A fixed iteration count isn't reliable here - how
		// many other no-time-slot engagements vera has accumulated by this point
		// varies with test scheduling/parallelism across the ~50 VisualTests
		// classes sharing this session, so LoadMoreUntilVisibleAsync bounds by
		// wall-clock time instead of a click count.
		//
		// Wait for the first page before starting: the WaitForLoadStateAsync above
		// can settle before the engagements fetch is even issued, since
		// useLoadMore only requests from an effect after React commits.
		await Expect(Page.Locator("#activity [data-testid='engagement-card']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await LoadMoreUntilVisibleAsync(card);

		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgLink = card.Locator($"a[href='/organizations/{organizationId}']");
		await Expect(orgLink).ToBeVisibleAsync();
		await Expect(orgLink).ToHaveTextAsync(orgName);

		// Leave vera's account clean for the rest of this shared Aspire session.
		var withdrawResponse = await veraHttp.PostAsync($"/v1/engagements/{engagementId}/withdraw", content: null);
		withdrawResponse.EnsureSuccessStatusCode();
	}

	[Test]
	public async Task MyEngagementsPage_StatesItsTitleOnce_WithTheInContentHeadingSrOnly()
	{
		// #1796: /my-signups printed its own title twice - once as the header
		// band's <h1> ("My sign-ups", myEngagementsPage.title) and again
		// roughly 200px below it as a SectionHeading eyebrow rendering a second
		// key with the same string ("My sign-ups", myEngagements.title), where
		// every other page's eyebrow carries a *category* the title does not
		// repeat. That second one is sr-only now: it still marks where the
		// invitations block ends and the sign-ups list begins for a screen
		// reader, but it no longer costs vertical space on a page that is short
		// of content, and the scope tabs move up into the space it held.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My sign-ups", Level = 1 }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		// Accessible-name matching is case-insensitive, so this finds the
		// in-content heading whichever of the two casings it carries.
		var inContentTitle = Page.Locator("#activity")
			.GetByRole(AriaRole.Heading, new() { Name = "My sign-ups" });
		await Expect(inContentTitle).ToHaveCountAsync(1);

		// sr-only clips it to a 1px box: still in the accessibility tree, gone
		// from the page. The eyebrow this replaces rendered ~16px tall, so a
		// regression would blow well past this bound. Playwright counts an
		// sr-only element as visible (it has a non-empty box), which is why
		// this asserts geometry rather than Not.ToBeVisibleAsync().
		var box = await inContentTitle.BoundingBoxAsync();
		box.Should().NotBeNull();
		box!.Height.Should().BeLessThan(4,
			"the in-content heading must stay sr-only - a second visible copy of the <h1> is what #1796 removed");

		// What sits where that eyebrow did: the scope switcher, which now
		// carries the group name the heading above it used to imply.
		await Expect(Page.GetByRole(AriaRole.Group, new() { Name = "Time range" }))
			.ToBeVisibleAsync();
	}
}
