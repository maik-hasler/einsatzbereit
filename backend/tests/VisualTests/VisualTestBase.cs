using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace VisualTests;

/// <summary>
/// Base class for all VisualTests. Sets two context-level things:
/// <c>traceparent</c> off (<see cref="PropagateTraceContext"/>), and a per-test
/// unique <c>X-Forwarded-For</c> IP so parallel tests do not share one anonymous
/// rate-limit bucket and 429 each other. The header is honored despite
/// spoofing hardening because this backend is only reached over loopback, which
/// TrustedNetworksOptions keeps trusted.
///
/// Keycloak's CORS preflight allows neither header, so a browser crossing into
/// Keycloak fails oidc-client-ts discovery silently unless the test calls
/// <see cref="AuthHelper.AllowKeycloakCrossOriginRequestsAsync"/> first.
///
/// Both are set via context options rather than a
/// <c>Context.RouteAsync("**/*", ...)</c> handler: enabling routing disables the
/// Vite dev server's HTTP cache suite-wide, and a page-level
/// <c>Page.RouteAsync</c> takes precedence over a context-level one, continuing
/// straight to the network and silently bypassing it.
/// </summary>
public abstract class VisualTestBase(AspireFixture fixture) : PageTest
{
	public AspireFixture Fixture => fixture;

	private static int _testIpSequence;
	private bool _tracingStarted;

	public override bool PropagateTraceContext => false;

	// global.css's .animate-fade-up-* entrance animations run for real in the
	// Playwright browser. An axe-core scan can land while an element is still
	// fading, and the alpha-blended colour axe reads at that instant computes a
	// lower contrast ratio than the settled one - a spurious a11y failure with
	// nothing wrong in the rendered UI. Disabling motion context-wide removes
	// the race entirely rather than trying to time scans around a transition.
	public override BrowserNewContextOptions ContextOptions(TestContext testContext)
	{
		var n = Interlocked.Increment(ref _testIpSequence);
		var uniqueTestIp = $"10.{(n >> 8) & 0xFF}.{n & 0xFF}.1";

		return new()
		{
			ReducedMotion = ReducedMotion.Reduce,
			ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Forwarded-For"] = uniqueTestIp },
			// Nothing in this suite intentionally exercises a service worker, so
			// block registration rather than let one silently intercept requests a
			// test expects to observe/mock.
			ServiceWorkers = ServiceWorkerPolicy.Block,
		};
	}

	[Before(Test)]
	public async Task SetupVisualTest()
	{
		await fixture.WaitForResourceAsync("frontend");
		// Sources omitted: the trace viewer's source-file pane duplicates a repo
		// any debugger already has checked out, and this runs before every test
		// whether or not its trace is kept.
		await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
		_tracingStarted = true;
	}

