using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1339: the Impressum published "Adresse auf Anfrage" (address on request)
/// instead of a permanently available postal address, which German law
/// requires (former § 5 TMG, now § 5 DDG; former § 55 Abs. 2 RStV, now
/// § 18 Abs. 2 MStV also superseded). The privacy policy's responsible-party
/// section had the same placeholder. These tests pin the real address and
/// current statutory references so the placeholder can't silently regress.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ImprintLegalComplianceTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ImprintPage_ShowsRealAddressAndCurrentLegalReferences()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/imprint");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Information according to § 5 DDG" })).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Responsible for content according to § 18 para. 2 MStV" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByText("Ammerländer Heerstraße 76", new() { Exact = false }))
			.ToHaveCountAsync(2);
		await Expect(Page.GetByText("26129 Oldenburg, Germany", new() { Exact = false }))
			.ToHaveCountAsync(2);

		// Regression guard for the placeholder reported in #1339.
		await Expect(Page.GetByText("Address available on request", new() { Exact = false }))
			.Not.ToBeVisibleAsync();
		await Expect(Page.GetByText("§ 5 TMG", new() { Exact = false })).Not.ToBeVisibleAsync();
		await Expect(Page.GetByText("§ 55", new() { Exact = false })).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task ImprintPage_ShowsRealAddressAndCurrentLegalReferences_InGerman()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/imprint");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		await Page.GetByRole(AriaRole.Option, new() { Name = "Deutsch" }).ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Angaben gemäß § 5 DDG" })).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Verantwortlich für den Inhalt nach § 18 Abs. 2 MStV" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByText("Ammerländer Heerstraße 76", new() { Exact = false }))
			.ToHaveCountAsync(2);
		await Expect(Page.GetByText("26129 Oldenburg", new() { Exact = false }))
			.ToHaveCountAsync(2);

		await Expect(Page.GetByText("Adresse auf Anfrage", new() { Exact = false }))
			.Not.ToBeVisibleAsync();
		await Expect(Page.GetByText("§ 5 TMG", new() { Exact = false })).Not.ToBeVisibleAsync();
		await Expect(Page.GetByText("§ 55 Abs. 2 RStV", new() { Exact = false }))
			.Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task PrivacyPolicyPage_ShowsRealAddressInsteadOfOnRequestPlaceholder()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/privacy-policy");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByText("Ammerländer Heerstraße 76", new() { Exact = false }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByText("26129 Oldenburg, Germany", new() { Exact = false }))
			.ToBeVisibleAsync();

		// Regression guard for the placeholder reported in #1339.
		await Expect(Page.GetByText("Available on request via email", new() { Exact = false }))
			.Not.ToBeVisibleAsync();
	}
}
