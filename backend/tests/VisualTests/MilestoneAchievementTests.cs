using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;

namespace VisualTests;

/// <summary>
/// Regression for #668: milestone achievements ("First Step" / "Dedicated" /
/// "Century") were awarded by matching a volunteer's live "currently confirmed"
/// engagement count against an exact threshold. That count is not monotonic -
/// it drops whenever a confirmed engagement is cancelled, including when an
/// organizer deletes the opportunity behind it (a normal, supported action).
/// A volunteer whose live count gets pulled back down this way could permanently
/// skip past a threshold and never land on it again.
///
/// The fix tracks a separate, monotonically-increasing lifetime confirmation
/// counter (<c>UserStreak.TotalConfirmedEngagements</c>, incremented on every
/// confirmation and never decremented) and evaluates milestones with
/// <c>&gt;=</c> against it instead of an exact match on the live count.
///
/// Verifying this needs a volunteer account with a guaranteed-clean history so
/// a 4-to-5 crossing can be isolated - the seeded vera/olaf accounts accumulate
/// real usage over time and are not reliable for this (see #668's own note on
/// why a live repro against them wasn't practical). A disposable Keycloak user
/// is provisioned for the duration of this test and deleted afterwards.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MilestoneAchievementTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const string Realm = "einsatzbereit";
	private const string BackendClientId = "backend";
	private const string BackendClientSecret = "backend-secret";
	private const string FrontendClientId = "frontend-test";

	[Test]
	public async Task DedicatedBadge_IsAwarded_OnFifthConfirmation_EvenAfterAnEarlierConfirmedOpportunityWasDeleted()
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		var (volunteerUsername, volunteerPassword, volunteerUserId) = await CreateDisposableVolunteerAsync(keycloak);
		try
		{
			using var olafHttp = new HttpClient { BaseAddress = backend };
			olafHttp.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", await GetTokenAsync(keycloak, "olaf", "olaf123"));

			using var volunteerHttp = new HttpClient { BaseAddress = backend };
			volunteerHttp.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", await GetTokenAsync(keycloak, volunteerUsername, volunteerPassword));

			var engagementIds = new List<string>();
			var opportunityIds = new List<string>();
			for (var i = 0; i < 5; i++)
			{
				var opportunityId = await CreateIndividualContactOpportunityAsync(olafHttp, $"Milestone668-{i}");
				opportunityIds.Add(opportunityId);
				engagementIds.Add(await ApplyAsync(volunteerHttp, opportunityId, "Please let me help."));
			}

			// Confirm the first 4 - lifetime counter reaches 4, no badge yet.
			for (var i = 0; i < 4; i++)
				await ConfirmAsync(olafHttp, engagementIds[i]);

			// Delete one of the already-confirmed opportunities. This cancels its
			// Engagement (DeleteVolunteerOpportunityCommandHandler), pulling the
			// volunteer's *live* confirmed count back down to 3 - but must not
			// affect the lifetime counter.
			var deleteResponse = await olafHttp.DeleteAsync($"/v1/volunteer-opportunities/{opportunityIds[0]}");
			deleteResponse.EnsureSuccessStatusCode();

			// Confirm the 5th - live confirmed count is now only 4 (3 remaining +
			// this one), but the lifetime counter reaches 5 and must award "Dedicated".
			await ConfirmAsync(olafHttp, engagementIds[4]);

			var achievementsResponse = await volunteerHttp.GetAsync("/v1/me/achievements");
			achievementsResponse.EnsureSuccessStatusCode();
			var achievements = await achievementsResponse.Content.ReadFromJsonAsync<JsonElement>();
			achievements.EnumerateArray()
				.Any(a => a.GetProperty("name").GetString() == "Dedicated")
				.Should().BeTrue(
					"5 lifetime confirmations must award the Dedicated badge even though " +
					"an earlier deletion left the live confirmed count at only 4");
		}
		finally
		{
			await DeleteKeycloakUserAsync(keycloak, volunteerUserId);
		}
	}

	private static async Task<string> CreateIndividualContactOpportunityAsync(HttpClient olafHttp, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		var createOrgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations",
			new { name = $"Milestone668 Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"Milestone668 {label} {suffix}",
			description = "Created by MilestoneAchievementTests",
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

	private static async Task<string> ApplyAsync(HttpClient volunteerHttp, string opportunityId, string message)
	{
		var response = await volunteerHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message });
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("id").GetString()!;
	}

	private static async Task ConfirmAsync(HttpClient olafHttp, string engagementId)
	{
		var response = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null);
		response.EnsureSuccessStatusCode();
	}

	private static async Task<(string Username, string Password, string UserId)> CreateDisposableVolunteerAsync(Uri keycloak)
	{
		var adminToken = await GetAdminTokenAsync(keycloak);
		var username = $"milestone668-{Guid.NewGuid():N}";
		var password = $"Milestone668!{Guid.NewGuid():N}";

		using var adminHttp = new HttpClient { BaseAddress = keycloak };
		adminHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var createResponse = await adminHttp.PostAsJsonAsync($"/admin/realms/{Realm}/users", new
		{
			username,
			email = $"{username}@example.test",
			enabled = true,
			emailVerified = true,
			credentials = new[] { new { type = "password", value = password, temporary = false } },
		});
		createResponse.EnsureSuccessStatusCode();
		var userId = createResponse.Headers.Location!.Segments[^1];

		var roleResponse = await adminHttp.GetAsync($"/admin/realms/{Realm}/roles/user");
		roleResponse.EnsureSuccessStatusCode();
		var role = await roleResponse.Content.ReadFromJsonAsync<JsonElement>();

		var assignRoleResponse = await adminHttp.PostAsJsonAsync(
			$"/admin/realms/{Realm}/users/{userId}/role-mappings/realm",
			new[] { new { id = role.GetProperty("id").GetString(), name = role.GetProperty("name").GetString() } });
		assignRoleResponse.EnsureSuccessStatusCode();

		return (username, password, userId);
	}

	private static async Task DeleteKeycloakUserAsync(Uri keycloak, string userId)
	{
		var adminToken = await GetAdminTokenAsync(keycloak);

		using var adminHttp = new HttpClient { BaseAddress = keycloak };
		adminHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var response = await adminHttp.DeleteAsync($"/admin/realms/{Realm}/users/{userId}");
		response.EnsureSuccessStatusCode();
	}

	private static async Task<string> GetAdminTokenAsync(Uri keycloak)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		var response = await http.PostAsync(
			$"/realms/{Realm}/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "client_credentials",
				["client_id"] = BackendClientId,
				["client_secret"] = BackendClientSecret,
			}));
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("access_token").GetString()!;
	}

	private static async Task<string> GetTokenAsync(Uri keycloak, string username, string password)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		var response = await http.PostAsync(
			$"/realms/{Realm}/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "password",
				["client_id"] = FrontendClientId,
				["username"] = username,
				["password"] = password,
				["scope"] = "openid",
			}));
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("access_token").GetString()!;
	}
}
