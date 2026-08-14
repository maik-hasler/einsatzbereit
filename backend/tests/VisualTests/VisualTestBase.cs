using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace VisualTests;

/// <summary>
/// Base class for all VisualTests. Disables W3C trace-context propagation
/// (<see cref="PropagateTraceContext"/>) rather than stripping <c>traceparent</c>
/// via a page route - Keycloak's CORS preflight does not allow it in
/// <c>Access-Control-Allow-Headers</c>, which would cause oidc-client-ts discovery
/// fetches to fail silently.
///
/// Also seeds a per-test unique <c>X-Forwarded-For</c> IP via <c>ExtraHTTPHeaders</c>
/// on every request. Parallel VisualTests all originate from 127.0.0.1, which would
/// otherwise share a single anonymous rate-limit bucket - React StrictMode
/// double-invokes effects in dev mode, and ~17 tests navigate the home page
/// concurrently, easily exhausting a shared quota and producing 429s. AppHost.cs
/// separately bumps that bucket to 10000 req/60s for VisualTests (see its own
/// comment), which alone is enough headroom for this whole suite - the per-test
/// IP partitioning here is kept as a second, independent layer on top of that
/// bump: it predates it, costs nothing, and means this suite doesn't silently
/// regress to 429s if that bump is ever narrowed or reverted. Still honored after
/// #1332 hardened the production rate limiter against X-Forwarded-For spoofing -
/// this backend process is only ever reached over loopback here, which
/// TrustedNetworksOptions deliberately keeps trusted.
///
/// This header hits the exact same Keycloak CORS wall as <c>traceparent</c> above -
/// Keycloak doesn't allow it in <c>Access-Control-Allow-Headers</c> either, so any
/// test whose browser crosses into Keycloak (a real login, or an anonymous
/// registration/auth redirect) must call
/// <see cref="AuthHelper.AllowKeycloakCrossOriginRequestsAsync"/> first to strip it
/// from just those requests - see that method's doc comment for why this is a
/// page-level route rather than widening this disabled-cache trade-off to every test.
///
/// Neither of the above uses a <c>Context.RouteAsync("**/*", ...)</c> handler like
/// they used to - enabling routing disables the Vite dev server's HTTP cache for
/// every request across all 209 tests, and a page-level <c>Page.RouteAsync</c> (10
/// call sites in this suite) takes precedence over a context-level route and calls
/// <c>ContinueAsync</c> straight to the network, silently bypassing whatever a
/// context handler would have done. <c>ExtraHTTPHeaders</c>/<see cref="PropagateTraceContext"/>
/// apply unconditionally at the context level with no such shadowing risk.
/// </summary>
public abstract class VisualTestBase(AspireFixture fixture) : PageTest
{
	public AspireFixture Fixture => fixture;

	private static int _testIpSequence;
	private bool _tracingStarted;

	public override bool PropagateTraceContext => false;

	// global.css's staggered .animate-fade-up-* entrance animations (opacity
	// 0 -> 1 over ~0.5s, some with an extra ~0.5s delay first) run for real in
	// the Playwright browser. On a busy/contended CI runner, an axe-core scan
	// can land while a still-fading element is at partial opacity, and the
	// alpha-blended colour axe reads at that instant can compute a lower
	// contrast ratio than the fully-settled one - a spurious a11y failure
	// with nothing wrong in the rendered UI (e.g.
	// HomePage_HasNoSeriousA11yViolations flagging the stats row's
	// text-brand-200 labels, which sit inside .animate-fade-up-d4). Disabling
	// motion for the whole context removes the animation - and this race -
	// entirely, the same way a real prefers-reduced-motion user would see the
	// page, rather than trying to time scans around a transition.
	public override BrowserNewContextOptions ContextOptions(TestContext testContext)
	{
		var n = Interlocked.Increment(ref _testIpSequence);
		var uniqueTestIp = $"10.{(n >> 8) & 0xFF}.{n & 0xFF}.1";

		return new()
		{
			ReducedMotion = ReducedMotion.Reduce,
			ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Forwarded-For"] = uniqueTestIp },
			// The dialog/traceparent-stripping route this used to require is gone
			// (see class doc); nothing in this suite intentionally exercises a
			// service worker, so block registration rather than let one silently
			// intercept requests a test expects to observe/mock.
			ServiceWorkers = ServiceWorkerPolicy.Block,
		};
	}

