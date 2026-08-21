using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

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

		summary.NextTimeSlotStart.Should().NotBeNull();
		summary.NextTimeSlotStart.Should().BeCloseTo(slotStart, TimeSpan.FromMinutes(1));
		summary.ValidUntil.Should().BeNull();
	}

	[Test]
	public async Task GetPublicOrganizationProfile_ShouldCarryValidUntil_ForAnInterestBasedOpportunity(
		CancellationToken cancellationToken)
	{
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
