using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SignOutLanguagePersistenceTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// #1838: handleSignOut (Header.tsx) used to clear "i18nextLng" and
	// "einsatzbereit:language-explicit" alongside the #1676 account-tied
	// storage cleanup, reverting an explicit UI language choice back to the
	// browser-detected default on every sign-out. A UI language pick is a
	// device preference, not account data - unlike DangerZoneCard's full
	// account deletion, sign-out must leave it alone. A real LoginAsync (not
	// FastSignInAsync) is required here since the assertion is that the
	// language survives the real signoutRedirect() round trip through
	// Keycloak and back.
	[Test]
	public async Task SignOut_PreservesExplicitLanguageChoice()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		// This suite's default browser context resolves to English with no
		// stored choice (see NavigationTests's
		// HomePage_LanguageSelector_SwitchingLanguage_LazilyLoadsAndAppliesTranslations),
		// so switching to German here is a real change to persist, not a no-op.
		await Page.GetByTestId("language-selector-trigger").ClickAsync();
		await Page.GetByTestId("language-selector-menu")
			.GetByRole(AriaRole.Button, new() { Name = "Deutsch" })
			.ClickAsync();
		await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "de");

		await Page.GetByRole(AriaRole.Button, new() { Name = "Benutzermenü" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Abmelden" }).ClickAsync();

		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 30_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Anmelden" }).First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "de");

		var explicitChoice = await Page.EvaluateAsync<string?>(
			"() => localStorage.getItem('einsatzbereit:language-explicit')");
		var storedLanguage = await Page.EvaluateAsync<string?>(
			"() => localStorage.getItem('i18nextLng')");

		explicitChoice.Should().Be("true", "sign-out must not clear the explicit language choice flag");
		storedLanguage.Should().Be("de", "sign-out must not revert the previously chosen UI language");
	}
}
