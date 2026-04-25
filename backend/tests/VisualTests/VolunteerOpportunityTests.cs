using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class VolunteerOpportunityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
    [Test]
    public async Task HomePage_RendersOpportunitiesList()
    {
        var frontend = Fixture.GetEndpoint("frontend");

        await Page.GotoAsync(frontend.ToString());
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator("main")).ToBeVisibleAsync();
    }
}
