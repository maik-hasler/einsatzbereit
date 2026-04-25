using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrganizationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
    [Test]
    public async Task Organisator_LoginAsOlaf_Succeeds()
    {
        var frontend = Fixture.GetEndpoint("frontend");

        await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var origin = frontend.GetLeftPart(UriPartial.Authority);
        await Expect(Page).ToHaveURLAsync($"{origin}/");
    }
}
