using AwesomeAssertions;
using Microsoft.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace VisualTests;

/// <summary>
/// Base class for all VisualTests. Strips the <c>traceparent</c> header that
/// Microsoft.Playwright .NET injects from <c>Activity.Current</c> (set by TUnit)
/// into browser-initiated requests. Keycloak's CORS preflight does not allow
/// <c>traceparent</c> in <c>Access-Control-Allow-Headers</c>, which would cause
/// oidc-client-ts discovery fetches to fail silently.
///
/// Also injects a per-test unique <c>X-Forwarded-For</c> IP for backend requests.
/// Parallel VisualTests all originate from 127.0.0.1, which shares a single
/// anonymous rate-limit bucket (60 req/min). React StrictMode double-invokes
/// effects in dev mode, and ~17 tests navigate the home page concurrently, easily
/// exhausting the shared quota and producing 429s. A unique IP per test gives each
/// its own 60 req/min bucket so no individual test can exceed the limit.
/// </summary>
public abstract class VisualTestBase(AspireFixture fixture) : PageTest
{
	public AspireFixture Fixture => fixture;

	private static int _testIpSequence;

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
	public override BrowserNewContextOptions ContextOptions(TestContext testContext) =>
		new() { ReducedMotion = ReducedMotion.Reduce };

	[Before(Test)]
	public async Task SetupVisualTest()
	{
		await fixture.WaitForResourceAsync("frontend");

		// Assign a unique loopback-range IP to this test instance.
		var n = Interlocked.Increment(ref _testIpSequence);
		var uniqueTestIp = $"10.{(n >> 8) & 0xFF}.{n & 0xFF}.1";
		var backendOrigin = Fixture.GetEndpoint("backend").GetLeftPart(UriPartial.Authority);

		await Context.RouteAsync("**/*", async route =>
		{
			var headers = new Dictionary<string, string>(
				route.Request.Headers,
				StringComparer.OrdinalIgnoreCase);
			headers.Remove("traceparent");
			headers.Remove("tracestate");
			// Tag backend requests with a per-test IP so each test has its own
			// anonymous rate-limit bucket and parallel tests can't exhaust each other's quota.
			if (route.Request.Url.StartsWith(backendOrigin, StringComparison.Ordinal)
				&& !headers.ContainsKey("X-Forwarded-For"))
				headers["X-Forwarded-For"] = uniqueTestIp;
			await route.ContinueAsync(new() { Headers = headers });
		});
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

		var mainBox = await main.BoundingBoxAsync();
		var containerBox = await container.BoundingBoxAsync();
		mainBox.Should().NotBeNull();
		containerBox.Should().NotBeNull();

		var leftGap = containerBox!.X - mainBox!.X;
		var rightGap = mainBox.X + mainBox.Width - (containerBox.X + containerBox.Width);

		Math.Abs(leftGap - rightGap).Should().BeLessThan(2,
			$"{label}: .max-w-2xl content should be horizontally centered within <main>");
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

		var mainBox = await main.BoundingBoxAsync();
		var containerBox = await container.BoundingBoxAsync();
		mainBox.Should().NotBeNull();
		containerBox.Should().NotBeNull();

		// BoundingBoxAsync returns <main>'s border box, which includes its own
		// (symmetric) horizontal padding - net that out so a left-aligned child
		// is expected to sit at <main>'s padded content edge, not at X=0.
		var mainPaddingLeft = await main.EvaluateAsync<double>(
			"el => parseFloat(getComputedStyle(el).paddingLeft)");

		var leftGap = containerBox!.X - mainBox!.X - mainPaddingLeft;

		Math.Abs(leftGap).Should().BeLessThan(2,
			$"{label}: .max-w-2xl content should sit flush against <main>'s left padding edge, not be centered");
	}
}
