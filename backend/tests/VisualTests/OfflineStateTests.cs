using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1774 (finding F33). Nothing in <c>frontend/src</c> was
/// offline-aware: a grep for <c>navigator.onLine</c> or for online/offline
/// event listeners returned zero hits. The service worker precaches the app
/// shell, so losing the connection brought back the header, hero, every filter
/// chip and the footer - and then threw all of that away by rendering "An
/// unexpected error occurred. Please try again later." in the content area,
/// next to a "Retry" button that could not possibly succeed while the
/// connection was down.
///
/// The list now says it is offline and refetches by itself the moment the
/// connection is back. #2065 added a manual "Try again" fallback on top of
/// that: the automatic recovery depends on the browser firing an `online`
/// event, which some captive portals and mobile networks never do even once
/// the connection is genuinely back, and a manual retry that re-issues the
/// same request can still succeed in exactly that case.
///
/// Both tests reach <c>/opportunities</c> by clicking through the header nav
/// rather than by <c>GotoAsync</c> while offline. That is deliberate on two
/// counts: this suite blocks service workers (see
/// <c>VisualTestBase.ContextOptions</c>), so an offline document navigation
/// could not load the app shell at all here, and the route's page component is
/// lazy-loaded (see <c>App.tsx</c>) - visiting it once while still online is
/// what puts its chunk in the module registry, so the offline visit exercises
/// the data fetch rather than a chunk-load failure.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OfflineStateTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OpportunityList_WhileOffline_SaysSoAndOffersAManualRetryFallback()
	{
		var origin = await WarmOpportunitiesRouteThenLeaveAsync();

		await Context.SetOfflineAsync(true);
		try
		{
			await GoToOpportunitiesAsync(origin);

			var offline = Page.GetByTestId("opportunities-offline");
			await Expect(offline).ToBeVisibleAsync(new() { Timeout = 20_000 });
			await Expect(offline).ToContainTextAsync("You are offline");

			// The old generic server-error wording is gone. #2065: unlike the
			// original #1774 design, a single "Try again" button is now offered
			// alongside the offline notice - the fallback for a connection that
			// came back without the browser ever firing an `online` event.
			// Scoped to the offline block rather than page-wide, so this can't
			// trip Playwright's strict mode on some unrelated control elsewhere
			// in the chrome.
			await Expect(Page.GetByTestId("opportunities-error")).Not.ToBeVisibleAsync();
			await Expect(offline.GetByRole(AriaRole.Button, new() { Name = "Try again" }))
				.ToHaveCountAsync(1);

			// The announcement has to come from the list's own always-mounted
			// sr-only region, not from a region inside the notice above: a
			// role="status" node inserted into the DOM already populated does not
			// reliably announce (this repo has hit that three times - see
			// CheckInModal and ToastContext). That region is empty before the
			// connection drops and is written into here, which is what makes the
			// offline state audible rather than only visible.
			await Expect(Page.GetByTestId("opportunities-result-count"))
				.ToHaveTextAsync(new Regex("You are offline"));
			await Expect(offline.Locator("[role='status'], [role='alert']")).ToHaveCountAsync(0);
		}
		finally
		{
			// Restored even on failure - the browser context is per-test, but
			// leaving it offline would fail the teardown trace write for an
			// unrelated reason and bury the real assertion error.
			await Context.SetOfflineAsync(false);
		}
	}

	/// <summary>
	/// Regression for #1901: OpportunityResultsList used to decide offline-vs-
	/// generic-error purely from <c>useOnlineStatus()</c> (<c>navigator.onLine</c>).
	/// That flag is only reliable when it reads false - true only means the
	/// browser has a network interface up, and a well-documented cross-browser
	/// quirk lets it keep reading true across a hard reload or cold PWA launch
	/// even while genuinely offline. A visitor in that state used to see the
	/// generic "An unexpected error occurred" screen with a "Retry" button that
	/// could not possibly succeed, instead of the dedicated offline state.
	///
	/// Reproduced with a real document navigation (<c>GotoAsync</c>, not the
	/// header-nav click <see cref="WarmOpportunitiesRouteThenLeaveAsync"/> uses)
	/// since only the one list request is aborted here, not the whole context
	/// (<c>Context.SetOfflineAsync</c>) - the app shell's own JS/CSS still load
	/// over the network same as a real hard reload, with no service worker
	/// needed to bring back a precached shell.
	/// </summary>
	[Test]
	public async Task OpportunityList_HardReloadWhileNavigatorOnLineMisreportsTrue_StillShowsOfflineState()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.AddInitScriptAsync(
			"Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => true });");
		// Single asterisk deliberately: it does not cross a `/`, so this only
		// matches the list endpoint's own query string, not
		// /volunteer-opportunities/{id} or /volunteer-opportunities/date-availability.
		await Page.RouteAsync("**/v1/volunteer-opportunities*", route =>
			route.AbortAsync("internetdisconnected"));

		await Page.GotoAsync($"{origin}/opportunities");

		var offline = Page.GetByTestId("opportunities-offline");
		await Expect(offline).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(offline).ToContainTextAsync("You are offline");

		await Expect(Page.GetByTestId("opportunities-error")).Not.ToBeVisibleAsync();
		await Expect(offline.GetByRole(AriaRole.Button, new() { Name = "Try again" }))
			.ToHaveCountAsync(1);
	}

	/// <summary>
	/// Regression for #2065's core scenario: a connection that came back
	/// without the browser ever firing an <c>online</c> event (a captive
	/// portal, some mobile networks). No <c>Context.SetOfflineAsync</c> and no
	/// <c>online</c> DOM event anywhere in this test - recovery has to come
	/// from the click alone, proving the manual retry does not depend on the
	/// event-driven path the other tests in this class exercise.
	/// </summary>
	[Test]
	public async Task OpportunityList_ManualRetry_SucceedsWithoutAnOnlineEvent()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var shouldFail = true;
		await Page.RouteAsync("**/v1/volunteer-opportunities*", async route =>
		{
			if (!shouldFail)
			{
				await route.ContinueAsync();
				return;
			}
			await route.AbortAsync("internetdisconnected");
		});

		await Page.GotoAsync($"{origin}/opportunities");

		var offline = Page.GetByTestId("opportunities-offline");
		await Expect(offline).ToBeVisibleAsync(new() { Timeout = 20_000 });

		shouldFail = false;
		await offline.GetByRole(AriaRole.Button, new() { Name = "Try again" }).ClickAsync();

		await Expect(offline).Not.ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(Page.GetByTestId("opportunities-result-count"))
			.ToHaveTextAsync(new Regex(@"opportunit(y|ies)"), new() { Timeout = 20_000 });
	}

	[Test]
	public async Task OpportunityList_WhenTheConnectionReturns_RefetchesWithoutBeingAsked()
	{
		var origin = await WarmOpportunitiesRouteThenLeaveAsync();

		await Context.SetOfflineAsync(true);
		try
		{
			await GoToOpportunitiesAsync(origin);
			await Expect(Page.GetByTestId("opportunities-offline"))
				.ToBeVisibleAsync(new() { Timeout = 20_000 });
		}
		finally
		{
			await Context.SetOfflineAsync(false);
		}

		// No click, no reload: the `online` event alone drives the recovery here
		// - the manual "Try again" button #2065 added is a fallback for when
		// that event never fires, not a replacement for it.
		await Expect(Page.GetByTestId("opportunities-offline"))
			.Not.ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(Page.GetByTestId("opportunities-error"))
			.Not.ToBeVisibleAsync(new() { Timeout = 20_000 });

		// Both of the above would also hold for a single frame the instant
		// `online` flipped, before any refetch had happened - so settle on
		// positive proof instead. OpportunityResultsList's result-count live
		// region carries a count only once a fetch has completed with no error
		// (while failed it holds the offline text, and while loading it is
		// empty), so a count here means the refetch really ran and succeeded.
		await Expect(Page.GetByTestId("opportunities-result-count"))
			.ToHaveTextAsync(new Regex(@"opportunit(y|ies)"), new() { Timeout = 20_000 });
	}

}
