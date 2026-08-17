using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #679: the "Message" textarea on a non-ScheduledSlots (Express
/// interest) sign-up was silently HTML-required with no visible/accessible
/// indication, so its markup had never been exercised by AccessibilityTests.
///
/// Regression for #1908: that same HTML `required` attribute made an empty
/// submit fall back to the browser's own native constraint-validation
/// tooltip, which follows the browser/OS UI language rather than the page's
/// chosen language. The field is validated in script now, surfacing a
/// translated inline message instead.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SignUpModalMessageFieldTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task SignUpModal_ExpressInterest_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync("MessageFieldA11y");
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		var messageField = Page.Locator("#sign-up-message");
		await Expect(messageField).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		var violations = result.Violations
			.Where(v => v.Impact is "serious" or "critical")
			.ToList();
		violations.Should().BeEmpty();
	}

	[Test]
	public async Task SignUpModal_MessageField_IsLabelledAndMarkedRequired()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync("MessageFieldLabel");
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		// The accessible name is the field name alone since #1797 unified the
		// required marker: the visible marker is an aria-hidden asterisk and
		// `aria-required` below is what announces the field as required. Not
		// the native `required` attribute (#1908) - that also runs the
		// browser's own constraint validation, which pops a native tooltip in
		// the browser/OS UI language rather than the page's chosen language.
		var messageField = Page.GetByRole(AriaRole.Textbox, new() { Name = "Message", Exact = true });
		await Expect(messageField).ToBeVisibleAsync();
		await Expect(messageField).Not.ToHaveAttributeAsync("required", "");
		await Expect(messageField).ToHaveAttributeAsync("aria-required", "true");
		await Expect(Page.Locator("label[for='sign-up-message']")).ToHaveTextAsync("Message*");
	}

	[Test]
	public async Task SignUpModal_ExpressInterest_EmptyMessage_ShowsLocalizedInlineError()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync("MessageFieldInlineError");
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		var dialog = Page.Locator("[role='dialog']");
		await Page.WaitForSelectorAsync("[role='dialog']");

		// Submit with the "Message" field left empty - scoped to the dialog
		// since the submit button shares its label with the trigger behind it
		// ("Express interest" end to end, #1775).
		await dialog.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();

		// The dialog must stay open (no submit went out) and show the
		// translated inline message rather than a native browser tooltip,
		// which this test's Chromium instance would render in English
		// regardless of the page's language.
		await Expect(dialog).ToBeVisibleAsync();
		var messageField = Page.GetByRole(AriaRole.Textbox, new() { Name = "Message", Exact = true });
		await Expect(messageField).ToHaveAttributeAsync("aria-invalid", "true");
		await Expect(messageField).ToHaveAttributeAsync("aria-describedby", "sign-up-message-error");
		await Expect(Page.Locator("#sign-up-message-error")).ToHaveTextAsync("Please enter a message.");

		// Typing clears the error instead of leaving stale, now-wrong text
		// under a field the volunteer has already fixed.
		await messageField.FillAsync("Applying via VisualTests regression check.");
		await Expect(Page.Locator("#sign-up-message-error")).Not.ToBeVisibleAsync();
		await Expect(messageField).Not.ToHaveAttributeAsync("aria-invalid", "true");
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

		var draftResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"SignUpModalMessageField {label} {suffix}",
			descriptionDe = "Created by SignUpModalMessageFieldTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = true,
		});
		draftResponse.EnsureSuccessStatusCode();
		var draft = await draftResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = draft.GetProperty("id").GetString()!;

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		return opportunityId;
	}
}
