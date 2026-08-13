using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MobileHeaderTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 375;
	private const int MobileHeight = 812;

	[Test]
	public async Task MobileHeader_NotificationBell_IsAdjacentToBurger_NotCentered()
	{
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var frontend = Fixture.GetEndpoint("frontend");

		// This test can't go through AuthHelper.LoginAsync (needs the hamburger
		// click first), so it needs LoginAsync's Keycloak-CORS fix duplicated
		// here too - see AuthHelper.AllowKeycloakCrossOriginRequestsAsync's doc
		// comment.
		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		await Page.GotoAsync(frontend.ToString());

		// On mobile the Sign in button lives inside the hamburger menu.
		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });
		await Page.WaitForTimeoutAsync(400);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		// Wait on Keycloak's login form element, not the URL - WaitForURLAsync
		// races the frame's own navigation/detachment during the redirect (see
		// AuthHelper.LoginAsync, which hit the same flakiness and now uses this
		// same fix).
		await Page.Locator("#username").WaitForAsync(new() { Timeout = 15_000 });

		// Local Keycloak: single-step login (username + password on the same form).
		await Page.Locator("#username").FillAsync("vera");
		await Page.Locator("#password").FillAsync("vera123");
		await Page.Locator("#kc-login").ClickAsync();

		await Page.WaitForURLAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/",
			new() { Timeout = 30_000 });

		await Page.WaitForTimeoutAsync(1_000);

		var bell = Page.GetByTestId("notification-bell-mobile");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var bellBox = await bell.BoundingBoxAsync();
		bellBox.Should().NotBeNull("Could not get bounding box for notification bell");

		// Burger button (aria-label = "Open menu") should also be visible.
		var burger = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First;
		await Expect(burger).ToBeVisibleAsync(new() { Timeout = 5_000 });
		var burgerBox = await burger.BoundingBoxAsync();
		burgerBox.Should().NotBeNull("Could not get bounding box for burger button");

		double bellCenterX = bellBox!.X + bellBox.Width / 2.0;
		bellCenterX.Should().BeGreaterThan(
			MobileWidth / 2.0,
			$"Bell center ({bellCenterX:F0}px) should be in the right half of the {MobileWidth}px viewport - it was centered before fix #497");

		// They sit in the same flex wrapper, so the gap between them should stay tight.
		double gap = burgerBox!.X - (bellBox.X + bellBox.Width);
		gap.Should().BeLessThanOrEqualTo(
			60.0,
			$"Bell and burger gap ({gap:F0}px) should be <= 60px - a large gap indicates they are not grouped");
	}
}
