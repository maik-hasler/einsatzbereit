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
/// The list now says it is offline, offers no action it cannot honour, and
/// refetches by itself the moment the connection is back.
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
	public async Task OpportunityList_WhileOffline_SaysSoInsteadOfOfferingADeadRetry()
	{
		var origin = await WarmOpportunitiesRouteThenLeaveAsync();

		await Context.SetOfflineAsync(true);
		try
		{
			await GoToOpportunitiesAsync(origin);

			var offline = Page.GetByTestId("opportunities-offline");
			await Expect(offline).ToBeVisibleAsync(new() { Timeout = 20_000 });
			await Expect(offline).ToContainTextAsync("You are offline");

			// The two halves of the old behaviour, both gone: the generic
			// server-error wording, and the retry that cannot succeed. The retry
			// is asserted absent inside the offline block rather than page-wide,
			// so this can't trip Playwright's strict mode on some unrelated
			// control elsewhere in the chrome.
			await Expect(Page.GetByTestId("opportunities-error")).Not.ToBeVisibleAsync();
			await Expect(offline.GetByRole(AriaRole.Button)).ToHaveCountAsync(0);

			// The announcement has to come from the list's own always-mounted
			// sr-only region, not from a region inside the notice above: a
			// role="status" node inserted into the DOM already populated does not
			// reliably announce (this repo has hit that three times - see
			// CheckInModal and ToastContext). That region is empty before the
			// connection drops and is written into here, which is what makes the
			// offline state audible rather than only visible.
			await Expect(Page.Locator("#opportunities p[role='status']").First)
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

		// No click, no reload: the `online` event alone has to drive the
		// recovery, because the offline state deliberately offers no action.
		await Expect(Page.GetByTestId("opportunities-offline"))
			.Not.ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(Page.GetByTestId("opportunities-error"))
			.Not.ToBeVisibleAsync(new() { Timeout = 20_000 });

		// Both of the above would also hold for a single frame the instant
		// `online` flipped, before any refetch had happened - so settle on
		// positive proof instead. OpportunityResultsList's sr-only result-count
		// live region is written only once a fetch has completed with no error,
		// and is deliberately empty while loading or failed, so non-empty text
		// here means the refetch really ran and really succeeded.
		await Expect(Page.Locator("#opportunities p[role='status']").First)
			.ToHaveTextAsync(new Regex(@"\S"), new() { Timeout = 20_000 });
	}

	/// <summary>
	/// Visits /opportunities once (loading its lazy chunk) and then navigates
	/// back to the home page, leaving the SPA loaded and the route's chunk
	/// resident. Returns the frontend origin.
	/// </summary>
	private async Task<string> WarmOpportunitiesRouteThenLeaveAsync()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities");
		await Expect(Page.GetByTestId("opportunities-keyword-input"))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		await Page.GetByTestId("nav-home").ClickAsync();
		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 15_000 });

		return origin;
	}

	private async Task GoToOpportunitiesAsync(string origin)
	{
		await Page.GetByTestId("nav-findOpportunities").ClickAsync();
		await Page.WaitForURLAsync($"{origin}/opportunities", new() { Timeout = 15_000 });
	}
}
