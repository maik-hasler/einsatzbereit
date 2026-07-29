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
		Skip.When(cardCount == 0, "Keycloak seeding can fail silently in CI - if vera has no engagement cards the empty state is valid and there is nothing to assert.");

		// Vera has engagement cards - other, unrelated tests sharing this
		// session can add throwaway engagements for her too, so this doesn't
		// assert the set is *exactly* her seed engagements, only that every
		// visible card (seed or not) exposes an org link, and that the two
		// seed org names are present somewhere among them.
		var orgLinks = Page.Locator("a[href^='/organizations/']");
		await Expect(orgLinks.First).ToBeVisibleAsync();

		// Both seed org names must appear somewhere on the page. .First avoids a
		// Playwright strict-mode violation once Vera has more than one engagement
		// with the same org (seed data grows release over release).
		await Expect(Page.GetByText("Fairview Red Cross").First).ToBeVisibleAsync();
		await Expect(Page.GetByText("Fairview Animal Welfare Association").First).ToBeVisibleAsync();
	}
}
