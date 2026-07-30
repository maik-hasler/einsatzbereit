using System.Net.Http.Headers;
using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.VolunteerOpportunities;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetVolunteerOpportunitiesTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnEmptyPagedList_WhenNoneExist(
		CancellationToken cancellationToken)
	{
		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		result.TotalItems.Should().Be(0);
		result.Items.Should().BeEmpty();
		result.CurrentPage.Should().Be(1);
		result.PageCount.Should().Be(0);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnAll_WhenOpportunitiesExist(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", cancellationToken);
		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		result.TotalItems.Should().Be(2);
		result.Items.Should().HaveCount(2);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnCorrectPageSize_WhenPaginationIsApplied(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", cancellationToken);
		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", cancellationToken);
		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 3", "Description 3", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 2, cancellationToken: cancellationToken);

		result.TotalItems.Should().Be(3);
		result.Items.Should().HaveCount(2);
		result.PageCount.Should().Be(2);
		result.CurrentPage.Should().Be(1);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnRemainingItems_WhenRequestingLastPage(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", cancellationToken);
		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", cancellationToken);
		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 3", "Description 3", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(2, 2, cancellationToken: cancellationToken);

		result.TotalItems.Should().Be(3);
		result.Items.Should().HaveCount(1);
		result.CurrentPage.Should().Be(2);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnOrderedByCreatedOnDescending(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var first = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "First", "Created first", cancellationToken);
		var second = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Second", "Created second", cancellationToken);
		var third = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Third", "Created last", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		var items = result.Items.ToList();
		items.Should().HaveCount(3);
		items[0].Id.Should().Be(third.Id);
		items[1].Id.Should().Be(second.Id);
		items[2].Id.Should().Be(first.Id);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnOrganizationName(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity", "Description", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		var item = result.Items.Single();
		item.OrganizationName.Should().Contain("Testorg_");
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnAddressAndOccurrence(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity", "Description", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		var item = result.Items.Single();
		item.Street.Should().Be("Sample Street");
		item.HouseNumber.Should().Be("1");
		item.ZipCode.Should().Be("12345");
		item.City.Should().Be("Berlin");
		item.Occurrence.Should().Be("OneTime");
		item.ParticipationType.Should().Be("ScheduledSlots");
		item.IsRemote.Should().BeFalse();
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnNextTimeSlotStartAndEnd_WhenTimeSlotExists(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var opportunity = await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Opportunity with a slot",
			Description = "Description",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "ScheduledSlots",
			CheckInMethod = "None",
			IsDraft = true,
		}, cancellationToken);

		var start = DateTimeOffset.UtcNow.AddDays(7);
		var end = start.AddHours(2);
		await authenticatedClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = start,
				EndDateTime = end,
				MaxParticipants = 10,
				RecurrenceCount = 1,
			},
			cancellationToken);

		await authenticatedClient.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());
		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		var item = result.Items.Single();
		item.NextTimeSlotStart.Should().BeCloseTo(start, TimeSpan.FromSeconds(1));
		item.NextTimeSlotEnd.Should().BeCloseTo(end, TimeSpan.FromSeconds(1));
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnEarliestUpcomingTimeSlot_WhenMultipleSlotsExist(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var opportunity = await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Opportunity with recurring slots",
			Description = "Description",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "Recurring",
			ParticipationType = "ScheduledSlots",
			CheckInMethod = "None",
			IsDraft = true,
		}, cancellationToken);

		var earliestStart = DateTimeOffset.UtcNow.AddDays(3);
		await authenticatedClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = earliestStart,
				EndDateTime = earliestStart.AddHours(2),
				MaxParticipants = 10,
				RecurrenceFrequency = "WEEKLY",
				RecurrenceCount = 3,
			},
			cancellationToken);

		await authenticatedClient.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());
		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		var item = result.Items.Single();
		item.NextTimeSlotStart.Should().BeCloseTo(earliestStart, TimeSpan.FromSeconds(1));
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnNullNextTimeSlot_WhenNoTimeSlotsExist(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Opportunity without slots",
			Description = "Description",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "IndividualContact",
			CheckInMethod = "None",
		}, cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());
		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		var item = result.Items.Single();
		item.NextTimeSlotStart.Should().BeNull();
		item.NextTimeSlotEnd.Should().BeNull();
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldExcludeOpportunitiesWhoseOnlyTimeSlotsHaveExpired(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var expired = await CreateOpportunityWithExpiredTimeSlotAsync(authenticatedClient, orgId, cancellationToken);

		var future = await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Future opportunity",
			Description = "Has an upcoming slot",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "ScheduledSlots",
			CheckInMethod = "None",
			IsDraft = true,
		}, cancellationToken);
		await authenticatedClient.CreateTimeSlotAsync(
			future.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 10,
				RecurrenceCount = 1,
			},
			cancellationToken);
		await authenticatedClient.PublishVolunteerOpportunityAsync(future.Id, cancellationToken);

		var slotless = await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Slotless opportunity",
			Description = "Never has time slots",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "IndividualContact",
			CheckInMethod = "None",
		}, cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());
		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		var ids = result.Items.Select(i => i.Id).ToList();
		result.TotalItems.Should().Be(2);
		ids.Should().NotContain(expired.Id, "an opportunity whose only time slot has already ended must not surface publicly");
		ids.Should().Contain(future.Id);
		ids.Should().Contain(slotless.Id);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldStillAppear_WhenOnlySomeOfItsTimeSlotsHaveExpired(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var opportunity = await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Mixed expiry opportunity",
			Description = "One slot already ended, one is still upcoming",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "Recurring",
			ParticipationType = "ScheduledSlots",
			CheckInMethod = "None",
			IsDraft = true,
		}, cancellationToken);

		var futureStart = DateTimeOffset.UtcNow.AddDays(3);
		await authenticatedClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = futureStart,
				EndDateTime = futureStart.AddHours(2),
				MaxParticipants = 10,
				RecurrenceCount = 1,
			},
			cancellationToken);

		await AddExpiredTimeSlotDirectlyAsync(opportunity.Id, cancellationToken);

		await authenticatedClient.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());
		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		var item = result.Items.Single(i => i.Id == opportunity.Id);
		item.NextTimeSlotStart.Should().BeCloseTo(futureStart, TimeSpan.FromSeconds(1),
			"the still-upcoming slot must win over the already-expired one");
	}

	[Test]
	public async Task CreateVolunteerOpportunity_ShouldReturn403_WhenUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		var token = await fixture.GetAccessTokenAsync("vera", "vera123");
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		var client = new EinsatzbereitApi(httpClient);

		var act = () => client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Not allowed",
			Description = "Vera cannot create opportunities",
			OrganizationId = Guid.NewGuid(),
			Street = "Test Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "ScheduledSlots",
			CheckInMethod = "None"
		}, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task CreateVolunteerOpportunity_ShouldPersistAddressAndOccurrence(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var result = await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Opportunity with address",
			Description = "Test",
			OrganizationId = orgId,
			Street = "Main Street",
			HouseNumber = "42a",
			ZipCode = "54321",
			City = "Munich",
			Occurrence = "Recurring",
			ParticipationType = "IndividualContact",
			CheckInMethod = "None"
		}, cancellationToken);

		result.Street.Should().Be("Main Street");
		result.HouseNumber.Should().Be("42a");
		result.ZipCode.Should().Be("54321");
		result.City.Should().Be("Munich");
		result.Occurrence.Should().Be("Recurring");
		result.ParticipationType.Should().Be("IndividualContact");
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldFilterByIsRemote(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "On-site task", "Description", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var onSite = await sut.GetVolunteerOpportunitiesAsync(1, 10, isRemote: false, cancellationToken: cancellationToken);
		var remote = await sut.GetVolunteerOpportunitiesAsync(1, 10, isRemote: true, cancellationToken: cancellationToken);

		onSite.TotalItems.Should().Be(1);
		remote.TotalItems.Should().Be(0);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnBadRequest_WhenRadiusIsNotPositive(
		CancellationToken cancellationToken)
	{
		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => sut.GetVolunteerOpportunitiesAsync(
			1, 10, centerLatitude: 52.5, centerLongitude: 13.4, radiusKm: 0, cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnBadRequest_WhenOccurrenceIsInvalid(
		CancellationToken cancellationToken)
	{
		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => sut.GetVolunteerOpportunitiesAsync(
			1, 10, occurrence: "Bogus", cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnBadRequest_WhenParticipationTypeIsInvalid(
		CancellationToken cancellationToken)
	{
		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => sut.GetVolunteerOpportunitiesAsync(
			1, 10, participationType: "Nonsense", cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnBadRequest_WhenCategoryIsInvalid(
		CancellationToken cancellationToken)
	{
		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => sut.GetVolunteerOpportunitiesAsync(
			1, 10, categories: ["NotACategory"], cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldFilterByOccurrenceAndParticipationTypeAndCategory_WhenValid(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var opportunity = await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Matching opportunity",
			Description = "Description",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "ScheduledSlots",
			CheckInMethod = "None",
			Category = "Environment",
			IsDraft = true,
		}, cancellationToken);

		await authenticatedClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 10,
				RecurrenceCount = 1,
			},
			cancellationToken);

		await authenticatedClient.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var matching = await sut.GetVolunteerOpportunitiesAsync(
			1, 10, occurrence: "onetime", participationType: "ScheduledSlots", categories: ["environment"], cancellationToken: cancellationToken);
		var nonMatching = await sut.GetVolunteerOpportunitiesAsync(
			1, 10, occurrence: "Recurring", cancellationToken: cancellationToken);

		matching.TotalItems.Should().Be(1);
		nonMatching.TotalItems.Should().Be(0);
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
	{
		var token = await fixture.GetAccessTokenAsync("olaf", "olaf123");
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, CancellationToken cancellationToken)
	{
		var uniqueName = $"Testorg_{Guid.NewGuid()}";
		var organization = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = uniqueName }, cancellationToken);
		return organization.Id.Value;
	}

	private static async Task<CreateVolunteerOpportunityResponse> CreateVolunteerOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, string title, string description,
		CancellationToken cancellationToken)
	{
		// A ScheduledSlots opportunity can't be published until it has at least one time
		// slot, so create it as a draft, add a slot, then publish it.
		var opportunity = await client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = title,
			Description = description,
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "ScheduledSlots",
			CheckInMethod = "None",
			IsDraft = true,
		}, cancellationToken);

		await client.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 10,
				RecurrenceCount = 1,
			},
			cancellationToken);

		await client.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		return opportunity;
	}

	// CreateTimeSlotAsync's domain validation rejects a past StartDateTime (see
	// TimeSlot.Validate's "TimeSlot.StartMustBeFuture" rule), so there is no API path to
	// create an already-expired slot. Seeding one directly through the aggregate - with an
	// artificially past "now" older than the slot itself - reproduces what happens for real
	// once enough wall-clock time passes after a legitimately-created future slot; the read
	// repository's expiry filter (VolunteerOpportunityReadRepository.GetPagedSummariesAsync)
	// only cares about the stored dates, not how they got there.
	private async Task<CreateVolunteerOpportunityResponse> CreateOpportunityWithExpiredTimeSlotAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken)
	{
		var opportunity = await client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Expired opportunity",
			Description = "Only has a time slot that already ended",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "ScheduledSlots",
			CheckInMethod = "None",
			IsDraft = true,
		}, cancellationToken);

		await AddExpiredTimeSlotDirectlyAsync(opportunity.Id, cancellationToken);
		await client.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		return opportunity;
	}

	private async Task AddExpiredTimeSlotDirectlyAsync(Guid opportunityId, CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var opportunityId_ = VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow();
		var aggregate = await dbContext.VolunteerOpportunities.FindAsync(opportunityId_, cancellationToken)
			?? throw new InvalidOperationException($"Seeded opportunity '{opportunityId}' not found.");

		var start = DateTimeOffset.UtcNow.AddDays(-7);
		aggregate.AddTimeSlot(start, start.AddHours(2), maxParticipants: 10, now: start.AddDays(-1)).GetValueOrThrow();

		await dbContext.SaveChangesAsync(cancellationToken);
	}
}
