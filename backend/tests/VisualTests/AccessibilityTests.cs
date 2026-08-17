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
	// #973: axe reports the "page has no h1" defect (page-has-heading-one) and
	// most landmark-structure defects at "moderate" impact, not "serious" or
	// "critical" - the plain serious/critical filter below let all four org app
	// pages ship with no h1 for months without CI ever seeing it. Escalate just
	// these rule IDs to CI-blocking rather than every moderate violation, which
	// would also flag things like color-contrast-enhanced noise unrelated to
	// this gate's purpose.
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
		// "region" (also moderate) is deliberately NOT escalated here: axe's
		// region rule flags any visible content not contained by a landmark,
		// and ToastContext.tsx mounts its toast list at the app root (outside
		// AppLayout's <main>) - escalating this blind, without being able to
		// run the full ~200-test Playwright suite in this sandbox (no Docker/
		// Aspire, see root AGENTS.md), risks breaking CI across every test
		// that happens to scan a page with a toast or similar page-root
		// overlay visible. Fixed the one instance found by inspection
		// (ToastList now has role="region" + aria-label) without gating CI on
		// a rule this sandbox can't verify page-by-page.
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
		// einsatzbereit#1284: neither layout had a bypass mechanism - a keyboard
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
		// The action row above the at-a-glance panel used to render for every
		// visitor (the Share button was its one unconditional child), so the
		// anonymous scan above incidentally covered it. Share is gone and the
		// row is now conditional, which leaves the signed-in-non-owner state -
		// the row holding nothing but Report - as the only render path of it
		// that no axe scan reaches. Vera is a plain user, never an organizer,
		// so isOwner is false for every opportunity.
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
	public async Task VolunteerOpportunityDetailPage_OwnerDraft_AsOlaf_HasNoSeriousA11yViolations()
	{
		// #1027: the draftBadge chip plus owner-only Edit/Publish actions
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
		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"A11y Draft Org {suffix}" });
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
	public async Task VolunteerOpportunityDetailPage_MobileActionRail_AsVera_HasNoSeriousA11yViolations()
	{
		// #1965: the sign-up CTA (and its deadline/status/login-prompt
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

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"A11y Mobile Rail Org {suffix}" });
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
		// #1938: the sidebar "application-status" card (label + status Chip +
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

		var orgResponse = await organizerHttp.PostAsJsonAsync("/v1/organizations", new { name = $"A11y Status Slot Org {suffix}" });
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
	public async Task VolunteerOpportunityDetailPage_MapMarker_HasAccessibleName()
	{
		// #1681: SingleMarkerMap.tsx's Leaflet divIcon marker rendered an
		// unnamed role="button" tab stop (WCAG 4.1.2) - axe's button-name rule
		// would flag this (impact "critical"), but no seeded opportunity here
		// ever gets real coordinates (VisualTests always runs against
		// FakeGeocodingService, which reports TransientFailure - see
		// SingleMarkerMapTouchScrollTests.cs), so the map - and this entire bug
		// class - was structurally unreachable by any existing scan, including
		// VolunteerOpportunityDetailPage_HasNoSeriousA11yViolations above.
		// Patch coordinates the same way SingleMarkerMapTouchScrollTests does
		// to actually exercise it, and assert the fix (Marker's title prop)
		// directly rather than relying only on the axe scan.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Marker A11y Org {suffix}" });
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
		// #1963: a non-remote opportunity whose address hasn't resolved to
		// coordinates now renders a "no map available" note in place of the
		// map section instead of omitting it silently. VisualTests always runs
		// against FakeGeocodingService (see VolunteerOpportunityDetailPage_MapMarker_HasAccessibleName
		// above), which reports TransientFailure - a freshly seeded non-remote
		// opportunity here always lands in that state, so no route patching is
		// needed to reach it deterministically. Assert the placeholder is
		// actually visible before scanning, same as the sibling tests in this
		// file, so a future regression that made it stop rendering wouldn't
		// silently reduce this to a no-op pass.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"No Map A11y Org {suffix}" });
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
		// #794: /profile was consolidated from a Profile/Activity tab switcher
		// into a single page. #1684: it was later split again - Profile
		// Details and Badges render here; invitations/sign-ups moved to
		// /my-signups and notifications/export/deletion moved to
		// /profile/settings (both scanned separately below).
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
		// #794: Edit/Save/Cancel moved from inline buttons into the header's
		// quick actions - the read-only scan above never opens the edit form,
		// so scan it separately here. Also asserts the Badges section stays
		// mounted and visible alongside the open edit form, since it doesn't
		// live behind a separate tab. #1684: My sign-ups no longer lives on
		// this page - see MyEngagementsPage_HasNoSeriousA11yViolations below.
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
		// #1684: invitations/sign-ups split out of /profile onto their own
		// page - this scan is what ProfileOverviewPage_EditMode's "My
		// sign-ups" heading assertion used to (indirectly) cover.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Pinned to the page's <h1>: #1755 gave the page a header band whose
		// title carries this name, and the section heading further down the
		// page still carries it too, so an unqualified lookup matches both.
		// #1796 made that second one sr-only (it is structure now, not a
		// visible eyebrow) - it keeps its accessible name, so it keeps
		// matching an unqualified lookup and the level pin is still needed.
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My sign-ups", Level = 1 }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ProfileSettingsPage_HasNoSeriousA11yViolations()
	{
		// #1684: email notification preferences, data export and account
		// deletion split out of /profile onto their own page.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/profile/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #1792: this panel's heading is "Delete account" now, not the shared
		// "Danger zone".
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Delete account" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ProfileSettingsPage_AsOrganizationMember_HasNoSeriousA11yViolations()
	{
		// #1783 gated the two organizer-only email preferences on organization
		// membership, and the scan above signs in as vera, who has none - so
		// without this sibling scan those two rows would render in production
		// and never be scanned at all. Olaf belongs to seeded organizations,
		// which is what makes them render here.
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
		// #1063: the "Remove" button next to the avatar only renders once the
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
		// #1708: the old locator (`ul > li .relative.z-10 a`) never matched
		// anything - OpportunityListItem.tsx's org link carries `relative
		// z-20` (the stretched card-cover Link is the one at z-10), so this
		// test always timed out and silently skipped, giving /organizations/{id}
		// zero axe coverage. Target the org link by data-testid instead of a
		// brittle Tailwind class combination, and seed data always publishes
		// opportunities (ApplicationDbContextInitializer.cs), so a missing
		// link is a genuine failure, not a "not seeded yet" skip.
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
		// #576: the public /users/{userId} page previously showed only avatar,
		// name, engagement count, and badges - it now also renders bio/skills/
		// languages via the shared ProfileFieldsView component (preferredContact
		// is deliberately excluded from this page, see #1028).
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

		// #973: OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Dashboard");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_AsOlaf_SkipLink_MovesFocusToMainContent()
	{
		// einsatzbereit#1284: same bypass gap as HomePage's skip link, but the
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
	public async Task OrgDashboardPage_LayoutLoadFailed_AsOlaf_HasNoSeriousA11yViolations()
	{
		// #1234: a failed dashboard-layout fetch now renders its own inline
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
		// #1780: the KPI endpoint is a different request from the layout fetch
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
		// #762 rebuilt the dashboard as a widget grid; the Calendar widget's
		// color-picker dialog only exists in the DOM while open, so the plain
		// page-load scan above can't reach it.
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
		// einsatzbereit#1726: picking a color that fails the 4.5:1 chip-text
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

		// #973: OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Opportunities");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgMembersPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		// Members lives in the page header's section rail (OrgPageHeader.tsx) -
		// the same rail an organizer uses, and unambiguous unlike a bare
		// "member" name match, which the Settings widget's own member-count link
		// also answers to.
		await Page.GetByTestId("org-tab-members").ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #973: OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Members");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgMembersPage_MemberRowWithPromoteDemoteButtons_AsOlaf_HasNoSeriousA11yViolations()
	{
		// #1050: the new "Promote to organizer"/"Demote to member" button pair
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
		// einsatzbereit#1294: this button's accessible name now interpolates
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

		// #771: the tab bar is gone - reach the page via the Settings widget's
		// "Edit settings" link instead.
		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #973: OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Settings");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationSettingsPage_EditMode_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The Edit/Save/Cancel buttons moved from inline page content into the
		// header's quick actions (#771 follow-up) - the read-only scan above
		// never opens the edit form itself, so scan it separately here.
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
		// #851: OrgSettingsPage gained the same react-hook-form + zod
		// validation as CreateOrganizationModal (previously it had none) -
		// scan the new inline validation-error state, not just the clean
		// edit-mode form covered above.
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
		// #845: the "Remove" button next to the logo only renders once an
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

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"A11yLogo {suffix}" });
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
		// Corner-to-corner placement (#782) renders its own extra surface
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
		// The "Add Widget" picker (#771 follow-up review feedback) only
		// exists in the DOM while open - the edit-mode scan above never
		// opens it.
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
		// #1932: a saved layout with some rows narrower than the full grid
		// now renders each row as its own independent, width-capped grid
		// container (groupIntoRowBands in widgetCatalog.ts) instead of one
		// shared full-width grid - a DOM shape the plain page-load scan above
		// never reaches, since a fresh org's DEFAULT_LAYOUT always fills
		// every row edge to edge. Settings (full width) plus VolunteerStats
		// (a separate, narrower row right below it) reproduces the mixed
		// shape - one uncapped band next to a capped one - that #1932's own
		// fix is about.
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
	/// Same per-widget removal loop OrgDashboardCustomizeTests.cs and
	/// OrgDashboardRowBandingTests.cs each already have their own copy of -
	/// kept local here too rather than shared, matching how every VisualTests
	/// class in this suite already owns its own copy of this kind of setup
	/// helper.
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
		// #1122: UpcomingOpportunitiesWidget and QuickCheckInWidget's empty
		// states gained a "Create one" CTA (EmptyState's new compact
		// variant) that opens CreateVolunteerOpportunityModal directly from
		// the widget - worded distinctly from CreateOpportunityWidget's own
		// "Create opportunity" button (also on this dashboard by default)
		// so the two don't collide as duplicate accessible names. Olaf's
		// seeded org (used by NavigateToOrgAppDashboardAsOlafAsync above)
		// almost certainly already has opportunities by the time this suite
		// runs - a fresh, otherwise-untouched org is the only deterministic
		// way to reach this branch.
		//
		// A fresh org also has zero pending sign-ups, so this is the scan that
		// reaches ToDoWidget's #1780 "resolved" branch (the check chip plus
		// "Nothing pending - every sign-up is handled.") for real - keep the
		// org fresh here, or that state loses its only axe coverage.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await olafHttp.PostAsJsonAsync(
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
		// einsatzbereit#1306: this used to reach the page via olaf's shared
		// seed data and Skip.Test when no published opportunity with a
		// pending applicant happened to exist, so the page's "Confirm"
		// button (the finding's subject) had no guaranteed axe coverage -
		// same gap the CancelDialog test below closed for the cancel/revoke
		// dialog. Seed a fresh org/opportunity/engagement instead, mirroring
		// that pattern, so this fails loudly on a regression rather than
		// silently passing on an empty scan.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await olafHttp.PostAsJsonAsync(
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

		// #973: on a nested route, the h1 must track the breadcrumb's trailing
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
		// einsatzbereit#1048: the dashboard's "To-Do" widget counts pending
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
		var orgResponse = await olafHttp.PostAsJsonAsync(
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

		// einsatzbereit#1680: "engagements" became a real ORG_TABS entry, so the
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
		// #573: the native time slot <select> was replaced with a custom
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

		// #1708: the SignUpModal is already open and this opportunity was just
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
		// #1772: the header's language switcher wrapped each <button> in an
		// <li role="option">, which axe reports as nested-interactive
		// ("Interactive controls must not be nested", serious). This control
		// sits in the header of every page, so the violation was site-wide -
		// and still invisible to every scan in this file, because none of them
		// opens this particular overlay. Scanned on /opportunities (where the
		// review found it) rather than the home page: that route renders a
		// PageHeaderBand, so the header is transparent and this covers the
		// selector's white-on-dark variant, which no other scan reaches with
		// the menu open.
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
		// #676 Pitch 2 rewrote this modal with custom ARIA machinery (a manual
		// Tab trap, an aria-live step announcer, sr-only radio-cards, and a
		// nested unsaved-changes ConfirmDialog) that a plain page-load axe
		// scan can't reach, since the modal only exists in the DOM while open.
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
		// #851: this modal gained react-hook-form + zod validation (it
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
		// #1384: the dropdown gained cursor-based "load more" pagination on top
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
		var orgResponse = await olafHttp.PostAsJsonAsync(
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
		// #1672: MobileMenu gained a scrim, role="dialog"/aria-modal, a Tab
		// focus trap, and a body scroll lock - none of that markup existed
		// when this suite's other scans ran, and every other scan here runs
		// at the default desktop viewport (the panel is md:hidden), so
		// nothing has ever axe-scanned it. Olaf, not Vera, so the org
		// entry and its section links (#775, promoted out of the account
		// section's disclosure by #1785) are present too - the whole
		// panel's markup exercised in one scan, not just the
		// anonymous/no-org subset.
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

	// One case per administration section: they are separate routes behind a
	// shared left rail now, not four stacked sections on one page, so a single
	// scan of /administration would only ever cover the first of them.
	//
	// [Retry(2)]: this is the one AuthHelper.LoginAsync call site that reliably
	// lands in the very first concurrently-started batch of tests (see the
	// "organizations" case, which repeatedly logged as one of the first two
	// "[slow] still running" entries across many unrelated PRs' CI runs). The
	// real Keycloak round trip it drives occasionally exceeds even a generous
	// fixed timeout while the Aspire stack is still warming up - raising
	// AuthHelper's timeout from 30s to 90s across several rounds never
	// eliminated it, it just kept landing a few seconds past whatever ceiling
	// was set (63s at a 60s cap, ~93.5s at a 90s cap). A per-test retry costs
	// nothing on the common case (a fast top-of-suite login) and absorbs the
	// rare slow one without inflating every other LoginAsync caller's timeout.
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

	// #1774 introduced one shared route-level state component (RouteState) for
	// the four ways a route can fail to show what was asked for. Two scans, one
	// per shell it renders in: inside AppLayout (header/main/footer already
	// present, so this is the heading-order and landmark case) and inside the
	// org app, which bypasses AppLayout entirely and has to supply its own
	// <main> - the exact landmark the previous 404 branch got wrong by handing
	// a chrome-less NotFoundPage to a route with no layout above it.
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

		// Assert something rendered before scanning - a scan of a page whose
		// list failed to load passes vacuously.
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

		// Assert something rendered before scanning - a scan of a page whose
		// list failed to load passes vacuously.
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

	// #1224: OrgAppLayout's "not authorized" screen (a non-organizer hitting
	// a 403) had zero axe coverage before this, despite predating the fix -
	// pinning down its own unique markup while touching this file for the
	// new "error" state right below.
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

	// #1224: the new recoverable "something went wrong, try again" state (a
	// 500/network failure, as opposed to the permanent 403 above) - its own
	// unique markup, otherwise never scanned. OrgAppLayoutErrorStatesTests.cs
	// covers this state's functional behavior (branching + retry); this is
	// its axe-core pass.
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

		var response = await http.PostAsJsonAsync("/v1/organizations", new { name = $"{label} {suffix}" });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()
			?? throw new InvalidOperationException("Created organization had no id.");
	}

	[Test]
	public async Task EngagementManagementPage_CancelDialog_HasNoSeriousA11yViolations()
	{
		// #1051: the cancel/revoke ConfirmDialog gained an optional reason
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
		var orgResponse = await olafHttp.PostAsJsonAsync(
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
		var orgResponse = await olafHttp.PostAsJsonAsync(
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
		// einsatzbereit#1038: the org opportunities hub gained an Unpublish
		// action (a plain confirm, same shape as the existing unscanned Delete
		// dialog on this page) and a Cancel action whose ConfirmDialog carries
		// an optional reason <label>/<textarea> + character-counter <p> - the
		// same "new form control on a previously plain confirm" gap
		// EngagementManagementPage_CancelDialog_HasNoSeriousA11yViolations
		// above exists to cover. OrgOpportunitiesPage_AsOlaf_... never opens
		// this dialog, so seed a fresh published opportunity here instead of
		// relying on olaf's shared seed data.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await olafHttp.PostAsJsonAsync(
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

	// einsatzbereit#1297: the axe gate never opened a toast, CheckInModal,
	// SubmitFeedbackModal, or the home page's date-range popover - all four
	// states below are seeded deterministically (not "skip if missing seed
	// data" like several of the tests above) so a regression fails loudly
	// instead of silently passing on an empty scan.
	private async Task<(string OrganizationId, string OpportunityId, string EngagementId)>
		SeedConfirmedEngagementAsync(string checkInMethod, string label)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var orgResponse = await olafHttp.PostAsJsonAsync(
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
		// einsatzbereit#1305/#1297: the "Leave feedback" button (white text on
		// yellow-500) only renders for a checked-in-without-feedback engagement -
		// vera's seeded data never has one, so the base MyEngagementsPage scan
		// never actually rendered this control. Also exercises SubmitFeedbackModal
		// (einsatzbereit#1287's star-rating contrast fix).
		var (_, _, engagementId) = await SeedConfirmedEngagementAsync("Manual", "FeedbackA11y");
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");
		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/check-in", null)).EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		// #1684: ActivitySection (and this data-testid) moved from /profile to
		// its own page at /my-signups.
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// einsatzbereit#675: a checked-in Confirmed engagement is classified as
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
		// einsatzbereit#1069: the axe gate above only ever renders the
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
		// #1684: ActivitySection (and this data-testid) moved from /profile to
		// its own page at /my-signups.
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
		// einsatzbereit#1297: CheckInModal's PIN-entry state (and its
		// einsatzbereit#1289 success announcement) never had axe coverage.
		var (_, _, engagementId) = await SeedConfirmedEngagementAsync("PINCode", "CheckInModalA11y");
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		// #1684: ActivitySection (and this data-testid) moved from /profile to
		// its own page at /my-signups.
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
		// einsatzbereit#1297/#1285: no scan ever opened a toast, so the
		// white-on-yellow-500/green-600 contrast failures shipped unnoticed.
		// Also exercises einsatzbereit#1289's new success-toast dispatch on
		// confirm (previously only the failure path was announced at all).
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var orgResponse = await olafHttp.PostAsJsonAsync(
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
		// einsatzbereit#1297/#1292: none of the seven home-page filter popovers
		// were ever scanned - MiniCalendar's day-grid gained full ARIA
		// grid/keyboard-navigation semantics (einsatzbereit#1292) with no test
		// covering the open state those semantics live in.
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
		// einsatzbereit#1779: the day grid gained two states the scan above can
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

		var orgResponse = await olafHttp.PostAsJsonAsync(
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
		// Previously only covered as a side effect of the search-alert toggle's
		// own a11y test (removed along with that feature) - preserves the only
		// scan of /opportunities in its "active filter" DOM state: a
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
		// einsatzbereit#1297: /callback (the OIDC redirect landing page) never
		// had axe coverage of any kind.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/callback");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}
}
