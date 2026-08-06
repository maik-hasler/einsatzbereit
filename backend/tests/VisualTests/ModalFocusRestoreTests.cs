using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1670: Modal.tsx's trigger-capture effect was declared
/// *after* the effect that moves focus into the dialog. React fires mount
/// effects in declaration order, so the capture read document.activeElement
/// only once the other effect had already focused something inside the
/// dialog - it captured that inner element instead of the button that opened
/// the modal, and the restore-on-close silently no-oped.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ModalFocusRestoreTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task CreateVolunteerOpportunityModal_Close_RestoresFocusToTriggerButton()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var trigger = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" }).First;
		await Expect(trigger).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await trigger.ClickAsync();

		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Step 1's title field is the first focusable element inside the
		// wizard body (initialFocusRef scopes past the header close button and
		// the stepper) - a focusable child inside the dialog, the exact
		// condition #1670 depended on to mis-capture "the trigger".
		await Expect(Page.Locator("#opportunity-title")).ToBeFocusedAsync();

		// Untouched form, so Escape closes immediately with no discard-changes
		// confirmation in the way.
		await Page.Keyboard.PressAsync("Escape");
		await Expect(dialog).Not.ToBeVisibleAsync();

		(await trigger.EvaluateAsync<bool>("el => el === document.activeElement")).Should().BeTrue(
			"closing the modal must restore focus to the button that opened it, per the WAI-ARIA Dialog pattern");
	}
}
