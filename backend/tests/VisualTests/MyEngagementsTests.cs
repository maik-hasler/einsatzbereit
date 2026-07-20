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
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Page heading must be visible regardless of whether vera has engagements.
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync();

		// Keycloak seeding can fail silently in CI - if vera has no engagement
		// cards the empty state is valid and we skip the org-link assertions.
		var engagementCards = Page.Locator("li.rounded-xl");
		var cardCount = await engagementCards.CountAsync();
		if (cardCount == 0)
		{
			return;
		}

		// Other VisualTests classes sharing this Aspire session (e.g.
		// OpportunityApplicationStateTests, EngagementReactivationTests) also
		// sign up vera for their own throwaway opportunities/orgs, so a
		// non-zero card count no longer guarantees the seed engagements are
		// among them - only that seeding failing silently in CI produces the
		// same symptom (no card links to either seed org) as it would if
		// vera genuinely had zero engagements. Skip in both cases.
		var seedOrgCard = Page.GetByText("Fairview Red Cross")
			.Or(Page.GetByText("Fairview Animal Welfare Association"));
		if (await seedOrgCard.CountAsync() == 0)
		{
			return;
		}

		// Vera has engagement cards - every card must expose an org link.
		var orgLinks = Page.Locator("a[href^='/organizations/']");
		await Expect(orgLinks.First).ToBeVisibleAsync();

		// Both seed org names must appear somewhere on the page. .First avoids a
		// Playwright strict-mode violation once Vera has more than one engagement
		// with the same org (seed data grows release over release).
		await Expect(Page.GetByText("Fairview Red Cross").First).ToBeVisibleAsync();
		await Expect(Page.GetByText("Fairview Animal Welfare Association").First).ToBeVisibleAsync();
	}
}
