using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SmokeTests(AspireFixture fixture) : VisualTestBase(fixture)
{
    [Test]
    public async Task HomePage_LoadsForAnonymousUser()
    {
        var frontend = Fixture.GetEndpoint("frontend");

        await Page.GotoAsync(frontend.ToString());
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator("h1")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_AsHannah_CompletesAndReturnsToHome()
    {
        var frontend = Fixture.GetEndpoint("frontend");

        await AuthHelper.LoginAsync(Page, frontend, "hannah", "hannah123");

        var origin = frontend.GetLeftPart(UriPartial.Authority);
        await Expect(Page).ToHaveURLAsync($"{origin}/");
    }
}
