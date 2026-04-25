using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AuthGuardTests(AspireFixture fixture) : VisualTestBase(fixture)
{
    [Test]
    public async Task MyEngagements_Anonymous_RedirectsToKeycloak()
    {
        var frontend = Fixture.GetEndpoint("frontend");

        await Page.GotoAsync($"{frontend}my-engagements", new() { WaitUntil = WaitUntilState.Commit });
        await Page.WaitForURLAsync("**/realms/einsatzbereit/protocol/openid-connect/auth*");

        await Expect(Page.Locator("#username")).ToBeVisibleAsync();
        await Expect(Page.Locator("#password")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Header_Anonymous_ShowsAnmeldenButton()
    {
        var frontend = Fixture.GetEndpoint("frontend");

        await Page.GotoAsync(frontend.ToString());
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Anmelden" }).First)
            .ToBeVisibleAsync();
    }
}