	[Before(Test)]
	public async Task SetupVisualTest()
	{
		await fixture.WaitForResourceAsync("frontend");
		// Sources omitted: the trace viewer's source-file pane is a nice-to-have
		// over a repo any debugger already has checked out, not worth its CPU/
		// disk cost across all 209 tests on top of Screenshots+Snapshots - this
		// runs before every test, whether or not its trace ends up kept.
		await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
		_tracingStarted = true;
	}

	// Playwright traces are the only reliable way this suite has ever had to
	// diagnose a flake after the fact instead of guessing - only keep them for
	// failures, so CI isn't uploading a multi-MB zip per test session.
	[After(Test)]
	public async Task TeardownTracingAsync(TestContext testContext)
	{
		// If WaitForResourceAsync above threw before Tracing.StartAsync ran,
		// there's nothing to stop - calling StopAsync anyway would throw a
		// second, unrelated error that masks the real one in the test output.
		if (!_tracingStarted)
			return;

		if (testContext.Execution.Result?.State == TestState.Failed)
		{
			var traceDir = Path.Combine(AppContext.BaseDirectory, "trace-artifacts");
			Directory.CreateDirectory(traceDir);
			var traceName = string.Join('_', testContext.Metadata.TestName.Split(Path.GetInvalidFileNameChars()));
			await Context.Tracing.StopAsync(new() { Path = Path.Combine(traceDir, $"{traceName}.zip") });
		}
		else
		{
			await Context.Tracing.StopAsync();
		}
	}

