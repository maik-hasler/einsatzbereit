using System.Net.Http.Json;
using System.Text.Json;
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
		await Expect(heading).ToHaveCSSAsync("text-align", "center");

		// Subtitle line is present below the heading.
		await Expect(Page.GetByText(new Regex("lend a hand", RegexOptions.IgnoreCase)))
			.ToBeVisibleAsync();

		// If opportunities are seeded, each card carries the redesigned visuals:
		// a clickable organisation link and the brand-gradient category banner.
		var firstCard = Page
			.Locator("ul li:has(a[href*='/volunteer-opportunities/'])")
			.First;
		Skip.When(await firstCard.CountAsync() == 0, "no opportunities seeded - skip card-specific checks");

		await Expect(firstCard.Locator("a[href*='/organizations/']"))
			.ToBeVisibleAsync();
		await Expect(firstCard.Locator("[class*='from-brand-500']")).Not.ToHaveCountAsync(0);
	}

	[Test]
	public async Task CreateWizard_HasStepperFreeNavigationAndDraftButton()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await createBtn.First.ClickAsync();

		var dialog = Page.Locator("[role='dialog']");
		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });

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

		// Step 2 hint card present. Selects on data-testid rather than the
		// bg-brand-50 Tailwind utility class - see #1328. That class match
		// was never actually anchored to the hint card: the remote-checkbox
		// label a few lines above it in LocationStep.tsx also carries
		// "bg-brand-50" (as part of hover:bg-brand-50/has-[:checked]:bg-brand-50)
		// and always renders, so `[class*='bg-brand-50']` silently matched
		// that label instead - passing regardless of remote state. The hint
		// card itself only renders when not remote, so "remote" (checked
		// above to skip step 2's address validation) must be unchecked
		// again first for this to assert against the real element.
		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
		await Page.Locator("#opportunity-remote").UncheckAsync();
		var hint = Page.GetByTestId("wizard-step-2").GetByTestId("location-hint");
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
		Skip.When(await firstCard.CountAsync() == 0, "no opportunities seeded, skip");

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity card link had no href");

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
	public async Task DetailPage_AnonymousVisitor_SeesPrimarySignInButton()
	{
		// Regression for #979: the anonymous sign-up CTA used to be an
		// underlined text link inside a grey notice, with less visual weight
		// than the Share button beside the title. It must now use the shared
		// primary Button component (solid brand background), matching the
		// prominence of the signed-in sign-up CTA below it.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		Skip.When(await firstCard.CountAsync() == 0, "no opportunities seeded, skip");

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity card link had no href");

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var signInBtn = Page.GetByTestId("opportunity-signin");
		await Expect(signInBtn).ToBeVisibleAsync();
		await Expect(signInBtn).ToHaveTextAsync("Sign in");

		// The CTA must use the shared primary Button styling (bg-brand-700), not
		// a bare underlined text link.
		await Expect(signInBtn).ToContainClassAsync("bg-brand-700");
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
		Skip.When(await firstCard.CountAsync() == 0, "no opportunities seeded, skip");

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity card link had no href");

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertMaxWidthContentCenteredAsync("Opportunity detail page");
	}

	[Test]
	public async Task DetailPage_ShowsAboutOrganizationCard_SocialProofStat_AndMoreFromOrgTeaser()
	{
		// Issue #759: the detail page adds four frontend-only enrichment
		// sections so it stays substantial even when an organizer writes a
		// short description - an "About this organization" card (reusing the
		// org's already-public contact info), a participant-count social-proof
		// stat, a "more from this organization" teaser capped at 3 and
		// excluding the opportunity being viewed, and a "posted X days ago"
		// freshness line.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgName = $"Detail Enrichment Org {suffix}";
		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = orgName });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var contactEmail = $"contact-{suffix}@example.test";
		var updateResponse = await http.PutAsJsonAsync($"/v1/organizations/{organizationId}", new
		{
			name = orgName,
			description = "We coordinate volunteers across the region for issue 759 coverage.",
			contactEmail,
			contactPhone = "+49 555 0100",
			website = "https://example.test",
			address = new { street = "Teststrasse", houseNumber = "1", zipCode = "12345", city = "Musterstadt" },
		});
		updateResponse.EnsureSuccessStatusCode();

		async Task<(string Id, string Title)> CreateOpportunityAsync(string label)
		{
			var title = $"{label} {suffix}";
			var response = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				title,
				description = $"{label} opportunity for detail enrichment coverage.",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "IndividualContact",
				checkInMethod = "None",
				isDraft = false,
			});
			response.EnsureSuccessStatusCode();
			var body = await response.Content.ReadFromJsonAsync<JsonElement>();
			return (body.GetProperty("id").GetString()!, title);
		}

		// Created sequentially: Primary first (oldest), then Other1..Other4
		// (newest last). GetPublicOrganizationProfile orders by CreatedOn
		// descending, so after excluding Primary and capping at 3, the teaser
		// should show Other4/Other3/Other2 and drop Other1 (the oldest "other").
		var (primaryId, primaryTitle) = await CreateOpportunityAsync("Primary");
		var others = new List<(string Id, string Title)>();
		for (var i = 1; i <= 4; i++)
			others.Add(await CreateOpportunityAsync($"Other{i}"));

		// Vera engages with the primary opportunity so currentParticipantCount > 0.
		var veraToken = (await Fixture.SignInAsync("vera", "vera123")).AccessToken;
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraToken}");
		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{primaryId}/engagements",
			new { message = "Signing up for issue 759 detail enrichment coverage." });
		engagementResponse.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{primaryId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Freshness line - "Posted today"/"Posted X days ago" both contain "Posted".
		await Expect(Page.GetByText(new Regex("posted", RegexOptions.IgnoreCase)))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Social proof: Vera's engagement is reflected as a participant-count stat.
		await Expect(Page.GetByText(new Regex(@"1\s+person", RegexOptions.IgnoreCase)))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		// About this organization: description + all contact fields surfaced.
		var aboutOrg = Page.GetByTestId("about-organization");
		await Expect(aboutOrg).ToBeVisibleAsync();
		await Expect(aboutOrg.GetByRole(AriaRole.Link, new() { Name = contactEmail }))
			.ToBeVisibleAsync();
		await Expect(aboutOrg.GetByText("Teststrasse 1, 12345 Musterstadt")).ToBeVisibleAsync();

		// More from this organization: capped at 3, excludes the opportunity being viewed.
		var teaser = Page.GetByTestId("more-from-organization");
		await Expect(teaser).ToBeVisibleAsync();
		await Expect(teaser.Locator("li")).ToHaveCountAsync(3);
		await Expect(teaser.GetByText(primaryTitle)).Not.ToBeVisibleAsync();
		await Expect(teaser.GetByText(others[0].Title)).Not.ToBeVisibleAsync(); // oldest "other", pushed out by the cap
		foreach (var other in others.Skip(1))
			await Expect(teaser.GetByText(other.Title)).ToBeVisibleAsync();
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
	public async Task CreateDraft_DoesNotAppearInPublicList_AppearsOnOpportunitiesTabWithAmberBadge()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var uniqueTitle = $"Draft Visual Test {Guid.NewGuid().ToString("N")[..8]}";

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await createBtn.First.ClickAsync();

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });

		// Fill title (minimum required for draft save).
		await Page.Locator("#opportunity-title").FillAsync(uniqueTitle);

		// Save as draft - this now routes to the Opportunities tab, where drafts
		// live (they no longer sit above the calendar).
		await Page.GetByTestId("modal-save-draft").ClickAsync();
		await Expect(Page).ToHaveURLAsync(new Regex(@"/opportunities"), new() { Timeout = 30_000 });

		var draftsSection = Page.GetByTestId("drafts-section");
		await Expect(draftsSection).ToBeVisibleAsync();
		await Expect(draftsSection.GetByText(uniqueTitle)).ToBeVisibleAsync();

		// Draft status pill present - selects on data-testid and asserts the
		// draft-specific label rather than matching the bg-amber-100 Tailwind
		// utility class directly, which a cosmetic restyle would otherwise
		// silently break (see #1328).
		var statusBadge = draftsSection.GetByTestId("opportunity-status-badge").First;
		await Expect(statusBadge).ToHaveTextAsync("Draft");

		// The public home page must NOT show the draft.
		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Filter the <li> card, not the stretched <a> overlay - the card link
		// carries the title only as aria-label (empty text content), so
		// HasText never matches it. The <li> contains the visible <h3> title.
		var draftInPublicList = Page
			.Locator("ul li:has(a[href*='/volunteer-opportunities/'])")
			.Filter(new() { HasText = uniqueTitle });
		await Expect(draftInPublicList).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task SaveDraft_RoutesToOpportunitiesTab_ToastAndHighlight()
	{
		// Regression for #708: after saving a new opportunity as a draft from the
		// Calendar tab, the organizer could not tell where the draft landed.
		// Drafts now live on the Opportunities tab; saving one routes there, the
		// toast names that tab, and the just-saved draft is highlighted.
		var frontend = Fixture.GetEndpoint("frontend");
		var uniqueTitle = $"Draft Discoverability Test {Guid.NewGuid().ToString("N")[..8]}";

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });

		await Page.Locator("#opportunity-title").FillAsync(uniqueTitle);
		await Page.GetByTestId("modal-save-draft").ClickAsync();

		// Saving a draft routes to the Opportunities tab.
		await Expect(Page).ToHaveURLAsync(new Regex(@"/opportunities"), new() { Timeout = 30_000 });

		// The success toast now names the Opportunities tab, instead of the old
		// vague "on your organization dashboard" copy.
		var toast = Page.GetByRole(AriaRole.Alert)
			.Filter(new() { HasTextString = "Opportunities" });
		await Expect(toast).ToBeVisibleAsync();

		// The just-saved draft is highlighted so it is easy to spot.
		var draftsSection = Page.GetByTestId("drafts-section");
		await Expect(draftsSection).ToBeVisibleAsync();

		var highlighted = draftsSection.Locator("li[data-highlighted='true']");
		await Expect(highlighted).ToBeVisibleAsync();
		await Expect(highlighted).ToContainTextAsync(uniqueTitle);
	}

	[Test]
	public async Task EditWizard_ReopenedDraft_ShowsSaveAsDraftAndAcceptsPartialSave()
	{
		// Regression for #707: reopening a saved draft via "Edit" hid the
		// "Save as draft" action entirely (gated on create-vs-edit mode
		// instead of the opportunity's actual Draft/Published status), so an
		// organizer could not persist further incremental edits without
		// first satisfying full publish-level validation.
		var frontend = Fixture.GetEndpoint("frontend");
		var uniqueTitle = $"Edit Draft Visual Test {Guid.NewGuid().ToString("N")[..8]}";

		// "Switch organization" only exists inside the org app shell
		// (/app/{orgId}/...), never on the plain home page AuthHelper leaves
		// you on - go straight to olaf's pinned org dashboard instead of
		// looking for a control that can never render here.
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });

		// Deliberately leave the draft incomplete - only a title, no address,
		// no description - the exact situation "save as draft" exists for.
		await Page.Locator("#opportunity-title").FillAsync(uniqueTitle);
		await Page.GetByTestId("modal-save-draft").ClickAsync();

		// Saving routes to the Opportunities tab, where the draft is listed.
		await Expect(Page).ToHaveURLAsync(new Regex(@"/opportunities"), new() { Timeout = 30_000 });

		var draftsSection = Page.GetByTestId("drafts-section");
		await Expect(draftsSection).ToBeVisibleAsync();

		// Reopen the draft's edit wizard directly from the list row (inline edit).
		var draftRow = draftsSection.Locator("li", new() { HasText = uniqueTitle });
		await Expect(draftRow).ToBeVisibleAsync();
		await draftRow.GetByTestId("opportunity-edit").ClickAsync();

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 10_000 });

		// The #707 regression: this action must be available in edit mode too,
		// since the opportunity being edited is still a Draft.
		var saveDraftBtn = Page.GetByTestId("modal-save-draft");
		await Expect(saveDraftBtn).ToBeVisibleAsync();

		// Make an incremental edit - still no address, still no time slots -
		// and persist it via "Save as draft" rather than the strict "Save".
		var updatedTitle = $"{uniqueTitle} Updated";
		await Page.Locator("#opportunity-title").FillAsync(updatedTitle);
		await saveDraftBtn.ClickAsync();

		// A lenient partial save must succeed without full-publish validation
		// blocking it - the dialog closes and no validation error is shown.
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The edit persisted - the Opportunities list reloads and reflects the
		// new title.
		await Expect(draftsSection.GetByText(updatedTitle)).ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	[Test]
	public async Task OpportunitiesHub_ShowsDraftAndPublished_AndPublishesDraftInline()
	{
		// The org "Engagements" tab is now the unified "Opportunities" hub: it
		// lists every opportunity grouped by status (Draft / Published), lets the
		// organizer publish a draft straight from the list, and reflects the move.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < localStorage.length; i++) {
				const key = localStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(localStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in localStorage after login");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"VisualOppHub {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var draftTitle = $"Hub Draft {suffix}";
		var publishedTitle = $"Hub Published {suffix}";

		var draftResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = draftTitle,
			description = "Seeded draft for OpportunitiesHub test",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			isDraft = true,
		});
		draftResponse.EnsureSuccessStatusCode();

		var publishedResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = publishedTitle,
			description = "Seeded published for OpportunitiesHub test",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			isDraft = false,
		});
		publishedResponse.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var draftsSection = Page.GetByTestId("drafts-section");
		var publishedSection = Page.GetByTestId("published-section");

		// Both statuses are visible in one place, each under its own heading.
		await Expect(draftsSection.GetByText(draftTitle)).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(publishedSection.GetByText(publishedTitle)).ToBeVisibleAsync();

		// Publish the draft directly from the list (no slots needed for an
		// IndividualContact opportunity).
		var draftRow = draftsSection.Locator("li", new() { HasText = draftTitle });
		await draftRow.GetByTestId("opportunity-publish").ClickAsync();

		// It moves out of Drafts and into the Published section.
		await Expect(publishedSection.GetByText(draftTitle)).ToBeVisibleAsync(new() { Timeout = 15_000 });
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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await createBtn.First.ClickAsync();

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });

		// Step 1: title/description.
		await Page.Locator("#opportunity-title").FillAsync(uniqueTitle);
		await Page.Locator("#opportunity-description").FillAsync(
			"Regression test for the Waitlist publish-with-no-slots gap.");

		// Step 2: remote, to skip address fields.
		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
		await Page.Locator("#opportunity-remote").CheckAsync();

		// Step 3: Waitlist participation type. Click the visible label card, not
		// the sr-only radio <input>, which is not a reliable pointer target.
		await Page.GetByTestId("wizard-stepper-3").ClickAsync();
		await Page.Locator("label:has(input[name='participationType'][value='Waitlist'])").ClickAsync();

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

		// The newly published opportunity is visible in the public list. Filter
		// the <li> card, not the stretched <a> overlay - the card link carries
		// the title only as aria-label (empty text content), so HasText never
		// matches it; the <li> contains the visible <h3> title. Keep the 30s
		// window: under the shared, contended CI stack the listing can lag
		// behind the publish call by more than 15s even when nothing is wrong.
		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var listedCard = Page
			.Locator("ul li:has(a[href*='/volunteer-opportunities/'])")
			.Filter(new() { HasText = uniqueTitle });
		await Expect(listedCard).ToBeVisibleAsync(new() { Timeout = 30_000 });
	}

	[Test]
	public async Task DetailPage_ClearsStaleError_AfterClientSideNavigationToAnotherOpportunity()
	{
		// Regression for #1223: load() reset `loading` but never reset `error`,
		// so once one opportunity failed to load, the ErrorBanner stayed pinned
		// over every opportunity visited afterwards - render checks `error`
		// before `opportunity`, and the component instance is reused across
		// /volunteer-opportunities/:opportunityId route changes (no remount),
		// so a stale error from a previous id was never cleared by a later
		// successful load.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Stale Error Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		async Task<(string Id, string Title)> CreateOpportunityAsync(string label)
		{
			var title = $"{label} {suffix}";
			var response = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				title,
				description = $"{label} opportunity for issue 1223 stale-error coverage.",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "IndividualContact",
				checkInMethod = "None",
				isDraft = false,
			});
			response.EnsureSuccessStatusCode();
			var body = await response.Content.ReadFromJsonAsync<JsonElement>();
			return (body.GetProperty("id").GetString()!, title);
		}

		var (idA, titleA) = await CreateOpportunityAsync("Sibling A");
		var (idB, titleB) = await CreateOpportunityAsync("Sibling B");

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{idA}");
		await Expect(Page.Locator("h1").First).ToHaveTextAsync(titleA, new() { Timeout = 15_000 });

		var teaser = Page.GetByTestId("more-from-organization");
		var siblingLink = teaser.GetByRole(AriaRole.Link, new() { Name = titleB });
		await Expect(siblingLink).ToBeVisibleAsync();

		// Force exactly the next request for opportunity B to fail, simulating
		// a broken/deleted listing - the failure itself is expected behavior
		// here, not the bug under test.
		var failedOnce = false;
		await Page.RouteAsync($"**/v1/volunteer-opportunities/{idB}", async route =>
		{
			if (failedOnce)
			{
				await route.ContinueAsync();
				return;
			}
			failedOnce = true;
			await route.FulfillAsync(new()
			{
				Status = 500,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.6.1\",\"status\":500}",
			});
		});

		// Client-side navigation (SPA <Link>, no full page reload) into the
		// failing opportunity.
		await siblingLink.ClickAsync();
		var errorBanner = Page.Locator("[role='alert'][aria-live='assertive']");
		await Expect(errorBanner).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Client-side back navigation to opportunity A - same mounted component,
		// no full page reload - must show A's content cleanly, without the
		// stale error banner pinned from B's earlier failed load.
		await Page.GoBackAsync();
		await Expect(Page.Locator("h1").First).ToHaveTextAsync(titleA, new() { Timeout = 15_000 });
		await Expect(errorBanner).Not.ToBeVisibleAsync();
	}
}
