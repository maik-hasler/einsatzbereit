using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

// Regression coverage for #1666: ApplicationDbContext.GetOrCreateUserAsync and
// GetOrCreateUsersAsync used to query the User set without IgnoreQueryFilters(),
// so a shadow-deleted user's row (physically present, hidden by the global
// !IsDeleted query filter) was treated as missing and re-inserted, 500ing both
// the shadow-deleted user's own profile load and any organizer's edit of an
// opportunity that user was signed up to.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class AdminShadowDeleteUserTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetUserProfile_ShouldNotFail_WhenCallerWasShadowDeleted(
		CancellationToken cancellationToken)
	{
		var (userId, username, password) = await fixture.CreateEphemeralUserAsync(cancellationToken);
		var volunteerClient = await CreateAuthenticatedClientAsync(username, password);

		// The very first profile load lazily creates the local `user` row -
		// GetOrCreateUserAsync must run once before the shadow-delete below has
		// anything to hide.
		await volunteerClient.GetUserProfileAsync(cancellationToken);

		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");
		await adminClient.AdminShadowDeleteUserAsync(userId, cancellationToken);

		var profile = await volunteerClient.GetUserProfileAsync(cancellationToken);

		profile.Id.Should().Be(userId);
		profile.Username.Should().Be(username);
	}

	[Test]
	public async Task UpdateVolunteerOpportunity_ShouldNotFail_WhenSignedUpVolunteerWasShadowDeleted(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var (volunteerUserId, volunteerUsername, volunteerPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);
		var volunteerClient = await CreateAuthenticatedClientAsync(volunteerUsername, volunteerPassword);

		// AdminShadowDeleteUserCommandHandler looks up the local `user` row via
		// a filtered Users.FindAsync and 404s if it's missing -
		// CreateEngagementCommandHandler only does a nullable lookup on it, it
		// doesn't lazily create it, so force the same first-load path GetUserProfile
		// uses before the admin shadow-deletes below.
		await volunteerClient.GetUserProfileAsync(cancellationToken);
		await volunteerClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");
		await adminClient.AdminShadowDeleteUserAsync(volunteerUserId, cancellationToken);

		// City change is a material edit (UpdateVolunteerOpportunityCommandHandler's
		// AddressTextChanged check), which routes through
		// OpportunityNotificationHelper.NotifyActiveVolunteersAsync ->
		// dbContext.GetOrCreateUsersAsync for every actively engaged volunteer -
		// including the now shadow-deleted one.
		var act = () => olafClient.UpdateVolunteerOpportunityAsync(
			opportunity.Id,
			new UpdateVolunteerOpportunityRequest
			{
				Title = "Test Opportunity",
				Description = "Integration test opportunity",
				IsRemote = false,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Hamburg",
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
				ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
			},
			cancellationToken);

		await act.Should().NotThrowAsync();
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

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, CancellationToken cancellationToken)
	{
		var uniqueName = $"ShadowDeleteTestOrg_{Guid.NewGuid()}";
		var org = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = uniqueName }, cancellationToken);
		return org.Id.Value;
	}

	private static async Task<CreateVolunteerOpportunityResponse> CreateOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken)
	{
		return await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = "Test Opportunity",
				Description = "Integration test opportunity",
				OrganizationId = orgId,
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
	}
}
