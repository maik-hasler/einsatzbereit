using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
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

		var opportunity = VolunteerOpportunity.Create(
			DomainOrganizationId.New(), "Titel", "Beschreibung", false, DefaultAddress, Occurrence.Recurring,
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

	// Regression for #1051: EngagementSummary never carried CancellationReason,
	// so a reason set via Engagement.Cancel(reason) never reached the
	// volunteer's own "My Profile -> Engagements" list (GetByVolunteerAsync)
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

		var page = await repository.GetPagedByOpportunityAsync(opportunity.Id, pageNumber: 1, pageSize: 10, cancellationToken);

		page.Items.Should().ContainSingle()
			.Which.CancellationReason.Should().Be("Position filled");
	}
}
