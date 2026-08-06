using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.CreateTimeSlot.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.CreateTimeSlot;

public class CreateTimeSlotCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly CreateTimeSlotCommandHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();
	private static readonly Address DefaultAddress = Address.Create("Hauptstrasse", "1", "12345", "Berlin").Value;
	private static readonly DateTimeOffset BaseStart = DateTimeOffset.UtcNow.AddDays(7);
	private static readonly DateTimeOffset BaseEnd = DateTimeOffset.UtcNow.AddDays(7).AddHours(2);

	public CreateTimeSlotCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new CreateTimeSlotCommandHandler(_dbContext);
	}

	private VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", "Beschreibung", false, DefaultAddress,
			Occurrence.Recurring, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldCreateSingleSlot_WhenNoRecurrence(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 10, DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().HaveCount(1);
		result[0].StartDateTime.Should().Be(BaseStart);
		result[0].EndDateTime.Should().Be(BaseEnd);
	}

	[Test]
	public async Task Handle_ShouldLeaveSeriesIdNull_WhenNoRecurrence(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 10, DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().ContainSingle().Which.SeriesId.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldStampSameSeriesId_AcrossAllRecurringSlots(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 5, DefaultRequestingUserId,
			RecurrenceFrequency: "Weekly", RecurrenceCount: 8);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().OnlyContain(ts => ts.SeriesId != null);
		result.Select(ts => ts.SeriesId).Distinct().Should().ContainSingle();
		result.Should().OnlyContain(ts => ts.RecurrenceFrequency == "Weekly" && ts.RecurrenceCount == 8);
	}

	[Test]
	public async Task Handle_ShouldCreate8WeeklySlots_WithWeeklyFrequency(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 5, DefaultRequestingUserId,
			RecurrenceFrequency: "Weekly", RecurrenceCount: 8);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().HaveCount(8);
		for (var i = 0; i < 8; i++)
			result[i].StartDateTime.Should().Be(BaseStart.AddDays(7 * i));
	}

	[Test]
	public async Task Handle_ShouldCreate3MonthlySlots_WithMonthlyFrequency(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 20, DefaultRequestingUserId,
			RecurrenceFrequency: "Monthly", RecurrenceCount: 3);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().HaveCount(3);
		result[0].StartDateTime.Should().Be(BaseStart);
		result[1].StartDateTime.Should().Be(BaseStart.AddMonths(1));
		result[2].StartDateTime.Should().Be(BaseStart.AddMonths(2));
	}

	[Test]
	public async Task Handle_ShouldClampCountTo52_WhenCountExceeds52(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 5, DefaultRequestingUserId,
			RecurrenceFrequency: "Weekly", RecurrenceCount: 100);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().HaveCount(52);
	}

	[Test]
	public async Task Handle_ShouldPreserveDuration_ForAllRecurringSlots(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		var expectedDuration = BaseEnd - BaseStart;
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 10, DefaultRequestingUserId,
			RecurrenceFrequency: "Weekly", RecurrenceCount: 4);

		var result = await _sut.Handle(command, cancellationToken);

		foreach (var slot in result)
			(slot.EndDateTime - slot.StartDateTime).Should().Be(expectedDuration);
	}

	[Test]
	public async Task Handle_ShouldCreateSingleSlot_WhenFrequencyIsNullEvenIfCountIsGreaterThan1(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 10, DefaultRequestingUserId,
			RecurrenceFrequency: null, RecurrenceCount: 5);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().HaveCount(1);
		result[0].StartDateTime.Should().Be(BaseStart);
		result[0].EndDateTime.Should().Be(BaseEnd);
	}

	[Test]
	public async Task Handle_ShouldCreateSlot_WithUnlimitedCapacity_WhenMaxParticipantsIsNull(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, MaxParticipants: null, DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().HaveCount(1);
		result[0].MaxParticipants.Should().BeNull();
	}

	// --- DST-aware recurrence (#1160) ---

	[Test]
	public async Task Handle_ShouldKeepLocalWallClockTime_AcrossADstTransition(
		CancellationToken cancellationToken)
	{
		// Regression for #1160: a weekly Saturday 10:00 Europe/Berlin shift created
		// in September must still read 10:00 local after the Oct 25, 2026 CEST->CET
		// transition - naive UTC AddDays() carried the *original* +02:00 offset
		// forward unchanged, silently shifting every post-transition occurrence an
		// hour early. This mirrors the issue's own repro dates exactly.
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var start = new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.FromHours(2));
		var end = start.AddHours(2);
		var command = new CreateTimeSlotCommand(
			opportunityId, start, end, 10, DefaultRequestingUserId,
			RecurrenceFrequency: "Weekly", RecurrenceCount: 9, Timezone: "Europe/Berlin");

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().HaveCount(9);
		// Pre-transition occurrence keeps +02:00 (CEST).
		result[3].StartDateTime.Should().Be(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.FromHours(2)));
		// Post-transition occurrence (the 9th slot, per the issue's own repro) moves
		// to +01:00 (CET) - still 10:00 local, not the naive +02:00 (which would be 11:00 CET).
		result[8].StartDateTime.Should().Be(new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.FromHours(1)));
	}

	[Test]
	public async Task Handle_ShouldFallBackToEuropeBerlin_WhenTimezoneHeaderIsMissingOrInvalid(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var start = new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.FromHours(2));
		var end = start.AddHours(2);
		var command = new CreateTimeSlotCommand(
			opportunityId, start, end, 10, DefaultRequestingUserId,
			RecurrenceFrequency: "Weekly", RecurrenceCount: 9, Timezone: "Not/A_Real_Zone");

		var result = await _sut.Handle(command, cancellationToken);

		result[8].StartDateTime.Should().Be(new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.FromHours(1)));
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		Func<Task> act = async () => await _sut.Handle(
			new CreateTimeSlotCommand(opportunityId, BaseStart, BaseEnd, 10, DefaultRequestingUserId),
			cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage($"*{opportunityId}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: caller belongs to a different organization than the opportunity's.
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new CreateTimeSlotCommand(opportunityId, BaseStart, BaseEnd, 10, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		opportunity.TimeSlots.Should().BeEmpty();
	}
}
