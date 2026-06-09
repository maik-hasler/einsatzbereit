using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class VolunteerOpportunityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task HomePage_RendersOpportunitiesList()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("main")).ToBeVisibleAsync();
	}

	[Test]
	public async Task HomePage_TogglesBetweenListAndMapView()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var mapToggle = Page.GetByTestId("view-toggle-map");
		await Expect(mapToggle).ToBeVisibleAsync();
		await mapToggle.ClickAsync();

		await Expect(Page.GetByTestId("opportunity-map")).ToBeVisibleAsync();
		await Expect(Page.Locator(".leaflet-container")).ToBeVisibleAsync();

		Page.Url.Should().Contain("view=map");

		var listToggle = Page.GetByTestId("view-toggle-list");
		await listToggle.ClickAsync();
		await Expect(Page.GetByTestId("opportunity-map")).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task OccurrenceFilter_UpdatesUrlWithOccurrenceParam()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("filter-frequency").ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "One-time" }).ClickAsync();

		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*occurrence=OneTime"));
	}

	[Test]
	public async Task ParticipationTypeFilter_UpdatesUrlWithParticipationTypeParam()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("filter-type").ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Waitlist" }).ClickAsync();

		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*participationType=Waitlist"));
	}

	[Test]
	public async Task MultipleFilters_AllReflectedInUrl()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("filter-frequency").ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "One-time" }).ClickAsync();
		await Page.GetByTestId("filter-type").ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Waitlist" }).ClickAsync();

		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*occurrence=OneTime"));
		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*participationType=Waitlist"));
	}

	[Test]
	public async Task HomePage_OpportunitiesSection_IsCenteredWithStyledCards()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Section heading is rendered and centre-aligned (matches "How it works").
		var heading = Page
			.GetByRole(AriaRole.Heading, new() { Name = "Current Opportunities" })
			.First;
		await Expect(heading).ToBeVisibleAsync();
		var textAlign = await heading.EvaluateAsync<string>(
			"el => getComputedStyle(el).textAlign");
		textAlign.Should().Be("center");

		// Subtitle line is present below the heading.
		await Expect(Page.GetByText(new Regex("lend a hand", RegexOptions.IgnoreCase)))
			.ToBeVisibleAsync();

		// If opportunities are seeded, each card carries the redesigned visuals:
		// a clickable organisation link and the brand-gradient category banner.
		var firstCard = Page
			.Locator("ul li:has(a[href*='/volunteer-opportunities/'])")
			.First;
		if (await firstCard.CountAsync() == 0)
			return; // no opportunities seeded, skip card-specific checks

		await Expect(firstCard.Locator("a[href*='/organizations/']"))
			.ToBeVisibleAsync();
		(await firstCard.Locator("[class*='from-brand-500']").CountAsync())
			.Should().BeGreaterThan(0);
	}

	[Test]
	public async Task CreateWizard_HasGradientHeaderAndStepNavigation()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Create button only appears when an org is active.
		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		if (await createBtn.CountAsync() == 0)
			return; // no org selected in seed - skip

		await createBtn.First.ClickAsync();

		// Dialog must open.
		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();

		// Step 1 content visible.
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();

		// Gradient header present (from-brand-600 class).
		var header = dialog.Locator("[class*='from-brand-600']").First;
		await Expect(header).ToBeVisibleAsync();

		// Next button disabled when title/description are empty.
		var nextBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Next" });
		await Expect(nextBtn).ToBeDisabledAsync();

		// Fill required fields.
		await Page.Locator("#opportunity-title").FillAsync("Wizard CI Test");
		await Page.Locator("#opportunity-description").FillAsync("Automated visual test verifying the wizard.");

		// Next now enabled.
		await Expect(nextBtn).ToBeEnabledAsync();
		await nextBtn.ClickAsync();

		// Step 2: location.
		await Expect(Page.GetByTestId("wizard-step-2")).ToBeVisibleAsync();

		// Hint card present.
		var hint = Page.GetByTestId("wizard-step-2").Locator("[class*='bg-brand-50']").First;
		await Expect(hint).ToBeVisibleAsync();

		// Close with Escape.
		await Page.Keyboard.PressAsync("Escape");
		await Expect(dialog).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task DetailPage_ShowsBreadcrumbAndShareButton()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		if (await firstCard.CountAsync() == 0)
			return; // no opportunities seeded, skip

		var href = await firstCard.GetAttributeAsync("href");
		if (href is null)
			return;

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #394 breadcrumb navigation present (aria-label is hardcoded "Breadcrumb").
		await Expect(Page.Locator("nav[aria-label='Breadcrumb']"))
			.ToBeVisibleAsync();

		// #373 share button present (matched by stable test id, locale-independent).
		await Expect(Page.GetByTestId("share-opportunity"))
			.ToBeVisibleAsync();
	}
}
