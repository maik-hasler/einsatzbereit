using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CreateOpportunityModalViewportTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int ViewportWidth = 375;
	private const int ViewportHeight = 553;

	[Test]
	public async Task CreateOpportunityWizard_TallDialogAtIPhoneSeViewport_FooterButtonsReachableByWheelScroll()
	{
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

		var titleBox = await Page.Locator("#create-opportunity-dialog-title").BoundingBoxAsync();
		titleBox.Should().NotBeNull();
		titleBox!.Y.Should().BeGreaterThanOrEqualTo(0,
			"the dialog title must not be clipped above the top of the viewport at rest");

		var scrollHeight = await dialog.EvaluateAsync<int>("el => el.parentElement.scrollHeight");
		var clientHeight = await dialog.EvaluateAsync<int>("el => el.parentElement.clientHeight");
		scrollHeight.Should().BeGreaterThan(clientHeight,
			"the wizard dialog must be taller than the 553px viewport for this regression test to be meaningful");

		await Page.GetByTestId("wizard-stepper-1").HoverAsync();

		var footerTestIds = new[] { "modal-cancel", "modal-save-draft", "modal-next" };
		var lastObserved = new Dictionary<string, string>();
		await PollUntilAsync(async () =>
		{
			await Page.Mouse.WheelAsync(0, 400);
			var allWithinViewport = true;
			foreach (var testId in footerTestIds)
			{
				var box = await Page.GetByTestId(testId).BoundingBoxAsync();
				var withinViewport = box is not null && box.Y >= 0 && box.Y + box.Height <= ViewportHeight;
				lastObserved[testId] = box is null
					? "<no box>"
					: $"top={box.Y:F0} bottom={box.Y + box.Height:F0}";
				if (!withinViewport)
					allWithinViewport = false;
			}
			return allWithinViewport;
		}, () => "footer buttons never became fully visible within the "
			+ $"{ViewportHeight}px viewport after repeated wheel scrolling (last observed: "
			+ string.Join(", ", footerTestIds.Select(id => $"{id}: {lastObserved.GetValueOrDefault(id, "<none>")}"))
			+ ")",
			timeoutMs: 10_000);
	}
}
