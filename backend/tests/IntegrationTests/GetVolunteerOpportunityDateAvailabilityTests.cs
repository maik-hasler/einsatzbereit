using System.Globalization;
using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetVolunteerOpportunityDateAvailabilityTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetDateAvailability_ShouldReturnTheDayASlotStartsOn_WithItsOpportunityCount(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync();
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var slotStart = UtcDayAt(daysFromToday: 5, hour: 10);
		await CreateOpportunityWithTimeSlotAsync(authenticatedClient, orgId, "Park cleanup", slotStart, cancellationToken);
		await CreateOpportunityWithTimeSlotAsync(authenticatedClient, orgId, "River cleanup", slotStart.AddHours(3), cancellationToken);

		var sut = CreateAnonymousClient("10.0.3.1");

		var result = await sut.GetVolunteerOpportunityDateAvailabilityAsync(
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(30),
			cancellationToken: cancellationToken);

		var day = result.Should().ContainSingle(d => d.Date == slotStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Subject;
		day.OpportunityCount.Should().Be(2, "both opportunities have a slot starting on that day");
	}

	[Test]
	public async Task GetDateAvailability_ShouldCountAnOpportunityOnce_WhenItHasSeveralSlotsOnTheSameDay(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync();
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var slotStart = UtcDayAt(daysFromToday: 6, hour: 9);
		var opportunity = await CreateOpportunityWithTimeSlotAsync(
			authenticatedClient, orgId, "Two shifts, one day", slotStart, cancellationToken);

		await authenticatedClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = slotStart.AddHours(5),
				EndDateTime = slotStart.AddHours(7),
				MaxParticipants = 10,
				RecurrenceCount = 1,
			},
			cancellationToken);

		var sut = CreateAnonymousClient("10.0.3.2");

		var result = await sut.GetVolunteerOpportunityDateAvailabilityAsync(
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(30),
			cancellationToken: cancellationToken);

		var day = result.Should().ContainSingle(d => d.Date == slotStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Subject;
		day.OpportunityCount.Should().Be(1,
			"the count is opportunities to pick from, not shifts on offer");
	}

	[Test]
	public async Task GetDateAvailability_ShouldAttributeASlotToTheCallersLocalDay_NotTheUtcOne(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync();
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var slotStart = UtcDayAt(daysFromToday: 7, hour: 23, minute: 30);
		await CreateOpportunityWithTimeSlotAsync(authenticatedClient, orgId, "Late night shift", slotStart, cancellationToken);

		var sut = CreateAnonymousClient("10.0.3.3");

		var result = await sut.GetVolunteerOpportunityDateAvailabilityAsync(
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(30),
			utcOffsetMinutes: 120,
			cancellationToken: cancellationToken);

		result.Should().ContainSingle()
			.Which.Date.Should().Be(slotStart.AddHours(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
	}

	[Test]
	public async Task GetDateAvailability_ShouldExcludeSlotsOutsideTheRequestedWindow(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync();
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var inside = UtcDayAt(daysFromToday: 3, hour: 10);
		var outside = UtcDayAt(daysFromToday: 40, hour: 10);
		await CreateOpportunityWithTimeSlotAsync(authenticatedClient, orgId, "Inside the window", inside, cancellationToken);
		await CreateOpportunityWithTimeSlotAsync(authenticatedClient, orgId, "Outside the window", outside, cancellationToken);

		var sut = CreateAnonymousClient("10.0.3.4");

		var result = await sut.GetVolunteerOpportunityDateAvailabilityAsync(
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(10),
			cancellationToken: cancellationToken);

		result.Select(d => d.Date).Should().Equal(inside.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
	}

	[Test]
	public async Task GetDateAvailability_ShouldNotMarkAnyDay_ForAnOpportunityWithoutTimeSlots(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync();
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			TitleDe = "Express interest opportunity",
			DescriptionDe = "No time slots - IndividualContact",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "IndividualContact",
			CheckInMethod = "None",
			ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
		}, cancellationToken);

		var sut = CreateAnonymousClient("10.0.3.5");

		var result = await sut.GetVolunteerOpportunityDateAvailabilityAsync(
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(30),
			cancellationToken: cancellationToken);

		result.Should().BeEmpty(
			"an opportunity with no dates of its own belongs to no single day - it stays in the results whichever range is picked, so marking every day for it would say nothing");
	}

	[Test]
	public async Task GetDateAvailability_ShouldNotMarkADay_ForAnUnpublishedOpportunity(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync();
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var slotStart = UtcDayAt(daysFromToday: 4, hour: 10);
		var draft = await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			TitleDe = "Still a draft",
			DescriptionDe = "Never published",
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
			draft.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = slotStart,
				EndDateTime = slotStart.AddHours(2),
				MaxParticipants = 10,
				RecurrenceCount = 1,
			},
			cancellationToken);

		var sut = CreateAnonymousClient("10.0.3.6");

		var result = await sut.GetVolunteerOpportunityDateAvailabilityAsync(
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(30),
			cancellationToken: cancellationToken);

		result.Should().BeEmpty(
			"a draft is not in the public listing, so it must not mark a day in the calendar in front of it");
	}

	[Test]
	public async Task GetDateAvailability_ShouldHonourTheSameFilters_AsTheListingItself(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync();
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var matching = UtcDayAt(daysFromToday: 3, hour: 10);
		var other = UtcDayAt(daysFromToday: 4, hour: 10);
		await CreateOpportunityWithTimeSlotAsync(
			authenticatedClient, orgId, "Beach cleanup", matching, cancellationToken, category: "Environment");
		await CreateOpportunityWithTimeSlotAsync(
			authenticatedClient, orgId, "Chess club", other, cancellationToken, category: "Culture");

		var sut = CreateAnonymousClient("10.0.3.7");

		var result = await sut.GetVolunteerOpportunityDateAvailabilityAsync(
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(30),
			categories: ["Environment"],
			cancellationToken: cancellationToken);

		result.Select(d => d.Date).Should().Equal(
			new[] { matching.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
			"a day is only worth marking if the filters already applied would let its opportunity through");
	}

	[Test]
	public async Task GetDateAvailability_ShouldReturnBadRequest_WhenToIsBeforeFrom(
		CancellationToken cancellationToken)
	{
		var sut = CreateAnonymousClient("10.0.3.8");

		var act = () => sut.GetVolunteerOpportunityDateAvailabilityAsync(
			DateTimeOffset.UtcNow.AddDays(10),
			DateTimeOffset.UtcNow.AddDays(1),
			cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetDateAvailability_ShouldReturnBadRequest_WhenTheWindowIsWiderThanTwoMonths(
		CancellationToken cancellationToken)
	{
		var sut = CreateAnonymousClient("10.0.3.9");

		var act = () => sut.GetVolunteerOpportunityDateAvailabilityAsync(
			DateTimeOffset.UtcNow,
			DateTimeOffset.UtcNow.AddDays(400),
			cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetDateAvailability_ShouldReturnBadRequest_WhenACategoryIsNotAKnownOne(
		CancellationToken cancellationToken)
	{
		var sut = CreateAnonymousClient("10.0.3.10");

		var act = () => sut.GetVolunteerOpportunityDateAvailabilityAsync(
			DateTimeOffset.UtcNow,
			DateTimeOffset.UtcNow.AddDays(30),
			categories: ["NotACategory"],
			cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400,
			"the listing rejects it, and silently ignoring it here would mark days the listing then refuses to fill");
	}

	private static DateTimeOffset UtcDayAt(int daysFromToday, int hour, int minute = 0) =>
		new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(daysFromToday).Date, TimeSpan.Zero)
			.AddHours(hour)
			.AddMinutes(minute);

	private EinsatzbereitApi CreateAnonymousClient(string clientIp)
	{
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Add("X-Forwarded-For", clientIp);
		return new EinsatzbereitApi(httpClient);
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync()
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
		var organization = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Testorg_{Guid.NewGuid()}" }, cancellationToken);
		return organization.Id.Value;
	}

	private static async Task<CreateVolunteerOpportunityResponse> CreateOpportunityWithTimeSlotAsync(
		EinsatzbereitApi client,
		Guid orgId,
		string title,
		DateTimeOffset slotStart,
		CancellationToken cancellationToken,
		string? category = null)
	{
		var opportunity = await client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			TitleDe = title,
			DescriptionDe = "Scheduled slots opportunity seeded for the date-availability calendar",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
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
				MaxParticipants = 10,
				RecurrenceCount = 1,
			},
			cancellationToken);

		await client.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		return opportunity;
	}
}
