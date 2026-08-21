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

	/// <summary>
	/// Stays end-to-end: <c>updateFilter</c> rebuilds its params from
	/// <c>window.location.search</c> rather than the functional
	/// <c>setSearchParams(prev =&gt; ...)</c> its siblings use, so a second
	/// filter write only survives under a router that writes through to
	/// <c>window.location</c>. Tracked as einsatzbereit#2157.
	/// </summary>
	[Test]
	public async Task MultipleFilters_AllReflectedInUrl()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("filter-frequency").ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "One-time" }).ClickAsync();
		await Page.GetByTestId("filter-type").ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Scheduled slots" }).ClickAsync();

		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*occurrence=OneTime"));
		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*participationType=ScheduledSlots"));
	}

	[Test]
	public async Task FrequencyFilter_PanelStaysBelowHeader()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("filter-frequency").ClickAsync();
		var oneTimeOption = Page.GetByRole(AriaRole.Button, new() { Name = "One-time" });
		await Expect(oneTimeOption).ToBeVisibleAsync();

		// The panel's own ancestors (the filter bar, <main>) are all
		// unpositioned, so its z-index competes directly with Header.tsx's
		// sticky z-40 at the document root instead of nesting inside it - pin
		// it below the header rather than the old z-[200] that painted over
		// it.
		var panel = oneTimeOption.Locator("xpath=..");
		await Expect(panel).ToHaveCSSAsync("z-index", "30");
	}

	[Test]
	public async Task DetailPage_ShowsHomeLink_AndNoShareButton()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities");
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		// Seed data always publishes opportunities - a non-waiting
		// CountAsync() right after the h1 check above raced the list's
		// opportunity fetch (h1 paints before the list leaves its loading
		// skeleton) and could silently skip this test instead of failing.
		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		await Expect(firstCard).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity card link had no href");

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// No breadcrumb bar and no in-page "Home" link: a link home is a site
		// destination rather than a property of this page, so it lives in the
		// header nav, reachable everywhere, instead of being restated in each
		// page's hero.
		await Expect(Page.Locator("nav[aria-label='Breadcrumb']")).ToHaveCountAsync(0);
		await Expect(Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToHaveCountAsync(0);
		await Expect(Page.GetByTestId("nav-home")).ToBeVisibleAsync();

		// No Share button: every browser and OS this page runs on already offers
		// sharing the current URL, so an in-page duplicate only spends room in the
		// action row. Pinned by test id so a re-introduction fails here rather
		// than shipping unnoticed.
		await Expect(Page.GetByTestId("share-opportunity")).ToHaveCountAsync(0);

		// The action row still renders for an anonymous visitor: Report is
		// reachable without being signed in (clicking it redirects to sign-in
		// instead of the control being hidden entirely, #2061) - Share is the
		// only thing genuinely gone from it.
		await Expect(Page.GetByTestId("opportunity-detail-actions")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("report-opportunity")).ToBeVisibleAsync();
	}

	[Test]
	public async Task DetailPage_ContentIsCenteredWithinMain()
	{
		// The content wrapper (`max-w-2xl`) had no
		// `mx-auto`, so it hugged the left edge of <main> instead of being
		// centered within the page like every other page.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities");
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		// Seed data always publishes opportunities - a non-waiting
		// CountAsync() right after the h1 check above raced the browse page's
		// opportunity fetch (h1 paints before the list leaves its loading
		// skeleton) and could silently skip this test instead of failing.
		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		await Expect(firstCard).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity card link had no href");

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertMaxWidthContentCenteredAsync("Opportunity detail page");
	}

	[Test]
	public async Task DetailPage_ShowsAboutOrganizationCard_SocialProofStat_AndMoreFromOrgTeaser()
	{
		// Four frontend-only enrichment sections keep the detail page substantial
		// even when an organizer writes a short description: an "About this
		// organization" card (reusing already-public contact info), a
		// participant-count stat, a "more from this organization" teaser capped at
		// 3 and excluding the opportunity being viewed, and a "posted X days ago"
		// freshness line.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgName = $"Detail Enrichment Org {suffix}";
		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = orgName });
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
				titleDe = title,
				descriptionDe = $"{label} opportunity for detail enrichment coverage.",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "IndividualContact",
				checkInMethod = "None",
				validUntil = DateTimeOffset.UtcNow.AddDays(30),
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
	public async Task EditWizard_ReopenedDraft_ShowsSaveAsDraftAndAcceptsPartialSave()
	{
		// Reopening a saved draft via "Edit" hid the
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
		await OpportunityRowHelper.ClickActionAsync(draftRow, "opportunity-edit");

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 10_000 });

		// This action must be available in edit mode too, since the opportunity
		// being edited is still a Draft.
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
	public async Task PublishScheduledSlots_BlockedWithNoTimeSlots_SucceedsAfterAddingOne()
	{
		// A ScheduledSlots opportunity could be published with
		// zero time slots via the direct-create-as-Published path, since
		// VolunteerOpportunity.Create() had no equivalent guard to Publish().
		// Verifies the UI still blocks publishing with no slots, and that the
		// supported draft -> add-slot -> publish flow succeeds.
		var frontend = Fixture.GetEndpoint("frontend");
		var uniqueTitle = $"ScheduledSlots Publish Gap Test {Guid.NewGuid().ToString("N")[..8]}";

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await createBtn.First.ClickAsync();

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });

		// Step 1: title/description.
		await Page.Locator("#opportunity-title").FillAsync(uniqueTitle);
		await Page.Locator("#opportunity-description").FillAsync(
			"Regression test for the ScheduledSlots publish-with-no-slots gap.");

		// Step 2: remote, to skip address fields.
		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
		await Page.Locator("#opportunity-remote").CheckAsync();

		// Step 3: ScheduledSlots participation type. Click the visible label card, not
		// the sr-only radio <input>, which is not a reliable pointer target.
		await Page.GetByTestId("wizard-stepper-3").ClickAsync();
		await Page.Locator("label:has(input[name='participationType'][value='ScheduledSlots'])").ClickAsync();

		// Step 4: publishing with no time slots must still be blocked client-side.
		await Page.GetByTestId("wizard-stepper-4").ClickAsync();
		await Page.GetByTestId("modal-submit").ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("wizard-step-4")).ToBeVisibleAsync();

		// The publish-blocking error must be announced (role="alert") and
		// scrolled/focused into view, not merely present in the DOM below the fold
		// of the modal's scrollable body.
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
		// matches it; the <li> contains the visible <h2> title. Keep the 30s
		// window: under the shared, contended CI stack the listing can lag
		// behind the publish call by more than 15s even when nothing is wrong.
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var listedCard = Page
			.Locator("ul li:has(a[href*='/volunteer-opportunities/'])")
			.Filter(new() { HasText = uniqueTitle });
		await Expect(listedCard).ToBeVisibleAsync(new() { Timeout = 30_000 });
	}

	[Test]
	public async Task DetailPage_ClearsStaleError_AfterClientSideNavigationToAnotherOpportunity()
	{
		// Load() reset `loading` but never reset `error`,
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

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"Stale Error Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		async Task<(string Id, string Title)> CreateOpportunityAsync(string label)
		{
			var title = $"{label} {suffix}";
			var response = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				titleDe = title,
				descriptionDe = $"{label} opportunity for issue 1223 stale-error coverage.",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "IndividualContact",
				checkInMethod = "None",
				validUntil = DateTimeOffset.UtcNow.AddDays(30),
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

	[Test]
	public async Task DetailPage_OwnerViewingOwnPublishedOpportunity_HidesDraftBadgeAndPublishEditActions()
	{
		// The draft affordances are gated on isDraft && isOwner, not isOwner alone
		// - an organizer viewing their own already-published opportunity must see
		// neither the draft badge nor the Edit/Publish actions.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var suffix = Guid.NewGuid().ToString("N")[..8];
		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"Detail Published Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var publishedTitle = $"Detail Published Test {suffix}";
		var response = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = publishedTitle,
			descriptionDe = "Seeded published opportunity for the detail-page owner-affordances edge case.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		response.EnsureSuccessStatusCode();
		var opportunity = await response.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Expect(Page.Locator("h1").First).ToHaveTextAsync(publishedTitle, new() { Timeout = 15_000 });

		await Expect(Page.GetByTestId("opportunity-detail-draft-badge")).Not.ToBeVisibleAsync();
		await Expect(Page.GetByTestId("opportunity-detail-edit")).Not.ToBeVisibleAsync();
		await Expect(Page.GetByTestId("opportunity-detail-publish")).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task ListCard_TagChips_AreClickableLinks_SwitchTagFilterAndSurviveSpecialCharacters()
	{
		// Companion to the detail page's own tag-chip case, which #2148 moved
		// down to VolunteerOpportunityDetailPage.test.tsx: list cards must
		// expose the same clickable tag chips, since
		// that's where most volunteers actually browse before ever opening a
		// detail page. Also covers two edge cases: an opportunity with more
		// than one tag renders a distinct chip per tag, and a tag containing
		// URL-unsafe characters (space, "&") round-trips correctly through
		// encodeURIComponent on the frontend and the exact-match filter on
		// the backend.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"List Tag Chip Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var tagA = $"listtagchip-{suffix}";
		var tagB = $"list tag & chip {suffix}";
		var title = $"List Tag Chip Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by ListCard_TagChips_AreClickableLinks_SwitchTagFilterAndSurviveSpecialCharacters",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
			tags = new[] { tagA, tagB },
		});
		oppResponse.EnsureSuccessStatusCode();

		// Land on the list already filtered by tagA, so the card is visible
		// without depending on how many other opportunities are seeded.
		await Page.GotoAsync($"{origin}/opportunities?tag={Uri.EscapeDataString(tagA)}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var card = Page.Locator("ul li:has(a[href*='/volunteer-opportunities/'])").Filter(new() { HasText = title });
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Both tags render as their own chip on the card.
		await Expect(card.GetByRole(AriaRole.Link, new() { Name = $"Filter by tag: {tagA}" })).ToBeVisibleAsync();
		var tagBChip = card.GetByRole(AriaRole.Link, new() { Name = $"Filter by tag: {tagB}" });
		await Expect(tagBChip).ToBeVisibleAsync();

		// Clicking the second tag switches the filter entirely (tagA drops
		// out of the URL) and the special characters survive the round trip.
		// A Regex (like every other ToHaveURLAsync in this file) rather than
		// a plain string: the string overload kept failing this assertion
		// even at a generous 15s timeout with an actual URL that printed
		// identically to the expected one - some exact-match quirk specific
		// to percent-encoded reserved characters (%20/%26) in this Playwright
		// binding. The Regex path is what every passing URL assertion here
		// already uses.
		await tagBChip.ClickAsync();
		await Expect(Page).ToHaveURLAsync(
			new Regex($"^{Regex.Escape($"{origin}/opportunities?tag={Uri.EscapeDataString(tagB)}")}$"),
			new() { Timeout = 15_000 });
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });
	}
}
