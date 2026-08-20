using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #588: an uploaded user avatar / organization logo used to
/// only render on that user's/organization's own profile page - the nav bar
/// and opportunity card badges always showed initials, even with an image on
/// file, because neither read the field that already carried the URL.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AvatarAndLogoDisplayTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// 1x1 transparent PNG.
	private static readonly byte[] TinyPng = Convert.FromBase64String(
		"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

	// Serialized against every other test that writes vera's single avatar_url
	// field. This test uploads an avatar and then asserts the nav bar renders an
	// <img> instead of her initials - but RemoveUserAvatar_... below (same class,
	// so TUnit co-schedules the two by default) uploads and then DELETEs that
	// same field. Those two are the only writers of /v1/users/me/avatar left in
	// the suite (AccessibilityTests' ProfileOverviewPage_EditModeWithAvatar_...
	// was a third until einsatzbereit#2148 moved it down to a component test). With the delete landing between this upload and this assertion,
	// GET /v1/users/me returns avatarUrl: null and AccountControls renders the
	// initials span with no <img> at all - the exact observed CI failure
	// ("element(s) not found", aria snapshot showing button "User menu": VV).
	// A keyed [NotInParallel] is cheap here: the assembly parallel limit is
	// already ProcessorCount - 2, so serializing three tests costs almost nothing.
	[Test]
	[NotInParallel("visualtests-vera-avatar")]
	public async Task UploadedAvatar_ShowsInNavBar_InsteadOfInitials()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync();

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		using var content = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(TinyPng);
		fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		content.Add(fileContent, "file", "avatar.png");

		var response = await http.PutAsync("/v1/users/me/avatar", content);
		response.EnsureSuccessStatusCode();

		// Header only fetches the profile once on mount, so a full navigation is
		// needed to pick up the freshly uploaded avatar - observed failing
		// consistently (not just occasionally) on the very first such
		// navigation in CI, still showing initials. Ruled out both output
		// caching (GetUserProfile isn't .CacheOutput-decorated - only
		// AllowAnonymous, response-invariant endpoints opt in, see
		// OutputCachingExtensions's #1391 comment) and browser HTTP caching
		// (Program.cs sets Cache-Control: no-store on every authenticated
		// response, so a stale cached GET isn't possible either). Whatever the
		// remaining timing gap is between the upload's HTTP connection and the
		// page's own, poll via fresh navigations instead of asserting on a
		// single one - the same fix already applied to this file's DELETE-flow
		// tests below for the identical class of issue (#946).
		var userMenu = Page.GetByRole(AriaRole.Button, new() { Name = "User menu" });
		for (var attempt = 0; ; attempt++)
		{
			await Page.GotoAsync(origin);
			await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });
			userMenu = Page.GetByRole(AriaRole.Button, new() { Name = "User menu" });
			if (await userMenu.Locator("img").IsVisibleAsync() || attempt >= 5)
				break;
			await Task.Delay(500);
		}
		await Expect(userMenu.Locator("img")).ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task OrganizationLogo_ShowsOnOpportunityCard_InsteadOfInitials()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync();

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"VisualLogo {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		using var content = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(TinyPng);
		fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		content.Add(fileContent, "file", "logo.png");

		(await http.PutAsync($"/v1/organizations/{organizationId}/logo", content)).EnsureSuccessStatusCode();

		var tag = $"visual588-{suffix}";
		var oppTitle = $"VisualLogo Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by AvatarAndLogoDisplayTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
			tags = new[] { tag },
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var start = DateTimeOffset.UtcNow.AddDays(3);
		var end = start.AddHours(2);
		(await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = end, maxParticipants = 5, recurrenceCount = 1 }))
			.EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/opportunities?tag={tag}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var card = Page.Locator("li", new() { HasText = oppTitle });
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgLink = card.Locator($"a[href='/organizations/{organizationId}']");
		await Expect(orgLink.Locator("img")).ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task RemoveOrganizationLogo_ClearsLogoUrl_AndHidesRemoveButton()
	{
		// #845: the organization-logo upload feature had no matching
		// delete/remove endpoint, unlike the symmetric opportunity-banner
		// feature. Verifies the new DELETE endpoint and its "Remove" button
		// in OrgSettingsPage actually clear the stored logo.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync();

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"LogoRemoval {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		using var content = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(TinyPng);
		fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		content.Add(fileContent, "file", "logo.png");

		(await http.PutAsync($"/v1/organizations/{organizationId}/logo", content)).EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();

		var removeButton = Page.GetByTestId("logo-remove");
		await Expect(removeButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await removeButton.ClickAsync();
		await Expect(removeButton).ToBeHiddenAsync(new() { Timeout = 10_000 });

		// The DELETE the button click awaited has already committed by the time
		// it resolves (TransactionPipelineBehavior commits before the endpoint
		// returns), but this is a separate HTTP connection from a fresh
		// HttpClient - poll briefly instead of asserting on a single read, to
		// absorb any connection-pool/scheduling jitter between the two rather
		// than flake on it (observed intermittently in CI - see #946).
		JsonElement afterOrg = default;
		for (var attempt = 0; ; attempt++)
		{
			var afterResponse = await http.GetAsync($"/v1/organizations/{organizationId}");
			afterResponse.EnsureSuccessStatusCode();
			afterOrg = await afterResponse.Content.ReadFromJsonAsync<JsonElement>();
			if (afterOrg.GetProperty("logoUrl").ValueKind == JsonValueKind.Null || attempt >= 5)
				break;
			await Task.Delay(500);
		}
		afterOrg.GetProperty("logoUrl").ValueKind.Should().Be(JsonValueKind.Null);
	}

	// Same keyed [NotInParallel] as UploadedAvatar_... above - this test's DELETE
	// is what makes that one's uploaded avatar vanish mid-assertion.
	[Test]
	[NotInParallel("visualtests-vera-avatar")]
	public async Task RemoveUserAvatar_ClearsAvatarUrl_AndHidesRemoveButton()
	{
		// #1063: the user-avatar upload feature had no matching delete/remove
		// endpoint, unlike the symmetric organization-logo feature. Verifies the
		// new DELETE endpoint and its "Remove" button in ProfileOverviewPage
		// actually clear the stored avatar.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync();

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		using var content = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(TinyPng);
		fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		content.Add(fileContent, "file", "avatar.png");

		(await http.PutAsync("/v1/users/me/avatar", content)).EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("profile-edit").ClickAsync();
		await Expect(Page.GetByTestId("profile-save")).ToBeVisibleAsync();

		var removeButton = Page.GetByTestId("avatar-remove");
		await Expect(removeButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await removeButton.ClickAsync();
		await Expect(removeButton).ToBeHiddenAsync(new() { Timeout = 10_000 });

		// Same connection-pool/scheduling jitter as RemoveOrganizationLogo below -
		// poll briefly rather than asserting on a single read (#946).
		JsonElement afterProfile = default;
		for (var attempt = 0; ; attempt++)
		{
			var afterResponse = await http.GetAsync("/v1/users/me");
			afterResponse.EnsureSuccessStatusCode();
			afterProfile = await afterResponse.Content.ReadFromJsonAsync<JsonElement>();
			if (afterProfile.GetProperty("avatarUrl").ValueKind == JsonValueKind.Null || attempt >= 5)
				break;
			await Task.Delay(500);
		}
		afterProfile.GetProperty("avatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
	}

	private async Task<string> GetAccessTokenAsync()
	{
		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in sessionStorage after login");
		return token!;
	}
}
