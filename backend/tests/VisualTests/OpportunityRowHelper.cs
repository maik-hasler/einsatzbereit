using Microsoft.Playwright;

namespace VisualTests;

internal static class OpportunityRowHelper
{
	public static async Task<ILocator> OpenActionsAsync(ILocator row)
	{
		await row.GetByTestId("row-actions-trigger").ClickAsync();
		return row;
	}

	public static async Task ClickActionAsync(ILocator row, string testId)
	{
		await OpenActionsAsync(row);
		await row.GetByTestId(testId).ClickAsync();
	}
}
