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
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"CheckInPinSwitch Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"CheckInPinSwitch Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by CheckInAndSlotTests",
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
}
