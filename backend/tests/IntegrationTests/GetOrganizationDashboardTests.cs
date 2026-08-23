using System.Net.Http.Headers;
using AwesomeAssertions;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetOrganizationDashboardTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetOrganizationDashboard_ShouldReturnAllZeros_WhenOrganizationHasNoOpportunities(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);

		var kpis = await olafClient.GetOrganizationDashboardAsync(orgId, cancellationToken);

		kpis.PendingEngagements.Should().Be(0);
		kpis.ConfirmedEngagementsTotal.Should().Be(0);
	}

	[Test]
	public async Task GetOrganizationDashboard_ShouldReturnAccurateKpis_ForOrganizationWithMixedEngagementStatuses(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);

		var opportunityA = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);
		var opportunityB = await CreateScheduledSlotsOpportunityAsync(olafClient, orgId, cancellationToken);

		var slotNear = (await olafClient.CreateTimeSlotAsync(
			opportunityB.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(3),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(3).AddHours(2),
				MaxParticipants = 5,
				RecurrenceCount = 1,
			},
			cancellationToken)).Single().Id;

		var slotFar = (await olafClient.CreateTimeSlotAsync(
			opportunityB.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(20),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(20).AddHours(2),
				MaxParticipants = 5,
				RecurrenceCount = 1,
			},
			cancellationToken)).Single().Id;
		await olafClient.PublishVolunteerOpportunityAsync(opportunityB.Id, cancellationToken);

		await veraClient.CreateEngagementAsync(
			opportunityA.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var toBeCancelled = await olafClient.CreateEngagementAsync(
			opportunityA.Id,
			new CreateEngagementRequest { Message = "Second helper" },
			cancellationToken);
		await olafClient.CancelEngagementAsync(
			toBeCancelled.Id,
			new CancelEngagementRequest { Reason = "No longer needed" },
			cancellationToken);

		var nearEngagement = await veraClient.CreateEngagementAsync(
			opportunityB.Id,
			new CreateEngagementRequest { TimeSlotId = slotNear },
			cancellationToken);
		await olafClient.ConfirmEngagementAsync(nearEngagement.Id, cancellationToken);

		var farEngagement = await olafClient.CreateEngagementAsync(
			opportunityB.Id,
			new CreateEngagementRequest { TimeSlotId = slotFar },
			cancellationToken);
		await olafClient.ConfirmEngagementAsync(farEngagement.Id, cancellationToken);

		var kpis = await olafClient.GetOrganizationDashboardAsync(orgId, cancellationToken);

		kpis.PendingEngagements.Should().Be(1);
		kpis.ConfirmedEngagementsTotal.Should().Be(2);
	}

	[Test]
	public async Task GetOrganizationDashboard_ShouldNotCountAnotherOrganizationsEngagements(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity1 = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken);
		var engagement1 = await veraClient.CreateEngagementAsync(
			opportunity1.Id,
			new CreateEngagementRequest { Message = "Helping org1" },
			cancellationToken);
		await olafClient.ConfirmEngagementAsync(engagement1.Id, cancellationToken);

		var org2Id = await CreateOrganizationAsync(veraClient, cancellationToken);
		var veraOrganizerClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var opportunity2 = await CreateOpportunityAsync(veraOrganizerClient, org2Id, cancellationToken);
		await olafClient.CreateEngagementAsync(
			opportunity2.Id,
			new CreateEngagementRequest { Message = "Helping org2" },
			cancellationToken);

		var org1Kpis = await olafClient.GetOrganizationDashboardAsync(org1Id, cancellationToken);

		org1Kpis.PendingEngagements.Should().Be(0);
		org1Kpis.ConfirmedEngagementsTotal.Should().Be(1);
	}

	[Test]
	public async Task GetOrganizationDashboard_ShouldSucceed_WhenRequestingUserIsAPlainMember(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);

		await fixture.AddPlainMemberDirectlyAsync(orgId, vera.Id, cancellationToken);

		var kpis = await veraClient.GetOrganizationDashboardAsync(orgId, cancellationToken);

		kpis.PendingEngagements.Should().Be(0);
		kpis.ConfirmedEngagementsTotal.Should().Be(0);
	}

	[Test]
	public async Task GetDashboardLayout_ShouldSucceed_WhenRequestingUserIsAPlainMember(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);

		await fixture.AddPlainMemberDirectlyAsync(orgId, vera.Id, cancellationToken);

		var layout = await veraClient.GetDashboardLayoutAsync(orgId, cancellationToken);

		layout.HasCustomLayout.Should().BeFalse();
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
		var uniqueName = $"DashboardTestOrg_{Guid.NewGuid()}";
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
				TitleDe = "Test Opportunity",
				DescriptionDe = "Integration test opportunity",
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

	private static async Task<CreateVolunteerOpportunityResponse> CreateScheduledSlotsOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken)
	{
		return await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = "ScheduledSlots Opportunity",
				DescriptionDe = "Integration test ScheduledSlots opportunity",
				OrganizationId = orgId,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "ScheduledSlots",
				CheckInMethod = "None",
				IsDraft = true,
			},
			cancellationToken);
	}
}
