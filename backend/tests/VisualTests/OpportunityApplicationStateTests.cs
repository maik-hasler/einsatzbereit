using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Page.Locator("textarea").FillAsync("Applying via VisualTests regression check.");

		await Page.Locator("[role='dialog']").GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByTestId("application-status").GetByText("Your sign-up", new() { Exact = true }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Express interest" }))
			.Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task DetailPage_KeepsAlreadyAppliedStatus_WhenEarlierUnauthenticatedResponseResolvesLast()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync("RaceGuard");
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Page.Locator("textarea").FillAsync("Applying via race-guard regression check.");
		await Page.Locator("[role='dialog']").GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var releaseAnonymousRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		await Page.RouteAsync($"**/v1/volunteer-opportunities/{opportunityId}", async route =>
		{
			if (route.Request.Method == "GET" && !route.Request.Headers.ContainsKey("authorization"))
			{
				await releaseAnonymousRead.Task;
			}

			await route.ContinueAsync();
		});

		await Page.GotoAsync(detailUrl);

		var signUpStatus = Page.GetByTestId("application-status")
			.GetByText("Your sign-up", new() { Exact = true });

		await Expect(signUpStatus).ToBeVisibleAsync(new() { Timeout = 15_000 });

		releaseAnonymousRead.TrySetResult();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(signUpStatus).ToBeVisibleAsync(new() { Timeout = 15_000 });
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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Page.Locator("textarea").FillAsync("First sign-up attempt.");

		var page2 = await Context.NewPageAsync();
		await AuthHelper.FastSignInAsync(page2, Fixture, frontend, "vera", "vera123", pinActiveOrg: false);
		await page2.GotoAsync(detailUrl);
		await page2.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await page2.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await page2.Locator("textarea").FillAsync("Second (duplicate) sign-up attempt.");

		await Page.Locator("[role='dialog']").GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var secondSignUpResponseTask = page2.WaitForResponseAsync(r =>
			r.Url.Contains($"/volunteer-opportunities/{opportunityId}/engagements") &&
			r.Request.Method == "POST");
		await page2.Locator("[role='dialog']").GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		var secondSignUpResponse = await secondSignUpResponseTask;
		secondSignUpResponse.Status.Should().Be(409);

		var errorText = await page2.GetByRole(AriaRole.Alert).First.TextContentAsync();
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

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"OpportunityApplicationState {label} {suffix}",
			descriptionDe = "Created by OpportunityApplicationStateTests",
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
		return opportunity.GetProperty("id").GetString()!;
	}
}
