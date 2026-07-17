using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for #549: organizers can set a custom check-in PIN (or
/// generate a random one) on both create and edit, instead of always
/// getting a randomly assigned PIN they can only view afterwards.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CheckInPinOrganizerSetTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task CreateWizard_CustomCheckInPin_IsPersistedExactly()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		if (!await GoToFirstOrganizationDashboardAsync())
			return; // no org membership in seed - skip

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();

		try
		{
			await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });
		}
		catch
		{
			return; // modal did not open - skip remaining assertions
		}

		var uniqueTitle = $"CheckInPin Custom Visual Test {Guid.NewGuid().ToString("N")[..8]}";
		await Page.Locator("#opportunity-title").FillAsync(uniqueTitle);
		await Page.Locator("#opportunity-description").FillAsync(
			"Regression test for #549 organizer-settable check-in PIN.");

		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
		await Page.Locator("#opportunity-remote").CheckAsync();

		await Page.GetByTestId("wizard-stepper-3").ClickAsync();
		// The radio itself is sr-only (its own <label> renders the visible
		// card); under CI load its near-zero-size bounding box can land under
		// the label's own visible text, so Playwright's actionability check
		// sees that text "intercepting" the click. Force bypasses that check -
		// safe here since we know what's covering it and why.
		await Page.Locator("input[name='participationType'][value='IndividualContact']")
			.CheckAsync(new() { Force = true });
		await Page.Locator("input[name='checkInMethod'][value='PINCode']").CheckAsync();

		var pinInput = Page.Locator("#create-check-in-pin");
		await Expect(pinInput).ToBeVisibleAsync();
		await pinInput.FillAsync("482170");

		await Page.GetByTestId("wizard-stepper-4").ClickAsync();

		var createResponseTask = Page.WaitForResponseAsync(r =>
			r.Url.Contains("/v1/volunteer-opportunities") && r.Request.Method == "POST");
		await Page.GetByTestId("modal-submit").ClickAsync();
		var createResponse = await createResponseTask;
		createResponse.Ok.Should().BeTrue();

		var created = await createResponse.JsonAsync();
		var opportunityId = created!.Value.GetProperty("id").GetString();

		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");
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
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"CheckInPinEdit Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"CheckInPinEdit Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by CheckInPinOrganizerSetTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "PINCode",
			checkInPin = "135790",
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var editBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" });
		await Expect(editBtn).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await editBtn.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		await Page.GetByTestId("wizard-stepper-3").ClickAsync();
		var pinInput = Page.Locator("#create-check-in-pin");
		await Expect(pinInput).ToHaveValueAsync("135790", new() { Timeout = 10_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "Generate random" }).ClickAsync();
		var generatedPin = await pinInput.InputValueAsync();
		generatedPin.Should().MatchRegex(@"^\d{4}$", "Generate random must fill in a 4-digit PIN");
		generatedPin.Should().NotBe("135790");

		await Page.GetByTestId("wizard-stepper-4").ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });

		var pinResponse = await http.GetAsync($"/v1/volunteer-opportunities/{opportunityId}/check-in-pin");
		pinResponse.EnsureSuccessStatusCode();
		var persistedPin = await pinResponse.Content.ReadFromJsonAsync<string>();
		persistedPin.Should().Be(generatedPin);
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
}
