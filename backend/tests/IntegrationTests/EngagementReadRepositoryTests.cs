using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.VolunteerOpportunities;
using TUnit.Core.Interfaces;

using DomainAddress = Domain.Common.Address;
using DomainOrganization = Domain.Organizations.Organization;
using DomainOrganizationId = Domain.Organizations.OrganizationId;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class EngagementReadRepositoryTests(IntegrationTestFixture fixture)
{
	private static readonly DomainAddress DefaultAddress = DomainAddress.Create("Teststrasse", "1", "12345", "Berlin").Value;

	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetActiveVolunteerIdsByOpportunityAsync_ShouldReturnOnlyPendingAndConfirmed_ExcludingTerminalAndAnonymized(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var opportunityId = VolunteerOpportunityId.New();

		var pending = Engagement.CreateIndividualContact(opportunityId, UserId.New(), "Pending").GetValueOrThrow();
		var confirmed = Engagement.CreateIndividualContact(opportunityId, UserId.New(), "Confirmed").GetValueOrThrow();
		confirmed.Confirm().ThrowIfFailure();
		var cancelled = Engagement.CreateIndividualContact(opportunityId, UserId.New(), "Cancelled").GetValueOrThrow();
		cancelled.Cancel().ThrowIfFailure();
		var withdrawn = Engagement.CreateIndividualContact(opportunityId, UserId.New(), "Withdrawn").GetValueOrThrow();
		withdrawn.Withdraw().ThrowIfFailure();

		var anonymizedPending = Engagement.CreateIndividualContact(opportunityId, UserId.New(), "Anonymized").GetValueOrThrow();
		anonymizedPending.Anonymize();

		foreach (var engagement in new[] { pending, confirmed, cancelled, withdrawn, anonymizedPending })
			await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var volunteerIds = await repository.GetActiveVolunteerIdsByOpportunityAsync(
			opportunityId, timeSlotId: null, cancellationToken);

		volunteerIds.Should().BeEquivalentTo(
		[
			pending.VolunteerId!.Value.Value,
			confirmed.VolunteerId!.Value.Value,
		]);
	}

	[Test]
	public async Task GetActiveVolunteerIdsByOpportunityAsync_ShouldScopeToTimeSlot_WhenGiven(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.Recurring,
			ParticipationType.ScheduledSlots, CheckInMethod.None, new RandomPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		var slotA = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2), 10, DateTimeOffset.UtcNow).GetValueOrThrow();
		var slotB = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(2).AddHours(2), 10, DateTimeOffset.UtcNow).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var onSlotA = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), slotA.Id);
		var onSlotB = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), slotB.Id);
		await dbContext.Engagements.AddAsync(onSlotA, cancellationToken);
		await dbContext.Engagements.AddAsync(onSlotB, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var volunteerIds = await repository.GetActiveVolunteerIdsByOpportunityAsync(
			opportunity.Id, slotA.Id, cancellationToken);

		volunteerIds.Should().BeEquivalentTo([onSlotA.VolunteerId!.Value.Value]);
	}

	[Test]
	public async Task GetCalendarInfoAsync_ShouldReturnNull_WhenOpportunityIsDraft(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.ScheduledSlots, CheckInMethod.None, new RandomPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		var slot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2), 10, DateTimeOffset.UtcNow).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), slot.Id);
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var calendarInfo = await repository.GetCalendarInfoAsync(engagement.Id, cancellationToken);

		calendarInfo.Should().BeNull();
	}

	[Test]
	public async Task GetCalendarInfoAsync_ShouldReturnInfo_WhenOpportunityIsPublished(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.ScheduledSlots, CheckInMethod.None, new RandomPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		var slot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2), 10, DateTimeOffset.UtcNow).GetValueOrThrow();
		opportunity.Publish().ThrowIfFailure();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), slot.Id);
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var calendarInfo = await repository.GetCalendarInfoAsync(engagement.Id, cancellationToken);

		calendarInfo.Should().NotBeNull();
		calendarInfo!.OpportunityTitle.Should().Be("Titel");
	}

	[Test]
	public async Task GetPagedByOpportunityAsync_ShouldEnrichVolunteerPhone_FromLocalUserRow(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"PhoneTestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		dbContext.Set<VolunteerOpportunity>().Add(opportunity);

		var volunteerWithPhone = UserId.New();
		var volunteerWithoutProfile = UserId.New();

		var user = User.Create(volunteerWithPhone);
		user.SetPhone("+49 30 1234567");
		await dbContext.Users.AddAsync(user, cancellationToken);

		await dbContext.SaveChangesAsync(cancellationToken);

		var engagementWithPhone = Engagement.CreateIndividualContact(opportunity.Id, volunteerWithPhone, "Call me").GetValueOrThrow();
		var engagementWithoutProfile = Engagement.CreateIndividualContact(opportunity.Id, volunteerWithoutProfile, "No profile row").GetValueOrThrow();
		await dbContext.Engagements.AddAsync(engagementWithPhone, cancellationToken);
		await dbContext.Engagements.AddAsync(engagementWithoutProfile, cancellationToken);

		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var page = await repository.GetPagedByOpportunityAsync(opportunity.Id, pageNumber: 1, pageSize: 10, cancellationToken: cancellationToken);

		page.Items.Should().ContainSingle(e => e.VolunteerId == volunteerWithPhone.Value && e.VolunteerPhone == "+49 30 1234567");
		page.Items.Should().ContainSingle(e => e.VolunteerId == volunteerWithoutProfile.Value && e.VolunteerPhone == null);
	}

	private sealed class NoOpPinGenerator : IPinGenerator
	{
		public string GeneratePin() => "0000";
	}

	private async Task<(VolunteerOpportunity Opportunity, TimeSlot Slot)> CreatePublishedOpportunityWithSlotAsync(
		ApplicationDbContext dbContext,
		CancellationToken cancellationToken)
	{
		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"CalendarOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.Recurring,
			ParticipationType.ScheduledSlots, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		var slot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2), 10, DateTimeOffset.UtcNow).GetValueOrThrow();
		opportunity.Publish().ThrowIfFailure();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		return (opportunity, slot);
	}

	[Test]
	public async Task GetCalendarInfoAsync_ShouldReturnInfo_WhenOpportunityPublishedAndEngagementConfirmed(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var (opportunity, slot) = await CreatePublishedOpportunityWithSlotAsync(dbContext, cancellationToken);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), slot.Id);
		engagement.Confirm().ThrowIfFailure();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var info = await repository.GetCalendarInfoAsync(engagement.Id, cancellationToken);

		info.Should().NotBeNull();
		info!.OpportunityTitle.Should().Be("Titel");
	}

	[Test]
	[Arguments(OpportunityStatus.Unpublished)]
	[Arguments(OpportunityStatus.Cancelled)]
	public async Task GetCalendarInfoAsync_ShouldReturnNull_WhenOpportunityNotPublished(
		OpportunityStatus status,
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var (opportunity, slot) = await CreatePublishedOpportunityWithSlotAsync(dbContext, cancellationToken);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), slot.Id);
		engagement.Confirm().ThrowIfFailure();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		if (status == OpportunityStatus.Unpublished)
			opportunity.Unpublish().ThrowIfFailure();
		else
			opportunity.Cancel().ThrowIfFailure();
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var info = await repository.GetCalendarInfoAsync(engagement.Id, cancellationToken);

		info.Should().BeNull();
	}

	[Test]
	[Arguments(EngagementStatus.Cancelled)]
	[Arguments(EngagementStatus.Withdrawn)]
	public async Task GetCalendarInfoAsync_ShouldReturnNull_WhenEngagementInTerminalStatus(
		EngagementStatus terminalStatus,
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var (opportunity, slot) = await CreatePublishedOpportunityWithSlotAsync(dbContext, cancellationToken);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), slot.Id);
		if (terminalStatus == EngagementStatus.Cancelled)
			engagement.Cancel().ThrowIfFailure();
		else
			engagement.Withdraw().ThrowIfFailure();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var info = await repository.GetCalendarInfoAsync(engagement.Id, cancellationToken);

		info.Should().BeNull();
	}

	[Test]
	public async Task GetByVolunteerAsync_ShouldIncludeCancellationReason_WhenEngagementCancelledWithReason(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();
		var opportunityId = VolunteerOpportunityId.New();

		var engagement = Engagement.CreateIndividualContact(opportunityId, volunteerId, "Please let me help.").GetValueOrThrow();
		engagement.Cancel("No longer needed").ThrowIfFailure();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var page = await repository.GetByVolunteerAsync(volunteerId, upcoming: false, pageNumber: 1, pageSize: 10, cancellationToken);

		page.Items.Should().ContainSingle()
			.Which.CancellationReason.Should().Be("No longer needed");
	}

	[Test]
	public async Task GetByVolunteerAsync_ShouldReturnNullReason_WhenEngagementCancelledWithoutReason(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();
		var opportunityId = VolunteerOpportunityId.New();

		var engagement = Engagement.CreateIndividualContact(opportunityId, volunteerId, "Please let me help.").GetValueOrThrow();
		engagement.Cancel().ThrowIfFailure();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var page = await repository.GetByVolunteerAsync(volunteerId, upcoming: false, pageNumber: 1, pageSize: 10, cancellationToken);

		page.Items.Should().ContainSingle()
			.Which.CancellationReason.Should().BeNull();
	}

	[Test]
	public async Task GetPagedByOpportunityAsync_ShouldIncludeCancellationReason_WhenEngagementCancelledWithReason(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"CancellationReasonOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.None, new RandomPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var engagement = Engagement.CreateIndividualContact(opportunity.Id, UserId.New(), "Please let me help.").GetValueOrThrow();
		engagement.Cancel("Position filled").ThrowIfFailure();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var page = await repository.GetPagedByOpportunityAsync(opportunity.Id, pageNumber: 1, pageSize: 10, cancellationToken: cancellationToken);

		page.Items.Should().ContainSingle()
			.Which.CancellationReason.Should().Be("Position filled");
	}

	[Test]
	public async Task GetByVolunteerAsync_ShouldIncludeCheckInMethod_FromOpportunity(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"CheckInMethodOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.QRCode, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft, validUntil: DateTimeOffset.UtcNow.AddDays(30)).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var volunteerId = UserId.New();
		var engagement = Engagement.CreateIndividualContact(opportunity.Id, volunteerId, "Please let me help.").GetValueOrThrow();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var page = await repository.GetByVolunteerAsync(volunteerId, upcoming: true, pageNumber: 1, pageSize: 10, cancellationToken);

		page.Items.Should().ContainSingle()
			.Which.CheckInMethod.Should().Be("QRCode");
	}

	[Test]
	public async Task GetByVolunteerAsync_ShouldDefaultCheckInMethodToNone_WhenOpportunityWasDeleted(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();
		var opportunityId = VolunteerOpportunityId.New();

		var engagement = Engagement.CreateIndividualContact(opportunityId, volunteerId, "Please let me help.").GetValueOrThrow();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var page = await repository.GetByVolunteerAsync(volunteerId, upcoming: false, pageNumber: 1, pageSize: 10, cancellationToken);

		page.Items.Should().ContainSingle()
			.Which.CheckInMethod.Should().Be("None");
	}

	[Test]
	public async Task GetPagedByOpportunityAsync_ShouldIncludeCheckInMethod_FromOpportunity(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"CheckInMethodOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.PINCode, new RandomPinGenerator(),
			status: OpportunityStatus.Draft, validUntil: DateTimeOffset.UtcNow.AddDays(30)).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var engagement = Engagement.CreateIndividualContact(opportunity.Id, UserId.New(), "Please let me help.").GetValueOrThrow();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var page = await repository.GetPagedByOpportunityAsync(opportunity.Id, pageNumber: 1, pageSize: 10, cancellationToken: cancellationToken);

		page.Items.Should().ContainSingle()
			.Which.CheckInMethod.Should().Be("PINCode");
	}

	[Test]
	public async Task GetByVolunteerAsync_ShouldMoveEndedTimeSlotToPastBucket_EvenWhenNeverCheckedIn(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.ScheduledSlots, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		var pastNow = DateTimeOffset.UtcNow.AddDays(-11);
		var endedSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-9), 10, pastNow).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, endedSlot.Id);
		engagement.Confirm().ThrowIfFailure();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var upcoming = await repository.GetByVolunteerAsync(volunteerId, upcoming: true, pageNumber: 1, pageSize: 10, cancellationToken);
		var past = await repository.GetByVolunteerAsync(volunteerId, upcoming: false, pageNumber: 1, pageSize: 10, cancellationToken);

		upcoming.Items.Should().BeEmpty();
		past.Items.Should().ContainSingle(e => e.Id == engagement.Id.Value);
	}

	[Test]
	public async Task GetByVolunteerAsync_ShouldKeepFutureConfirmedEngagement_InTheUpcomingBucket(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.ScheduledSlots, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		var futureSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(10), DateTimeOffset.UtcNow.AddDays(10).AddHours(2), 10, DateTimeOffset.UtcNow).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, futureSlot.Id);
		engagement.Confirm().ThrowIfFailure();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var upcoming = await repository.GetByVolunteerAsync(volunteerId, upcoming: true, pageNumber: 1, pageSize: 10, cancellationToken);

		upcoming.Items.Should().ContainSingle(e => e.Id == engagement.Id.Value);
	}

	[Test]
	public async Task GetByVolunteerAsync_ShouldSplitPendingAndWithdrawn_AcrossUpcomingAndPast(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(
			DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var stillPending = Engagement
			.CreateIndividualContact(opportunity.Id, volunteerId, "Still pending.")
			.GetValueOrThrow();
		var withdrawn = Engagement
			.CreateIndividualContact(opportunity.Id, volunteerId, "About to withdraw.")
			.GetValueOrThrow();
		withdrawn.Withdraw().ThrowIfFailure();

		await dbContext.Engagements.AddAsync(stillPending, cancellationToken);
		await dbContext.Engagements.AddAsync(withdrawn, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var upcoming = await repository.GetByVolunteerAsync(
			volunteerId, upcoming: true, pageNumber: 1, pageSize: 10, cancellationToken);
		var past = await repository.GetByVolunteerAsync(
			volunteerId, upcoming: false, pageNumber: 1, pageSize: 10, cancellationToken);

		upcoming.Items.Should().ContainSingle()
			.Which.Id.Should().Be(stillPending.Id.Value);
		past.Items.Should().ContainSingle()
			.Which.Id.Should().Be(withdrawn.Id.Value);
	}

	[Test]
	public async Task GetByVolunteerAsync_ShouldKeepCheckedInEngagement_InUpcomingBucket_WhenTimeSlotHasNotEndedYet(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.ScheduledSlots, CheckInMethod.Manual, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		var futureSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(14).AddHours(8), 10, DateTimeOffset.UtcNow).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, futureSlot.Id);
		engagement.Confirm().ThrowIfFailure();
		engagement.CheckIn().ThrowIfFailure();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var upcoming = await repository.GetByVolunteerAsync(volunteerId, upcoming: true, pageNumber: 1, pageSize: 10, cancellationToken);
		var past = await repository.GetByVolunteerAsync(volunteerId, upcoming: false, pageNumber: 1, pageSize: 10, cancellationToken);

		upcoming.Items.Should().ContainSingle(e => e.Id == engagement.Id.Value);
		past.Items.Should().BeEmpty();
	}

	[Test]
	public async Task GetByVolunteerAsync_ShouldMoveCheckedInEngagement_ToPastBucket_OnceTimeSlotHasEnded(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.ScheduledSlots, CheckInMethod.Manual, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		var pastNow = DateTimeOffset.UtcNow.AddDays(-11);
		var endedSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-9), 10, pastNow).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, endedSlot.Id);
		engagement.Confirm().ThrowIfFailure();
		engagement.CheckIn().ThrowIfFailure();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var upcoming = await repository.GetByVolunteerAsync(volunteerId, upcoming: true, pageNumber: 1, pageSize: 10, cancellationToken);
		var past = await repository.GetByVolunteerAsync(volunteerId, upcoming: false, pageNumber: 1, pageSize: 10, cancellationToken);

		upcoming.Items.Should().BeEmpty();
		past.Items.Should().ContainSingle(e => e.Id == engagement.Id.Value);
	}

	[Test]
	public async Task GetByVolunteerAsync_ShouldKeepCheckedInEngagementWithNoTimeSlot_InPastBucket(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.Manual, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft, validUntil: DateTimeOffset.UtcNow.AddDays(30)).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var engagement = Engagement.CreateIndividualContact(opportunity.Id, volunteerId, "Please let me help.").GetValueOrThrow();
		engagement.Confirm().ThrowIfFailure();
		engagement.CheckIn().ThrowIfFailure();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var upcoming = await repository.GetByVolunteerAsync(volunteerId, upcoming: true, pageNumber: 1, pageSize: 10, cancellationToken);
		var past = await repository.GetByVolunteerAsync(volunteerId, upcoming: false, pageNumber: 1, pageSize: 10, cancellationToken);

		upcoming.Items.Should().BeEmpty();
		past.Items.Should().ContainSingle(e => e.Id == engagement.Id.Value);
	}

	[Test]
	public async Task GetCheckedInByVolunteerAsync_ShouldReturnOnlyCheckedInEngagements_WithResolvedTimeSlot(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"RecordOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Strandreinigung", null, "Beschreibung", null, false, DefaultAddress, Occurrence.Recurring,
			ParticipationType.ScheduledSlots, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		var slot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-2).AddHours(3), 10, DateTimeOffset.UtcNow.AddDays(-3)).GetValueOrThrow();

		var otherSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-1).AddHours(3), 10, DateTimeOffset.UtcNow.AddDays(-3)).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var checkedIn = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, slot.Id);
		checkedIn.Confirm().ThrowIfFailure();
		checkedIn.CheckIn().ThrowIfFailure();
		var confirmedNotCheckedIn = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, otherSlot.Id);
		confirmedNotCheckedIn.Confirm().ThrowIfFailure();
		var pending = Engagement.CreateIndividualContact(opportunity.Id, volunteerId, "Please let me help.").GetValueOrThrow();
		await dbContext.Engagements.AddAsync(checkedIn, cancellationToken);
		await dbContext.Engagements.AddAsync(confirmedNotCheckedIn, cancellationToken);
		await dbContext.Engagements.AddAsync(pending, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var result = await repository.GetCheckedInByVolunteerAsync(volunteerId, cancellationToken);

		result.Should().ContainSingle();
		var entry = result[0];
		entry.Id.Should().Be(checkedIn.Id.Value);
		entry.OpportunityTitle.Should().Be("Strandreinigung");
		entry.OrganizationName.Should().Be(organization.Name);
		entry.TimeSlotStartDateTime.Should().BeCloseTo(slot.StartDateTime, TimeSpan.FromSeconds(1));
		entry.TimeSlotEndDateTime.Should().BeCloseTo(slot.EndDateTime, TimeSpan.FromSeconds(1));
	}

	[Test]
	public async Task GetCheckedInByVolunteerAsync_ShouldReturnEmpty_WhenVolunteerHasNoCheckedInEngagements(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var engagement = Engagement.CreateIndividualContact(VolunteerOpportunityId.New(), volunteerId, "Please let me help.").GetValueOrThrow();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var result = await repository.GetCheckedInByVolunteerAsync(volunteerId, cancellationToken);

		result.Should().BeEmpty();
	}

	[Test]
	public async Task GetByVolunteerAsync_ShouldOrderUpcomingBucket_BySlotStartTimeAscending(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.ScheduledSlots, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();

		var laterSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(20), DateTimeOffset.UtcNow.AddDays(20).AddHours(2), 10, DateTimeOffset.UtcNow).GetValueOrThrow();
		var soonerSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(5).AddHours(2), 10, DateTimeOffset.UtcNow).GetValueOrThrow();
		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var laterEngagement = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, laterSlot.Id);
		await dbContext.Engagements.AddAsync(laterEngagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var soonerEngagement = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, soonerSlot.Id);
		await dbContext.Engagements.AddAsync(soonerEngagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		var upcoming = await repository.GetByVolunteerAsync(volunteerId, upcoming: true, pageNumber: 1, pageSize: 10, cancellationToken);

		upcoming.Items.Select(e => e.Id).Should().ContainInOrder(soonerEngagement.Id.Value, laterEngagement.Id.Value);
	}
}
