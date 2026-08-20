using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1070/#1342 (Keycloak's realm SMTP config pointed at a
/// mailpit host that does not exist outside local dev) and #1341 (the
/// backend's SmtpOptions had no auth/TLS support and silently swallowed send
/// failures). Both Keycloak and the backend now send through a configured
/// SMTP relay - locally that is the Mailpit container Aspire already runs,
/// so these tests prove mail actually leaves each sender by polling Mailpit's
/// own message store, rather than only asserting the UI/API call succeeded.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EmailDeliveryTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const string Realm = "einsatzbereit";

	[Test]
	public async Task SendVerifyEmail_DeliversToMailpit_ThroughRealmSmtpConfig()
	{
		var keycloak = Fixture.GetEndpoint("keycloak");
		var mailpit = Fixture.GetEndpoint("mailpit", "webui");
		var email = $"emaildelivery-{Guid.NewGuid():N}@example.test";

		var adminToken = await AuthHelper.GetAdminTokenAsync(keycloak);
		using var adminHttp = new HttpClient { BaseAddress = keycloak };
		adminHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var createResponse = await adminHttp.PostAsJsonAsync($"/admin/realms/{Realm}/users", new
		{
			username = $"emaildelivery-{Guid.NewGuid():N}",
			email,
			enabled = true,
			emailVerified = false,
			requiredActions = new[] { "VERIFY_EMAIL" },
			credentials = new[] { new { type = "password", value = $"Test1070!{Guid.NewGuid():N}", temporary = false } },
		});
		createResponse.EnsureSuccessStatusCode();
		var userId = createResponse.Headers.Location!.Segments[^1];

		try
		{
			// Exercises the exact same realm smtpServer config that
			// verifyEmail/resetPasswordAllowed rely on during self-registration.
			var sendResponse = await adminHttp.PutAsync(
				$"/admin/realms/{Realm}/users/{userId}/send-verify-email", content: null);
			sendResponse.EnsureSuccessStatusCode();

			await AssertMailpitReceivedMessageToAsync(mailpit, email);
		}
		finally
		{
			await adminHttp.DeleteAsync($"/admin/realms/{Realm}/users/{userId}");
		}
	}

	[Test]
	public async Task CreateEngagement_DeliversConfirmationToMailpit_ThroughBackendSmtpConfig()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var mailpit = Fixture.GetEndpoint("mailpit", "webui");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");
		var title = $"EmailDelivery {suffix}";

		var opportunityId = await CreateIndividualContactOpportunityAsync(title);
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");
		await Page.Locator("#sign-up-message").FillAsync($"Delivery check for {suffix}");
		// The submit button now carries the same label as the trigger behind it
		// ("Express interest" end to end, #1775), so the click is scoped to the
		// dialog rather than matching both.
		await Page.Locator("[role='dialog']").GetByRole(AriaRole.Button, new() { Name = "Express interest" }).ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']", new() { State = WaitForSelectorState.Detached });

		await AssertMailpitReceivedMessageToAsync(mailpit, "vera@example.com", subjectContains: suffix);
	}

	// Polls Mailpit's own message store (rather than trusting the sender's
	// HTTP response alone) so this fails loudly if SMTP delivery is broken -
	// exactly the failure mode #1070/#1341/#1342 describe. Client-side
	// filtering (recipient + optional subject substring) avoids depending on
	// Mailpit's search query syntax for an assertion this test-critical.
	private static async Task AssertMailpitReceivedMessageToAsync(
		Uri mailpit, string recipientEmail, string? subjectContains = null)
	{
		using var http = new HttpClient { BaseAddress = mailpit };
		var deadline = DateTime.UtcNow.AddSeconds(30);

		while (DateTime.UtcNow < deadline)
		{
			var response = await http.GetAsync("/api/v1/messages?limit=50");
			if (response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadFromJsonAsync<JsonElement>();
				if (body.TryGetProperty("messages", out var messages))
				{
					var found = messages.EnumerateArray().Any(message =>
						message.TryGetProperty("To", out var to)
						&& to.EnumerateArray().Any(recipient =>
							recipient.TryGetProperty("Address", out var address)
							&& string.Equals(address.GetString(), recipientEmail, StringComparison.OrdinalIgnoreCase))
						&& (subjectContains is null
							|| (message.TryGetProperty("Subject", out var subject)
								&& (subject.GetString()?.Contains(subjectContains, StringComparison.Ordinal) ?? false))));

					if (found)
						return;
				}
			}

			await Task.Delay(1000);
		}

		throw new TimeoutException(
			$"Mailpit did not receive a message to '{recipientEmail}'"
			+ (subjectContains is null ? "" : $" with subject containing '{subjectContains}'")
			+ " within 30s.");
	}

	private async Task<string> CreateIndividualContactOpportunityAsync(string title)
	{
		var keycloak = Fixture.GetEndpoint("keycloak");
		using var tokenHttp = new HttpClient { BaseAddress = keycloak };
		var tokenResponse = await tokenHttp.PostAsync(
			$"/realms/{Realm}/protocol/openid-connect/token",
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

		var backend = Fixture.GetEndpoint("backend");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var orgsResponse = await http.GetAsync("/v1/organizations");
		orgsResponse.EnsureSuccessStatusCode();
		var orgs = await orgsResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = orgs.EnumerateArray().First().GetProperty("id").GetString();

		var draftResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by EmailDeliveryTests",
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
