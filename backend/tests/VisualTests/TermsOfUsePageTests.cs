using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class TermsOfUsePageTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task KeycloakRegistrationForm_RequiresAcceptingTermsOfUse()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).First.ClickAsync();

		await Expect(Page.Locator("#kc-register-form")).ToBeVisibleAsync(new() { Timeout = 30_000 });
		await Expect(Page.Locator("#kc-registration-terms-text")).ToBeVisibleAsync();
		await Expect(Page.Locator("#termsAccepted")).ToBeVisibleAsync();
	}
}
