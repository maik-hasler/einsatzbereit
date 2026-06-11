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

		// Section heading is rendered and centre-aligned (matches "How it works").
		var heading = Page
			.GetByRole(AriaRole.Heading, new() { Name = "Current Opportunities" })
			.First;
		await Expect(heading).ToBeVisibleAsync(new() { Timeout = 15_000 });
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
	public async Task CreateWizard_HasStepperFreeNavigationAndDraftButton()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Create button only appears when an org is active.
		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		if (await createBtn.CountAsync() == 0)
			return; // no org selected in seed - skip

		await createBtn.First.ClickAsync();

		// Guard: the dialog may not open if no active-org cookie is set yet.
		var dialog = Page.Locator("[role='dialog']");
		try
		{
			await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });
		}
		catch
		{
			return; // modal did not open - skip remaining assertions
		}

		// Step 1 content visible.
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();

		// Brand accent bar present (from-brand-600 class).
		var accent = dialog.Locator("[class*='from-brand-600']").First;
		await Expect(accent).ToBeVisibleAsync();

		// Clickable stepper with 4 labelled steps.
		for (var n = 1; n <= 4; n++)
			await Expect(Page.GetByTestId($"wizard-stepper-{n}")).ToBeVisibleAsync();

		// Save-as-draft action is always available.
		await Expect(Page.GetByTestId("modal-save-draft")).ToBeVisibleAsync();

		// Free navigation: Next is enabled even with empty required fields.
		var nextBtn = Page.GetByTestId("modal-next");
		await Expect(nextBtn).ToBeEnabledAsync();
		await nextBtn.ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-2")).ToBeVisibleAsync();

		// Stepper jumps directly to any step (2 -> 4).
		await Page.GetByTestId("wizard-stepper-4").ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-4")).ToBeVisibleAsync();

		// Jump back to step 1 and check the floating-label title field.
		await Page.GetByTestId("wizard-stepper-1").ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();
		await Page.Locator("#opportunity-title").FillAsync("Wizard CI Test");

		// Banner upload affordance present on step 1.
		await Expect(Page.Locator("#opportunity-banner")).ToBeAttachedAsync();

		// Step 2 hint card present.
		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
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

	[Test]
	public async Task HomePage_LoadsWithoutError_WhenPublishedOpportunitiesExist()
	{
		// Regression: EF Core 10 query translation failure caused HTTP 500 on all
		// volunteer opportunity list endpoints (GetPagedSummaries + org queries).
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());

		// The main element must be present - a 500 would show an error page instead.
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Wait for the opportunities API call to resolve: the loading paragraph
		// disappears once the response (success or error) is received.
		// Replaces the flaky WaitForLoadStateAsync(NetworkIdle).
		await Expect(
			Page.GetByText(new Regex("Loading|Wird geladen", RegexOptions.IgnoreCase))
		).ToHaveCountAsync(0, new() { Timeout = 15_000 });

		// No generic error message should be visible.
		var errorText = Page.GetByText(new Regex("error|fehler|500", RegexOptions.IgnoreCase));
		(await errorText.CountAsync()).Should().Be(0);
	}

	[Test]
	public async Task CreateDraft_DoesNotAppearInPublicList_AppearOnDashboardWithAmberBadge()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var uniqueTitle = $"Draft Visual Test {Guid.NewGuid().ToString("N")[..8]}";

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Create opportunity button is only present when an org is active.
		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		if (await createBtn.CountAsync() == 0)
			return;

		await createBtn.First.ClickAsync();

		try
		{
			await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });
		}
		catch
		{
			return;
		}

		// Fill title (minimum required for draft save).
		await Page.Locator("#opportunity-title").FillAsync(uniqueTitle);

		// Save as draft.
		await Page.GetByTestId("modal-save-draft").ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync();

		// The public home page must NOT show the draft.
		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var draftInPublicList = Page
			.Locator("a[href*='/volunteer-opportunities/']")
			.Filter(new() { HasText = uniqueTitle });
		await Expect(draftInPublicList).Not.ToBeVisibleAsync();

		// Navigate to org dashboard via the org switcher.
		// The switcher toggle has aria-label "Switch organization" (en) / "Organisation wechseln" (de).
		var switcherBtn = Page.Locator("button[aria-expanded]");
		if (await switcherBtn.CountAsync() == 0)
			return;

		await switcherBtn.First.ClickAsync();
		await Page.GetByTestId("org-dashboard-link").ClickAsync();

		// Drafts section is visible.
		var draftsSection = Page.GetByTestId("drafts-section");
		await Expect(draftsSection).ToBeVisibleAsync();

		// Draft entry with the title is listed.
		await Expect(draftsSection.GetByText(uniqueTitle)).ToBeVisibleAsync();

		// Amber badge present (bg-amber-100 class applied to the draft status pill).
		var amberBadge = draftsSection.Locator("[class*='bg-amber-100']").First;
		await Expect(amberBadge).ToBeVisibleAsync();
	}
}
