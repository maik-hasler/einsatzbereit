using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MyEngagementsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MyEngagementsPage_ShowsOrganizationNameLinks_ForVerasEngagements()
	{
		// Regression: org name and org link were missing from the My Engagements
		// card list before PR #475 added OrganizationId/OrganizationName to
		// EngagementSummary and the frontend rendered them as links.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Page heading must be visible.
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync();

		// Vera has 3 seeded engagements - at least one org link must appear.
		var orgLinks = Page.Locator("a[href^='/organizations/']");
		var count = await orgLinks.CountAsync();
		count.Should().BeGreaterThan(0, "each engagement card must show an org link");

		// Both seed org names must appear somewhere on the page.
		await Expect(Page.GetByText("Rotes Kreuz Musterstadt")).ToBeVisibleAsync();
		await Expect(Page.GetByText("Tierschutzverein Musterstadt")).ToBeVisibleAsync();
	}
}
