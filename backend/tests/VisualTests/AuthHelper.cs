using Microsoft.Playwright;

namespace VisualTests;

public static class AuthHelper
{
    public static async Task LoginAsync(IPage page, Uri frontendUrl, string username, string password)
    {
        await page.GotoAsync(frontendUrl.ToString());

        await page.GetByRole(AriaRole.Button, new() { Name = "Anmelden" }).First.ClickAsync();

        await page.WaitForURLAsync("**/realms/einsatzbereit/**");

        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(password);
        await page.Locator("#kc-login").ClickAsync();

        await page.WaitForURLAsync($"{frontendUrl.GetLeftPart(UriPartial.Authority)}/", new()
        {
            Timeout = 30_000,
        });
    }
}
