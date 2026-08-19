using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #2065 (finding 1): LatestOpportunitiesSection removed itself
/// on any failure, offline included - reasonable for a generic error (arguing
/// against the hero directly above it), but wrong for offline, where every
/// other offline-aware surface in the app (starting with #1774's
/// /opportunities list) says so instead of silently vanishing. A visitor
/// reloading the landing page with no connection used to see the hero promise
/// "find an opportunity that fits you" and then nothing backing it up at all.
///
/// Simulated by pinning <c>navigator.onLine</c> false and aborting just the
/// list request rather than by <c>Context.SetOfflineAsync</c>, because this
/// suite blocks service workers (see <see cref="VisualTestBase.ContextOptions"/>),
/// so a genuinely offline document navigation could not load the app shell at
/// all - the same technique <c>OrgAppLayoutErrorStatesTests</c> uses for the
/// org shell.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HomePageOfflineTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task HomePage_WhileOffline_ShowsOfflineNoticeInsteadOfHidingTheSection()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.AddInitScriptAsync(
			"Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => false });");
		// Single asterisk deliberately: it does not cross a `/`, so this only
		// matches the list endpoint's own query string, not
		// /volunteer-opportunities/{id} (see OfflineStateTests for the same note).
		await Page.RouteAsync("**/v1/volunteer-opportunities*", route =>
			route.AbortAsync("internetdisconnected"));

		await Page.GotoAsync(origin);

		// The section itself (heading + "Browse all opportunities" link) still
		// renders - only the grid of cards is replaced by the offline notice.
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "These opportunities need people" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		var offline = Page.GetByTestId("landing-latest-offline");
		await Expect(offline).ToBeVisibleAsync();
		await Expect(offline).ToContainTextAsync("You are offline");

		// #2065 added a manual retry alongside the offline notice - the
		// fallback for a connection that comes back without the browser ever
		// firing an `online` event, same as every other offline surface now
		// offers.
		await Expect(offline.GetByRole(AriaRole.Button, new() { Name = "Try again" }))
			.ToBeVisibleAsync();

		// The section degrades in place - it does not take the rest of the
		// landing page down with it, and the "no evidence there is anything to
		// find" hole this regression closes is specifically about this
		// section, not the whole page.
		await Expect(Page.GetByTestId("hero-keyword-input")).ToBeVisibleAsync();
	}

	[Test]
	public async Task HomePage_WhenNoOpportunitiesButOnline_StillHidesTheSection()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// A generic (non-offline) failure is deliberately left alone by #2065 -
		// only the offline branch changed. Aborting with a plain HTTP 500
		// response (not a transport-level "internetdisconnected") keeps
		// `navigator.onLine` true and gives the request a real HTTP status, so
		// useLoadMore's errorIsOffline reads false and the section still
		// removes itself rather than arguing against the hero above it.
		await Page.RouteAsync("**/v1/volunteer-opportunities*", async route =>
		{
			await route.FulfillAsync(new()
			{
				Status = 500,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.6.1\",\"status\":500}",
			});
		});

		await Page.GotoAsync(origin);

		await Expect(Page.GetByTestId("hero-keyword-input")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByTestId("landing-latest-offline")).Not.ToBeVisibleAsync();
		await Expect(Page.GetByText("These opportunities need people")).Not.ToBeVisibleAsync();
	}
}
