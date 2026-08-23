using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ErrorBoundaryOfflineTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task NavigatingToUncachedRouteWhileOffline_ShowsOfflineState_NotGenericErrorBoundary()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(origin);
		await Expect(Page.GetByTestId("nav-help")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Context.SetOfflineAsync(true);
		try
		{
			await Page.GetByTestId("nav-help").ClickAsync();

			await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are offline" }))
				.ToBeVisibleAsync(new() { Timeout = 20_000 });

			await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Something went wrong" }))
				.Not.ToBeVisibleAsync();
			await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Reload" }))
				.Not.ToBeVisibleAsync();

			await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Try again" }))
				.ToBeVisibleAsync();
		}
		finally
		{
			await Context.SetOfflineAsync(false);
		}
	}

	[Test]
	public async Task UncachedRouteFailsWhileOffline_WhenTheConnectionReturns_LoadsWithoutBeingAsked()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(origin);
		await Expect(Page.GetByTestId("nav-help")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Context.SetOfflineAsync(true);
		try
		{
			await Page.GetByTestId("nav-help").ClickAsync();
			await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are offline" }))
				.ToBeVisibleAsync(new() { Timeout = 20_000 });
		}
		finally
		{
			await Context.SetOfflineAsync(false);
		}

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Help", Exact = true }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });
	}
}
