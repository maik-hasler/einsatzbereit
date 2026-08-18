using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #2077: the create-opportunity wizard marks required fields
/// with an asterisk and wires up aria-required (see RequiredFieldMarkerTests,
/// #1797), but never called react-hook-form's own <c>handleSubmit()</c> -
/// "Next", a blocked stepper jump and the final submit all call
/// <c>trigger()</c> directly instead (see WizardLiveRevalidationTests, #1928),
/// so <c>shouldFocusError</c>'s automatic focus-the-first-invalid-field never
/// ran. A blocked step advance left every screen-reader and keyboard user to
/// hunt for whichever field had turned red, with no signal of where focus
/// should go. Focus must now move to the first field that is actually
/// invalid, not always the first field in the step.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class WizardFocusFirstInvalidFieldTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task Next_WithBothRequiredFieldsBlank_FocusesTheTitleField()
	{
		await OpenCreateWizardAsync();

		// Title and description both empty - title comes first in the step's
		// field order, so it is the one that should receive focus.
		await Page.GetByTestId("modal-next").ClickAsync();

		await Expect(Page.Locator("#opportunity-title-error")).ToHaveTextAsync("Please fill this in.");
		await Expect(Page.Locator("#opportunity-title")).ToBeFocusedAsync();
	}

	[Test]
	public async Task Next_WithOnlyDescriptionBlank_FocusesTheDescriptionFieldNotTheTitle()
	{
		await OpenCreateWizardAsync();

		// Title is already valid - the first *invalid* field is the
		// description, not simply the first field in the step.
		await Page.Locator("#opportunity-title").FillAsync("Focus regression test");
		await Page.GetByTestId("modal-next").ClickAsync();

		await Expect(Page.Locator("#opportunity-description-error")).ToHaveTextAsync("Please fill this in.");
		await Expect(Page.Locator("#opportunity-description")).ToBeFocusedAsync();
		await Expect(Page.Locator("#opportunity-title-error")).Not.ToBeAttachedAsync();
	}

	[Test]
	public async Task StepperJump_BlockedByTheCurrentStep_AlsoFocusesItsFirstInvalidField()
	{
		await OpenCreateWizardAsync();

		// Step 1 is untouched (both fields required) - jumping to step 4
		// must be refused and land focus back on the field standing in the way.
		await Page.GetByTestId("wizard-stepper-4").ClickAsync();

		await Expect(Page.Locator("#create-opportunity-step-blocked")).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();
		await Expect(Page.Locator("#opportunity-title")).ToBeFocusedAsync();
	}

	private async Task OpenCreateWizardAsync()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn.First).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync(new() { Timeout = 5_000 });
	}
}
