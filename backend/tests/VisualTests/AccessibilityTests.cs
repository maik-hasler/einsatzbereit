using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AccessibilityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Axe reports "page has no h1" (page-has-heading-one) and most
	// landmark-structure defects at "moderate" impact, which the
	// serious/critical filter below would let through. Escalate just these
	// rule IDs rather than every moderate violation, which would also flag
	// color-contrast-enhanced noise unrelated to this gate's purpose.
	private static readonly string[] EscalatedModerateRuleIds =
	[
		"page-has-heading-one",
		"heading-order",
		"landmark-one-main",
		"landmark-banner-is-top-level",
		"landmark-complementary-is-top-level",
		"landmark-contentinfo-is-top-level",
		"landmark-main-is-top-level",
		"landmark-no-duplicate-banner",
		"landmark-no-duplicate-contentinfo",
		"landmark-no-duplicate-main",
		"landmark-unique",
		// "region" (also moderate) is deliberately NOT escalated: axe's region
		// rule flags any visible content outside a landmark, and ToastContext.tsx
		// mounts its toast list at the app root, outside AppLayout's <main>.
		// Escalating it would fail every scan that catches a toast or similar
		// page-root overlay mid-render.
	];

	private static void AssertNoViolations(AxeResult result)
	{
		var violations = result.Violations
			.Where(v => v.Impact is "serious" or "critical"
				|| (v.Impact is "moderate" && EscalatedModerateRuleIds.Contains(v.Id)))
			.ToList();

		if (violations.Count == 0)
			return;

		var summary = string.Join("\n", violations.Select(v =>
			$"[{v.Impact}] {v.Id}: {v.Description}\n" +
			string.Join("\n", v.Nodes.Select(n => $"  - {n.Html}"))));

		throw new Exception($"Axe found {violations.Count} a11y violation(s):\n{summary}");
	}

	[Test]
	public async Task HomePage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task HomePage_SkipLink_MovesFocusToMainContent()
	{
		// Neither layout had a bypass mechanism - a keyboard
		// user had to tab through the entire header (brand link, nav links,
		// language selector, sign-in/register) on every single page before this.
		// The skip link is the first child in the DOM (before <Header>), so it
		// must also be the very first Tab stop.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.Keyboard.PressAsync("Tab");
		var skipLink = Page.GetByRole(AriaRole.Link, new() { Name = "Skip to content" });
		await Expect(skipLink).ToBeFocusedAsync();

		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.Locator("#main-content")).ToBeFocusedAsync();
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		// Wait for opportunity cards (not footer links which also match ul>li a)
		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		try
		{
			await firstCard.WaitForAsync(new() { Timeout = 15_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("no opportunities seeded");
		}

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity card had no href attribute");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_SignedInNonOwner_AsVera_HasNoSeriousA11yViolations()
	{
		// The action row above the at-a-glance panel renders conditionally, so
		// the signed-in-non-owner state - the row holding nothing but Report -
		// is the only render path of it the anonymous scan above cannot reach.
		// Vera is a plain user, never an organizer, so isOwner is false for
		// every opportunity.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/opportunities");
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		// Same card-link locator (and skip-on-empty handling) as the anonymous
		// scan above: footer links also match a bare ul>li a.
		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		try
		{
			await firstCard.WaitForAsync(new() { Timeout = 15_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("no opportunities seeded");
		}

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity card had no href attribute");

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Proves the scan below actually saw the state this test exists for,
		// rather than passing against a page where the row never rendered.
		await Expect(Page.GetByTestId("report-opportunity")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_SignUpCta_IsReachableBeforeReportButton()
	{
		// #2050: the desktop sticky rail holding the primary sign-up CTA was the
		// last child of its two-column grid (put there for #1755's layout), even
		// though CSS placed it visually in the right-hand column - so a keyboard
		// user had to tab through the report button, the map, its Leaflet
		// attribution links and the organization's contact links (7 stops) before
		// ever reaching the CTA. Fixed by making the <aside> the *first* child of
		// the grid and pinning both children back to their original visual
		// column/row with explicit grid placement (lg:col-start-*/lg:row-start-*),
		// decoupling DOM/focus order from visual order. Axe has no rule for a
		// DOM-order-vs-visual-order mismatch (WCAG 2.4.3 Focus Order is exactly
		// the kind of thing axe-core documents as untestable), so this asserts
		// the fact directly the same way HomePage_SkipLink_MovesFocusToMainContent
		// above does for the skip link.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new
		{
			name = $"A11y Focus Order Org {suffix}",
			contactEmail = "contact@example.org",
			contactPhone = "+49 30 1234567",
			website = "https://example.org",
		});
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"A11y Focus Order Test {suffix}",
			descriptionDe = "Seeded for #2050 focus-order coverage.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var start = DateTimeOffset.UtcNow.AddDays(5);
		(await http.PostAsJsonAsync($"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
		{
			startDateTime = start,
			endDateTime = start.AddHours(2),
			maxParticipants = 5,
			recurrenceCount = 1,
		})).EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Proves the states this regression depends on actually rendered: the
		// report button (Vera is signed in and not the owner) and the sign-up CTA.
		await Expect(Page.GetByTestId("report-opportunity")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var ctaButton = Page.GetByTestId("signup-cta").GetByRole(AriaRole.Button);
		await Expect(ctaButton).ToBeVisibleAsync();

		// Same skip-link jump as HomePage_SkipLink_MovesFocusToMainContent, so
		// the count below starts from <main> rather than depending on how many
		// links the Header itself happens to have.
		await Page.Keyboard.PressAsync("Tab");
		var skipLink = Page.GetByRole(AriaRole.Link, new() { Name = "Skip to content" });
		await Expect(skipLink).ToBeFocusedAsync();
		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.Locator("#main-content")).ToBeFocusedAsync();

		// First stop inside <main> is the organization link in the page's
		// eyebrow; the second must be the sign-up CTA now that the rail is the
		// grid's first child - not the report button, which sits later in the
		// reading column.
		await Page.Keyboard.PressAsync("Tab");
		await Page.Keyboard.PressAsync("Tab");
		await Expect(ctaButton).ToBeFocusedAsync();
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_OwnerDraft_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The draftBadge chip plus owner-only Edit/Publish actions
		// (isDraft && isOwner) are new interactive elements this page never
		// rendered before - VolunteerOpportunityDetailPage_HasNoSeriousA11yViolations
		// above only ever reaches the anonymous/non-owner render path via a
		// home-page card link, so it can never exercise this state.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in sessionStorage after login");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N")[..8];
		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"A11y Draft Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var draftResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"A11y Draft Test {suffix}",
			descriptionDe = "Seeded draft for the owner-affordances a11y scan.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = true,
		});
		draftResponse.EnsureSuccessStatusCode();
		var draft = await draftResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = draft.GetProperty("id").GetString();

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Expect(Page.GetByTestId("opportunity-detail-draft-badge")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var closedResult = await Page.RunAxe();
		AssertNoViolations(closedResult);

		// The lazy-loaded Edit wizard is a distinct rendered state this page's
		// own axe coverage has never seen - scan it too, not just the trigger.
		await Page.GetByTestId("opportunity-detail-edit").ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 10_000 });

		var editModalResult = await Page.RunAxe();
		AssertNoViolations(editModalResult);
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_OwnerNotice_AsOlaf_HasNoSeriousA11yViolations()
	{
		// #2081: an owner viewing their own already-published (non-draft)
		// opportunity now renders a new "owner notice" card with a link to
		// the engagement-management view, in place of the sign-up CTA/login
		// blocks. VolunteerOpportunityDetailPage_OwnerDraft_AsOlaf above only
		// ever seeds isDraft: true, so it never reaches this !isDraft branch -
		// same rationale as that test's own comment.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var suffix = Guid.NewGuid().ToString("N")[..8];
		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"A11y Owner Notice Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var response = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"A11y Owner Notice Test {suffix}",
			descriptionDe = "Seeded published opportunity for the owner-notice a11y scan.",
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
		await Expect(Page.GetByTestId("opportunity-owner-notice")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_MobileActionRail_AsVera_HasNoSeriousA11yViolations()
	{
		// The sign-up CTA (and its deadline/status/login-prompt
		// siblings) now renders a second time - testid-suffixed "-mobile" -
		// right above the map on narrow viewports, with the desktop `<aside>`
		// hidden below `lg` instead. Every detail-page scan above runs at
		// Playwright's default 1280x720 viewport (above `lg`), so none of
		// them ever render - or scan - this new mobile-only markup.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"A11y Mobile Rail Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"A11y Mobile Rail Test {suffix}",
			descriptionDe = "Seeded for #1965 mobile action-rail a11y coverage.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.SetViewportSizeAsync(375, 812);
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Proves the scan below actually saw the new mobile-only markup,
		// rather than passing against a page where it never rendered.
		await Expect(Page.GetByTestId("signup-cta-mobile")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_ApplicationStatusWithTimeSlot_AsVera_HasNoSeriousA11yViolations()
	{
		// The sidebar "application-status" card (label + status Chip +
		// Withdraw button) now also renders the registered slot's date/time -
		// new content in a render state ("signed-in volunteer with an existing
		// engagement") none of this file's other detail-page scans ever reach,
		// since they all land on the sign-up-CTA/login-prompt/owner-draft
		// states instead. Vera is a plain user, never an organizer, so
		// isOwner is false and the status card (not the sign-up CTA) renders.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var organizerHttp = new HttpClient { BaseAddress = backend };
		organizerHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(organizerHttp, "/v1/organizations", new { name = $"A11y Status Slot Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await organizerHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"A11y Status Slot Test {suffix}",
			descriptionDe = "Seeded for #1938 application-status time-slot a11y coverage.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var start = DateTimeOffset.UtcNow.AddDays(5);
		var slotResponse = await organizerHttp.PostAsJsonAsync($"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
		{
			startDateTime = start,
			endDateTime = start.AddHours(2),
			maxParticipants = 5,
			recurrenceCount = 1,
		});
		slotResponse.EnsureSuccessStatusCode();
		var slots = await slotResponse.Content.ReadFromJsonAsync<JsonElement>();
		var timeSlotId = slots[0].GetProperty("id").GetString();

		(await organizerHttp.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		var veraToken = (await Fixture.SignInAsync("vera", "vera123")).AccessToken;
		using var volunteerHttp = new HttpClient { BaseAddress = backend };
		volunteerHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraToken}");

		var engagementResponse = await volunteerHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { type = "ScheduledSlots", timeSlotId, message = (string?)null });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		(await organizerHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Proves the scan below actually saw the new date/time row, rather than
		// passing against a page where it never rendered.
		await Expect(Page.GetByTestId("application-status").GetByText("Scheduled:")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_ApplicationStatusCheckedIn_AsVera_HasNoSeriousA11yViolations()
	{
		// Once the engagement is checked in, the application-status card swaps
		// Withdraw for a "Checked in" Chip (Engagement.Withdraw's IsCheckedIn
		// guard would 409 otherwise) - a render state no other detail-page scan
		// in this file reaches, since none of them check anyone in.
		var (_, opportunityId, engagementId) =
			await SeedConfirmedEngagementAsync("Manual", "DetailCheckedInA11y");
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");
		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/check-in", null)).EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Proves the scan below actually saw the checked-in state, rather than
		// passing against a page where the Withdraw button just never rendered
		// for an unrelated reason.
		var statusCard = Page.GetByTestId("application-status");
		await Expect(statusCard.GetByText("Checked in")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(statusCard.GetByRole(AriaRole.Button, new() { Name = "Withdraw" }))
			.Not.ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_MapMarker_HasAccessibleName()
	{
		// No seeded opportunity ever gets real coordinates - VisualTests always
		// runs against FakeGeocodingService, which reports TransientFailure - so
		// the map is structurally unreachable by every other scan in this file.
		// Patch coordinates the way SingleMarkerMapTouchScrollTests does to
		// exercise it, and assert the marker's accessible name directly rather
		// than relying only on the axe scan (an unnamed role="button" tab stop
		// is WCAG 4.1.2 / axe's button-name rule).
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"Marker A11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var title = $"Marker A11y Test {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Seeded for the map marker accessible-name regression (#1681).",
			organizationId,
			isRemote = false,
			street = "Teststrasse",
			houseNumber = "1",
			zipCode = "12345",
			city = "Musterstadt",
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		await Page.RouteAsync($"**/v1/volunteer-opportunities/{opportunityId}", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			var response = await route.FetchAsync();
			var body = JsonNode.Parse(await response.TextAsync())!.AsObject();
			body["latitude"] = JsonValue.Create(52.52);
			body["longitude"] = JsonValue.Create(13.405);

			await route.FulfillAsync(new()
			{
				Response = response,
				ContentType = "application/json",
				Body = body.ToJsonString(),
			});
		});

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Expect(Page.Locator("h1").First).ToHaveTextAsync(title, new() { Timeout = 15_000 });

		var marker = Page.Locator(".leaflet-marker-icon");
		await Expect(marker).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(marker).ToHaveAttributeAsync("role", "button");
		await Expect(marker).ToHaveAttributeAsync("title", "Teststrasse 1, 12345 Musterstadt");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_MapUnavailable_HasNoSeriousA11yViolations()
	{
		// A non-remote opportunity whose address has not resolved to coordinates
		// renders a "no map available" note in place of the map section. A
		// freshly seeded one always lands in that state under
		// FakeGeocodingService, so no route patching is needed. Assert the
		// placeholder is visible before scanning, so a regression that stopped it
		// rendering would not silently reduce this to a no-op pass.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"No Map A11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var title = $"No Map A11y Test {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Seeded for the map-unavailable placeholder a11y regression (#1963).",
			organizationId,
			isRemote = false,
			street = "Teststrasse",
			houseNumber = "1",
			zipCode = "12345",
			city = "Musterstadt",
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Expect(Page.Locator("h1").First).ToHaveTextAsync(title, new() { Timeout = 15_000 });
		await Expect(Page.GetByTestId("map-unavailable")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ProfileOverviewPage_HasNoSeriousA11yViolations()
	{
		// /profile renders Profile Details and Badges only. Invitations and
		// sign-ups live at /my-signups; notifications, export and deletion at
		// /profile/settings - both scanned separately below.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ProfileOverviewPage_EditMode_HasNoSeriousA11yViolations()
	{
		// Edit/Save/Cancel live in the header's quick actions, and the read-only
		// scan above never opens the edit form - scan it separately here. Also
		// asserts Badges stays mounted alongside the open edit form, since it is
		// not behind a separate tab.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("profile-edit").ClickAsync();
		await Expect(Page.GetByTestId("profile-save")).ToBeVisibleAsync();

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Badges" })).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task MyEngagementsPage_HasNoSeriousA11yViolations()
	{
		// Invitations/sign-ups split out of /profile onto their own
		// page - this scan is what ProfileOverviewPage_EditMode's "My
		// sign-ups" heading assertion used to (indirectly) cover.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Pinned to the page's <h1>: the header band's title and the sr-only
		// section heading further down both carry this name, so an unqualified
		// lookup matches two elements.
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My sign-ups", Level = 1 }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ProfileSettingsPage_HasNoSeriousA11yViolations()
	{
		// Email notification preferences, data export and account
		// deletion split out of /profile onto their own page.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/profile/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// This panel's heading is "Delete account" now, not the shared
		// "Danger zone".
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Delete account" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ProfileSettingsPage_AsOrganizationMember_HasNoSeriousA11yViolations()
	{
		// The two organizer-only email preferences are gated on organization
		// membership, and the scan above signs in as vera, who has none. Olaf
		// belongs to seeded organizations, which is what makes them render here.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/profile/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Waiting on the gated checkbox itself, not the page chrome: the scan
		// must not run before the rows it exists to cover have rendered.
		await Expect(Page.Locator("#notifyOnNewSignUp"))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	// Keyed [NotInParallel] shared with AvatarAndLogoDisplayTests - this is the
	// third and last writer of vera's single avatar_url field, and its upload
	// racing that class's upload/delete pair is what produced the intermittent
	// "nav bar still shows initials" CI failure. See that file for the full
	// mechanism.
	[Test]
	[NotInParallel("visualtests-vera-avatar")]
	public async Task ProfileOverviewPage_EditModeWithAvatar_HasNoSeriousA11yViolations()
	{
		// The "Remove" button next to the avatar only renders once the
		// user has an avatar. Vera's seeded account has none, so the edit-mode
		// scan above never renders it - seed an avatar via the upload endpoint
		// here instead of mutating Vera's shared seed data further.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync();

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		using var content = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(TinyPng);
		fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		content.Add(fileContent, "file", "avatar.png");

		(await http.PutAsync("/v1/users/me/avatar", content)).EnsureSuccessStatusCode();

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("profile-edit").ClickAsync();
		await Expect(Page.GetByTestId("profile-save")).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Remove" })).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationProfilePage_HasNoSeriousA11yViolations()
	{
		// Targets the org link by data-testid, not a Tailwind class combination:
		// OpportunityListItem.tsx's org link is `relative z-20` while the
		// stretched card-cover Link is the z-10 one, so a class-based locator
		// silently matches nothing and skips the scan. Seed data always
		// publishes opportunities (ApplicationDbContextInitializer.cs), so a
		// missing link is a genuine failure, not a "not seeded yet" skip.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgLink = Page.GetByTestId("opportunity-org-link").First;
		await Expect(orgLink).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var href = await orgLink.GetAttributeAsync("href");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task UserProfilePage_HasNoSeriousA11yViolations()
	{
		// The public /users/{userId} page renders bio/skills/languages via the
		// shared ProfileFieldsView component; preferredContact is deliberately
		// excluded from this page.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var userId = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.profile?.sub) return entry.profile.sub;
				}
			}
			return null;
		}");
		Skip.When(userId is null, "could not resolve the logged-in user's id");

		await Page.GotoAsync($"{origin}/users/{userId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	// Olaf's seed data always organizes at least one org, so FastSignInAsync
	// always resolves a pinned id for him to navigate straight to.
	private async Task NavigateToOrgAppDashboardAsOlafAsync(Uri frontend)
	{
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
	}

	[Test]
	public async Task OrgDashboardPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Dashboard");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_AsOlaf_SkipLink_MovesFocusToMainContent()
	{
		// Same bypass gap as HomePage's skip link, but the
		// org app shell's header (org switcher, notification bell, avatar menu,
		// breadcrumb + quick actions) is a separate implementation from the
		// public site's - this covers OrgAppLayout's own copy.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.Keyboard.PressAsync("Tab");
		var skipLink = Page.GetByRole(AriaRole.Link, new() { Name = "Skip to content" });
		await Expect(skipLink).ToBeFocusedAsync();

		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.Locator("#main-content")).ToBeFocusedAsync();
	}

	[Test]
	public async Task OrgDashboardPage_CalendarWidgetMonthView_AsOlaf_DateCellsHaveAccessibleDateLabel()
	{
		// react-big-calendar's default DateHeader gives Month view's date-number
		// button no accessible name beyond the visible digit, so
		// CalendarWidget.tsx's components.month.dateHeader (CalDateHeader) sets
		// aria-label to a full date. Forces Month view via the toolbar rather than
		// relying on seeded events landing in the visible month - the day grid
		// renders the same either way, so no seed data is needed. Asserts the
		// accessible name directly (mirroring
		// VolunteerOpportunityDetailPage_MapMarker_HasAccessibleName above)
		// rather than only an axe scan: the original bug already had visible
		// button text, which satisfies axe's own accessible-name rules and
		// would never have caught this regression.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Month" }).ClickAsync();

		// `.rbc-current` marks the cell matching the calendar's own `date`
		// state, which CalendarWidget.tsx initializes to `new Date()` and this
		// test never navigates away from - i.e. today's cell.
		var todayCell = Page.Locator(".rbc-current .rbc-button-link");
		await Expect(todayCell).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var visibleLabel = await todayCell.InnerTextAsync();
		var ariaLabel = await todayCell.GetAttributeAsync("aria-label");

		ariaLabel.Should().NotBeNullOrWhiteSpace();
		ariaLabel.Should().NotBe(visibleLabel,
			"the accessible name must be more than the bare day-of-month digit visible on screen (einsatzbereit#1924)");
		ariaLabel!.Length.Should().BeGreaterThan(visibleLabel.Length,
			"a full date label (weekday, month, year) is always longer than the bare digit it replaces");
	}

	[Test]
	public async Task OrgDashboardPage_LayoutLoadFailed_AsOlaf_HasNoSeriousA11yViolations()
	{
		// A failed dashboard-layout fetch now renders its own inline
		// error banner + retry button and disables the "Edit" quick action -
		// a DOM state the plain page-load scan above never reaches, since its
		// GET .../dashboard/layout always succeeds.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.RouteAsync("**/dashboard/layout", async route =>
		{
			if (route.Request.Method == "GET")
				await route.AbortAsync();
			else
				await route.ContinueAsync();
		});

		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Expect(Page.GetByTestId("dashboard-layout-retry")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_KpiLoadFailed_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The KPI endpoint is a different request from the layout fetch
		// covered above - GET .../dashboard, not .../dashboard/layout - and
		// since the split it feeds two tiles at once (ToDo and VolunteerStats),
		// so one failure renders two inline banners side by side. Both are
		// deliberately role="status"/aria-live="polite" rather than
		// ErrorBanner's default assertive alert, so a single passive load
		// failure doesn't interrupt a screen reader twice.
		var frontend = Fixture.GetEndpoint("frontend");

		// Anchored so it can't also swallow .../dashboard/layout, which has to
		// keep succeeding for the widget grid to render at all.
		await Page.RouteAsync("**/v1/organizations/*/dashboard", async route =>
		{
			if (route.Request.Method == "GET")
				await route.AbortAsync();
			else
				await route.ContinueAsync();
		});

		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Expect(Page.GetByTestId("widget-tile-ToDo").GetByText("Failed to load summary."))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByTestId("widget-tile-VolunteerStats")
				.GetByText("Failed to load the volunteer count."))
			.ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_CalendarWidgetColorDialog_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The Calendar widget's color-picker dialog only exists in the DOM while
		// open, so the plain page-load scan above cannot reach it.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var calendarEvent = Page.Locator(".rbc-event").First;
		try
		{
			await calendarEvent.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("olaf's org has no calendar events seeded for the current month");
		}

		await calendarEvent.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_CalendarWidgetColorDialogInvalidContrast_AsOlaf_HasNoSeriousA11yViolations()
	{
		// Picking a color that fails the 4.5:1 chip-text
		// contrast floor now renders a conditional aria-invalid/aria-describedby
		// pair on the color input plus an inline warning paragraph and disables
		// Save - a DOM state the plain color-dialog scan above never reaches,
		// since olaf's seeded event keeps the default brand-700 color.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var calendarEvent = Page.Locator(".rbc-event").First;
		try
		{
			await calendarEvent.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("olaf's org has no calendar events seeded for the current month");
		}

		await calendarEvent.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		// #2d8a5e (brand-600): clears the 3:1 chip-vs-page floor but not the
		// 4.5:1 text floor - see EventColorContrast.cs's MinimumTextContrastRatio.
		await Page.Locator("#event-color-picker").FillAsync("#2d8a5e");
		await Expect(Page.Locator("#event-color-contrast-warning")).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }))
			.ToBeDisabledAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgOpportunitiesPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		// Reached through the page header's own section rail (OrgPageHeader.tsx),
		// the way an organizer reaches it.
		await Page.GetByTestId("org-tab-opportunities").ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Opportunities");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgMembersPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		// Via the page header's section rail, not a bare "member" name match -
		// the Settings widget's member-count link answers to that too.
		await Page.GetByTestId("org-tab-members").ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Members");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgMembersPage_MemberRowWithPromoteDemoteButtons_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The new "Promote to organizer"/"Demote to member" button pair
		// only renders for a non-self member row - NavigateToOrgAppDashboardAsOlafAsync
		// pins an org where Olaf is the only member, so the scan above never
		// reaches it. Create a fresh org rather than adding a second member to
		// one of the shared seeded orgs, which other tests may rely on staying
		// single-member.
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();
		var orgName = $"Visual1050 A11y {Guid.NewGuid():N}";
		await createDialog.Locator("input[type='text']").FillAsync(orgName);
		await Page.GetByTestId("modal-submit").ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToContainTextAsync(orgName, new() { Timeout = 15_000 });

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = Guid.Parse(match.Groups[1].Value);

		var vera = await Fixture.SignInAsync("vera", "vera123");
		await Fixture.AddPlainMemberDirectlyAsync(organizationId, vera.UserId);

		// OrgAppLayout only refetches org details on organizationId change -
		// force a refetch, same as OrganizationTests.cs's equivalent setup.
		await Page.ReloadAsync();
		await Page.GetByTestId("org-tab-members").ClickAsync();
		// This button's accessible name now interpolates
		// the member's own name in the middle ("Promote {name} to organizer"),
		// so match with a regex rather than the old literal substring.
		await Expect(Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Promote .* to organizer") }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationSettingsPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		// The tab bar is gone - reach the page via the Settings widget's
		// "Edit settings" link instead.
		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Settings");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationSettingsPage_EditMode_AsOlaf_HasNoSeriousA11yViolations()
	{
		// Edit/Save/Cancel live in the header's quick actions, and the read-only
		// scan above never opens the edit form - scan it separately here.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationSettingsPage_EditModeValidationError_AsOlaf_HasNoSeriousA11yViolations()
	{
		// OrgSettingsPage carries the same react-hook-form + zod validation as
		// CreateOrganizationModal - scan its inline validation-error state, not
		// just the clean edit-mode form covered above.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();

		await Page.Locator("#org-name").FillAsync("");
		await Page.GetByTestId("quick-action-save").ClickAsync();

		await Expect(Page.Locator("#org-name-error")).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationSettingsPage_EditModeWithLogo_HasNoSeriousA11yViolations()
	{
		// The "Remove" button next to the logo only renders once an
		// organization has a logo. Olaf's seeded org has none, so the edit-mode
		// scan above never renders it - seed a fresh org with a logo here
		// instead of mutating Olaf's shared seed data.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync();

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"A11yLogo {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		using var content = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(TinyPng);
		fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		content.Add(fileContent, "file", "logo.png");

		(await http.PutAsync($"/v1/organizations/{organizationId}/logo", content)).EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Remove" })).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	private async Task<string> GetAccessTokenAsync()
	{
		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in sessionStorage after login");
		return token!;
	}

	// 1x1 transparent PNG.
	private static readonly byte[] TinyPng = Convert.FromBase64String(
		"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

	[Test]
	public async Task OrgDashboardPage_EditMode_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The customizable widget grid's edit-mode chrome (inert widget
		// content, move/resize/remove toolbar) only exists in the DOM while
		// editing - the read-only scan above can't reach it. The "Add
		// Widget" modal and the corner-to-corner placement surface are their
		// own DOM-only-while-open states, scanned separately below.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_PlacingAWidget_AsOlaf_HasNoSeriousA11yViolations()
	{
		// Corner-to-corner placement renders its own extra surface
		// while active - the green/blue/red-tinted grid backdrop and the
		// role="status" placement banner - that the plain edit-mode scan
		// above never reaches, since it never clicks "Move or resize".
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs your attention" }).ClickAsync();
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_AddWidgetModal_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The "Add Widget" picker only exists in the DOM while open - the
		// edit-mode scan above never opens it.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_QuickCheckInAndSettingsIconWidgets_AsOlaf_HasNoSeriousA11yViolations()
	{
		// QuickCheckIn and SettingsIcon aren't in DEFAULT_LAYOUT (see
		// widgetCatalog.ts), so a fresh org's dashboard scan above never
		// renders them for real - only AddWidgetModal's static mockup preview
		// gets scanned incidentally as part of that dialog. Add both here so
		// their actual rendered content (the opportunity dropdown + scan
		// button, and the settings shortcut tile) gets its own axe pass.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();

		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await dialog.GetByTestId("add-widget-option-QuickCheckIn").ClickAsync();
		await dialog.GetByTestId("add-widget-option-SettingsIcon").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();

		await Expect(Page.GetByTestId("widget-tile-QuickCheckIn")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("widget-tile-SettingsIcon")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_RowBandedLayout_AsOlaf_HasNoSeriousA11yViolations()
	{
		// A saved layout with rows narrower than the full grid renders each row
		// as its own width-capped grid container (groupIntoRowBands in
		// widgetCatalog.ts) - a DOM shape the page-load scan above never reaches,
		// since a fresh org's DEFAULT_LAYOUT fills every row edge to edge.
		// Settings (full width) plus VolunteerStats (a narrower row below it)
		// reproduces the mixed shape: one uncapped band next to a capped one.
		var frontend = Fixture.GetEndpoint("frontend");
		var organizationId = await CreateOrganizationAsOlafAsync("A11yRowBanding");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard");

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllDashboardWidgetsAsync();

		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await dialog.GetByTestId("add-widget-option-Settings").ClickAsync();
		await dialog.GetByTestId("add-widget-option-VolunteerStats").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByTestId("widget-tile-Settings")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("widget-tile-VolunteerStats")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	/// <summary>
	/// Deliberately local rather than shared with the identical loops in
	/// OrgDashboardCustomizeTests.cs and OrgDashboardRowBandingTests.cs -
	/// every class in this suite owns its own copy of this kind of setup.
	/// </summary>
	private async Task RemoveAllDashboardWidgetsAsync()
	{
		foreach (var (testId, widgetTitle) in new[]
		{
			("CreateOpportunity", "Create opportunity"),
			("ToDo", "Needs your attention"),
			("VolunteerStats", "Volunteers"),
			("UpcomingOpportunities", "Upcoming opportunities"),
			("Calendar", "Calendar"),
			("Settings", "Organization"),
		})
		{
			var tile = Page.GetByTestId($"widget-tile-{testId}");
			if (await tile.CountAsync() == 0)
				continue;
			await tile
				.GetByRole(AriaRole.Button, new() { Name = $"Remove {widgetTitle} widget" })
				.ClickAsync();
		}
	}

	[Test]
	public async Task OrgDashboardPage_EmptyOpportunitiesCreateOpportunityCta_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The empty-state "Create one" CTAs are worded distinctly from
		// CreateOpportunityWidget's "Create opportunity" button on the same
		// dashboard, so the two do not collide as duplicate accessible names.
		// Needs a fresh org: olaf's shared one has opportunities by now, and a
		// fresh one also has zero pending sign-ups, which is the only way to reach
		// ToDoWidget's "resolved" branch.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"EmptyDashA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = Guid.Parse(org.GetProperty("id").GetProperty("value").GetString()!);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, organizationId);

		var upcomingWidget = Page.GetByTestId("widget-tile-UpcomingOpportunities");
		await Expect(upcomingWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var upcomingCreateButton = upcomingWidget.GetByRole(
			AriaRole.Button, new() { Name = "Create one" });
		await Expect(upcomingCreateButton).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);

		await upcomingCreateButton.ClickAsync();
		var upcomingDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(upcomingDialog).ToBeVisibleAsync();

		var upcomingDialogResult = await Page.RunAxe();
		AssertNoViolations(upcomingDialogResult);

		await Page.Keyboard.PressAsync("Escape");
		await Expect(upcomingDialog).Not.ToBeVisibleAsync();

		// QuickCheckIn isn't in DEFAULT_LAYOUT (see widgetCatalog.ts) - add it
		// via the picker, save to leave edit mode (widget content is inert
		// while editing - see EditableWidgetTile), then reach its own,
		// separately-wired empty-state CTA.
		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var addWidgetDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(addWidgetDialog).ToBeVisibleAsync();
		await addWidgetDialog.GetByTestId("add-widget-option-QuickCheckIn").ClickAsync();
		await addWidgetDialog.GetByTestId("add-widget-done").ClickAsync();
		await Expect(addWidgetDialog).Not.ToBeVisibleAsync();

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var quickCheckInWidget = Page.GetByTestId("widget-tile-QuickCheckIn");
		await Expect(quickCheckInWidget).ToBeVisibleAsync();

		var quickCheckInCreateButton = quickCheckInWidget.GetByRole(
			AriaRole.Button, new() { Name = "Create one" });
		await Expect(quickCheckInCreateButton).ToBeVisibleAsync();

		await quickCheckInCreateButton.ClickAsync();
		var quickCheckInDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(quickCheckInDialog).ToBeVisibleAsync();

		var quickCheckInDialogResult = await Page.RunAxe();
		AssertNoViolations(quickCheckInDialogResult);

		await Page.Keyboard.PressAsync("Escape");
		await Expect(quickCheckInDialog).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task EngagementManagementPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		// Seeds a fresh org/opportunity/engagement rather than relying on olaf's
		// shared seed data, which would let this skip when no published
		// opportunity with a pending applicant happens to exist - leaving the
		// page's "Confirm" button with no guaranteed coverage. Same pattern as
		// the CancelDialog test below.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"EngagementManagementA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"EngagementManagementA11y Opportunity {suffix}",
			descriptionDe = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "For the a11y scan." });
		applyResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// On a nested route, the h1 must track the breadcrumb's trailing
		// "extra" segment (the opportunity title, set via
		// useSetOrgBreadcrumbExtra) rather than staying on the parent tab's
		// own label ("Opportunities") - the one place this pageTitle logic
		// could regress silently.
		await Expect(Page.Locator("h1")).Not.ToHaveTextAsync("Opportunities");
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Confirm" })).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgEngagementsPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The dashboard's "To-Do" widget counts pending
		// engagements across every opportunity in the organization, but the
		// only way to view them used to be per-opportunity via
		// EngagementManagementPage above. This is the resulting org-wide
		// queue - seeds its own org/opportunity/engagement rather than
		// relying on olaf's shared seed data, same rationale as
		// EngagementManagementPage_AsOlaf_... above.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"OrgEngagementsA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"OrgEngagementsA11y Opportunity {suffix}",
			descriptionDe = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "For the a11y scan." });
		applyResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard/engagements?status=Pending");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// "engagements" became a real ORG_TABS entry, so the
		// h1 now comes from OrgAppLayout's own tab-label lookup (orgOverview.tabEngagements)
		// like every other tab, rather than from a page-local useSetOrgBreadcrumbExtra call.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Sign-ups");

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Confirm" })).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task PrivacyPolicyPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/privacy-policy");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ImprintPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/imprint");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task TermsOfUsePage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/terms-of-use");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ContactPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/contact");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task HelpPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/help");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task NotFoundPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/this-page-does-not-exist");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task UnsubscribePage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/unsubscribed?type=EngagementReminder");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task UnsubscribeConfirmPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/unsubscribe?userId={Guid.NewGuid()}&type=EngagementReminder&token={Guid.NewGuid()}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task UnsubscribeConfirmPage_HasNoSeriousA11yViolations_WhenLinkIsInvalid()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/unsubscribe");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task SignUpModal_OpenTimeSlotDropdown_HasNoSeriousA11yViolations()
	{
		// The native time slot <select> was replaced with a custom
		// accessible combobox/listbox - assert the open dropdown itself is
		// axe-clean, not just the page around it.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");

		// "Select a slot" only exists on an opportunity's own detail page
		// (VolunteerOpportunityDetailPage.tsx) - the home page's cards link
		// there but never render the button themselves. Filter to ScheduledSlots-type
		// opportunities (seed data has two, both with open capacity) and follow
		// the first card's link in, matching the navigation pattern
		// VolunteerOpportunityDetailPage_HasNoSeriousA11yViolations above uses.
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities?participationType=ScheduledSlots");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		try
		{
			await firstCard.WaitForAsync(new() { Timeout = 15_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("no ScheduledSlots opportunity seeded");
		}

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity card had no href attribute");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var signUpBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Select a slot" });
		try
		{
			await signUpBtn.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("no ScheduledSlots opportunity with open slots seeded");
		}

		await signUpBtn.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		// The SignUpModal is already open and this opportunity was just
		// filtered above to one with open ScheduledSlots capacity, so the
		// dropdown is always going to render - a non-waiting CountAsync() here
		// raced the modal's own mount and could silently skip the axe scan
		// instead of failing on a genuine regression.
		var dropdown = Page.Locator("#sign-up-time-slot");
		await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await dropdown.ClickAsync();
		await Expect(Page.Locator("[role='option']").First).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task LanguageSelector_Open_HasNoSeriousA11yViolations()
	{
		// The header's language switcher is in every page's header, but no other
		// scan in this file opens the overlay, so nested-interactive violations
		// there stay invisible. Scanned on /opportunities rather than the home
		// page: that route renders a PageHeaderBand, so the header is
		// transparent and this covers the selector's white-on-dark variant.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var banner = Page.GetByRole(AriaRole.Banner);
		var trigger = banner.GetByTestId("language-selector-trigger");
		await Expect(trigger).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await trigger.ClickAsync();

		var menu = banner.GetByTestId("language-selector-menu");
		await Expect(menu).ToBeVisibleAsync(new() { Timeout = 5_000 });

		// Pins the claim the comment above makes. isTransparent is
		// `overlaysBand && !scrolled` (Header.tsx), so a future page that
		// restores scroll position past 100px would silently flip this scan to
		// the light variant and keep passing while covering the wrong thing.
		await Expect(menu).ToHaveClassAsync(new Regex("bg-brand-800"));

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task CreateVolunteerOpportunityModal_HasNoSeriousA11yViolations()
	{
		// This modal carries custom ARIA machinery (a manual Tab trap, an
		// aria-live step announcer, sr-only radio-cards, and a nested
		// unsaved-changes ConfirmDialog) that a page-load scan cannot reach,
		// since the modal only exists in the DOM while open.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);

		// Dirty the form, then open the nested discard-changes confirmation -
		// both dialogs stacked must remain axe-clean together.
		await Page.Locator("#opportunity-title").FillAsync("A11y Axe Test");
		await Page.Keyboard.PressAsync("Escape");
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Discard changes" }))
			.ToBeVisibleAsync();

		var nestedResult = await Page.RunAxe();
		AssertNoViolations(nestedResult);
	}

	[Test]
	public async Task CreateOrganizationModal_HasNoSeriousA11yViolations()
	{
		// This modal gained react-hook-form + zod validation (it
		// previously had none) - scan both the clean state and the
		// blank-submit validation-error state it can now render.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);

		await createDialog.GetByTestId("modal-submit").ClickAsync();
		await Expect(createDialog.Locator("#create-org-name-error")).ToBeVisibleAsync(
			new() { Timeout = 5_000 });

		var errorResult = await Page.RunAxe();
		AssertNoViolations(errorResult);

		await createDialog.GetByTestId("modal-cancel").ClickAsync();
	}

	[Test]
	public async Task NotificationDropdown_Open_HasNoSeriousA11yViolations()
	{
		// The dropdown gained cursor-based "load more" pagination on top
		// of the existing mark-all-read/per-item controls - the panel only
		// exists in the DOM while open, so a plain page-load scan never reaches
		// it (see NotificationBell_OpensPanel_WhenClicked in NotificationTests.cs
		// for the equivalent functional-only coverage).
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"NotifA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"NotifA11y Opportunity {suffix}",
			descriptionDe = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "For the a11y scan." });
		applyResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");

		var bell = Page.GetByTestId("notification-bell");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await bell.ClickAsync();

		var panel = Page.GetByTestId("notification-panel");
		await Expect(panel).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(panel.Locator("li").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task MobileMenu_Open_AsOlaf_HasNoSeriousA11yViolations()
	{
		// MobileMenu carries a scrim, role="dialog"/aria-modal, a Tab focus trap
		// and a body scroll lock. Every other scan here runs at the default
		// desktop viewport and the panel is md:hidden, so nothing else reaches
		// it. Olaf, not Vera, so the org entry and its section links are present
		// too - the whole panel in one scan, not the anonymous/no-org subset.
		var frontend = Fixture.GetEndpoint("frontend");

		// FastSignInAsync verifies auth via the desktop "User menu" button,
		// which is CSS-hidden below the md breakpoint (see
		// OrganizationDashboardNavLinkTests) - sign in before shrinking to a
		// mobile viewport, not after.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(375, 812);

		var banner = Page.GetByRole(AriaRole.Banner);
		await banner.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Menu" });
		await Expect(dialog).ToBeVisibleAsync(new() { Timeout = 5_000 });

		// The org rows are on screen as soon as the panel opens now, so there is
		// no disclosure left to click - but the scan still needs one row in a
		// real :hover state, which is what catches a too-light hover colour
		// (see MobileMenu's menuItemVariant comment).
		await dialog.GetByTestId("mobile-nav-organization").HoverAsync();
		await Expect(dialog.GetByRole(AriaRole.Link, new() { Name = "Members", Exact = true }))
			.ToBeVisibleAsync(new() { Timeout = 5_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	// One case per administration section: separate routes behind a shared left
	// rail, so a single scan of /administration covers only the first.
	//
	// [Retry(2)]: this LoginAsync call site lands in the first concurrent batch,
	// where the Keycloak round trip can outlast any fixed timeout while Aspire
	// is still warming up. Cheaper than inflating every caller's timeout.
	[Test]
	[Retry(2)]
	[Arguments("organizations")]
	[Arguments("users")]
	[Arguments("reports")]
	[Arguments("audit-log")]
	public async Task AdministrationPage_HasNoSeriousA11yViolations(string section)
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/administration/{section}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	// RouteState is one shared component for the four ways a route can fail to
	// show what was asked for. Two scans, one per shell it renders in: inside
	// AppLayout (header/main/footer already present - the heading-order and
	// landmark case) and inside the org app, which bypasses AppLayout entirely
	// and has to supply its own <main>.
	[Test]
	public async Task AdminOnlyRouteAsNonAdmin_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/administration");

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Admin rights required" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	// The third RouteState combination the two scans around this one don't
	// reach: the `offline` variant, and the only user of `inline` - the mode
	// that deliberately renders no heading, which is exactly what the escalated
	// heading-order/page-has-heading-one rules police. The warm-then-navigate
	// dance is shared with OfflineStateTests (see VisualTestBase) and is not
	// optional: service workers are blocked here and the route is lazy-loaded.
	[Test]
	public async Task OpportunityListOffline_HasNoSeriousA11yViolations()
	{
		var origin = await WarmOpportunitiesRouteThenLeaveAsync();

		await Context.SetOfflineAsync(true);
		try
		{
			await GoToOpportunitiesAsync(origin);
			await Expect(Page.GetByTestId("opportunities-offline"))
				.ToBeVisibleAsync(new() { Timeout = 20_000 });

			var result = await Page.RunAxe();
			AssertNoViolations(result);
		}
		finally
		{
			await Context.SetOfflineAsync(false);
		}
	}

	/// <summary>
	/// #2065: the opportunity detail page's new full-page (non-`inline`) offline
	/// branch - a different DOM/landmark shape than the `inline` case
	/// <see cref="OpportunityListOffline_HasNoSeriousA11yViolations"/> above
	/// covers (this one replaces <c>PageHeaderBand</c> and the rest of the page
	/// too, the same way the page's existing not-found/generic-error branches
	/// already do), and it also now carries a "Try again" button neither of
	/// those two variants had axe coverage for on this page before.
	///
	/// Simulated by pinning <c>navigator.onLine</c> false and aborting just the
	/// detail request, not <c>Context.SetOfflineAsync</c> - same technique
	/// <c>OrgAppLayoutErrorStatesTests</c> uses for the org shell, and simpler
	/// than the warm-then-navigate dance the test above needs, since this page
	/// is reached with a normal document <c>GotoAsync</c> either way.
	/// </summary>
	[Test]
	public async Task VolunteerOpportunityDetailPage_Offline_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"DetailOfflineA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"DetailOfflineA11y Opportunity {suffix}",
			descriptionDe = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		await Page.AddInitScriptAsync(
			"Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => false });");
		await Page.RouteAsync($"**/v1/volunteer-opportunities/{opportunityId}", route =>
			route.AbortAsync("internetdisconnected"));

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are offline" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	/// <summary>
	/// #2065: the landing page's "These opportunities need people" preview
	/// section used to remove itself on any failure, offline included, so its
	/// new inline offline notice (plus the "Try again" button #2065 added to
	/// the offline variant generally) had no axe coverage anywhere - the plain
	/// <see cref="HomePage_HasNoSeriousA11yViolations"/> scan above never
	/// simulates a failure.
	/// </summary>
	[Test]
	public async Task HomePage_LatestOpportunitiesOffline_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.AddInitScriptAsync(
			"Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => false });");
		await Page.RouteAsync("**/v1/volunteer-opportunities*", route =>
			route.AbortAsync("internetdisconnected"));

		await Page.GotoAsync(origin);
		await Expect(Page.GetByTestId("landing-latest-offline"))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgAppUnknownOrganization_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/app/{Guid.NewGuid()}/dashboard");

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Organization not found" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OpportunitiesPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// A scan of a page whose list failed to load passes vacuously.
		await Expect(Page.GetByTestId("opportunities-keyword-input"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationsPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/organizations");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// A scan of a page whose list failed to load passes vacuously.
		await Expect(Page.GetByTestId("organizations-search"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	// The row overflow menu is an overlay with hand-rolled markup, so it gets
	// scanned in its open state the way NotificationDropdown_Open and the
	// sign-up modal's slot dropdown do - closed, it contributes nothing.
	[Test]
	public async Task OrgOpportunitiesPage_RowActionsMenuOpen_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var organizationId = await AuthHelper.FastSignInAsync(
			Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var trigger = Page.GetByTestId("row-actions-trigger").First;
		await Expect(trigger).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await trigger.ClickAsync();
		await Expect(Page.GetByTestId("opportunity-delete").First).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	// OrgAppLayout's "not authorized" screen (a non-organizer hitting a 403)
	// has its own unique markup no other scan reaches.
	[Test]
	public async Task OrgAppLayout_Forbidden_AsVera_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var organizationId = await CreateOrganizationAsOlafAsync("A11yForbidden");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard");

		await Expect(Page.Locator("h1")).ToHaveTextAsync("You don't have access to this organization.");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	// The recoverable "something went wrong, try again" state (a 500/network
	// failure, as opposed to the permanent 403 above) - its own unique markup,
	// otherwise never scanned. OrgAppLayoutErrorStatesTests.cs covers its
	// functional behavior; this is its axe-core pass.
	[Test]
	public async Task OrgAppLayout_ServerError_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var organizationId = await CreateOrganizationAsOlafAsync("A11yServerError");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");

		await Page.RouteAsync($"**/v1/organizations/{organizationId}", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			await route.FulfillAsync(new()
			{
				Status = 500,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.6.1\",\"status\":500}",
			});
		});

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard");

		await Expect(Page.Locator("h1")).ToHaveTextAsync("Something went wrong");
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Try again" })).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	private async Task<string> CreateOrganizationAsOlafAsync(string label)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var response = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"{label} {suffix}" });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()
			?? throw new InvalidOperationException("Created organization had no id.");
	}

	[Test]
	public async Task EngagementManagementPage_CancelDialog_HasNoSeriousA11yViolations()
	{
		// The cancel/revoke ConfirmDialog gained an optional reason
		// <label>/<textarea> + character-counter <p> (previously a plain
		// yes/no dialog with no form control) - EngagementManagementPage_AsOlaf_...
		// above never opens this dialog, so its new markup had zero axe
		// coverage. Seeds its own opportunity/engagement rather than relying
		// on olaf's shared seed data, so this doesn't skip like that test can.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"CancelDialogA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"CancelDialogA11y Opportunity {suffix}",
			descriptionDe = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "For the a11y scan." });
		applyResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.Locator("#cancel-reason")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgEngagementsPage_CancelDialog_HasNoSeriousA11yViolations()
	{
		// The cancel/revoke ConfirmDialog here carries the same optional-reason
		// <label>/<textarea> + character-counter <p> as
		// EngagementManagementPage_CancelDialog_HasNoSeriousA11yViolations
		// above - OrgEngagementsPage_AsOlaf_... never opens it, so seed a
		// fresh org/opportunity/engagement here too.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"OrgEngagementsCancelDialogA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"OrgEngagementsCancelDialogA11y Opportunity {suffix}",
			descriptionDe = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "For the a11y scan." });
		applyResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard/engagements?status=Pending");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.Locator("#org-engagement-cancel-reason")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgOpportunitiesPage_CancelDialog_HasNoSeriousA11yViolations()
	{
		// The hub's Cancel ConfirmDialog carries an optional reason
		// <label>/<textarea> + character-counter <p>, the same form-control-on-a-
		// plain-confirm shape EngagementManagementPage_CancelDialog_... covers.
		// OrgOpportunitiesPage_AsOlaf_... never opens it, so seed a fresh published
		// opportunity rather than relying on olaf's shared seed data.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"CancelOpportunityDialogA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"CancelOpportunityDialogA11y Opportunity {suffix}",
			descriptionDe = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("row-actions-trigger").First.ClickAsync();
		await Page.GetByTestId("opportunity-cancel").First.ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.Locator("#cancel-opportunity-reason")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	// Seeds the toast, CheckInModal, SubmitFeedbackModal and date-range
	// popover states deterministically, rather than skipping on missing seed
	// data like several tests above, so a regression fails loudly instead of
	// passing on an empty scan.
	private async Task<(string OrganizationId, string OpportunityId, string EngagementId)>
		SeedConfirmedEngagementAsync(string checkInMethod, string label)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"{label} Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()
			?? throw new InvalidOperationException("Created organization had no id.");

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"{label} Opportunity {suffix}",
			descriptionDe = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod,
			isDraft = false,
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString()
			?? throw new InvalidOperationException("Created opportunity had no id.");

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = $"{label} application." });
		applyResponse.EnsureSuccessStatusCode();
		var engagement = await applyResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString()
			?? throw new InvalidOperationException("Created engagement had no id.");

		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", null)).EnsureSuccessStatusCode();

		return (organizationId, opportunityId, engagementId);
	}

	[Test]
	public async Task MyEngagementsPage_CheckedInAwaitingFeedback_AsVera_HasNoSeriousA11yViolations()
	{
		// The "Leave feedback" button only renders for a
		// checked-in-without-feedback engagement, which vera's seeded data never
		// has - so the base MyEngagementsPage scan never renders this control.
		// Also exercises SubmitFeedbackModal's star rating.
		var (_, _, engagementId) = await SeedConfirmedEngagementAsync("Manual", "FeedbackA11y");
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");
		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/check-in", null)).EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		// ActivitySection lives at /my-signups, not /profile.
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// A checked-in Confirmed engagement is classified as
		// Past (it represents a shift that already happened), not "Current &
		// upcoming" - see EngagementReadRepository.GetByVolunteerAsync.
		await Page.GetByTestId("engagements-scope-past").ClickAsync();

		var card = Page.Locator($"[data-engagement-id='{engagementId}']");
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);

		await card.GetByRole(AriaRole.Button, new() { Name = "Leave feedback" }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

		var modalResult = await Page.RunAxe();
		AssertNoViolations(modalResult);
	}

	[Test]
	public async Task MyEngagementsPage_EditableFeedback_AsVera_HasNoSeriousA11yViolations()
	{
		// The axe gate above only ever renders the
		// create-mode "Leave feedback" state - it never opens the edit-mode
		// SubmitFeedbackModal, the badge+Edit+Delete buttons state, or the
		// delete-feedback ConfirmDialog. Seed feedback that's already been
		// submitted so this scan actually reaches all three.
		var (_, _, engagementId) = await SeedConfirmedEngagementAsync("Manual", "FeedbackEditA11y");
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");
		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/check-in", null)).EnsureSuccessStatusCode();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		(await veraHttp.PostAsJsonAsync($"/v1/engagements/{engagementId}/feedback", new { rating = 4, comment = "Great!" }))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		// ActivitySection lives at /my-signups, not /profile.
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("engagements-scope-past").ClickAsync();

		var card = Page.Locator($"[data-engagement-id='{engagementId}']");
		await Expect(card.GetByRole(AriaRole.Button, new() { Name = "Edit" })).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var badgeAndButtonsResult = await Page.RunAxe();
		AssertNoViolations(badgeAndButtonsResult);

		await card.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

		var editModalResult = await Page.RunAxe();
		AssertNoViolations(editModalResult);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Dialog)).Not.ToBeVisibleAsync();

		await card.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

		var deleteDialogResult = await Page.RunAxe();
		AssertNoViolations(deleteDialogResult);
	}

	[Test]
	public async Task MyEngagementsPage_CheckInModalPinCode_AsVera_HasNoSeriousA11yViolations()
	{
		// CheckInModal's PIN-entry state and its success announcement are
		// reachable from no other scan in this file.
		var (_, _, engagementId) = await SeedConfirmedEngagementAsync("PINCode", "CheckInModalA11y");
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		// ActivitySection lives at /my-signups, not /profile.
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Confirmed-but-not-checked-in, so this lands in the default "Current &
		// upcoming" scope - where this test's own slot-less engagement sorts last
		// behind everything the rest of the suite has left on vera, several pages
		// down. Page to it rather than assuming it is on page 1.
		var card = Page.Locator($"[data-engagement-id='{engagementId}']");
		await Expect(Page.Locator("#activity [data-testid='engagement-card']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await LoadMoreUntilVisibleAsync(card);
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await card.GetByRole(AriaRole.Button, new() { Name = "Check in" }).ClickAsync();
		await Expect(Page.Locator("#pin-input")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task EngagementManagementPage_ConfirmSuccessToast_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The only scan that opens a toast - white-on-yellow-500/green-600
		// contrast is invisible to every other test here. Also exercises the
		// success-toast dispatch on confirm, not just the failure path.
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"ToastA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"ToastA11y Opportunity {suffix}",
			descriptionDe = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			isDraft = false,
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "For the toast a11y scan." });
		applyResponse.EnsureSuccessStatusCode();

		var frontend = Fixture.GetEndpoint("frontend");
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm" }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OpportunitiesPage_DateRangeFilterOpen_HasNoSeriousA11yViolations()
	{
		// MiniCalendar's day grid carries full ARIA grid/keyboard-navigation
		// semantics that only exist while the popover is open, which no other
		// scan of the seven home-page filter popovers reaches.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Date", Exact = true }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Grid)).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OpportunitiesPage_DateRangeFilterWithMarkedDays_HasNoSeriousA11yViolations()
	{
		// The day grid gained two states the scan above can
		// never see, because it seeds nothing and only ever opens on the current
		// month - a marked day (dot + "N opportunities" in its accessible name)
		// and the legend that only renders once some day in view is marked. This
		// seeds a slot into the month that is open on arrival so both are in the
		// DOM when axe runs, rather than hoping another test in the shared
		// session published one first.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"MarkedDayA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"MarkedDayA11y Opportunity {suffix}",
			descriptionDe = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		// Tomorrow midday, so the marked day is inside the month the calendar
		// opens on for all but one day of each month - and today itself is never
		// used, since a slot starting today would have to be later than "now".
		var slotStart = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero).AddHours(12);
		var slotResponse = await olafHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
			{
				startDateTime = slotStart,
				endDateTime = slotStart.AddHours(2),
				maxParticipants = 10,
				recurrenceCount = 1,
			});
		slotResponse.EnsureSuccessStatusCode();

		var publishResponse = await olafHttp.PostAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/publish", null);
		publishResponse.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Date", Exact = true }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Grid)).ToBeVisibleAsync();

		// The day slotStart falls on is only off-screen when it belongs to next
		// month; either way, waiting for a marked cell is what makes the scan
		// below deterministic instead of a race with the availability request.
		if (await Page.Locator("[data-marked='true']").CountAsync() == 0)
			await Page.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();

		await Expect(Page.Locator("[data-marked='true']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OpportunitiesPage_FilterApplied_HasNoSeriousA11yViolations()
	{
		// The only scan of /opportunities in its "active filter" DOM state: a
		// FilterDropdown's active/selected trigger variant plus its clear ("x")
		// button, and the "Reset" pill that only renders once a filter is applied.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("filter-frequency").ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "One-time" }).ClickAsync();

		// Scoped to the filter bar: an unscoped "Reset" also matches the
		// results list's own empty-state reset CTA (opportunities.clearFilters
		// - the same label) whenever the "One-time" filter happens to catch
		// zero opportunities in the shared VisualTests database at the moment
		// this runs, which depends on what other concurrently-running test
		// classes have created - a strict-mode violation unrelated to this
		// test's own subject.
		await Expect(
			Page.GetByTestId("opportunities-filter-bar")
				.GetByRole(AriaRole.Button, new() { Name = "Reset" }))
			.ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task CallbackPage_HasNoSeriousA11yViolations()
	{
		// /callback (the OIDC redirect landing page) never
		// had axe coverage of any kind.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/callback");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}
}
