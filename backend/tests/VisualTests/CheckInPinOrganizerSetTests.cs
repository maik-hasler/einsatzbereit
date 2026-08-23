using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CheckInPinOrganizerSetTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task CreateWizard_CustomCheckInPin_IsPersistedExactly()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });

		var uniqueTitle = $"CheckInPin Custom Visual Test {Guid.NewGuid().ToString("N")[..8]}";
		await Page.Locator("#opportunity-title").FillAsync(uniqueTitle);
		await Page.Locator("#opportunity-description").FillAsync(
			"Regression test for #549 organizer-settable check-in PIN.");

		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
		await Page.Locator("#opportunity-remote").CheckAsync();

		await Page.GetByTestId("wizard-stepper-3").ClickAsync();

		await Page.Locator("label:has(input[name='participationType'][value='IndividualContact'])").ClickAsync();
		await Page.Locator("label:has(input[name='checkInMethod'][value='PINCode'])").ClickAsync();

		var pinInput = Page.Locator("#create-check-in-pin");
		await Expect(pinInput).ToBeVisibleAsync();
		await pinInput.FillAsync("482170");

		await Page.GetByTestId("wizard-stepper-4").ClickAsync();

		await Page.Locator("#create-valid-until").FillAsync(DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"));

		var createResponseTask = Page.WaitForResponseAsync(r =>
			r.Url.Contains("/v1/volunteer-opportunities") && r.Request.Method == "POST");
		await Page.GetByTestId("modal-submit").ClickAsync();
		var createResponse = await createResponseTask;
		createResponse.Ok.Should().BeTrue();

		var created = await createResponse.JsonAsync();
		var opportunityId = created!.Value.GetProperty("id").GetString();

		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		var pinResponse = await http.GetAsync($"/v1/volunteer-opportunities/{opportunityId}/check-in-pin");
		pinResponse.EnsureSuccessStatusCode();
		var persistedPin = await pinResponse.Content.ReadFromJsonAsync<string>();
		persistedPin.Should().Be("482170", "the organizer-typed PIN must be persisted exactly, not overwritten by a random one");
	}

	[Test]
	public async Task EditWizard_CheckInPinField_PrefillsExistingPin_AndGenerateRandomReplacesIt()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"CheckInPinEdit Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"CheckInPinEdit Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by CheckInPinOrganizerSetTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "PINCode",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			checkInPin = "135790",
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var oppRow = Page.Locator("li", new() { HasText = oppTitle });
		await Expect(oppRow).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await OpportunityRowHelper.ClickActionAsync(oppRow, "opportunity-edit");
		await Page.WaitForSelectorAsync("[role='dialog']");

		await Page.GetByTestId("wizard-stepper-3").ClickAsync();
		var pinInput = Page.Locator("#create-check-in-pin");
		await Expect(pinInput).ToHaveValueAsync("135790", new() { Timeout = 10_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "Generate random" }).ClickAsync();
		var generatedPin = await pinInput.InputValueAsync();
		generatedPin.Should().MatchRegex(@"^\d{6}$", "Generate random must fill in a 6-digit PIN");
		generatedPin.Should().NotBe("135790");

		await Page.GetByTestId("wizard-stepper-4").ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });

		var pinResponse = await http.GetAsync($"/v1/volunteer-opportunities/{opportunityId}/check-in-pin");
		pinResponse.EnsureSuccessStatusCode();
		var persistedPin = await pinResponse.Content.ReadFromJsonAsync<string>();
		persistedPin.Should().Be(generatedPin);
	}
}
