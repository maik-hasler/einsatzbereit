using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

/// <summary>
/// <c>GET /v1/organizations/{id}/profile</c>, specifically the fields
/// <c>PublicOpportunitySummaryDto</c> carries. The shared `OpportunityCard`
/// renders this DTO on the organization profile the same way it renders
/// `VolunteerOpportunitySummary` on /opportunities (#2054) - so any field the
/// DTO does not carry silently collapses the block that needs it, with no
/// error anywhere.
///
/// Moved down from `OpportunityCardContractTests` in #2148. That class's own
/// doc comment names the defect: the DTO never carried ValidUntil or
/// NextTimeSlotStart even though the repository resolved both, and later gained
/// Category/TotalMaxParticipants/CurrentParticipantCount for the same reason.
/// The card's rendering contract (the `data-date-kind` attribute, the capacity
/// line) is covered by the RTL cases in
/// `frontend/src/components/OpportunityCard.test.tsx`; a browser test cannot
/// distinguish "the card chose not to render this" from "the field never
/// arrived", and an RTL test hand-authors the field into a mock and so can
/// never catch it being dropped from the wire at all.
/// </summary>
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetPublicOrganizationProfileTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetPublicOrganizationProfile_ShouldCarryNextTimeSlotStart_ForASlotBasedOpportunity(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var organizationId = await CreateOrganizationAsync(client, cancellationToken);
		var slotStart = DateTimeOffset.UtcNow.AddDays(7);
		await PublishSlotBasedOpportunityAsync(
			client, organizationId, "Slot based", slotStart, cancellationToken);

		var profile = await new EinsatzbereitApi(fixture.CreateHttpClient())
			.GetPublicOrganizationProfileAsync(organizationId, cancellationToken);

		var summary = profile.OpenOpportunities.Should().ContainSingle().Which;

		// The card reads this to print "Starts <date>" rather than repeating the
		// occurrence, so a null here is the whole regression.
		summary.NextTimeSlotStart.Should().NotBeNull();
		summary.NextTimeSlotStart.Should().BeCloseTo(slotStart, TimeSpan.FromMinutes(1));
		// And ValidUntil stays null for a slot-based opportunity, which is what
		// makes the card pick the start date over the deadline.
		summary.ValidUntil.Should().BeNull();
	}

	[Test]
	public async Task GetPublicOrganizationProfile_ShouldCarryValidUntil_ForAnInterestBasedOpportunity(
		CancellationToken cancellationToken)
	{
		// The other branch of the card's date line, and the other half of the
		// dropped-field pair.
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var organizationId = await CreateOrganizationAsync(client, cancellationToken);
		var deadline = DateTimeOffset.UtcNow.AddDays(30);
		await PublishInterestBasedOpportunityAsync(
			client, organizationId, "Interest based", deadline, cancellationToken);

		var profile = await new EinsatzbereitApi(fixture.CreateHttpClient())
			.GetPublicOrganizationProfileAsync(organizationId, cancellationToken);

		var summary = profile.OpenOpportunities.Should().ContainSingle().Which;

		summary.ValidUntil.Should().NotBeNull();
		summary.ValidUntil.Should().BeCloseTo(deadline, TimeSpan.FromMinutes(1));
		summary.NextTimeSlotStart.Should().BeNull();
	}

	[Test]
	public async Task GetPublicOrganizationProfile_ShouldCarryCategoryAndCapacity(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var organizationId = await CreateOrganizationAsync(client, cancellationToken);
		await PublishSlotBasedOpportunityAsync(
			client, organizationId, "Categorised", DateTimeOffset.UtcNow.AddDays(7),
			cancellationToken, category: "Environment", maxParticipants: 10);

		var profile = await new EinsatzbereitApi(fixture.CreateHttpClient())
			.GetPublicOrganizationProfileAsync(organizationId, cancellationToken);

		var summary = profile.OpenOpportunities.Should().ContainSingle().Which;

		summary.Category.Should().Be("Environment");
		// `TotalMaxParticipants` is tri-state (see lib/opportunityCapacity.ts):
		// null means unlimited, 0 means no time slots, > 0 means capped. The one
		// slot seeded above caps it at 10, and nobody has signed up yet.
		summary.TotalMaxParticipants.Should().Be(10);
		summary.CurrentParticipantCount.Should().Be(0);
	}

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, CancellationToken cancellationToken)
	{
		var organization = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Card contract {Guid.NewGuid():N}" },
			cancellationToken);
		return organization.Id.Value;
	}

	private static async Task PublishSlotBasedOpportunityAsync(
		EinsatzbereitApi client,
		Guid organizationId,
		string title,
		DateTimeOffset slotStart,
		CancellationToken cancellationToken,
		string? category = null,
		int maxParticipants = 10)
	{
		// ScheduledSlots cannot publish without a slot, so: draft, slot, publish.
		var opportunity = await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = title,
				DescriptionDe = "Coverage for the public profile's opportunity summary.",
				OrganizationId = organizationId,
				IsRemote = true,
				Occurrence = "OneTime",
				ParticipationType = "ScheduledSlots",
				CheckInMethod = "None",
				Category = category,
				IsDraft = true,
			}, cancellationToken);

		await client.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = slotStart,
				EndDateTime = slotStart.AddHours(2),
				MaxParticipants = maxParticipants,
				RecurrenceCount = 1,
			},
			cancellationToken);

		await client.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);
	}

	private static async Task PublishInterestBasedOpportunityAsync(
		EinsatzbereitApi client,
		Guid organizationId,
		string title,
		DateTimeOffset validUntil,
		CancellationToken cancellationToken)
	{
		var opportunity = await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = title,
				DescriptionDe = "Coverage for the public profile's opportunity summary.",
				OrganizationId = organizationId,
				IsRemote = true,
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
				ValidUntil = validUntil,
				IsDraft = true,
			}, cancellationToken);

		await client.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}
}
