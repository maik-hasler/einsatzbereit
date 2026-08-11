using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// The org app's opportunity cards show one primary action plus an overflow
/// menu; Edit/Unpublish/Cancel/Delete live inside that menu rather than as
/// side-by-side buttons. Tests that drive those actions have to open the menu
/// first, so they go through here instead of each re-deriving the trigger.
/// </summary>
internal static class OpportunityRowHelper
{
	/// <summary>Opens a row's overflow menu and returns the row.</summary>
	public static async Task<ILocator> OpenActionsAsync(ILocator row)
	{
		await row.GetByTestId("row-actions-trigger").ClickAsync();
		return row;
	}

	/// <summary>Opens the row's overflow menu, then clicks one of its items.</summary>
	public static async Task ClickActionAsync(ILocator row, string testId)
	{
		await OpenActionsAsync(row);
		await row.GetByTestId(testId).ClickAsync();
	}
}