	/// <summary>
	/// Polls <paramref name="predicate"/> (each call a single round trip - put
	/// everything the predicate needs to read in one <c>EvaluateAsync</c> call
	/// rather than several, so nothing else can change layout/state between
	/// two samples that are supposed to describe the same instant) until it
	/// returns true or <paramref name="timeoutMs"/> elapses. Use this for
	/// assertions Playwright's own auto-waiting <c>Expect</c> can't express
	/// directly - geometry/computed-style comparisons, mostly - instead of a
	/// single un-awaited read.
	///
	/// <paramref name="timeoutMessage"/> is a factory, not a plain string, so
	/// it can read whatever local variable the predicate last wrote (the most
	/// recently observed value) at throw-time - a fixed message written
	/// before the loop runs could only ever describe the first attempt. There
	/// is deliberately no separate assertion after this call succeeds:
	/// on success the condition already holds, and on failure this throws
	/// with the message instead - a trailing `.Should()` would be dead code
	/// in the first case and unreachable in the second.
	/// </summary>
	protected static async Task PollUntilAsync(
		Func<Task<bool>> predicate, Func<string> timeoutMessage, int timeoutMs = 5000)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (true)
		{
			if (await predicate())
				return;
			if (DateTime.UtcNow >= deadline)
				throw new TimeoutException(timeoutMessage());
			await Task.Delay(100);
		}
	}

	/// <summary>
	/// POSTs <paramref name="body"/> as JSON to <paramref name="requestUri"/> and
	/// retries a handful of times with a short backoff if the response is a
	/// transient 5xx, before returning whatever the final attempt returned.
	/// Mirrors AspireFixture's PostTokenRequestWithRetryAsync, applied to the
	/// same class of problem one level up: creating a test organization (POST
	/// /v1/organizations) calls out to Keycloak's admin API in turn (create
	/// organization, add member, assign the organisator role), and under this
	/// suite's sustained concurrent load that chain can trip the same
	/// resilience-pipeline rejection/timeout #1709 traces GetMembersAsync's
	/// admin-API calls back to - surfacing as a 500 from our own backend that
	/// is not attributable to the request this suite sent, and that a bare
	/// re-run of just the failing test does not reproduce. Retrying the exact
	/// same request is safe here in practice: the dominant failure mode is the
	/// resilience pipeline rejecting or timing out the Keycloak call before any
	/// organization exists, so there is nothing for a retry to collide with -
	/// and on the rarer case where Keycloak's side did commit, the retry surfaces
	/// as a 409 (never retried below) rather than silently duplicating anything.
	/// Never retries a 4xx - that's a real failure, not a blip.
	/// </summary>
	protected static async Task<HttpResponseMessage> PostJsonWithRetryAsync(
		HttpClient client, string requestUri, object body, CancellationToken cancellationToken = default)
	{
		const int maxAttempts = 3;
		HttpResponseMessage response;
		for (var attempt = 1; ; attempt++)
		{
			response = await client.PostAsJsonAsync(requestUri, body, cancellationToken);
			if (response.StatusCode < HttpStatusCode.InternalServerError || attempt >= maxAttempts)
				break;

			response.Dispose();
			await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
		}

		return response;
	}

	/// <summary>
	/// Clicks "Load more" inside <paramref name="listSelector"/> until
	/// <paramref name="target"/> is visible, the list is fully loaded, or
	/// <paramref name="timeoutSeconds"/> elapses.
	///
	/// Exists because the seeded vera/olaf accounts accumulate unbounded state
	/// across a shared session: ~15 classes leave vera engagements they never
	/// withdraw, and EngagementReadRepository.GetByVolunteerAsync orders the
	/// "Current &amp; upcoming" scope by time-slot start with slot-less entries
	/// last, tie-broken by the UUIDv7 engagement id. A test's own freshly
	/// created IndividualContact engagement is therefore deterministically the
	/// LAST row of the whole list, several 10-row pages down, rather than
	/// anywhere near the top.
	///
	/// Three details make this reliable, and the first two were wrong in the
	/// hand-rolled copies this replaces:
	/// <list type="bullet">
	/// <item>The button is found by <c>data-testid</c>, never by accessible
	/// name. LoadMoreButton renders <c>{loading ? loadingLabel : label}</c> on
	/// the same element, so a name-based locator matches zero elements while a
	/// page is in flight and a non-waiting <c>IsVisibleAsync</c> guard reads
	/// false mid-load, ending the walk after a single click.</item>
	/// <item>A <c>WaitForLoadStateAsync(NetworkIdle)</c> between clicks does not
	/// straddle the fetch at all, since useLoadMore only issues it from an
	/// effect after React commits the page increment - it can return before the
	/// request has even been made.</item>
	/// <item>Each iteration waits for the in-flight page to land <em>before</em>
	/// clicking again, rather than leaning on <c>ClickAsync</c>'s
	/// auto-wait-for-enabled to absorb it. That reliance is what made this
	/// flaky (einsatzbereit CI run 31155273854, main): the button is
	/// <c>disabled={loadingMore}</c> while a page loads, but when the
	/// <em>last</em> page lands useLoadMore flips <c>hasMore</c> false and
	/// ActivitySection unmounts the button entirely. A click issued during that
	/// final load is therefore waiting on a button that gets detached rather
	/// than re-enabled, and Playwright's detached-element retry then burns its
	/// full 30s action timeout on a locator that will never resolve again -
	/// "element was detached from the DOM, retrying".</item>
	/// </list>
	/// </summary>
	protected async Task LoadMoreUntilVisibleAsync(
		ILocator target, string listSelector = "#activity", int timeoutSeconds = 60)
	{
		var loadMoreButton = Page.Locator($"{listSelector} [data-testid='load-more']");
		var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
		var clickedAtElementCount = -1;
		var commitDeadline = DateTimeOffset.MinValue;

		while (DateTimeOffset.UtcNow < deadline)
		{
			if (await target.IsVisibleAsync())
				return;

			var (state, elementCount) = await ReadLoadMoreStateAsync(listSelector);

			// Nothing left to page through. Return instead of stalling until the
			// deadline, so the caller's own Expect reports the real problem (the
			// row genuinely isn't in this list) rather than this helper eating
			// the whole budget first.
			if (state == LoadMoreState.Gone)
				return;

			// A page is in flight - the button is still mounted but
			// disabled={loadingMore}. Waiting this out instead of clicking
			// through it is the actual fix: a second click during the *final*
			// load waits on a button useLoadMore is about to unmount rather
			// than re-enable (see this method's doc).
			if (state == LoadMoreState.Loading)
			{
				await Task.Delay(100);
				continue;
			}

			// Enabled, but nothing has changed since our last click - React
			// re-renders a tick after ClickAsync returns, so this is most likely
			// "your click hasn't been committed yet" rather than "the page
			// landed". Clicking again here would double-advance `page`, and the
			// superseded fetch's cleanup drops a whole page of rows silently -
			// possibly the one holding the target.
			//
			// Bounded rather than waited out indefinitely, because an unchanged
			// list is not proof of an uncommitted click: a page that legitimately
			// lands with no new rows (this suite pages over live data that
			// sibling tests concurrently withdraw from) looks identical. After
			// the grace period, click again and make progress.
			if (elementCount == clickedAtElementCount && DateTimeOffset.UtcNow < commitDeadline)
			{
				await Task.Delay(100);
				continue;
			}

			// Bound the click by what is left of the caller's budget, so a stuck
			// click cannot overrun timeoutSeconds by Playwright's own separate
			// 30s default action timeout.
			var remainingMs = (deadline - DateTimeOffset.UtcNow).TotalMilliseconds;
			if (remainingMs <= 0)
				return;

			clickedAtElementCount = elementCount;
			try
			{
				await loadMoreButton.ClickAsync(new() { Timeout = (float)remainingMs });
			}
			// Both types are needed, and neither alone is enough. There is no
			// Microsoft.Playwright.TimeoutException in this version (CS0234 if
			// you try) - Playwright raises a *System*.TimeoutException for
			// "Timeout Nms exceeded", which does not derive from
			// PlaywrightException, so catching only the latter would miss the
			// timeout this clause primarily exists for. PlaywrightException
			// still covers the non-timeout action failures (a button detached
			// between the state read above and this click).
			catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
			{
				// The button was unmounted between the read above and the click,
				// or the budget ran out mid-click. Either way there is nothing
				// more to page through - same as Gone above.
				return;
			}

			// Started only once the click has actually been dispatched. ClickAsync
			// can block for a while first (waiting out actionability on a list
			// that is still settling), and a grace period started before that
			// wait could be spent before React has had a single tick to commit.
			commitDeadline = DateTimeOffset.UtcNow.AddSeconds(2);
		}
	}

	/// <summary>
	/// Visits /opportunities once - so its lazy route chunk (see App.tsx) lands
	/// in the module registry - and then returns to the home page by clicking
	/// the header nav, leaving the SPA loaded. Returns the frontend origin.
	///
	/// Pair with <see cref="GoToOpportunitiesAsync"/> to reach the list again
	/// with the browser context offline (#1774). Both halves are needed: this
	/// suite blocks service workers (see <see cref="ContextOptions"/>), so an
	/// offline <c>GotoAsync</c> could not load the app shell at all, and a
	/// route chunk not yet fetched would fail to load - which would exercise a
	/// chunk-load failure rather than the offline state under test.
	/// </summary>
	protected async Task<string> WarmOpportunitiesRouteThenLeaveAsync()
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

	/// <summary>
	/// Client-side navigation to /opportunities via the header nav - no
	/// document load, so it works with the context offline. See
	/// <see cref="WarmOpportunitiesRouteThenLeaveAsync"/>.
	/// </summary>
	protected async Task GoToOpportunitiesAsync(string origin)
	{
		await Page.GetByTestId("nav-findOpportunities").ClickAsync();
		await Page.WaitForURLAsync($"{origin}/opportunities", new() { Timeout = 15_000 });
	}

	private enum LoadMoreState
	{
		/// <summary>Mounted, rendered and enabled - safe to click.</summary>
		Ready,

		/// <summary>Mounted but <c>disabled={loadingMore}</c> - a page is in flight.</summary>
		Loading,

		/// <summary>Unmounted or not rendered - <c>hasMore</c> is false, the list is fully loaded.</summary>
		Gone,
	}

	/// <summary>
	/// Reads the load-more button's state and the list's current element count
	/// in a single round trip, for <see cref="LoadMoreUntilVisibleAsync"/>.
	///
	/// Deliberately not separate <c>CountAsync</c>/<c>IsVisibleAsync</c>/
	/// <c>IsEnabledAsync</c> locator calls: the button can unmount between any
	/// two of them - the exact race that method exists to avoid - and
	/// <c>IsEnabledAsync</c> throws once it has.
	///
	/// The element count is a progress signal, not an assertion: it only has to
	/// grow when a page of rows is appended, so it counts every descendant
	/// rather than assuming a row tag. A text-only change (the button's own
	/// label flipping to its loading state) leaves it untouched, which is what
	/// makes "unchanged count + enabled button" a reliable read of "React has
	/// not committed our click yet".
	/// </summary>
	private async Task<(LoadMoreState State, int ElementCount)> ReadLoadMoreStateAsync(string listSelector)
	{
		var snapshot = (await Page.EvaluateAsync(
			"""
			({ list }) => {
				const el = document.querySelector(`${list} [data-testid='load-more']`);
				// getClientRects() is empty for a display:none element, or one
				// inside a collapsed ancestor. The IsVisibleAsync() guard this
				// read replaces treated that the same as absent, and so does
				// 'gone' here.
				const state = !el || el.getClientRects().length === 0
					? 'gone'
					: el.disabled ? 'loading' : 'ready';
				return { state, elementCount: document.querySelectorAll(`${list} *`).length };
			}
			""",
			new { list = listSelector }))!.Value;

		var elementCount = snapshot.GetProperty("elementCount").GetInt32();
		var state = snapshot.GetProperty("state").GetString() switch
		{
			"ready" => LoadMoreState.Ready,
			"loading" => LoadMoreState.Loading,
			_ => LoadMoreState.Gone,
		};

		return (state, elementCount);
	}

	/// <summary>
	/// Asserts the page's content wrapper inside `&lt;main&gt;` (marked with the
	/// `data-content-wrapper` attribute - see #1328, this used to select on the
	/// `.max-w-2xl` Tailwind utility class directly, which a purely cosmetic
	/// class rename would silently break) has equal left/right whitespace,
	/// i.e. it is horizontally centered via `mx-auto` rather than left-aligned.
	/// See #694.
	/// </summary>
	protected async Task AssertMaxWidthContentCenteredAsync(string label)
	{
		var main = Page.Locator("main");
		await Expect(main).ToBeVisibleAsync();
		var container = main.Locator("[data-content-wrapper]").First;
		await Expect(container).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Both boxes read in a single EvaluateAsync call rather than two
		// separate BoundingBoxAsync round trips, so nothing can shift layout
		// between reading <main>'s box and the container's box.
		var gapDelta = 0d;
		await PollUntilAsync(async () =>
		{
			gapDelta = await container.EvaluateAsync<double>(
				"""
				el => {
					const mainBox = el.closest('main').getBoundingClientRect();
					const box = el.getBoundingClientRect();
					const leftGap = box.left - mainBox.left;
					const rightGap = (mainBox.left + mainBox.width) - (box.left + box.width);
					return Math.abs(leftGap - rightGap);
				}
				""");
			return gapDelta < 2;
		}, () => $"{label}: content wrapper should be horizontally centered within <main> "
			+ $"(last observed |leftGap - rightGap| = {gapDelta}px, must be <2px)");
	}

	/// <summary>
	/// Asserts the page's content wrapper inside `&lt;main&gt;` (marked with the
	/// `data-content-wrapper` attribute - see #1328) sits flush against the
	/// left edge, i.e. it is left-aligned rather than centered via `mx-auto`.
	/// See #766.
	/// </summary>
	protected async Task AssertMaxWidthContentLeftAlignedAsync(string label)
	{
		var main = Page.Locator("main");
		await Expect(main).ToBeVisibleAsync();
		var container = main.Locator("[data-content-wrapper]").First;
		await Expect(container).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Single EvaluateAsync call - see AssertMaxWidthContentCenteredAsync.
		// Nets out <main>'s own (symmetric) padding so a left-aligned child is
		// expected to sit at <main>'s padded content edge, not at x=0.
		var leftGap = 0d;
		await PollUntilAsync(async () =>
		{
			leftGap = await container.EvaluateAsync<double>(
				"""
				el => {
					const mainEl = el.closest('main');
					const mainBox = mainEl.getBoundingClientRect();
					const box = el.getBoundingClientRect();
					const mainPaddingLeft = parseFloat(getComputedStyle(mainEl).paddingLeft);
					return box.left - mainBox.left - mainPaddingLeft;
				}
				""");
			return Math.Abs(leftGap) < 2;
		}, () => $"{label}: content wrapper should sit flush against <main>'s left padding edge, "
			+ $"not be centered (last observed gap = {leftGap}px, must be <2px)");
	}

	/// <summary>
	/// Asserts <paramref name="lower"/> sits visibly below <paramref name="upper"/>
	/// with a non-trivial gap, not flush against it - i.e. the two locators are
	/// separated by real spacing rather than stacked with 0px between them.
	/// </summary>
	protected async Task AssertVerticalGapBetweenAsync(ILocator upper, ILocator lower, string label)
	{
		await Expect(upper).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(lower).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Both boxes read via PollUntilAsync (not a single read) - see
		// AdminUserManagementTests' Name-cell/Block-button assertion for the same
		// rationale: layout can still be mid-reflow the instant elements become
		// visible, which a single read can catch under CI resource contention.
		var upperBottom = 0f;
		var lowerTop = 0f;
		await PollUntilAsync(async () =>
		{
			var upperBox = await upper.BoundingBoxAsync();
			var lowerBox = await lower.BoundingBoxAsync();
			if (upperBox is null || lowerBox is null)
				return false;

			upperBottom = upperBox.Y + upperBox.Height;
			lowerTop = lowerBox.Y;
			return lowerTop - upperBottom >= 8f;
		}, () => $"{label}: expected a visible gap (>=8px) between blocks, "
			+ $"(last observed: upper bottom {upperBottom:F0}px, lower top {lowerTop:F0}px, "
			+ $"gap {lowerTop - upperBottom:F0}px)");
	}
}
