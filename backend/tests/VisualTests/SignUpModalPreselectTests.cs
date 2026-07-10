using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #657: SignUpModal always initialized the time-slot dropdown
/// to empty, even when a Waitlist opportunity had exactly one (non-full) time
/// slot and there was nothing else to pick - forcing an avoidable extra click
/// before "Sign up" was enabled.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SignUpModalPreselectTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task SignUpModal_PreselectsTheOnlyAvailableTimeSlot()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateWaitlistOpportunityAsync("SingleSlot", slotCount: 1);
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Select a slot" }).ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		var slotDropdown = Page.Locator("#sign-up-time-slot");
		await Expect(slotDropdown).ToBeVisibleAsync();

		var dropdownText = (await slotDropdown.TextContentAsync())?.Trim();
		dropdownText.Should().NotBeNullOrEmpty();
		dropdownText.Should().NotBe("Please select…", "the sole available slot must be pre-selected");

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign up" })).ToBeEnabledAsync();
	}

	[Test]
	public async Task SignUpModal_LeavesDropdownEmpty_WhenMultipleTimeSlotsExist()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateWaitlistOpportunityAsync("MultiSlot", slotCount: 2);
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Select a slot" }).ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		var slotDropdown = Page.Locator("#sign-up-time-slot");
		await Expect(slotDropdown).ToBeVisibleAsync();

		var dropdownText = (await slotDropdown.TextContentAsync())?.Trim();
		dropdownText.Should().Be("Please select…", "behaviour must be unchanged when more than one slot exists");

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign up" })).ToBeDisabledAsync();
	}

	private async Task<string> CreateWaitlistOpportunityAsync(string label, int slotCount)
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var suffix = Guid.NewGuid().ToString("N");

		using var tokenHttp = new HttpClient { BaseAddress = keycloak };
		var tokenResponse = await tokenHttp.PostAsync(
			"/realms/einsatzbereit/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "password",
				["client_id"] = "frontend-test",
				["username"] = "olaf",
				["password"] = "olaf123",
				["scope"] = "openid",
			}));
		tokenResponse.EnsureSuccessStatusCode();
		var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
		var token = tokenBody.GetProperty("access_token").GetString();

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var orgsResponse = await http.GetAsync("/v1/organizations");
		orgsResponse.EnsureSuccessStatusCode();
		var orgs = await orgsResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = orgs.EnumerateArray().First().GetProperty("id").GetString();

		var draftResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"SignUpModalPreselect {label} {suffix}",
			description = "Created by SignUpModalPreselectTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "Waitlist",
			checkInMethod = "None",
			isDraft = true,
		});
		draftResponse.EnsureSuccessStatusCode();
		var draft = await draftResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = draft.GetProperty("id").GetString()!;

		for (var i = 0; i < slotCount; i++)
		{
			var start = DateTimeOffset.UtcNow.AddDays(7 + i);
			var end = start.AddHours(2);
			var slotResponse = await http.PostAsJsonAsync(
				$"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
				{
					startDateTime = start,
					endDateTime = end,
					maxParticipants = 5,
					recurrenceCount = 1,
				});
			slotResponse.EnsureSuccessStatusCode();
		}

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		return opportunityId;
	}
}
