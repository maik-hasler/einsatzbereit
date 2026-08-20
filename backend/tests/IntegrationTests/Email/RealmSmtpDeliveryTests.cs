using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;

namespace IntegrationTests.Email;

/// <summary>
/// Moved down from <c>VisualTests.EmailDeliveryTests</c> in einsatzbereit#2148.
///
/// Regression for #1070/#1342: Keycloak's realm SMTP config pointed at a
/// mailpit host that does not exist outside local dev, so every
/// verification and password-reset mail a real signup depends on was sent
/// into nothing. This exercises the same realm <c>smtpServer</c> config that
/// <c>verifyEmail</c>/<c>resetPasswordAllowed</c> use, and proves the mail
/// arrives by polling Mailpit's own store rather than trusting Keycloak's
/// 204.
///
/// It never opened a browser even as a visual test - it was pure HTTP
/// against Keycloak's admin API and Mailpit's - so it was paying for
/// Playwright and a frontend it never touched.
///
/// The backend's own SMTP path (#1341) stays end-to-end in
/// <c>VisualTests.EmailDeliveryTests</c>, as the one journey that proves a
/// real user action ends in a delivered message.
/// </summary>
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
public class RealmSmtpDeliveryTests(IntegrationTestFixture fixture)
{
	private const string Realm = "einsatzbereit";

	[Test]
	public async Task SendVerifyEmail_DeliversToMailpit_ThroughRealmSmtpConfig(
		CancellationToken cancellationToken)
	{
		var email = $"emaildelivery-{Guid.NewGuid():N}@example.test";
		using var admin = await fixture.CreateKeycloakAdminClientAsync();

		var createResponse = await admin.PostAsJsonAsync($"/admin/realms/{Realm}/users", new
		{
			username = $"emaildelivery-{Guid.NewGuid():N}",
			email,
			enabled = true,
			emailVerified = false,
			requiredActions = new[] { "VERIFY_EMAIL" },
			credentials = new[]
			{
				new { type = "password", value = $"Test1070!{Guid.NewGuid():N}", temporary = false },
			},
		}, cancellationToken);
		createResponse.EnsureSuccessStatusCode();
		var userId = createResponse.Headers.Location!.Segments[^1];

		try
		{
			var sendResponse = await admin.PutAsync(
				$"/admin/realms/{Realm}/users/{userId}/send-verify-email",
				content: null,
				cancellationToken);
			sendResponse.EnsureSuccessStatusCode();

			await AssertMailpitReceivedMessageToAsync(email, cancellationToken);
		}
		finally
		{
			await admin.DeleteAsync($"/admin/realms/{Realm}/users/{userId}", cancellationToken);
		}
	}

	/// <summary>
	/// Polls Mailpit's own message store rather than trusting the sender's
	/// HTTP response, so this fails loudly if SMTP delivery is broken - which
	/// is exactly the failure mode #1070/#1342 describe. Filtered client-side
	/// so the assertion does not depend on Mailpit's search query syntax.
	/// </summary>
	private async Task AssertMailpitReceivedMessageToAsync(
		string recipientEmail, CancellationToken cancellationToken)
	{
		using var mailpit = fixture.CreateMailpitClient();
		var deadline = DateTime.UtcNow.AddSeconds(30);

		while (DateTime.UtcNow < deadline)
		{
			var response = await mailpit.GetAsync("/api/v1/messages?limit=50", cancellationToken);
			if (response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
				if (body.TryGetProperty("messages", out var messages)
					&& messages.EnumerateArray().Any(message =>
						message.TryGetProperty("To", out var to)
						&& to.EnumerateArray().Any(recipient =>
							recipient.TryGetProperty("Address", out var address)
							&& string.Equals(
								address.GetString(), recipientEmail, StringComparison.OrdinalIgnoreCase))))
				{
					return;
				}
			}

			await Task.Delay(500, cancellationToken);
		}

		throw new Exception(
			$"Mailpit never received a message addressed to {recipientEmail} within 30s - "
			+ "the realm's SMTP config is not delivering.");
	}
}
