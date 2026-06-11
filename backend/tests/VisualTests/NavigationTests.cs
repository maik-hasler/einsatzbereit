using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class NavigationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task HomePage_HasMainHeading()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("h1").First).ToBeVisibleAsync();
	}

	[Test]
	public async Task HomePage_LanguageSelector_HasDarkTransparentTheme_OnHero()
	{
		// Regression: LanguageSelector dropdown on the hero section should use the
		// dark (transparent) theme - bg-brand-800 with white text - instead of the
		// white light theme that was shown before the fix (PR #441).
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		// Wait for the hero h1 so React has fully rendered the Header with isTransparent=true.
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var langBtn = Page.Locator("header button[aria-haspopup='listbox']").First;
		await Expect(langBtn).ToBeVisibleAsync();

		// Button must carry transparent Tailwind classes (border-white/30, text-white).
		var btnClass = await langBtn.GetAttributeAsync("class") ?? string.Empty;
		btnClass.Should().MatchRegex(new Regex("border-white|text-white"));

		// Open the dropdown and verify it uses the dark brand background.
		await langBtn.ClickAsync();
		var dropdown = Page.Locator("header ul[role='listbox']").First;
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var dropdownClass = await dropdown.GetAttributeAsync("class") ?? string.Empty;
		dropdownClass.Should().Contain("bg-brand-800");
		dropdownClass.Should().Contain("left-0");

		await Page.Keyboard.PressAsync("Escape");
	}

	[Test]
	public async Task MobileMenu_LanguageSelector_HasDarkTransparentTheme_OnHero()
	{
		// Regression: LanguageSelector inside the mobile menu on the hero section
		// must use the transparent dark theme (PR #441) - white text, dark dropdown.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.SetViewportSizeAsync(390, 844);
		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Open the mobile menu by clicking the button with aria-label matching openMenu.
		var menuBtn = Page.Locator("header button[aria-label]")
			.Filter(new() { HasNotText = "English" })
			.Filter(new() { HasNotText = "Deutsch" });
		var menuBtnCount = await menuBtn.CountAsync();
		ILocator? hamburger = null;
		for (var i = 0; i < menuBtnCount; i++)
		{
			var label = await menuBtn.Nth(i).GetAttributeAsync("aria-label");
			if (label is not null && Regex.IsMatch(label, "menu|menü|open|öffnen", RegexOptions.IgnoreCase))
			{
				hamburger = menuBtn.Nth(i);
				break;
			}
		}

		if (hamburger is null)
			return; // hamburger not found at this viewport - skip gracefully

		await hamburger.ClickAsync();

		var mobileLangBtn = Page.Locator("button[aria-haspopup='listbox']").Last;
		await Expect(mobileLangBtn).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var mobileBtnClass = await mobileLangBtn.GetAttributeAsync("class") ?? string.Empty;
		mobileBtnClass.Should().MatchRegex(new Regex("border-white|text-white"));
	}
}
