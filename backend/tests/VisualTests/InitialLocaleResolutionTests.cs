using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression coverage for #1934: i18next defaults <c>initAsync</c> to
/// <c>true</c>, which defers changeLanguage() - and therefore setting
/// <c>i18next.language</c> - into a setTimeout. i18n.ts (frontend) read
/// <c>i18next.language</c> synchronously, one statement after calling
/// <c>.init()</c>, to set <c>document.documentElement.lang</c> before first
/// paint - so that assignment ran before the language had actually been
/// resolved. Language *detection* itself (localStorage/navigator) is
/// synchronous, so the fix (<c>initAsync: false</c>) removes the
/// unnecessary extra tick instead of touching detection order.
///
/// This mirrors the issue's own reproduction: repeated full navigations
/// between "/" and "/opportunities" with no language toggle touched at any
/// point, on a context with no stored language choice, asserting both the
/// html "lang" attribute and the actually rendered heading text stay
/// deterministic across all of them (rather than flip between English and
/// German with no user action).
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class InitialLocaleResolutionTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task Navigation_InitialLocale_StaysDeterministicAcrossFullPageLoads()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// This suite's default browser context resolves to English with no
		// stored choice (see NavigationTests's
		// HomePage_LanguageSelector_SwitchingLanguage_LazilyLoadsAndAppliesTranslations),
		// so "en" is the deterministic target for every one of these full
		// navigations - none of them touch the language selector.
		(string Url, string Heading)[] navigations =
		[
			(frontend.ToString(), "Your volunteering starts here."),
			($"{origin}/opportunities", "Find opportunities"),
			(frontend.ToString(), "Your volunteering starts here."),
			($"{origin}/opportunities", "Find opportunities"),
		];
		foreach (var (url, heading) in navigations)
		{
			await Page.GotoAsync(url);
			await Expect(Page.Locator("h1").First).ToHaveTextAsync(heading, new() { Timeout = 15_000 });
			await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "en");
		}

		var storedLanguage = await Page.EvaluateAsync<string?>("() => localStorage.getItem('i18nextLng')");
		storedLanguage.Should().Be("en");
	}
}
