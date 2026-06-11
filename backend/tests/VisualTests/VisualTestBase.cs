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
}
