using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AccessibilityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
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
	public async Task VolunteerOpportunityDetailPage_SignUpCta_IsReachableBeforeReportButton()
	{
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

		await Expect(Page.GetByTestId("report-opportunity")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var ctaButton = Page.GetByTestId("signup-cta").GetByRole(AriaRole.Button);
		await Expect(ctaButton).ToBeVisibleAsync();

		await Page.Keyboard.PressAsync("Tab");
		var skipLink = Page.GetByRole(AriaRole.Link, new() { Name = "Skip to content" });
		await Expect(skipLink).ToBeFocusedAsync();
		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.Locator("#main-content")).ToBeFocusedAsync();

		await Page.Keyboard.PressAsync("Tab");
		await Page.Keyboard.PressAsync("Tab");
		await Expect(ctaButton).ToBeFocusedAsync();
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_MapMarker_HasAccessibleName()
	{
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

		var mapContainer = Page.Locator(".leaflet-container");
		await Expect(mapContainer).ToHaveAttributeAsync("role", "group");
		await Expect(mapContainer).ToHaveAttributeAsync(
			"aria-label", "Map showing the location of Teststrasse 1, 12345 Musterstadt");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ProfileOverviewPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task MyEngagementsPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My sign-ups", Level = 1 }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

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

		await Expect(Page.Locator("h1")).ToHaveTextAsync("Dashboard");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_AsOlaf_SkipLink_MovesFocusToMainContent()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.Keyboard.PressAsync("Tab");
		var skipLink = Page.GetByRole(AriaRole.Link, new() { Name = "Skip to content" });
		await Expect(skipLink).ToBeFocusedAsync();

		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.Locator("#main-content")).ToBeFocusedAsync();
	}

	// Edit mode is reached by one click ("Bearbeiten"/"Edit") and is otherwise
	// unscanned by the resting-state check above - #2232 found three contrast
	// failures here (widget content washed out by the tile-dimming treatment)
	// that only show up once editing is active.
	[Test]
	public async Task OrgDashboardPage_AsOlaf_EditMode_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task EngagementManagementPage_AsOlaf_HasNoSeriousA11yViolations()
	{
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

		await Expect(Page.Locator("h1")).Not.ToHaveTextAsync("Opportunities");
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

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
	public async Task NotFoundPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/this-route-does-not-exist");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Scoped to #main-content: "Find opportunities" also appears in the header nav and the
		// footer (CTA button + link), so an unscoped GetByRole resolves to multiple elements.
		var mainContent = Page.Locator("#main-content");
		await Expect(mainContent.GetByRole(AriaRole.Link, new() { Name = "Find opportunities" })).ToBeVisibleAsync();
		await Expect(mainContent.GetByRole(AriaRole.Link, new() { Name = "Back to home" })).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

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

	[Test]
	public async Task OpportunitiesPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByTestId("opportunities-keyword-input"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}
}
