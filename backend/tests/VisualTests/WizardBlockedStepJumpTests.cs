using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression coverage for #1782: all four step buttons of the create-opportunity
/// wizard render enabled, but clicking one that sits behind an invalid earlier
/// step used to bail out of <c>handleStepClick</c> with no feedback tied to the
/// refused navigation - the dialog simply stayed put. The only signal was the
/// offending field turning red, which the user then had to connect to the click
/// themselves, and which they cannot even see when that field lives on a step
/// that isn't currently rendered. The buttons stay enabled (a disabled button
/// takes itself out of the tab order, so it could not carry the explanation
/// either); the refusal now names the step standing in the way, in an
/// assertive live region wired to the refused button via aria-describedby.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class WizardBlockedStepJumpTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const string BlockedMessageSelector = "#create-opportunity-step-blocked";

	[Test]
	public async Task StepperJump_BlockedByTheCurrentStep_NamesItInAnAssertiveLiveRegion()
	{
		await OpenCreateWizardAsync();

		// Step 1 is untouched and therefore invalid (title and description are
		// both required), so the jump to step 4 must be refused.
		await Page.GetByTestId("wizard-stepper-4").ClickAsync();

		var blocked = Page.Locator($"{BlockedMessageSelector}[role='alert'][aria-live='assertive']");
		await Expect(blocked).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(blocked).ToContainTextAsync("Step 4 is not available yet");
		await Expect(blocked).ToContainTextAsync("step 1 (Basics)");

		// The refusal has to be reachable from the control that was refused,
		// not just announced once into the void.
		await Expect(Page.GetByTestId("wizard-stepper-4"))
			.ToHaveAttributeAsync("aria-describedby", "create-opportunity-step-blocked");

		// And the jump really did not happen.
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("wizard-step-4")).Not.ToBeAttachedAsync();

		// Fixing the named step retires the message on its own - the form
		// validates on blur, so Tab out of the last field rather than relying
		// on the fill alone.
		await Page.Locator("#opportunity-title").FillAsync("Blocked step jump regression");
		await Page.Locator("#opportunity-description").FillAsync("Regression test for #1782.");
		await Page.Locator("#opportunity-description").PressAsync("Tab");
		await Expect(blocked).Not.ToBeVisibleAsync();
		await Expect(Page.GetByTestId("wizard-stepper-4"))
			.Not.ToHaveAttributeAsync("aria-describedby", "create-opportunity-step-blocked");

		// With step 1 valid the same click now goes through.
		await Page.GetByTestId("wizard-stepper-4").ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-4")).ToBeVisibleAsync();
		await Expect(Page.Locator(BlockedMessageSelector)).Not.ToBeAttachedAsync();
	}

	[Test]
	public async Task StepperJump_BlockedByAnIntermediateStep_NamesThatStepNotTheCurrentOne()
	{
		// The case the silent bail hurt most: the field standing in the way is
		// on a step the user is not looking at, so the red rule painted onto
		// that step's marker is the only clue, with nothing saying it is what
		// stopped the jump.
		await OpenCreateWizardAsync();

		await Page.Locator("#opportunity-title").FillAsync("Intermediate step block");
		await Page.Locator("#opportunity-description").FillAsync("Regression test for #1782.");

		// Step 2's address may be pre-filled from the organization, so break it
		// deliberately instead of depending on which fields arrive empty.
		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-2")).ToBeVisibleAsync();
		await Page.Locator("#opportunity-city").FillAsync("");

		// Back to step 1 (a backwards jump is never validated), then forward
		// past the broken step 2.
		await Page.GetByTestId("wizard-stepper-1").ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();
		await Page.GetByTestId("wizard-stepper-4").ClickAsync();

		var blocked = Page.Locator(BlockedMessageSelector);
		await Expect(blocked).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(blocked).ToContainTextAsync("step 2 (Location)");
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();
	}

	private async Task OpenCreateWizardAsync()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn.First).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();

		await Expect(Page.Locator("[role='dialog']")).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();
	}
}
