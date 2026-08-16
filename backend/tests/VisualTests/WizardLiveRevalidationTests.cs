using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression coverage for #1928: react-hook-form only re-validates an
/// already-errored field on every keystroke once the form has gone through a
/// <c>handleSubmit()</c> call - its <c>reValidateMode</c> default of
/// "onChange" only takes effect after <c>isSubmitted</c> flips. This wizard
/// never calls <c>handleSubmit</c>; both "Next" and the final submit call
/// <c>trigger()</c> directly instead. A field marked invalid by "Next" used
/// to keep showing "Please fill this in." and <c>aria-invalid="true"</c>
/// even after the user had already typed a valid value, until "Next" was
/// clicked a second time. The field's own validation now re-runs on every
/// keystroke once it already has an error, so the message clears the moment
/// the value becomes valid - no second "Next" click required.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class WizardLiveRevalidationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task InvalidTitleField_FixedWithoutClickingNextAgain_ClearsItsErrorLive()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn.First).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync(new() { Timeout = 5_000 });

		// Title and description both empty - "Next" must mark both invalid.
		await Page.GetByTestId("modal-next").ClickAsync();

		var titleInput = Page.Locator("#opportunity-title");
		var titleError = Page.Locator("#opportunity-title-error");
		var descriptionError = Page.Locator("#opportunity-description-error");
		await Expect(titleError).ToHaveTextAsync("Please fill this in.");
		await Expect(descriptionError).ToHaveTextAsync("Please fill this in.");
		await Expect(titleInput).ToHaveAttributeAsync("aria-invalid", "true");

		// Real keystrokes into the now-invalid title field - deliberately not
		// clicking "Next" again - matching how the bug was originally caught.
		await titleInput.PressSequentiallyAsync("Erste-Hilfe-Kurs fuer Anfaenger");
		await Expect(titleError).Not.ToBeAttachedAsync(new() { Timeout = 5_000 });
		await Expect(titleInput).Not.ToHaveAttributeAsync("aria-invalid", "true");

		// Description is untouched and must stay marked invalid - the fix is
		// per-field, not a blanket clear of every error on the step.
		await Expect(descriptionError).ToHaveTextAsync("Please fill this in.");
	}
}
