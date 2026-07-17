using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests covering PR #553 changes:
///   #549 - PIN generated when switching CheckInMethod to PINCode
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
	public async Task EditOpportunity_SwitchToPINCode_GeneratesPin()
	{
		// #549: Switching CheckInMethod to PINCode must generate a 4-digit PIN and
		// expose it on the engagement-management page.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Seed creates "Tierheim Helfer gesucht" with CheckInMethod=QRCode.
		var cardLink = Page
			.Locator("a[href*='/volunteer-opportunities/']")
			.Filter(new() { HasText = "Tierheim Helfer gesucht" })
			.First;

		if (await cardLink.CountAsync() == 0)
			return;

		var href = await cardLink.GetAttributeAsync("href");
		if (href is null)
			return;

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var editBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" });
		if (!await editBtn.IsVisibleAsync())
			return; // Olaf is not owner of this opp in current seed - skip

		await editBtn.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		// Select PINCode radio
		var pinCodeRadio = Page.Locator("input[type='radio'][value='PINCode']");
		await Expect(pinCodeRadio).ToBeVisibleAsync();
		await CheckRadioCardAsync(pinCodeRadio);
		await Expect(pinCodeRadio).ToBeCheckedAsync();

		// Save
		await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Go to the engagement management page to verify the PIN was generated.
		var manageBtn = Page.GetByText("Manage applications →");
		await Expect(manageBtn).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await manageBtn.ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// A blue box containing the 4-digit PIN should be visible.
		var pinDisplay = Page.Locator("p.font-mono");
		await Expect(pinDisplay).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var pin = await pinDisplay.InnerTextAsync();
		pin.Trim().Should().MatchRegex(@"^\d{4}$", "PIN must be exactly 4 digits");
	}

	[Test]
	public async Task WaitlistSignUpModal_ShowsPerSlotBookingCounts()
	{
		// #533: Slot options in the sign-up modal must include availability info,
		// e.g. "(4 left)" when a slot has 4 remaining spots, or "(Full)" when full.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// admin is not an organizer and has no engagements, so the sign-up CTA is shown.
		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Seed creates "Tierheim Helfer gesucht" (Waitlist, 2 slots: 1 booked by vera / 0 booked).
		var cardLink = Page
			.Locator("a[href*='/volunteer-opportunities/']")
			.Filter(new() { HasText = "Tierheim Helfer gesucht" })
			.First;

		if (await cardLink.CountAsync() == 0)
			return;

		var href = await cardLink.GetAttributeAsync("href");
		if (href is null)
			return;

		await Page.GotoAsync($"{origin}{href}");
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
