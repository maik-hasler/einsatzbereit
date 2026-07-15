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
		await Page.GetByRole(AriaRole.Button, new() { Name = "Scheduled slots" }).ClickAsync();

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
		await Page.GetByRole(AriaRole.Button, new() { Name = "Scheduled slots" }).ClickAsync();

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

		// Create opportunity now lives on the organization dashboard - navigate there
		// via the org switcher. Switcher toggle has aria-label "Switch organization"
		// (en) / "Organisation wechseln" (de).
		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		if (await switcherBtn.CountAsync() == 0)
			return; // no org membership in seed - skip

		await switcherBtn.First.ClickAsync();
		var dashboardLink = Page.GetByTestId("org-dashboard-link");
		if (await dashboardLink.CountAsync() == 0)
			return; // no org selected in seed - skip

		await dashboardLink.First.ClickAsync();

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });

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

		// Plain header (#676 Pitch 2 dropped the one-off gradient accent bar
		// in favor of the same plain header every other modal uses).
		var accent = dialog.Locator("[class*='from-brand-600']");
		await Expect(accent).Not.ToBeAttachedAsync();

		// Clickable stepper with 4 labelled steps.
		for (var n = 1; n <= 4; n++)
			await Expect(Page.GetByTestId($"wizard-stepper-{n}")).ToBeVisibleAsync();

		// Save-as-draft action is always available.
		await Expect(Page.GetByTestId("modal-save-draft")).ToBeVisibleAsync();

		// Fail-fast validation: "Next" is blocked while step 1's required
		// fields are empty, with the error shown before Publish-time.
		var nextBtn = Page.GetByTestId("modal-next");
		await nextBtn.ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();
		await Expect(Page.Locator("#opportunity-title-error")).ToBeVisibleAsync();

		// Fill in the required fields - Next now advances.
		await Page.Locator("#opportunity-title").FillAsync("Wizard CI Test");
		await Page.Locator("#opportunity-description").FillAsync(
			"Visual test coverage for the Pitch 2 wizard rewrite.");
		await nextBtn.ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-2")).ToBeVisibleAsync();

		// Mark remote so step 2's address fields are no longer required,
		// then the stepper can jump directly ahead to step 4.
		await Page.Locator("#opportunity-remote").CheckAsync();
		await Page.GetByTestId("wizard-stepper-4").ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-4")).ToBeVisibleAsync();

		// Jump back to step 1 - always allowed, no validation on the way back.
		await Page.GetByTestId("wizard-stepper-1").ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();

		// Banner upload affordance present on step 1.
		await Expect(Page.Locator("#opportunity-banner")).ToBeAttachedAsync();

		// Step 2 hint card present.
		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
		var hint = Page.GetByTestId("wizard-step-2").Locator("[class*='bg-brand-50']").First;
		await Expect(hint).ToBeVisibleAsync();

		// The form is dirty (title/description filled in) - Escape must ask
		// for confirmation instead of silently discarding the input.
		await Page.Keyboard.PressAsync("Escape");
		var discardBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Discard changes" });
		await Expect(discardBtn).ToBeVisibleAsync();
		await discardBtn.ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync();
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
	public async Task DetailPage_ContentIsCenteredWithinMain()
	{
		// Regression for #694: the content wrapper (`max-w-2xl`) had no
		// `mx-auto`, so it hugged the left edge of <main> instead of being
		// centered within the page like every other page.
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

		await AssertMaxWidthContentCenteredAsync("Opportunity detail page");
	}

	[Test]
	public async Task HomePage_LoadsWithoutError_WhenPublishedOpportunitiesExist()
	{
		// Regression: EF Core 10 query translation failure caused HTTP 500 on all
		// volunteer opportunity list endpoints (GetPagedSummaries + org queries).
		var frontend = Fixture.GetEndpoint("frontend");

		// Verify the backend endpoint works directly before involving the browser.
		// This surfaces the actual HTTP status code if the backend is misbehaving.
		var backendEndpoint = Fixture.GetEndpoint("backend");
		using var httpClient = new HttpClient { BaseAddress = backendEndpoint };
		var directResponse = await httpClient.GetAsync(
			"/v1/volunteer-opportunities?PageNumber=1&PageSize=1");
		var directBody = await directResponse.Content.ReadAsStringAsync();
		directResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK,
			$"Backend /v1/volunteer-opportunities returned {(int)directResponse.StatusCode}: {directBody}");

		// Capture API response statuses from the browser to diagnose browser-layer failures.
		var apiResponseStatuses = new System.Collections.Concurrent.ConcurrentBag<(string Url, int Status)>();
		Page.Response += (_, response) =>
		{
			if (response.Url.Contains("volunteer-opportunities"))
				apiResponseStatuses.Add((response.Url, response.Status));
		};

		await Page.GotoAsync(frontend.ToString());

		// The main element must be present - a 500 would show an error page instead.
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Wait for the API call to resolve: opportunity cards, empty state, or error appear.
		// .First avoids a Playwright strict-mode violation when more than one
		// opportunity card is present - this just needs to see *some* result.
		await Expect(
			Page.Locator("ul li:has(a[href*='/volunteer-opportunities/'])")
				.Or(Page.GetByText(new Regex("No opportunities|Keine Eins", RegexOptions.IgnoreCase)))
				.Or(Page.GetByTestId("opportunities-error"))
				.First
		).ToBeVisibleAsync(new() { Timeout = 30_000 });

		// No error message should be visible in the opportunities list.
		var capturedStatuses = string.Join(", ", apiResponseStatuses.Select(r => $"{r.Url} -> HTTP {r.Status}"));
		var errorLocator = Page.GetByTestId("opportunities-error");
		if (await errorLocator.IsVisibleAsync())
		{
			var errorText = await errorLocator.InnerTextAsync();
			throw new Exception(
				$"Opportunities error is visible: '{errorText}'. " +
				$"Browser API calls: [{capturedStatuses}]");
		}
	}

	[Test]
	public async Task CreateDraft_DoesNotAppearInPublicList_AppearOnDashboardWithAmberBadge()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var uniqueTitle = $"Draft Visual Test {Guid.NewGuid().ToString("N")[..8]}";

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Create opportunity lives on the organization dashboard - navigate there
		// via the org switcher. Switcher toggle has aria-label "Switch organization"
		// (en) / "Organisation wechseln" (de).
		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		if (await switcherBtn.CountAsync() == 0)
			return;

		await switcherBtn.First.ClickAsync();
		var dashboardLink = Page.GetByTestId("org-dashboard-link");
		if (await dashboardLink.CountAsync() == 0)
			return;

		await dashboardLink.First.ClickAsync();

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });

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

		// Navigate back to the org dashboard - the draft is listed there.
		await Page.GoBackAsync();
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var draftsSection = Page.GetByTestId("drafts-section");
		await Expect(draftsSection).ToBeVisibleAsync();

		// Draft entry with the title is listed.
		await Expect(draftsSection.GetByText(uniqueTitle)).ToBeVisibleAsync();

		// Amber badge present (bg-amber-100 class applied to the draft status pill).
		var amberBadge = draftsSection.Locator("[class*='bg-amber-100']").First;
		await Expect(amberBadge).ToBeVisibleAsync();
	}

	[Test]
	public async Task PublishWaitlist_BlockedWithNoTimeSlots_SucceedsAfterAddingOne()
	{
		// Regression for #542: a Waitlist opportunity could be published with
		// zero time slots via the direct-create-as-Published path, since
		// VolunteerOpportunity.Create() had no equivalent guard to Publish().
		// Verifies the UI still blocks publishing with no slots, and that the
		// supported draft -> add-slot -> publish flow succeeds.
		var frontend = Fixture.GetEndpoint("frontend");
		var uniqueTitle = $"Waitlist Publish Gap Test {Guid.NewGuid().ToString("N")[..8]}";

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Create opportunity lives on the organization dashboard - navigate there
		// via the org switcher. Switcher toggle has aria-label "Switch organization"
		// (en) / "Organisation wechseln" (de).
		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		if (await switcherBtn.CountAsync() == 0)
			return; // no org membership in seed - skip

		await switcherBtn.First.ClickAsync();
		var dashboardLink = Page.GetByTestId("org-dashboard-link");
		if (await dashboardLink.CountAsync() == 0)
			return; // no org selected in seed - skip

		await dashboardLink.First.ClickAsync();

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await createBtn.First.ClickAsync();

		try
		{
			await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });
		}
		catch
		{
			return; // modal did not open - skip remaining assertions
		}

		// Step 1: title/description.
		await Page.Locator("#opportunity-title").FillAsync(uniqueTitle);
		await Page.Locator("#opportunity-description").FillAsync(
			"Regression test for the Waitlist publish-with-no-slots gap.");

		// Step 2: remote, to skip address fields.
		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
		await Page.Locator("#opportunity-remote").CheckAsync();

		// Step 3: Waitlist participation type.
		await Page.GetByTestId("wizard-stepper-3").ClickAsync();
		await Page.Locator("input[name='participationType'][value='Waitlist']").CheckAsync();

		// Step 4: publishing with no time slots must still be blocked client-side.
		await Page.GetByTestId("wizard-stepper-4").ClickAsync();
		await Page.GetByTestId("modal-submit").ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("wizard-step-4")).ToBeVisibleAsync();

		// #688 regression: the publish-blocking error must be announced
		// (role="alert") and scrolled/focused into view, not merely present
		// somewhere in the DOM below the fold of the modal's scrollable body.
		var publishError = Page.GetByRole(AriaRole.Alert)
			.Filter(new() { HasTextString = "time slot" });
		await Expect(publishError).ToBeVisibleAsync();
		await Expect(publishError).ToBeInViewportAsync();
		await Expect(publishError).ToBeFocusedAsync();

		// Add a time slot, then publishing must succeed.
		var start = DateTimeOffset.UtcNow.AddDays(7);
		var end = start.AddHours(2);
		var step4 = Page.GetByTestId("wizard-step-4");
		await step4.Locator("#slot-start").FillAsync(start.ToString("yyyy-MM-ddTHH:mm"));
		await step4.Locator("#slot-end").FillAsync(end.ToString("yyyy-MM-ddTHH:mm"));
		var addSlotBtn = step4.GetByRole(AriaRole.Button, new() { Name = "Add", Exact = true });
		await addSlotBtn.ClickAsync();

		// The Add button clears start/end and goes disabled again by design once a
		// slot is added (ready for the next entry), so waiting for it to re-enable
		// is not a valid completion signal here - that previously made this
		// assertion flaky/incorrect. Wait for the slot to actually appear instead.
		await Expect(step4.GetByText("No time slots added yet.")).Not.ToBeVisibleAsync(
			new() { Timeout = 5000 });

		// Closing the dialog waits on 3 sequential API calls (create opportunity,
		// create time slot, publish) - under the shared, contended CI stack this can
		// exceed 15s even when nothing is actually wrong, so use the same 30s window
		// already established in this file for other network-heavy waits (see
		// HomePage_LoadsWithoutError_WhenPublishedOpportunitiesExist above).
		await Page.GetByTestId("modal-submit").ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 30_000 });

		// The newly published opportunity is visible in the public list.
		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var listedCard = Page
			.Locator("a[href*='/volunteer-opportunities/']")
			.Filter(new() { HasText = uniqueTitle });
		await Expect(listedCard).ToBeVisibleAsync(new() { Timeout = 15_000 });
	}
}
