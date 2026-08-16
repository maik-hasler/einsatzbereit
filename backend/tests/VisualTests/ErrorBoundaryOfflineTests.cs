using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1955. Distinct from #1774's OfflineStateTests, which cover
/// a route whose *data fetch* fails while offline - both there and here the
/// route's own chunk was already warmed first. Every page is lazy-loaded
/// (App.tsx's per-route <c>React.lazy</c>), so client-side navigation to a
/// route whose JS chunk was never fetched fails its dynamic <c>import()</c>
/// outright when offline: a plain <c>TypeError</c> caught only by the
/// top-level ErrorBoundary, which used to render the generic "Something went
/// wrong" screen - no acknowledgement the cause was connectivity, and no path
/// back except a reload that would fail again while still offline.
/// ErrorBoundary now recognizes that error shape (see
/// <c>lib/dynamicImportError.ts</c>) and renders the same dedicated offline
/// state every other offline-aware surface in the app already uses.
///
/// Deliberately does NOT warm the target route first - the opposite of
/// <see cref="VisualTestBase.WarmOpportunitiesRouteThenLeaveAsync"/> - since
/// the chunk must never have been fetched for its <c>import()</c> to fail at
/// all. Reaches <c>/help</c> by clicking the header's nav link rather than
/// <c>GotoAsync</c> while offline, for the same reason OfflineStateTests
/// does: this suite blocks service workers (see
/// <see cref="VisualTestBase.ContextOptions"/>), so an offline document
/// navigation could not load the app shell in the first place.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ErrorBoundaryOfflineTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task NavigatingToUncachedRouteWhileOffline_ShowsOfflineState_NotGenericErrorBoundary()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(origin);
		await Expect(Page.GetByTestId("nav-help")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Context.SetOfflineAsync(true);
		try
		{
			await Page.GetByTestId("nav-help").ClickAsync();

			await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are offline" }))
				.ToBeVisibleAsync(new() { Timeout = 20_000 });

			// The two halves of the old behaviour, both gone: the generic crash
			// screen, and a reload button that could not possibly succeed while
			// still offline.
			await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Something went wrong" }))
				.Not.ToBeVisibleAsync();
			await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Reload" }))
				.Not.ToBeVisibleAsync();
		}
		finally
		{
			await Context.SetOfflineAsync(false);
		}
	}

	[Test]
	public async Task UncachedRouteFailsWhileOffline_WhenTheConnectionReturns_LoadsWithoutBeingAsked()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(origin);
		await Expect(Page.GetByTestId("nav-help")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Context.SetOfflineAsync(true);
		try
		{
			await Page.GetByTestId("nav-help").ClickAsync();
			await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are offline" }))
				.ToBeVisibleAsync(new() { Timeout = 20_000 });
		}
		finally
		{
			await Context.SetOfflineAsync(false);
		}

		// No click needed: the `online` event alone drives the recovery - a real
		// reload, not just clearing the caught error, since React.lazy() caches
		// a rejected import() for the page's lifetime (see ErrorBoundary.tsx).
		// Same edge-triggered pattern useLoadMore already relies on for a failed
		// data fetch (see OfflineStateTests), just with a reload as the specific
		// recovery action instead of a refetch.
		// Exact: HelpPage's own "Still need help?" contact heading also matches
		// a plain (substring) "Help" filter.
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Help", Exact = true }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });
	}
}
