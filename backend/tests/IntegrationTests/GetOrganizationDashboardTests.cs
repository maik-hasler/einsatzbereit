using System.Net.Http.Headers;
using AwesomeAssertions;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

// Regression coverage for #1398: OrganizationDashboardReadRepository used to
// materialize every opportunity id for an org into memory and re-check it with
// in-memory Contains(), instead of an IN (SELECT ...) subquery. These tests pin
// down the KPI counts themselves so a future change to that query keeps
// producing the same numbers, whatever shape the query takes underneath.
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

		kpis.OpenOpportunities.Should().Be(0);
		kpis.PendingEngagements.Should().Be(0);
		kpis.CancelledEngagements.Should().Be(0);
		kpis.ConfirmedEngagementsTotal.Should().Be(0);
		kpis.ConfirmedEngagementsNext7Days.Should().Be(0);
	}

	[Test]
	public async Task GetOrganizationDashboard_ShouldReturnAccurateKpis_ForOrganizationWithMixedEngagementStatuses(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);

		// Two open opportunities.
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

		// Pending: vera signs up for opportunityA and is left untouched.
		await veraClient.CreateEngagementAsync(
			opportunityA.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		// Cancelled: olaf signs up for opportunityA himself, then cancels it as organizer.
		var toBeCancelled = await olafClient.CreateEngagementAsync(
			opportunityA.Id,
			new CreateEngagementRequest { Message = "Second helper" },
			cancellationToken);
		await olafClient.CancelEngagementAsync(
			toBeCancelled.Id,
			new CancelEngagementRequest { Reason = "No longer needed" },
			cancellationToken);

		// Confirmed, within next 7 days: vera on the near time slot.
		var nearEngagement = await veraClient.CreateEngagementAsync(
			opportunityB.Id,
			new CreateEngagementRequest { TimeSlotId = slotNear },
			cancellationToken);
		await olafClient.ConfirmEngagementAsync(nearEngagement.Id, cancellationToken);

		// Confirmed, beyond next 7 days: olaf himself on the far time slot.
		var farEngagement = await olafClient.CreateEngagementAsync(
			opportunityB.Id,
			new CreateEngagementRequest { TimeSlotId = slotFar },
			cancellationToken);
		await olafClient.ConfirmEngagementAsync(farEngagement.Id, cancellationToken);

		var kpis = await olafClient.GetOrganizationDashboardAsync(orgId, cancellationToken);

		kpis.OpenOpportunities.Should().Be(2);
		kpis.PendingEngagements.Should().Be(1);
		kpis.CancelledEngagements.Should().Be(1);
		kpis.ConfirmedEngagementsTotal.Should().Be(2);
		kpis.ConfirmedEngagementsNext7Days.Should().Be(1);
	}

	[Test]
	public async Task GetOrganizationDashboard_ShouldNotCountAnotherOrganizationsEngagements(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		// org1 (olaf): one opportunity with a pending and a confirmed engagement.
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity1 = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken);
		var engagement1 = await veraClient.CreateEngagementAsync(
			opportunity1.Id,
			new CreateEngagementRequest { Message = "Helping org1" },
			cancellationToken);
		await olafClient.ConfirmEngagementAsync(engagement1.Id, cancellationToken);

		// org2 (vera): its own opportunity with its own pending engagement, which
		// must never leak into org1's counts. CreateVolunteerOpportunity requires
		// the "organisator" role claim, which Keycloak only grants vera once she
		// creates an org - her existing veraClient token predates that, so a fresh
		// token has to be minted to pick it up.
		var org2Id = await CreateOrganizationAsync(veraClient, cancellationToken);
		var veraOrganizerClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var opportunity2 = await CreateOpportunityAsync(veraOrganizerClient, org2Id, cancellationToken);
		await olafClient.CreateEngagementAsync(
			opportunity2.Id,
			new CreateEngagementRequest { Message = "Helping org2" },
			cancellationToken);

		var org1Kpis = await olafClient.GetOrganizationDashboardAsync(org1Id, cancellationToken);

		org1Kpis.OpenOpportunities.Should().Be(1);
		org1Kpis.PendingEngagements.Should().Be(0);
		org1Kpis.ConfirmedEngagementsTotal.Should().Be(1);
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

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

	private static async Task<CreateVolunteerOpportunityResponse> CreateScheduledSlotsOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken)
	{
		// Created as a draft: a ScheduledSlots opportunity can't be published until it has
		// at least one time slot, and callers add slots separately after this returns.
		return await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = "ScheduledSlots Opportunity",
				Description = "Integration test ScheduledSlots opportunity",
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
