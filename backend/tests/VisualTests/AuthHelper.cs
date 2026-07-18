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
	/// Navigates a logged-in user (assumed to be on the home page, as they are
	/// right after <see cref="LoginAsync"/>) into the org app shell by clicking
	/// the "Organization overview" hero CTA, which resolves directly to
	/// /app/{organizationId}/dashboard - the /app intermediate picker page no
	/// longer exists (#747).
	/// </summary>
	public static async Task GoToOrgAppDashboardAsync(IPage page, Uri frontendUrl)
	{
		// Defensive: resolves instantly if the caller is already there (the
		// common case, right after LoginAsync), but also makes this helper
		// safe to call from elsewhere.
		await page.WaitForURLAsync($"{frontendUrl.GetLeftPart(UriPartial.Authority)}/", new() { Timeout = 15_000 });

		var cta = page.GetByRole(AriaRole.Link, new() { Name = "Organization overview" });
		await cta.First.WaitForAsync(new() { Timeout = 15_000 });
		await cta.First.ClickAsync();

		await page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}
}
