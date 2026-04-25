using Microsoft.Playwright;
using TUnit.Playwright;

namespace VisualTests;

/// <summary>
/// Base class for all VisualTests. Strips the <c>traceparent</c> header that
/// Microsoft.Playwright .NET injects from <c>Activity.Current</c> (set by TUnit)
/// into browser-initiated requests. Keycloak's CORS preflight does not allow
/// <c>traceparent</c> in <c>Access-Control-Allow-Headers</c>, which would cause
/// oidc-client-ts discovery fetches to fail silently.
/// </summary>
public abstract class VisualTestBase(AspireFixture fixture) : PageTest
{
    public AspireFixture Fixture => fixture;

    [Before(Test)]
    public async Task SetupVisualTest()
    {
        await fixture.WaitForResourceAsync("frontend");

        await Context.RouteAsync("**/*", async route =>
        {
            var headers = new Dictionary<string, string>(
                route.Request.Headers,
                StringComparer.OrdinalIgnoreCase);
            headers.Remove("traceparent");
            headers.Remove("tracestate");
            await route.ContinueAsync(new() { Headers = headers });
        });
    }
}
