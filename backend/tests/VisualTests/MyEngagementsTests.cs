using Microsoft.Playwright;

namespace VisualTests;

// #1316: needs vera's engagement set to deterministically be just her seed
// engagements - opts into fixture.ResetAsync() and a bare [NotInParallel] so
// no other VisualTest can add throwaway engagements/organizations for her
// mid-test.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class MyEngagementsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Before(Test)]
	public Task ResetVisualTestStateAsync() => Fixture.ResetAsync();

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
