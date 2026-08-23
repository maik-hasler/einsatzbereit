using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

// Stays E2E (#2162, correcting #2159's classification): asserts locale stays
// deterministic across repeated real hard navigations (full page loads, not
// client-side routing). The risk under test is a fresh-boot i18next
// detection race, which only exists across a real reload cycle - the RTL
// harness's `renderWithProviders` builds one already-initialized i18n
// instance per render and never re-runs that boot sequence.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class InitialLocaleResolutionTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task Navigation_InitialLocale_StaysDeterministicAcrossFullPageLoads()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

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
