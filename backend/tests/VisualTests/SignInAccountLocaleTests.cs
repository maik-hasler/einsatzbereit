using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression coverage for #1842: onSigninCallback (main.tsx) silently applied
/// the signed-in account's stored Keycloak locale as the app's UI language
/// whenever it differed from the current session's language and the user had
/// never made an explicit in-app choice - with nothing on screen marking that
/// this had just happened. The fix surfaces the switch as a toast, held open
/// long enough to survive the window.location.replace navigation that follows
/// it (the same race useSessionExpiryHandler.ts already had to solve for its
/// own toast-then-navigate sequence).
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SignInAccountLocaleTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Satisfies the realm's passwordPolicy (upperCase(1), length(8)) - see
	// KeycloakThemeTests.ThrowawayPassword's own doc comment for why this
	// matters at user-creation time rather than at login.
	private const string ThrowawayPassword = "Throwaway123";

	[Test]
	public async Task SignIn_AccountLocaleDiffersFromSessionLanguage_ShowsToastAndSwitchesLanguage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var username = $"localetoast1842-{Guid.NewGuid():N}";

		// A fresh throwaway account, never signed in before and with no
		// in-app language choice on record - the exact precondition
		// onSigninCallback's hasExplicitLanguageChoice guard checks for. Its
		// Keycloak "locale" attribute (mapped to the id_token's "locale"
		// claim by the realm's default "profile" client scope) is seeded to
		// "de", which differs from the "en" a fresh Playwright browser
		// context resolves to via i18next's navigator-based detection - see
		// OrgDashboardWidgetsTests' German-locale tests, which explicitly
		// switch language via the header for that same reason.
		var userId = await Fixture.CreateThrowawayUserAsync(
			username, ThrowawayPassword, emailVerified: true, requiredActions: [],
			attributes: new Dictionary<string, string[]> { ["locale"] = ["de"] });

		try
		{
			// Mirrors AuthHelper.LoginAsync up to the final URL wait, which is
			// deliberately not reused here: it waits for the post-redirect "/"
			// URL, but the toast this test asserts on renders earlier, on the
			// intermediate /callback page, before that redirect fires.
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

			// Confirms i18n.changeLanguage actually completed and survived the
			// hard navigation, not just that the toast fired - the header's
			// user-menu button only carries this label once German is active.
			await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Benutzermenü" }))
				.ToBeVisibleAsync(new() { Timeout = 15_000 });
		}
		finally
		{
			await Fixture.DeleteUserAsync(userId);
		}
	}
}
