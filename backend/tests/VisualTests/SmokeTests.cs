using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SmokeTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task HomePage_LoadsForAnonymousUser()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Login_AsVera_CompletesAndReturnsToHome()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var origin = frontend.GetLeftPart(UriPartial.Authority);
		await Expect(Page).ToHaveURLAsync($"{origin}/");
	}

	[Test]
	public async Task OnboardingBanner_ShowsExactlyOnce_WithNoRawTranslationKey()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var banners = Page.GetByRole(AriaRole.Region, new() { Name = "Welcome banner" });
		await Expect(banners).ToHaveCountAsync(1);
		await Expect(banners).ToContainTextAsync(
			"Browse volunteer opportunities near you, sign up with one click, and earn badges as you help your community.");
		await Expect(banners).Not.ToContainTextAsync("onboarding.message");
	}
}
