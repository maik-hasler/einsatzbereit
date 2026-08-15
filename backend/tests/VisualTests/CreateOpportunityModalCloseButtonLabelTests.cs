using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1957: the header X close button and the footer's step-1
/// text button both read their accessible name from the same
/// createOpportunity.cancel translation key ("Cancel"/"Abbrechen"), so two
/// distinct controls in the dialog were indistinguishable by accessible
/// name (WCAG 2.2 SC 4.1.2). The close button now uses its own
/// createOpportunity.close key.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CreateOpportunityModalCloseButtonLabelTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task CreateOpportunityWizard_HeaderCloseButton_HasDistinctAccessibleNameFromFooterCancelButton()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var trigger = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" }).First;
		await Expect(trigger).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await trigger.ClickAsync();

		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var footerCancel = dialog.GetByTestId("modal-cancel");
		await Expect(footerCancel).ToBeVisibleAsync();
		await Expect(footerCancel).ToHaveTextAsync("Cancel");

		var headerClose = dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true });
		await Expect(headerClose).ToBeVisibleAsync();

		var footerCancelAsCloseCandidate = dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true });
		(await footerCancelAsCloseCandidate.CountAsync()).Should().Be(1,
			"only the footer button should expose the \"Cancel\" accessible name - the header close "
			+ "button must use its own distinct name instead of sharing it");
	}
}
