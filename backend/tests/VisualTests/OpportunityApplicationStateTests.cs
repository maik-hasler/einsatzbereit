using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #644: a hard/direct navigation to an opportunity's detail
/// page could lose the volunteer's "already applied" status - the OIDC token
/// isn't always restored from storage before the details fetch first fires,
/// and the effect never re-ran once it was. Separately, the sign-up modal
/// fell back to a generic "Unknown error" instead of the backend's actual
/// conflict message on a duplicate sign-up attempt.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityApplicationStateTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task DetailPage_KeepsAlreadyAppliedStatus_AfterHardNavigation()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync("HardNav");
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Page.Locator("textarea").FillAsync("Applying via VisualTests regression check.");
		await Page.GetByRole(AriaRole.Button, new() { Name = "Sign up" }).ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Genuine hard navigation (full reload), not an SPA transition - this is
		// exactly the race the fix addresses: the OIDC token may not be restored
		// from storage by the time the details fetch first fires.
		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByText("Your application")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Express interest" }))
			.Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task SignUpModal_ShowsBackendConflictMessage_OnDuplicateSignUp()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync("DuplicateError");
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Page.Locator("textarea").FillAsync("First sign-up attempt.");

		// A second page in the same browser context: the OIDC user store is
		// localStorage (frontend/src/main.tsx), shared across pages in one
		// context, so this page is already authenticated as vera too - it
		// simulates a second tab racing the first to sign up for the same
		// opportunity.
		var page2 = await Context.NewPageAsync();
		await page2.GotoAsync(detailUrl);
		await page2.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await page2.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await page2.Locator("textarea").FillAsync("Second (duplicate) sign-up attempt.");

		await Page.GetByRole(AriaRole.Button, new() { Name = "Sign up" }).ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var secondSignUpResponseTask = page2.WaitForResponseAsync(r =>
			r.Url.Contains($"/volunteer-opportunities/{opportunityId}/engagements") &&
			r.Request.Method == "POST");
		await page2.GetByRole(AriaRole.Button, new() { Name = "Sign up" }).ClickAsync();
		var secondSignUpResponse = await secondSignUpResponseTask;
		secondSignUpResponse.Status.Should().Be(409);

		var errorText = await page2.Locator("p.text-red-600").First.TextContentAsync();
		errorText.Should().NotBeNullOrEmpty();
		errorText.Should().NotContain("Unknown error");
		errorText.Should().Contain("already signed up");
	}

	private async Task<string> CreateIndividualContactOpportunityAsync(string label)
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
				["client_id"] = "frontend",
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

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"OpportunityApplicationState {label} {suffix}",
			description = "Created by OpportunityApplicationStateTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		return opportunity.GetProperty("id").GetString()!;
	}
}