	// Traces are the only way to diagnose a flake after the fact, but keeping
	// them for passes would upload a multi-MB zip per test session.
	[After(Test)]
	public async Task TeardownTracingAsync(TestContext testContext)
	{
		// If WaitForResourceAsync threw before Tracing.StartAsync ran, calling
		// StopAsync anyway throws a second error that masks the real one.
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
	/// Polls <paramref name="predicate"/> until true or
	/// <paramref name="timeoutMs"/> elapses, for assertions Playwright's
	/// auto-waiting <c>Expect</c> cannot express (geometry/computed style).
	/// Read everything the predicate needs in one <c>EvaluateAsync</c>, so two
	/// samples cannot straddle a layout change.
	///
	/// <paramref name="timeoutMessage"/> is a factory so it can report what the
	/// predicate last observed; a fixed string could only describe attempt one.
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
	/// POSTs <paramref name="body"/> as JSON, retrying a transient 5xx with a
	/// short backoff. Creating a test organization calls Keycloak's admin API in
	/// turn, which under concurrent load can trip a resilience-pipeline rejection
	/// and surface as a 500 unrelated to this request. Retrying is safe: the
	/// pipeline usually rejects before any organization exists, and if Keycloak
	/// did commit, the retry surfaces as a 409 (never retried). Never retries a
	/// 4xx - that's a real failure, not a blip.
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
	/// A test's own freshly created IndividualContact engagement sorts LAST,
	/// several 10-row pages down: shared accounts accumulate state, and
	/// EngagementReadRepository.GetByVolunteerAsync orders "Current &amp; upcoming"
	/// by slot start with slot-less entries last, tie-broken by UUIDv7 id.
	///
	/// <list type="bullet">
	/// <item>Found by <c>data-testid</c>, never accessible name: LoadMoreButton
	/// renders <c>{loading ? loadingLabel : label}</c> on the same element, so a
	/// name-based locator matches nothing mid-flight.</item>
	/// <item><c>WaitForLoadStateAsync(NetworkIdle)</c> does not straddle the fetch:
	/// useLoadMore issues it from an effect after React commits the page
	/// increment, so it can return before the request is made.</item>
	/// <item>Each iteration waits for the in-flight page before clicking again.
	/// When the last page lands, <c>hasMore</c> flips false and the button
	/// unmounts, so a click during that load waits on a detached element and
	/// burns Playwright's full 30s action timeout.</item>
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

			// A page is in flight. Wait it out rather than clicking through: a
			// second click during the *final* load waits on a button useLoadMore
			// is about to unmount rather than re-enable (see this method's doc).
			if (state == LoadMoreState.Loading)
			{
				await Task.Delay(100);
				continue;
			}

			// Enabled but unchanged since the last click: React re-renders a tick
			// after ClickAsync returns, so the click is probably uncommitted, and
			// clicking again double-advances `page` while the superseded fetch's
			// cleanup silently drops a page of rows. Bounded, not waited out: a page
			// legitimately landing with no new rows looks identical.
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
			// Both types are needed. There is no Microsoft.Playwright.TimeoutException
			// in this version (CS0234 if you try): Playwright raises a
			// *System*.TimeoutException, which does not derive from
			// PlaywrightException. PlaywrightException still covers the non-timeout
			// action failures.
			catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
			{
				// Unmounted between the read and the click, or out of budget - either
				// way there is nothing more to page through, same as Gone above.
				return;
			}

			// Started only once the click has been dispatched: ClickAsync can block
			// first waiting out actionability, and a grace period started before
			// that wait could expire before React has had a tick to commit.
			commitDeadline = DateTimeOffset.UtcNow.AddSeconds(2);
		}
	}

	/// <summary>
	/// Visits /opportunities to land its lazy route chunk in the module registry,
	/// then returns home via the header nav, leaving the SPA loaded. Returns the
	/// frontend origin. Pair with <see cref="GoToOpportunitiesAsync"/> to reach
	/// the list again offline: service workers are blocked (see
	/// <see cref="ContextOptions"/>), so an offline <c>GotoAsync</c> could not
	/// load the app shell, and an unfetched chunk would exercise a chunk-load
	/// failure rather than the offline state.
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
	/// Button state plus the list's element count in one round trip, for
	/// <see cref="LoadMoreUntilVisibleAsync"/>. Not separate
	/// <c>CountAsync</c>/<c>IsVisibleAsync</c>/<c>IsEnabledAsync</c> calls: the
	/// button can unmount between any two of them - the race that method exists
	/// to avoid - and <c>IsEnabledAsync</c> throws once it has.
	///
	/// The count is a progress signal, not an assertion, so it counts every
	/// descendant rather than assuming a row tag. A label flipping to its loading
	/// state leaves it untouched, which is what makes "unchanged count + enabled
	/// button" a reliable read of an uncommitted click.
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
	/// Asserts the page's content wrapper inside `&lt;main&gt;` has equal left/right
	/// whitespace, i.e. it is horizontally centered via `mx-auto`. Selects on the
	/// `data-content-wrapper` attribute rather than a Tailwind utility class, which
	/// a cosmetic rename would silently break.
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
	/// Asserts the page's content wrapper inside `&lt;main&gt;` sits flush against
	/// the left edge, i.e. left-aligned rather than centered via `mx-auto`.
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

		// Polled rather than read once: layout can still be mid-reflow the instant
		// elements become visible, which a single read can catch.
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
