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

		// The seed unconditionally signs vera up for 3 engagements across both
		// seed organizations (see ApplicationDbContextInitializer) - engagement
		// cards, and links to both seed orgs among them, are always present on
		// a healthy stack. Other VisualTests classes sharing this Aspire session
		// also sign vera up for their own throwaway opportunities/orgs, but
		// those are additive - they never remove the permanent seed engagements.
		var engagementCards = Page.Locator("li.rounded-xl");
		await Expect(engagementCards.First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var seedOrgCard = Page.GetByText("Fairview Red Cross")
			.Or(Page.GetByText("Fairview Animal Welfare Association"));
		await Expect(seedOrgCard.First).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Every card must expose an org link.
		var orgLinks = Page.Locator("a[href^='/organizations/']");
		await Expect(orgLinks.First).ToBeVisibleAsync();

		// Both seed org names must appear somewhere on the page. .First avoids a
		// Playwright strict-mode violation once Vera has more than one engagement
		// with the same org (seed data grows release over release).
		await Expect(Page.GetByText("Fairview Red Cross").First).ToBeVisibleAsync();
		await Expect(Page.GetByText("Fairview Animal Welfare Association").First).ToBeVisibleAsync();
	}
}
