using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

public static class AuthHelper
{
	public static async Task LoginAsync(IPage page, Uri frontendUrl, string username, string password)
	{
		await page.GotoAsync(frontendUrl.ToString());

		await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).First.ClickAsync();

		await page.WaitForURLAsync("**/realms/einsatzbereit/**");

		await page.Locator("#username").FillAsync(username);
		await page.Locator("#password").FillAsync(password);
		await page.Locator("#kc-login").ClickAsync();

		await page.WaitForURLAsync($"{frontendUrl.GetLeftPart(UriPartial.Authority)}/", new()
		{
			Timeout = 30_000,
		});
	}

	/// <summary>
	/// Navigates a logged-in user into the org app shell via the /app entry point
	/// (org-count-conditional: auto-redirects for a single org, shows a picker for
	/// several - olaf's seed data always has two). Replaces the previous pattern of
	/// clicking "Your organizations" on the profile page, which no longer exists.
	/// </summary>
	public static async Task GoToOrgAppDashboardAsync(IPage page, Uri frontendUrl)
	{
		var origin = frontendUrl.GetLeftPart(UriPartial.Authority);
		await page.GotoAsync($"{origin}/app");

		var pickerRow = page.GetByTestId("org-entry-picker-row").First;
		try
		{
			await pickerRow.WaitForAsync(new() { Timeout = 3_000 });
			await pickerRow.ClickAsync();
		}
		catch (TimeoutException)
		{
			// A single org auto-redirects straight to its dashboard - no picker shown.
		}

		await page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}
}
