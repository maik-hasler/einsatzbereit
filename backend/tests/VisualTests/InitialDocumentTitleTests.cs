using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace VisualTests;

/// <summary>
/// Regression for #1945: <c>index.html</c>'s static, pre-hydration
/// <c>&lt;title&gt;</c> was a full German sentence ("Einsatzbereit - Spontan
/// Freiwilligenarbeit leisten. Finde deinen Einsatz."). Every route's real
/// title is set by <c>usePageTitle</c> only after React mounts, so an
/// English-resolving visitor's browser tab briefly showed that German
/// sentence before hydration replaced it with the correct English per-route
/// title.
///
/// Fetches the raw HTML byte-for-byte via <see cref="HttpClient"/> rather
/// than reading <c>Page.TitleAsync()</c> after a Playwright navigation -
/// <c>usePageTitle</c>'s effect runs on mount, well before the "load" event
/// Playwright's default <c>GotoAsync</c> waits for, so a browser-driven read
/// would already observe the post-hydration title and never exercise the
/// static fallback this bug is actually about.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class InitialDocumentTitleTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task StaticHtml_DocumentTitle_IsLanguageNeutralFallback()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		using var http = new HttpClient { BaseAddress = frontend };
		var html = await http.GetStringAsync("/");

		var titleMatch = Regex.Match(html, "<title>(.*?)</title>", RegexOptions.Singleline);
		titleMatch.Success.Should().BeTrue("index.html should declare a <title> element");

		// The bare app name - the same neutral fallback usePageTitle.ts's
		// APP_NAME resets document.title to once a page unmounts its own
		// title, so the pre- and post-hydration title never disagree on
		// language for a visitor who lands on a route with no title of its
		// own yet.
		titleMatch.Groups[1].Value.Should().Be("Einsatzbereit");
	}
}
