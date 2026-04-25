using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class NavigationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
    [Test]
    public async Task HomePage_HasMainHeading()
    {
        var frontend = Fixture.GetEndpoint("frontend");

        await Page.GotoAsync(frontend.ToString());
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator("h1").First).ToBeVisibleAsync();
    }
}
