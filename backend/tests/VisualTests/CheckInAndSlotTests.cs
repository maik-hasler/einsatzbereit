using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests covering PR #553 changes:
///   #549 - organizer-set PIN surfaced when switching CheckInMethod to PINCode
///   #533 - Per-slot booking counts shown in sign-up modal slot picker
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CheckInAndSlotTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	/// <summary>
	/// Regression for #1016: the "Check in" button used to render for every
	/// Confirmed engagement regardless of the opportunity's CheckInMethod, so a
	/// None-method opportunity (the wizard default) showed a button that only ever
	/// led to a modal saying no check-in was needed. It must not render at all now.
	/// Supersedes the #671 regression test that used to click this same button to
	/// assert the modal's None-method instruction text - that click-through is no
	/// longer reachable from the UI once the button itself is gone for this method.
	/// </summary>
	[Test]
	public async Task CheckInButton_IsHidden_ForNoneCheckInMethod()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"CheckInNone Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"CheckInNone Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by CheckInAndSlotTests",
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

		var engagementResponse = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Applying via CheckInAndSlotTests regression check." });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		(await http.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var row = Page.Locator("li", new() { HasText = oppTitle });
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Give the card a moment to finish rendering its action row before
		// asserting a negative - there's no positive signal to wait on here.
		await Page.WaitForTimeoutAsync(500);
		await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Check in" })).Not.ToBeVisibleAsync();
	}

	/// <summary>
	/// Regression for #1016: a Manual-method opportunity must show inline
	/// instructional text instead of a "Check in" button, since clicking it can't
	/// actually check the volunteer in - only the organizer can do that.
	/// </summary>
	[Test]
	public async Task CheckInButton_ShowsInlineText_ForManualCheckInMethod()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"CheckInManual Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"CheckInManual Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by CheckInAndSlotTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "Manual",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var engagementResponse = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Applying via CheckInAndSlotTests regression check." });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		(await http.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var row = Page.Locator("li", new() { HasText = oppTitle });
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(row.GetByText("The organizer will check you in manually."))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Check in" })).Not.ToBeVisibleAsync();
	}

	private static async Task<string> GetTokenAsync(Uri keycloak, string username, string password)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		var response = await http.PostAsync(
			"/realms/einsatzbereit/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "password",
				["client_id"] = "frontend-test",
				["username"] = username,
				["password"] = password,
				["scope"] = "openid",
			}));
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("access_token").GetString()!;
	}

	[Test]
	public async Task EditOpportunity_SwitchToPINCode_ShowsSetPinOnManagePage()
	{
		// #549: an organizer switches an opportunity's check-in method to PINCode
		// in the edit wizard and types a PIN; the manage-applications page then
		// surfaces that exact PIN. (Pre-#549 the PIN was auto-generated and always
		// 4 digits; it is now organizer-set, 4-6 digits.)
		//
		// The opportunity is created via the API rather than found in the public
		// home list: that list is paginated (10/page) and, under the shared test
		// session, this seed-independent card would rarely land on page 1.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");
		const string pin = "4821";

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"CheckInPinSwitch Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"CheckInPinSwitch Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by CheckInAndSlotTests",
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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Open the edit wizard from the org app's Opportunities tab - editing
		// (like engagement management) now lives exclusively there, not on
		// the public detail page (#751).
		var oppRow = Page.Locator("li", new() { HasText = oppTitle });
		await Expect(oppRow).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await OpportunityRowHelper.ClickActionAsync(oppRow, "opportunity-edit");
		await Page.WaitForSelectorAsync("[role='dialog']");

		// Step 3 (Format): switch the check-in method to PINCode and type a PIN.
		// Click the visible label card, not the sr-only radio <input> (not a
		// reliable pointer target).
		await Page.GetByTestId("wizard-stepper-3").ClickAsync();
		await Page.Locator("label:has(input[name='checkInMethod'][value='PINCode'])").ClickAsync();
		var pinInput = Page.Locator("#create-check-in-pin");
		await Expect(pinInput).ToBeVisibleAsync();
		await pinInput.FillAsync(pin);

		// Step 4: save the edit. Use the modal-submit testid rather than the
		// "Save" text - a draft would also render a "Save as draft" button that
		// the substring name match would collide with.
		await Page.GetByTestId("wizard-stepper-4").ClickAsync();
		await Page.GetByTestId("modal-submit").ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The organizer's manage-applications page (nested in the org app)
		// surfaces the PIN exactly.
		var manageLink = oppRow.GetByRole(AriaRole.Link, new() { Name = "Manage sign-ups" });
		await Expect(manageLink).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await manageLink.ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var pinDisplay = Page.Locator("p.font-mono");
		await Expect(pinDisplay).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(pinDisplay).ToHaveTextAsync(pin);
	}

	[Test]
	public async Task ScheduledSlotsSignUpModal_ShowsPerSlotBookingCounts()
	{
		// #533: Slot options in the sign-up modal must include availability info,
		// e.g. "4 spots left" when a slot has 4 remaining spots, or "Full" when full.
		//
		// Create the ScheduledSlots opportunity (draft -> add slots -> publish) via the
		// API rather than hunting for a seed card in the paginated public list.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"SlotCounts Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"SlotCounts Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by CheckInAndSlotTests",
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

		// A ScheduledSlots opportunity needs at least one time slot before it can be
		// published; two slots with spare capacity give the picker options that
		// each render an "N spots left" availability count.
		var start = DateTimeOffset.UtcNow.AddDays(7);
		(await http.PostAsJsonAsync($"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
		{
			startDateTime = start,
			endDateTime = start.AddHours(3),
			maxParticipants = 5,
			recurrenceCount = 1,
		})).EnsureSuccessStatusCode();
		(await http.PostAsJsonAsync($"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
		{
			startDateTime = start.AddDays(7),
			endDateTime = start.AddDays(7).AddHours(3),
			maxParticipants = 3,
			recurrenceCount = 1,
		})).EnsureSuccessStatusCode();
		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		// admin is not an organizer and has no engagement, so the sign-up CTA is shown.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// "Select a slot" is the English label for the ScheduledSlots sign-up button.
		var signUpBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Select a slot" });
		await Expect(signUpBtn).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await signUpBtn.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		// #573: the time slot picker is a custom accessible dropdown (role="combobox"
		// trigger + role="option" list), not a native <select>.
		var slotDropdown = Page.Locator("#sign-up-time-slot");
		await Expect(slotDropdown).ToBeVisibleAsync();
		await slotDropdown.ClickAsync();

		var optionLocator = Page.Locator("[role='option']");
		await Expect(optionLocator.First).ToBeVisibleAsync();

		// No fixed expected string to hand a static Expect matcher - the check is
		// "some option's text matches this regex" - so poll a single EvaluateAllAsync
		// round trip (all options' textContent read together) rather than a raw,
		// un-retried AllTextContentsAsync call right after the dropdown click.
		var options = Array.Empty<string>();
		var hasAvailabilityInfo = false;
		await PollUntilAsync(async () =>
		{
			options = await optionLocator.EvaluateAllAsync<string[]>(
				"els => els.map(el => el.textContent ?? '')");
			// #987: the slot dropdown now reuses the same opportunities.spotsLeft/
			// full/unlimitedSpots strings the rest of the app uses for remaining
			// capacity, instead of its own parenthetical signUp.* copies.
			hasAvailabilityInfo = options.Any(o =>
				Regex.IsMatch(o, @"\d+ spots? left|\bFull\b|Unlimited spots", RegexOptions.IgnoreCase));
			return options.Length > 0 && hasAvailabilityInfo;
		}, () => options.Length == 0
			? "slot options must be rendered, but none were found"
			: "each slot option should include booking count info like '4 spots left'. "
				+ $"Actual options: [{string.Join(", ", options)}]");
	}

	/// <summary>
	/// Regression for #1066: a time slot with no capacity cap must never read as
	/// "full" or block sign-up, regardless of how many volunteers have joined -
	/// covers both the public detail page's badge and the sign-up modal's slot
	/// picker.
	/// </summary>
	[Test]
	public async Task ScheduledSlotsWithUnlimitedCapacity_NeverReadsAsFull()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Unlimited Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"Unlimited Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by CheckInAndSlotTests",
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

		var start = DateTimeOffset.UtcNow.AddDays(7);
		(await http.PostAsJsonAsync($"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
		{
			startDateTime = start,
			endDateTime = start.AddHours(3),
			maxParticipants = (int?)null,
			recurrenceCount = 1,
		})).EnsureSuccessStatusCode();
		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		// admin is not an organizer and has no engagement, so the sign-up CTA is shown.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// The sign-up CTA badge must read "Unlimited spots", never a full/near-capacity
		// warning. Scoped to a <p> tag - the per-time-slot list also renders
		// "Unlimited spots" in a <span>, which would otherwise make this locator ambiguous.
		await Expect(Page.Locator("p", new() { HasTextString = "Unlimited spots" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		var signUpBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Select a slot" });
		await Expect(signUpBtn).ToBeVisibleAsync();
		await Expect(signUpBtn).ToBeEnabledAsync();
		await signUpBtn.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		var slotDropdown = Page.Locator("#sign-up-time-slot");
		await Expect(slotDropdown).ToBeVisibleAsync();
		await slotDropdown.ClickAsync();

		var optionLocator = Page.Locator("[role='option']");
		await Expect(optionLocator.First).ToBeVisibleAsync();
		// #987: the slot dropdown now reuses opportunities.unlimitedSpots ("Unlimited
		// spots") instead of its own parenthetical signUp.unlimitedSpots ("(Unlimited)").
		await Expect(optionLocator.First).ToContainTextAsync("Unlimited spots");
		await Expect(optionLocator.First).Not.ToHaveAttributeAsync("aria-disabled", "true");
	}
}
