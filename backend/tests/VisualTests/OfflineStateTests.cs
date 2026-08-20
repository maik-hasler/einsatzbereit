using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// The one offline case that still needs a real browser: recovery driven by
/// the browser's own <c>online</c> event, after the whole context's
/// connection was genuinely dropped.
///
/// Regression for #1774 (finding F33). Nothing in <c>frontend/src</c> was
/// offline-aware: a grep for <c>navigator.onLine</c> or for online/offline
/// event listeners returned zero hits. The service worker precaches the app
/// shell, so losing the connection brought back the header, hero, every filter
/// chip and the footer - and then threw all of that away by rendering "An
/// unexpected error occurred. Please try again later." in the content area,
/// next to a "Retry" button that could not possibly succeed while the
/// connection was down.
///
/// The three cases that asserted the *rendered* offline state - the notice
/// and its live region (#1774), the navigator.onLine-misreports-true branch
/// (#1901) and the manual "Try again" fallback (#2065) - moved to
/// <c>frontend/src/components/VolunteerOpportunitiesList/VolunteerOpportunitiesList.test.tsx</c>
/// in einsatzbereit#2148: each of them simulated the failure by aborting one
/// request, which is a rejected promise anywhere.
///
/// This one reaches <c>/opportunities</c> by clicking through the header nav
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
		// positive proof instead: a rendered card. The shared test session has
		// seed data plus dozens of other classes' seeded opportunities, so an
		// unfiltered list rendering one is what a genuinely successful refetch
		// looks like (same selector OpportunityCardContractTests uses for the
		// same reason).
		await Expect(Page.GetByTestId("opportunity-date-line").First)
			.ToBeVisibleAsync(new() { Timeout = 20_000 });
	}
}
