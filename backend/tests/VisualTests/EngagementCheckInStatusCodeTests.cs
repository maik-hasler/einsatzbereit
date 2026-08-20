using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;

namespace VisualTests;

/// <summary>
/// Regression tests for #710 (revive feature/ddd-improvements): the
/// Result-pattern migration initially mapped two engagement check-in
/// failures to the wrong HTTP status code (409/403 instead of the
/// pre-refactor 400). Both API-level, no browser needed.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementCheckInStatusCodeTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task CheckInEngagement_Returns400_WhenEngagementIsNotConfirmed()
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp, "/v1/organizations", new { name = $"CheckInStatusCode Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"CheckInStatusCode Opportunity {suffix}",
			descriptionDe = "Created by EngagementCheckInStatusCodeTests.",
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
		var opportunityId = opportunity.GetProperty("id").GetString();

		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { type = "IndividualContact", message = "I'd like to help!" });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		var checkInResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/check-in", null);

		checkInResponse.StatusCode.Should().Be(
			HttpStatusCode.BadRequest,
			"Engagement.CheckIn() on a Pending (not yet Confirmed) engagement must return 400, matching pre-refactor behaviour");
	}

	[Test]
	public async Task CheckInWithPin_Returns400_WhenCheckingInSomeoneElsesEngagement()
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp, "/v1/organizations", new { name = $"CheckInPinOwner Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		const string pin = "482170";
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"CheckInPinOwner Opportunity {suffix}",
			descriptionDe = "Created by EngagementCheckInStatusCodeTests.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "PINCode",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			checkInPin = pin,
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { type = "IndividualContact", message = "I'd like to help!" });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		var confirmResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", null);
		confirmResponse.EnsureSuccessStatusCode();

		// olaf is also a "user" (per test-seed roles) - trying to check in vera's
		// engagement with the correct PIN must still fail: only vera owns it.
		var wrongOwnerResponse = await olafHttp.PostAsJsonAsync(
			$"/v1/me/engagements/{engagementId}/check-in", new { pin });

		wrongOwnerResponse.StatusCode.Should().Be(
			HttpStatusCode.BadRequest,
			"CheckInWithPin on someone else's engagement must return 400, matching pre-refactor behaviour");
	}

	[Test]
	public async Task CheckInWithPin_ReturnsIdenticalNotOwnerError_RegardlessOfPinCorrectness_ForNonOwner()
	{
		// Regression for #806: a non-owner guessing PINs must not be able to tell a
		// correct guess from a wrong one via the error response - ownership has to
		// be checked before the PIN is ever compared, closing the validity oracle.
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp, "/v1/organizations", new { name = $"CheckInPinOracle Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		const string pin = "482170";
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"CheckInPinOracle Opportunity {suffix}",
			descriptionDe = "Created by EngagementCheckInStatusCodeTests.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "PINCode",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			checkInPin = pin,
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { type = "IndividualContact", message = "I'd like to help!" });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		var confirmResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", null);
		confirmResponse.EnsureSuccessStatusCode();

		var wrongPinResponse = await olafHttp.PostAsJsonAsync(
			$"/v1/me/engagements/{engagementId}/check-in", new { pin = "000000" });
		var correctPinResponse = await olafHttp.PostAsJsonAsync(
			$"/v1/me/engagements/{engagementId}/check-in", new { pin });

		wrongPinResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		correctPinResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var wrongPinProblem = await wrongPinResponse.Content.ReadFromJsonAsync<JsonElement>();
		var correctPinProblem = await correctPinResponse.Content.ReadFromJsonAsync<JsonElement>();

		wrongPinProblem.GetProperty("errorCode").GetString().Should().Be("Engagement.NotOwner");
		correctPinProblem.GetProperty("errorCode").GetString().Should().Be(
			"Engagement.NotOwner",
			"a non-owner must never learn whether their PIN guess was correct");
	}

	[Test]
	public async Task CheckInWithPin_Returns403_AfterTooManyFailedAttempts()
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp, "/v1/organizations", new { name = $"CheckInPinLockout Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		const string pin = "482170";
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"CheckInPinLockout Opportunity {suffix}",
			descriptionDe = "Created by EngagementCheckInStatusCodeTests.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "PINCode",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			checkInPin = pin,
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { type = "IndividualContact", message = "Ready to help!" });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		var confirmResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", null);
		confirmResponse.EnsureSuccessStatusCode();

		// 5 wrong guesses trip the per-engagement lockout, independent of the
		// generic 100 req/60s rate limit.
		for (var attempt = 0; attempt < 5; attempt++)
		{
			var wrongAttempt = await veraHttp.PostAsJsonAsync(
				$"/v1/me/engagements/{engagementId}/check-in", new { pin = "000000" });
			wrongAttempt.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		}

		var lockedOutAttempt = await veraHttp.PostAsJsonAsync(
			$"/v1/me/engagements/{engagementId}/check-in", new { pin });

		lockedOutAttempt.StatusCode.Should().Be(
			HttpStatusCode.Forbidden,
			"the correct PIN must still be rejected once the per-engagement lockout has tripped");
	}

}
