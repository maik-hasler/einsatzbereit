using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #972: an accessibility audit found zero aria-live/role="status"/
/// role="alert" regions across 16 page loads, because both the toast container
/// (ToastContext.tsx) and ErrorBanner only ever appear in the DOM once a toast or
/// error already exists - a screen reader has no live region to pick up until
/// content is already stale. The fix adds an always-mounted, empty, visually
/// hidden role="status" aria-live="polite" sentinel alongside (not wrapping) the
/// toast list, so a live region exists from initial page load without nesting it
/// around each toast's own role="alert" (nesting live regions is unreliable
/// across screen readers - see ToastContext.tsx), and adds an explicit
/// aria-live="assertive" to ErrorBanner alongside its existing role="alert".
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LiveRegionTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ToastLiveRegionSentinel_IsPresentOnPageLoad_BeforeAnyToastFires()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var liveRegion = Page.Locator("[role='status'][aria-live='polite']");
		await Expect(liveRegion).ToBeAttachedAsync();
	}

	[Test]
	public async Task Toast_KeepsRoleAlert_AsSiblingOfTheLiveRegionSentinel_NotNestedInsideIt()
	{
		// Regression for #760's fix (ToastDeduplicationTests) combined with #972:
		// the sentinel live region must not become an ancestor of individual
		// toasts, or their own role="alert" (implicitly "assertive") would be
		// downgraded to the sentinel's "polite" in some screen readers. Toasts
		// must keep their own role="alert" so they're still announced
		// immediately (and so existing toast-locating tests keep working).
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.RouteAsync("**/v1/**", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			await route.FulfillAsync(new()
			{
				Status = 403,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.4\",\"status\":403}",
			});
		});

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var toast = Page.GetByRole(AriaRole.Alert)
			.Filter(new() { HasTextString = "You do not have permission" });
		await Expect(toast).ToBeVisibleAsync(new() { Timeout = 2_000 });

		var sentinel = Page.Locator("[role='status'][aria-live='polite']");
		await Expect(sentinel).ToBeAttachedAsync();
		(await sentinel.Locator("[role='alert']").CountAsync()).Should().Be(0,
			"the toast must not be nested inside the polite sentinel region - " +
			"its own role=\"alert\" needs to stay the outermost live region so " +
			"it keeps its assertive announcement semantics");
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_ErrorBanner_HasAssertiveAriaLive()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var unknownId = Guid.NewGuid().ToString();

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{unknownId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var errorBanner = Page.Locator("[role='alert'][aria-live='assertive']");
		await Expect(errorBanner).ToBeVisibleAsync();
	}
}
