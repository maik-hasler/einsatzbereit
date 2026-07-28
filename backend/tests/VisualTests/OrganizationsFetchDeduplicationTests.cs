using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrganizationsFetchDeduplicationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task AuthenticatedHomePageLoad_FetchesOrganizationsOnce_NotOncePerHeaderAndPage()
	{
		// Regression for #1396: Header and HomePage each independently called
		// GET /v1/organizations on mount, so an authenticated home page load
		// fired the same query twice (and React StrictMode's dev-mode
		// double-invoke - see VisualTestBase - would have doubled that again
		// without the fix). Both now share a single in-flight request via
		// useSharedOrgFetch.
		var frontend = Fixture.GetEndpoint("frontend");

		var requestCount = 0;
		await Page.RouteAsync("**/v1/organizations", async route =>
		{
			Interlocked.Increment(ref requestCount);
			await route.ContinueAsync();
		});

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		requestCount.Should().Be(1);
	}
}
