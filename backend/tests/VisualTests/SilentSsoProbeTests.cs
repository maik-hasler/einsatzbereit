using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SilentSsoProbeTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task FreshTab_WithLiveSsoSession_HeaderShowsLoggedIn()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		var freshTab = await Context.NewPageAsync();
		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(freshTab);
		await freshTab.GotoAsync(origin);

		await Expect(freshTab.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(freshTab.GetByRole(AriaRole.Button, new() { Name = "Sign in" }))
			.Not.ToBeVisibleAsync();
	}
}
