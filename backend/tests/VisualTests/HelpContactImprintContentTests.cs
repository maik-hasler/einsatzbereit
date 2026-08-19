using Microsoft.Playwright;

namespace VisualTests;

// #2061: co-located content defects on /help, /contact and /imprint - a
// personal Gmail address shown as unclickable text (and carrying a public
// 24-hour SLA promise) instead of a role address behind mailto:, a German
// compound word ("Einsatzseite") split across two links so the first link's
// entire accessible name was the fragment "Einsatz-", and a homepage FAQ
// that answered four questions the Help Center's own FAQ never covered.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HelpContactImprintContentTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const string SupportEmail = "hallo@einsatzbereit.maik-hasler.de";

	[Test]
	public async Task ContactPage_EmailIsRoleAddressBehindMailtoLink()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/contact");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var emailLink = Page.GetByTestId("contact-email");
		await Expect(emailLink).ToBeVisibleAsync();
		await Expect(emailLink).ToHaveAttributeAsync("href", $"mailto:{SupportEmail}");

		await Expect(Page.GetByText("maikhslr", new() { Exact = false })).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task HelpPage_SupportEmailIsMailtoLink_AndSplitLinkLabelIsWholeWord()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/help");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var emailLink = Page.GetByRole(AriaRole.Link, new() { Name = SupportEmail });
		await Expect(emailLink).ToBeVisibleAsync();
		await Expect(emailLink).ToHaveAttributeAsync("href", $"mailto:{SupportEmail}");

		// The 24-hour reply SLA - a promise one maintainer can't reliably hold -
		// is gone along with the plain-text address it used to be attached to.
		await Expect(Page.GetByText("24 hours", new() { Exact = false })).Not.ToBeVisibleAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();

		// Regression guard for the split compound word: before the fix, the
		// first link's entire accessible name was just the fragment "Einsatz-".
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Einsatzseite", Exact = true }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Einsatz-", Exact = true }))
			.ToHaveCountAsync(0);
	}

	[Test]
	public async Task ImprintPage_ContactEmailIsMailtoLink_WithoutGmailAddressOrSlaClaim()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/imprint");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var emailLink = Page.GetByRole(AriaRole.Link, new() { Name = SupportEmail });
		await Expect(emailLink).ToBeVisibleAsync();
		await Expect(emailLink).ToHaveAttributeAsync("href", $"mailto:{SupportEmail}");

		await Expect(Page.GetByText("maikhslr", new() { Exact = false })).Not.ToBeVisibleAsync();
		await Expect(Page.GetByText("24 hours", new() { Exact = false })).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task HomePage_FaqQuestions_AreAlsoAnsweredOnHelpPage()
	{
		// Regression guard for #2061: the homepage FAQ used to answer four
		// questions the Help Center's own FAQ never covered at all, breaking
		// the "More questions? See Help" link's whole premise. /help is now
		// the single source those four questions are drawn from.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		const string question = "Does using Einsatzbereit cost anything?";

		await Page.GotoAsync(origin);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(Page.GetByText(question)).ToBeVisibleAsync();

		await Page.GotoAsync($"{origin}/help");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "General" })).ToBeVisibleAsync();
		await Expect(Page.GetByText(question)).ToBeVisibleAsync();
	}
}
