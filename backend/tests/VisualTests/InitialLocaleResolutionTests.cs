using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

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
