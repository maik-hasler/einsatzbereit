using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1775, the German vocabulary pass. "Anmelden" used to be
/// both nav.signIn (authenticate) and signUp.submit (commit to a shift) - most
/// visibly on the opportunity detail page, where the gate sentence "Melde dich
/// an, um dich fuer diesen Einsatz anzumelden." sat directly above an
/// "Anmelden" button. On the organizer side the same record's cancellation was
/// spelled three ways: a "Stornieren" button opening an "Anmeldung absagen?"
/// dialog whose confirm read "Ja, absagen".
///
/// The rules these tests lock in: "Anmelden" is authentication only, each
/// sign-up flow keeps one verb from trigger to submit, and the organizer's
/// cancellation is "Absagen" from button to dialog to confirm.
///
/// Assertions run in German (the served default) - the rest of the suite runs
/// in English, where none of these collisions existed.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SignUpVocabularyTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EngagementManagementPage_InGerman_CancelsWithAbsagenEndToEnd()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId) =
			await CreateIndividualContactOpportunityAsync(keycloak, backend, "VocabularyOrganizer");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");
		var engagementId = await ApplyAsync(veraHttp, opportunityId, "Please let me help.");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");

		// Confirmed, not Pending - the confirmed row is the one that used to
		// carry the odd verb out ("Stornieren").
		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await SwitchToGermanAsync();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var cancelButton = Page.GetByTestId($"engagement-revoke-{engagementId}");
		await Expect(cancelButton).ToHaveTextAsync("Absagen", new() { Timeout = 15_000 });
		await Expect(Page.GetByText("Stornieren")).ToHaveCountAsync(0);

		await cancelButton.ClickAsync();

		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog).ToContainTextAsync("Anmeldung absagen?");
		await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, absagen" })).ToBeVisibleAsync();
	}

	private async Task SwitchToGermanAsync()
	{
		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		// A plain <button> inside the selector's <ul>, not an option: #1825 dropped
		// the listbox/option roles this component never implemented the keyboard
		// model for. Scoped to the open menu so it cannot match anything else.
		await Page.GetByTestId("language-selector-menu")
			.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sprache wechseln" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	private static async Task<string> ApplyAsync(HttpClient http, string opportunityId, string message)
	{
		var response = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message });
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("id").GetString()!;
	}

	private static async Task<(string OpportunityId, string OrganizationId)> CreateIndividualContactOpportunityAsync(
		Uri keycloak, Uri backend, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");

		// A fresh organization per test rather than olaf's shared seed org -
		// other tests in this shared Aspire session mutate the seed orgs (see
		// EngagementCancellationReasonTests for the same reasoning).
		var createOrgResponse = await PostJsonWithRetryAsync(http,
			"/v1/organizations",
			new { name = $"{label} Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"{label} {suffix}",
			descriptionDe = "Created by SignUpVocabularyTests",
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
		return (opportunity.GetProperty("id").GetString()!, organizationId);
	}
}
