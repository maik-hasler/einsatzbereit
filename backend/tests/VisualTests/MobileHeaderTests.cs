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

		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		await Page.GotoAsync(frontend.ToString());

		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		var signIn = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).First;
		await Expect(signIn).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await signIn.ClickAsync(new() { Timeout = 10_000 });

		await Page.Locator("#username").WaitForAsync(new() { Timeout = 15_000 });

		await Page.Locator("#username").FillAsync("vera");
		await Page.Locator("#password").FillAsync("vera123");
		await Page.Locator("#kc-login").ClickAsync();

		await Page.WaitForURLAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/",
			new() { Timeout = 30_000 });

		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var bell = Page.GetByTestId("notification-bell-mobile");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var bellBox = await bell.BoundingBoxAsync();
		bellBox.Should().NotBeNull("Could not get bounding box for notification bell");

		var burger = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First;
		await Expect(burger).ToBeVisibleAsync(new() { Timeout = 5_000 });
		var burgerBox = await burger.BoundingBoxAsync();
		burgerBox.Should().NotBeNull("Could not get bounding box for burger button");

		double bellCenterX = bellBox!.X + bellBox.Width / 2.0;
		bellCenterX.Should().BeGreaterThan(
			MobileWidth / 2.0,
			$"Bell center ({bellCenterX:F0}px) should be in the right half of the {MobileWidth}px viewport - it was centered before fix #497");

		double gap = burgerBox!.X - (bellBox.X + bellBox.Width);
		gap.Should().BeLessThanOrEqualTo(
			60.0,
			$"Bell and burger gap ({gap:F0}px) should be <= 60px - a large gap indicates they are not grouped");
	}

	[Test]
	public async Task MobileMenu_ToggleLabel_SwapsBetweenOpenAndClose_AndStaysInTheFocusTrap()
	{
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var frontend = Fixture.GetEndpoint("frontend");
		await Page.GotoAsync(frontend.ToString());

		var toggle = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First;
		await Expect(toggle).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await toggle.ClickAsync();
		await Expect(toggle).ToHaveAttributeAsync("aria-label", "Close menu");
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

		var register = Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).First;
		await Expect(register).ToBeVisibleAsync();
		await register.FocusAsync();
		await Page.Keyboard.PressAsync("Tab");

		// Tab from the last item in the open mobile menu should reach the toggle
		// that opened it, not skip over it (#2234).
		await Expect(toggle).ToBeFocusedAsync();

		await Page.Keyboard.PressAsync("Shift+Tab");
		// Shift+Tab from the toggle should cycle back into the panel, not escape the trap.
		await Expect(register).ToBeFocusedAsync();

		await toggle.ClickAsync();
		await Expect(toggle).ToHaveAttributeAsync("aria-label", "Open menu");
	}
}
