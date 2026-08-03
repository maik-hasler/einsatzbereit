using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class UserDataExportTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task ExportMyData_ShouldReturnProfileEngagementsAchievementsStreakAndMemberships_ForTheCurrentUser(
		CancellationToken cancellationToken)
	{
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var veraProfile = await veraClient.GetUserProfileAsync(cancellationToken);

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Export Test Org" }, cancellationToken);
		var opportunity = await olafClient.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = "Export Test Opportunity",
				Description = "Integration test opportunity for account data export",
				OrganizationId = org.Id.Value,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
				ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
			},
			cancellationToken);

		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		// Confirming creates a fresh user_streak row (RecordActivity/RecordConfirmedEngagement)
		// and awards the "first-step" milestone achievement for vera.
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value,
			new CreateInvitationRequest { InviteeId = veraProfile.Id, Role = "Member" },
			cancellationToken);
		await veraClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		var export = await veraClient.ExportMyDataAsync(cancellationToken);

		export.Profile.Id.Should().Be(veraProfile.Id);
		export.Profile.Username.Should().Be("vera");
		export.Profile.Email.Should().Be(veraProfile.Email);

		export.Engagements.Should().ContainSingle(e => e.Id == engagement.Id && e.Status == "Confirmed");

		export.Achievements.Should().ContainSingle(a => a.Key == "first-step");

		// ActivityStreak is deterministic here (Respawn wipes user_streak before the test, and
		// this test's own ConfirmEngagement call is the only thing that touches it). LoginStreak
		// is deliberately not asserted: LoginStreakMiddleware's once-per-day dedup is an in-process
		// static that outlives Respawn's per-test DB reset, so its value depends on whether some
		// earlier test already recorded a login for the shared "vera" account today.
		export.Streak.ActivityStreak.Should().Be(1);

		export.OrganizationMemberships.Should().ContainSingle(m =>
			m.OrganizationId == org.Id.Value
			&& m.OrganizationName == "Export Test Org"
			&& m.Role == "Member");
	}

	[Test]
	public async Task ExportMyData_ShouldReturnNoActivityButAnEarlyAdopterAchievement_ForABrandNewUser(
		CancellationToken cancellationToken)
	{
		var (_, ephemeralUsername, ephemeralPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);
		var ephemeralClient = await CreateAuthenticatedClientAsync(ephemeralUsername, ephemeralPassword);

		var export = await ephemeralClient.ExportMyDataAsync(cancellationToken);

		export.Profile.Username.Should().Be(ephemeralUsername);
		export.Profile.Bio.Should().BeNull();
		export.Engagements.Should().BeEmpty();
		// This is the first ever authenticated request for this brand new username, so
		// LoginStreakMiddleware's once-per-day dedup is guaranteed not to have fired for it yet,
		// and Respawn wiped user_streak before this test - meaning this user is deterministically
		// among the first 100 to log in against this reset DB, so the "early-adopter" achievement
		// (#1000) is deterministically awarded rather than flaky.
		export.Achievements.Should().ContainSingle(a => a.Key == "early-adopter");
		export.Streak.LoginStreak.Should().Be(1);
		export.Streak.ActivityStreak.Should().Be(0);
		export.OrganizationMemberships.Should().BeEmpty();
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(
		string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}
}
