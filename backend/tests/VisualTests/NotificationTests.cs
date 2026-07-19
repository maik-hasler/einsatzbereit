using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class NotificationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task NotificationBell_IsVisible_WhenAuthenticated()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var bell = Page.GetByTestId("notification-bell");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	[Test]
	public async Task NotificationBell_OpensPanel_WhenClicked()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var bell = Page.GetByTestId("notification-bell");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await bell.ClickAsync();

		var panel = Page.GetByTestId("notification-panel");
		await Expect(panel).ToBeVisibleAsync(new() { Timeout = 5_000 });
	}
}
