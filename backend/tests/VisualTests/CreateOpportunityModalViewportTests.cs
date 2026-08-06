using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression coverage for #1663: Modal.tsx's wrapper centered the dialog with
/// `overflow-hidden` and no scroll container, so a dialog taller than the
/// viewport lost content off both the top and bottom edges with no way to
/// recover it via mouse wheel or touch - only a focus()-driven auto-scroll
/// (keyboard users) could nudge it. iPhone SE 2022 at 375x553 was the worst
/// measured case in the issue, against the create-opportunity wizard.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CreateOpportunityModalViewportTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int ViewportWidth = 375;
	private const int ViewportHeight = 553;

	[Test]
	public async Task CreateOpportunityWizard_TallDialogAtIPhoneSeViewport_FooterButtonsReachableByWheelScroll()
	{
		// Resize after FastSignInAsync, not before - its own success check
		// waits on the desktop-only "User menu" button, which is hidden below
		// the md breakpoint.
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(ViewportWidth, ViewportHeight);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn.First).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();

		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();

		// The top edge must already be reachable at rest, with no scrolling at
		// all: items-start (below the sm: breakpoint) keeps it flush against
		// the wrapper's own top padding, rather than items-center pushing it
		// partly above y=0 the way the reported bug did (measured at -30px,
		// permanently unreachable by any input method).
		var titleBox = await Page.Locator("#create-opportunity-dialog-title").BoundingBoxAsync();
		titleBox.Should().NotBeNull();
		titleBox!.Y.Should().BeGreaterThanOrEqualTo(0,
			"the dialog title must not be clipped above the top of the viewport at rest");

		// The dialog must actually be taller than the viewport here, or the
		// wheel-scroll assertions below would pass trivially regardless of
		// the fix.
		var scrollHeight = await dialog.EvaluateAsync<int>("el => el.parentElement.scrollHeight");
		var clientHeight = await dialog.EvaluateAsync<int>("el => el.parentElement.clientHeight");
		scrollHeight.Should().BeGreaterThan(clientHeight,
			"the wizard dialog must be taller than the 553px viewport for this regression test to be meaningful");

		// A real user-initiated mouse wheel (not a JS scrollTo()/scrollTop
		// assignment, and not Locator.HoverAsync/ClickAsync's own
		// auto-scroll-into-view - both of those are programmatic and, per the
		// issue's own measurements, still nudge scrollTop even on the broken
		// overflow-hidden wrapper) is what the bug report showed staying
		// stuck at scrollTop 0. Hover over the stepper first - already
		// visible without any scrolling - purely to position the mouse before
		// dispatching the wheel event at that point.
		await Page.GetByTestId("wizard-stepper-1").HoverAsync();
		await Page.Mouse.WheelAsync(0, 2000);

		foreach (var testId in new[] { "modal-cancel", "modal-save-draft", "modal-next" })
		{
			var button = Page.GetByTestId(testId);
			await Expect(button).ToBeVisibleAsync();
			var box = await button.BoundingBoxAsync();
			box.Should().NotBeNull();
			box!.Y.Should().BeGreaterThanOrEqualTo(0,
				$"{testId} top edge must be within the viewport after scrolling");
			(box.Y + box.Height).Should().BeLessThanOrEqualTo(ViewportHeight,
				$"{testId} bottom edge must be within the {ViewportHeight}px-tall viewport after scrolling");
		}
	}
}
