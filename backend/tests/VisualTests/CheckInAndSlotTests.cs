using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
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
	/// Regression for #671: the "Check in" modal rendered no branch for
	/// checkInMethod == "None", so clicking "Check in" on such an engagement
	/// opened a blank modal with only a title and a "Done" button.
	/// </summary>
	[Test]
	public async Task CheckInModal_ShowsInstruction_ForNoneCheckInMethod()
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

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var row = Page.Locator("li", new() { HasText = oppTitle });
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await row.GetByRole(AriaRole.Button, new() { Name = "Check in" }).ClickAsync();
		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();

		await Expect(dialog.GetByText("This opportunity doesn't require an explicit check-in step."))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
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
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Open the edit wizard (owner-only control - its presence also confirms
		// olaf is recognised as the organizer of this opportunity).
		var editBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" });
		await Expect(editBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await editBtn.ClickAsync();
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

		// The organizer's manage-applications page surfaces the PIN exactly.
		var manageBtn = Page.GetByText("Manage applications →");
		await Expect(manageBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await manageBtn.ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var pinDisplay = Page.Locator("p.font-mono");
		await Expect(pinDisplay).ToBeVisibleAsync(new() { Timeout = 15_000 });
		(await pinDisplay.InnerTextAsync()).Trim().Should().Be(pin,
			"the manage-applications page must show the organizer-set PIN exactly");
	}

	[Test]
	public async Task WaitlistSignUpModal_ShowsPerSlotBookingCounts()
	{
		// #533: Slot options in the sign-up modal must include availability info,
		// e.g. "(4 left)" when a slot has 4 remaining spots, or "(Full)" when full.
		//
		// Create the Waitlist opportunity (draft -> add slots -> publish) via the
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
			participationType = "Waitlist",
			checkInMethod = "None",
			isDraft = true,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		// A Waitlist opportunity needs at least one time slot before it can be
		// published; two slots with spare capacity give the picker options that
		// each render a "(N left)" availability count.
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
		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// "Select a slot" is the English label for the Waitlist sign-up button.
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
		var options = await optionLocator.AllTextContentsAsync();
		options.Should().NotBeEmpty("slot options must be rendered");

		var hasAvailabilityInfo = options.Any(o =>
			Regex.IsMatch(o, @"\(.*left\)|\(Full\)|\(noch \d+\)|\(Ausgebucht\)", RegexOptions.IgnoreCase));

		hasAvailabilityInfo.Should().BeTrue(
			$"each slot option should include booking count info like '(4 left)'. " +
			$"Actual options: [{string.Join(", ", options)}]");
	}
}
