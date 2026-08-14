using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.VolunteerOpportunities;
using TUnit.Core.Interfaces;
// ApiClient.cs (generated, same "IntegrationTests" namespace) also declares
// "Address"/"OrganizationId" DTO types, which would otherwise shadow the domain
// types of the same name pulled in via the "Domain.Common"/"Domain.Organizations"
// usings below (see the same workaround in OrganizationMembershipBackfillJobTests.cs).
using DomainAddress = Domain.Common.Address;
using DomainOrganization = Domain.Organizations.Organization;
using DomainOrganizationId = Domain.Organizations.OrganizationId;

namespace IntegrationTests;

// Exercises Infrastructure.Persistence.Repositories.EngagementReadRepository directly
// (InternalsVisibleTo, see Infrastructure.csproj) against the real integration Postgres.
// GetActiveVolunteerIdsByOpportunityAsync's DB-level status/time-slot filtering and its
// nullable-UserId Distinct() projection (einsatzbereit#1390) aren't exercised by the
// NSubstitute-mocked Application.UnitTests handler tests (which only assert on the
// interface's behavior, not that the EF query actually translates) - this is the only
// place that would catch an EF query-translation failure against real Postgres.
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

		// No time slots involved here, so plain individual-contact engagements
		// (TimeSlotId always null) avoid the TimeSlot foreign key entirely.
		var pending = Engagement.CreateIndividualContact(opportunityId, UserId.New(), "Pending").GetValueOrThrow();
		var confirmed = Engagement.CreateIndividualContact(opportunityId, UserId.New(), "Confirmed").GetValueOrThrow();
		confirmed.Confirm().ThrowIfFailure();
		var cancelled = Engagement.CreateIndividualContact(opportunityId, UserId.New(), "Cancelled").GetValueOrThrow();
		cancelled.Cancel().ThrowIfFailure();
		var withdrawn = Engagement.CreateIndividualContact(opportunityId, UserId.New(), "Withdrawn").GetValueOrThrow();
		withdrawn.Withdraw().ThrowIfFailure();
		// A pending engagement whose volunteer account was later deleted (#829) -
		// VolunteerId is null even though Status is still Pending, so there is no
		// one left to notify and it must not show up in the result.
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
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.Recurring,
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
		// Regression for #1155: this endpoint is anonymous (no organizer check), so an
		// unpublished Draft opportunity's details must not leak to whoever holds the
		// engagement id, matching what GetDetailsAsync already enforces.
		await using var dbContext = fixture.CreateApplicationDbContext();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
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
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
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
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
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

	// Regression for #1184: the anonymous .ics calendar feed (AllowAnonymous,
	// engagementId as capability token) must not leak title/description/address
	// for an opportunity the organizer has taken off Published, nor keep serving
	// a withdrawn/cancelled engagement's feed - both must 404 via a null return.
	//
	// ScheduledSlots opportunities can't be created directly as Published (Create()
	// requires at least one time slot to exist first), so every case here starts as
	// Draft, adds a slot, then Publish()es before being driven to the case's status.
	private async Task<(VolunteerOpportunity Opportunity, TimeSlot Slot)> CreatePublishedOpportunityWithSlotAsync(
		ApplicationDbContext dbContext,
		CancellationToken cancellationToken)
	{
		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"CalendarOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.Recurring,
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

	// Regression for #1051: EngagementSummary never carried CancellationReason,
	// so a reason set via Engagement.Cancel(reason) never reached the
	// volunteer's own "My profile -> Engagements" list (GetByVolunteerAsync)
	// nor the organizer's "Manage applications" list (GetPagedByOpportunityAsync),
	// even though the domain model, command, and email already supported it.
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

		// GetPagedByOpportunityAsync inner-joins through to OrganizationsQuery, so
		// a random unsaved OrganizationId would silently drop the row - a real
		// Organization must exist for the opportunity to be returned at all.
		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"CancellationReasonOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
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

	// Regression for #1016: EngagementSummary never carried CheckInMethod, so the
	// frontend couldn't tell a QRCode/PINCode opportunity (where a "Check in" button
	// makes sense) apart from a Manual or None one (where it doesn't) and always
	// rendered the button.
	[Test]
	public async Task GetByVolunteerAsync_ShouldIncludeCheckInMethod_FromOpportunity(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"CheckInMethodOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
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
		// Same no-inner-join scenario as the CancellationReason regression above
		// (#667): a hard-deleted opportunity leaves no row to join CheckInMethod
		// from, so the mapping must fall back to a safe default rather than throw.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();
		var opportunityId = VolunteerOpportunityId.New();

		var engagement = Engagement.CreateIndividualContact(opportunityId, volunteerId, "Please let me help.").GetValueOrThrow();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var repository = new EngagementReadRepository(dbContext);

		// A Pending engagement whose opportunity no longer exists moves to the Past
		// bucket (#703) - see GetByVolunteerAsync's opportunityExists handling.
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
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
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

	// --- Current & Upcoming never expiring (#1163) ---

	[Test]
	public async Task GetByVolunteerAsync_ShouldMoveEndedTimeSlotToPastBucket_EvenWhenNeverCheckedIn(
		CancellationToken cancellationToken)
	{
		// A Confirmed engagement for a CheckInMethod.None slot can never be checked
		// in (no check-in action applies), so IsCheckedIn alone used to leave it in
		// "upcoming" forever once its shift had already happened.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
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
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
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

	// --- Checked-in engagement ahead of its time slot's end (#1855) ---

	[Test]
	public async Task GetByVolunteerAsync_ShouldKeepCheckedInEngagement_InUpcomingBucket_WhenTimeSlotHasNotEndedYet(
		CancellationToken cancellationToken)
	{
		// Engagement.CheckIn() has no time-based guard - an organizer can check a
		// volunteer in as soon as it is Confirmed, e.g. at arrival for a
		// still-ongoing multi-hour shift. Before #1855 IsCheckedIn alone moved a
		// Confirmed engagement to Past regardless of the slot's own end time, so
		// a shift that had not even started yet could show up as a completed,
		// feedback-ready "Past" item.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
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
		// Complements the regression above: once the slot has genuinely ended,
		// a checked-in engagement still belongs in Past - #1855 only defers the
		// move until the slot's own end time actually arrives, it does not stop
		// checked-in engagements from ever reaching Past.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
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
		// A checked-in IndividualContact engagement has no TimeSlotId at all, so
		// there is no date to compare "now" against - #1855's new carve-out is
		// scoped to a resolvable TimeSlotEnd and must leave this case exactly as
		// before (Past), not reclassify it for lack of information either way.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"TestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
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

	// --- GetCheckedInByVolunteerAsync (engagement record, #1096) ---

	[Test]
	public async Task GetCheckedInByVolunteerAsync_ShouldReturnOnlyCheckedInEngagements_WithResolvedTimeSlot(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var volunteerId = UserId.New();

		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"RecordOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Strandreinigung", "Beschreibung", false, DefaultAddress, Occurrence.Recurring,
			ParticipationType.ScheduledSlots, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		var slot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-2).AddHours(3), 10, DateTimeOffset.UtcNow.AddDays(-3)).GetValueOrThrow();
		// A volunteer can only have one engagement per time slot (ix_engagement_volunteer_id_time_slot_id),
		// so the "not checked in" cases need their own slots on the same opportunity.
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
			organization.Id, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime,
			ParticipationType.ScheduledSlots, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).GetValueOrThrow();
		// Added out of chronological order - CreatedOn (sign-up order) must not
		// determine the upcoming bucket's order, only the slot's own start time.
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
