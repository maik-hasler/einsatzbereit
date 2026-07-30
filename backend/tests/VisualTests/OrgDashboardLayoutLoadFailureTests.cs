using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression coverage for #1234: a failed GET .../dashboard/layout used to be
/// swallowed silently, falling back to DEFAULT_LAYOUT with no indication
/// anything went wrong - indistinguishable from the same optimistic default
/// render a brand-new organizer sees. An organizer with a real saved layout
/// who hit this during a transient backend outage could edit the (wrong,
/// default) dashboard shown to them and Save, permanently overwriting their
/// actual server-side layout. index.tsx now tracks a `layoutLoadFailed` state:
/// an inline error banner with a retry button renders, and the "Edit" quick
/// action is disabled until a load actually succeeds.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgDashboardLayoutLoadFailureTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task LayoutFetchFails_ShowsInlineErrorAndDisablesEdit()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.RouteAsync("**/dashboard/layout", async route =>
		{
			if (route.Request.Method == "GET")
				await route.AbortAsync();
			else
				await route.ContinueAsync();
		});

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		await Expect(Page.GetByRole(AriaRole.Alert))
			.ToContainTextAsync("Couldn't confirm your saved dashboard layout");
		await Expect(Page.GetByTestId("dashboard-layout-retry")).ToBeVisibleAsync();

		// The organizer must not be able to enter edit mode (and from there,
		// Save) while the real saved layout is still unconfirmed - doing so
		// would persist whatever DEFAULT_LAYOUT happens to be showing right
		// now over their actual saved one.
		var editButton = Page.GetByTestId("quick-action-edit");
		await Expect(editButton).ToBeDisabledAsync();

		// A disabled native button drops out of the tab order with no other
		// indication why - review feedback on the first version of this fix
		// flagged the missing explanation, so a `title` carries the same
		// reason the inline banner shows.
		await Expect(editButton).ToHaveAttributeAsync(
			"title", new Regex("^Couldn't confirm your saved dashboard layout"));

		// The retry button's own accessible name ("Retry") says nothing about
		// what it's retrying - aria-describedby ties it to the banner text.
		var retryButton = Page.GetByTestId("dashboard-layout-retry");
		await Expect(retryButton).ToHaveAttributeAsync("aria-describedby", "dashboard-layout-load-error");
	}

	[Test]
	public async Task LayoutFetchFails_RetryButton_RecoversOnceTheFetchSucceeds()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		// Aborts only the very first GET - the retry's own request (and any
		// PUT save, though none happens in this test) goes through normally,
		// so this exercises an organizer recovering from a single transient
		// failure rather than a permanently broken backend.
		var getAttempts = 0;
		await Page.RouteAsync("**/dashboard/layout", async route =>
		{
			if (route.Request.Method == "GET" && Interlocked.Increment(ref getAttempts) == 1)
				await route.AbortAsync();
			else
				await route.ContinueAsync();
		});

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		await Expect(Page.GetByRole(AriaRole.Alert))
			.ToContainTextAsync("Couldn't confirm your saved dashboard layout");
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeDisabledAsync();

		await Page.GetByTestId("dashboard-layout-retry").ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0, new() { Timeout = 10_000 });
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeEnabledAsync();
	}
}
