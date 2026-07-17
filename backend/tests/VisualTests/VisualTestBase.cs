using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;
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
	/// Entry point into the organizer app shell (#691/#702): the org switcher
	/// no longer lives in the global Header, so the only way in from
	/// elsewhere in the site is /profile's "Your organizations" list. Navigates
	/// there and, if the logged-in user organizes at least one organization,
	/// clicks the first entry and lands on /app/{id}/dashboard. Returns false
	/// (having navigated to /profile but nothing further) if the list is
	/// absent or empty, so callers can skip gracefully the same way former
	/// callers skipped an absent header switcher.
	/// </summary>
	protected async Task<bool> GoToFirstOrganizationDashboardAsync()
	{
		var origin = Fixture.GetEndpoint("frontend").GetLeftPart(UriPartial.Authority);
		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var orgList = Page.GetByTestId("my-organizations-list");
		try
		{
			await orgList.WaitForAsync(new() { Timeout = 5_000 });
		}
		catch (TimeoutException)
		{
			return false; // no orgs in seed for this user - skip
		}

		var firstLink = Page.GetByTestId("my-organization-link").First;
		if (await firstLink.CountAsync() == 0)
			return false; // list rendered empty - skip

		await firstLink.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/.+/dashboard"), new() { Timeout = 10_000 });
		return true;
	}

	/// <summary>
	/// Checks a sr-only radio-card input (CreateVolunteerOpportunityModal's
	/// occurrence/participationType/checkInMethod steps, and the edit-wizard's
	/// check-in-method step: an `&lt;input type="radio" class="sr-only"&gt;`
	/// whose own `&lt;label&gt;` renders the visible card). Its near-zero-size
	/// bounding box can, under CI load, land under the label's own visible
	/// text, so Playwright's actionability check sees that text "intercepting"
	/// the click and retries for the full timeout. Force bypasses that check -
	/// safe here since we know exactly what's covering it and why.
	/// </summary>
	protected static async Task CheckRadioCardAsync(ILocator radio) =>
		await radio.CheckAsync(new() { Force = true });
}
