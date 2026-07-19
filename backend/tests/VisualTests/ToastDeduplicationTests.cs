using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ToastDeduplicationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MultipleSimultaneous403s_ShowOneToast_NotOnePerRequest()
	{
		// Regression for #760: the home page fires several concurrent
		// authenticated GETs on load (notifications, profile, organizations,
		// opportunities). When they all fail with the same "forbidden" error
		// (as happened for every baseline endpoint when a user's token was
		// missing an expected role), the toast bus used to stack one identical
		// toast per failed request instead of coalescing them.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.RouteAsync("**/v1/**", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			// The frontend and backend are cross-origin in this test environment,
			// so a mocked response still needs an Access-Control-Allow-Origin
			// header - the browser enforces CORS on fulfilled responses just as
			// it would on a real one, and without it fetch() rejects before the
			// app's response-handling code (and thus the toast) ever runs.
			await route.FulfillAsync(new()
			{
				Status = 403,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.4\",\"status\":403}",
			});
		});

		// Force the header + home page effects to remount and re-fire their
		// concurrent requests against the mocked-403 route above.
		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var forbiddenToasts = Page.GetByRole(AriaRole.Alert)
			.Filter(new() { HasTextString = "You do not have permission" });

		// Toasts auto-dismiss after 5s (see ToastContext.tsx) - assert well
		// before that so this check can't race the production dismiss timer.
		await Expect(forbiddenToasts).ToHaveCountAsync(1, new() { Timeout = 2_000 });
	}
}
