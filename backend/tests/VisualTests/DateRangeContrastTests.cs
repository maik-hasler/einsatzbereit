using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class DateRangeContrastTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OpportunitiesPage_WithSelectedDateRange_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Date", Exact = true }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Grid)).ToBeVisibleAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();

		var dayButtons = Page.Locator("[role='gridcell'] button[data-date]");
		await dayButtons.First.WaitForAsync();

		await dayButtons.Nth(2).ClickAsync();
		await dayButtons.Nth(5).ClickAsync();

		await Expect(Page.Locator("[aria-pressed='true']").First).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		var violations = result.Violations
			.Where(v => v.Impact is "serious" or "critical")
			.ToList();

		if (violations.Count > 0)
		{
			var summary = string.Join("\n", violations.Select(v =>
				$"[{v.Impact}] {v.Id}: {v.Description}\n" +
				string.Join("\n", v.Nodes.Select(n => $"  - {n.Html}"))));
			throw new Exception($"Axe found {violations.Count} serious/critical a11y violation(s):\n{summary}");
		}
	}
}
