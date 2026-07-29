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
/// on every request. Parallel VisualTests all originate from 127.0.0.1, which shares
/// a single anonymous rate-limit bucket (60 req/min). React StrictMode double-invokes
/// effects in dev mode, and ~17 tests navigate the home page concurrently, easily
/// exhausting the shared quota and producing 429s. A unique IP per test gives each
/// its own 60 req/min bucket so no individual test can exceed the limit.
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
/// every request across all 207 tests, and a page-level <c>Page.RouteAsync</c> (10
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
	/// Asserts the page's `.max-w-2xl` content wrapper inside `&lt;main&gt;` has equal
	/// left/right whitespace, i.e. it is horizontally centered via `mx-auto`
	/// rather than left-aligned. See #694.
	/// </summary>
	protected async Task AssertMaxWidthContentCenteredAsync(string label)
	{
		var main = Page.Locator("main");
		await Expect(main).ToBeVisibleAsync();
		var container = main.Locator(".max-w-2xl").First;
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
		}, () => $"{label}: .max-w-2xl content should be horizontally centered within <main> "
			+ $"(last observed |leftGap - rightGap| = {gapDelta}px, must be <2px)");
	}

	/// <summary>
	/// Asserts the page's `.max-w-2xl` content wrapper inside `&lt;main&gt;` sits
	/// flush against the left edge, i.e. it is left-aligned rather than centered
	/// via `mx-auto`. See #766.
	/// </summary>
	protected async Task AssertMaxWidthContentLeftAlignedAsync(string label)
	{
		var main = Page.Locator("main");
		await Expect(main).ToBeVisibleAsync();
		var container = main.Locator(".max-w-2xl").First;
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
		}, () => $"{label}: .max-w-2xl content should sit flush against <main>'s left padding edge, "
			+ $"not be centered (last observed gap = {leftGap}px, must be <2px)");
	}
}
