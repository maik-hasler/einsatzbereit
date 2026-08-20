using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1961: the mini-calendar's selected date-range endpoints
/// (MiniCalendar.tsx's `isEdge` cells) rendered white, font-semibold text on
/// a bg-brand-600 fill (~4.28:1), failing the WCAG AA 4.5:1 floor. The two
/// date-filter scans that existed in AccessibilityTests.cs at the time both
/// opened the calendar without ever selecting a range, so no scan ever
/// rendered an `isEdge` cell. This test deterministically selects a start and
/// end day so the fix (bg-brand-700, ~9.5:1) is always exercised - and it has
/// to stay in Playwright either way, since jsdom has no layout or canvas for
/// axe to sample a rendered colour from (einsatzbereit#2148).
/// </summary>
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

		// Jump to next month first so every day in view is in the future (none
		// aria-disabled) regardless of which day of the current month CI
		// happens to run on - picking by grid position rather than a computed
		// ISO date sidesteps needing to know which month is on screen.
		await Page.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();

		var dayButtons = Page.Locator("[role='gridcell'] button[data-date]");
		await dayButtons.First.WaitForAsync();

		// Day 3 as the range start, day 6 as the end - both isEdge cells once
		// selected, with two in-range days between them.
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
