using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SignInAccountLocaleTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const string ThrowawayPassword = "Throwaway123";

	[Test]
	public async Task SignIn_AccountLocaleDiffersFromSessionLanguage_ShowsToastAndSwitchesLanguage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var username = $"localetoast1842-{Guid.NewGuid():N}";

		var userId = await Fixture.CreateThrowawayUserAsync(
			username, ThrowawayPassword, emailVerified: true, requiredActions: [],
			attributes: new Dictionary<string, string[]> { ["locale"] = ["de"] });

		try
		{
			await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);
			await Page.GotoAsync(frontend.ToString());
			await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).First.ClickAsync();

			await Page.Locator("#username").WaitForAsync(new() { Timeout = 30_000 });
			await Page.Locator("#username").FillAsync(username);
			await Page.Locator("#password").FillAsync(ThrowawayPassword);
			await Page.Locator("#kc-login").ClickAsync();

			var languageToast = Page.GetByRole(AriaRole.Alert)
				.Filter(new() { HasTextString = "Switched to Deutsch based on your account" });
			await Expect(languageToast).ToBeVisibleAsync(new() { Timeout = 15_000 });

			await Page.WaitForURLAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/", new() { Timeout = 15_000 });

			await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Benutzermenü" }))
				.ToBeVisibleAsync(new() { Timeout = 15_000 });
		}
		finally
		{
			await Fixture.DeleteUserAsync(userId);
		}
	}
}
