using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace VisualTests;

public abstract class VisualTestBase(AspireFixture fixture) : PageTest
{
	public AspireFixture Fixture => fixture;

	private static int _testIpSequence;
	private bool _tracingStarted;

	public override bool PropagateTraceContext => false;

	public override BrowserNewContextOptions ContextOptions(TestContext testContext)
	{
		var n = Interlocked.Increment(ref _testIpSequence);
		var uniqueTestIp = $"10.{(n >> 8) & 0xFF}.{n & 0xFF}.1";

		return new()
		{
			ReducedMotion = ReducedMotion.Reduce,
			ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Forwarded-For"] = uniqueTestIp },

			ServiceWorkers = ServiceWorkerPolicy.Block,
		};
	}

	[Before(Test)]
	public async Task SetupVisualTest()
	{
		await fixture.WaitForResourceAsync("frontend");

		await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
		_tracingStarted = true;
	}

	[After(Test)]
	public async Task TeardownTracingAsync(TestContext testContext)
	{
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

	protected static async Task<HttpResponseMessage> PostJsonWithRetryAsync(
		HttpClient client, string requestUri, object body, CancellationToken cancellationToken = default)
	{
		const int maxAttempts = 4;
		HttpResponseMessage response;
		for (var attempt = 1; ; attempt++)
		{
			response = await client.PostAsJsonAsync(requestUri, body, cancellationToken);
			if (response.StatusCode < HttpStatusCode.InternalServerError || attempt >= maxAttempts)
				break;

			response.Dispose();
			await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1)), cancellationToken);
		}

		return response;
	}

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

			if (state == LoadMoreState.Gone)
				return;

			if (state == LoadMoreState.Loading)
			{
				await Task.Delay(100);
				continue;
			}

			if (elementCount == clickedAtElementCount && DateTimeOffset.UtcNow < commitDeadline)
			{
				await Task.Delay(100);
				continue;
			}

			var remainingMs = (deadline - DateTimeOffset.UtcNow).TotalMilliseconds;
			if (remainingMs <= 0)
				return;

			clickedAtElementCount = elementCount;
			try
			{
				await loadMoreButton.ClickAsync(new() { Timeout = (float)remainingMs });
			}

			catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
			{
				return;
			}

			commitDeadline = DateTimeOffset.UtcNow.AddSeconds(2);
		}
	}

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

	protected async Task GoToOpportunitiesAsync(string origin)
	{
		await Page.GetByTestId("nav-findOpportunities").ClickAsync();
		await Page.WaitForURLAsync($"{origin}/opportunities", new() { Timeout = 15_000 });
	}

	private enum LoadMoreState
	{
		Ready,

		Loading,

		Gone,
	}

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

	protected async Task AssertMaxWidthContentCenteredAsync(string label)
	{
		var main = Page.Locator("main");
		await Expect(main).ToBeVisibleAsync();
		var container = main.Locator("[data-content-wrapper]").First;
		await Expect(container).ToBeVisibleAsync(new() { Timeout = 10_000 });

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

	protected async Task AssertMaxWidthContentLeftAlignedAsync(string label)
	{
		var main = Page.Locator("main");
		await Expect(main).ToBeVisibleAsync();
		var container = main.Locator("[data-content-wrapper]").First;
		await Expect(container).ToBeVisibleAsync(new() { Timeout = 10_000 });

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

	protected async Task AssertVerticalGapBetweenAsync(ILocator upper, ILocator lower, string label)
	{
		await Expect(upper).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(lower).ToBeVisibleAsync(new() { Timeout = 10_000 });

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
