using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.UpdateTimeSlot.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.UpdateTimeSlot;

public class UpdateTimeSlotCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IEngagementReadRepository _engagementReadRepository = Substitute.For<IEngagementReadRepository>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly UpdateTimeSlotCommandHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();
	private static readonly Address DefaultAddress = Address.Create("Hauptstrasse", "1", "12345", "Berlin").Value;
	private static readonly DateTimeOffset BaseStart = DateTimeOffset.UtcNow.AddDays(7);
	private static readonly DateTimeOffset BaseEnd = DateTimeOffset.UtcNow.AddDays(7).AddHours(2);

	public UpdateTimeSlotCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_dbContext
			.CountActiveEngagementsForTimeSlotAsync(Arg.Any<TimeSlotId>(), Arg.Any<CancellationToken>())
			.Returns(0);
		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<TimeSlotId?>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_sut = new UpdateTimeSlotCommandHandler(_dbContext, _engagementReadRepository);
	}

	private VolunteerOpportunity CreateWaitlistOpportunity() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", "Beschreibung", false, DefaultAddress,
			Occurrence.Recurring, ParticipationType.Waitlist, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldUpdateTimeSlot_WhenValid(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateWaitlistOpportunity();
		var timeSlot = opportunity.AddTimeSlot(BaseStart, BaseEnd, 10, DateTimeOffset.UtcNow).Value;
		var opportunityId = opportunity.Id.Value;

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var newStart = BaseStart.AddDays(1);
		var newEnd = BaseEnd.AddDays(1);
		var command = new UpdateTimeSlotCommand(
			opportunityId, timeSlot.Id.Value, newStart, newEnd, 20, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		timeSlot.StartDateTime.Should().Be(newStart);
		timeSlot.EndDateTime.Should().Be(newEnd);
		timeSlot.MaxParticipants.Should().Be(20);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		var command = new UpdateTimeSlotCommand(
			opportunityId, Guid.CreateVersion7(), BaseStart, BaseEnd, 10, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage($"*{opportunityId}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenTimeSlotNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateWaitlistOpportunity();
		var opportunityId = opportunity.Id.Value;

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateTimeSlotCommand(
			opportunityId, Guid.CreateVersion7(), BaseStart, BaseEnd, 10, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*not found*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenCapacityReducedBelowActiveEngagements(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateWaitlistOpportunity();
		var timeSlot = opportunity.AddTimeSlot(BaseStart, BaseEnd, 10, DateTimeOffset.UtcNow).Value;
		var opportunityId = opportunity.Id.Value;

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_dbContext
			.CountActiveEngagementsForTimeSlotAsync(timeSlot.Id, cancellationToken)
			.Returns(5);

		var command = new UpdateTimeSlotCommand(
			opportunityId, timeSlot.Id.Value, BaseStart, BaseEnd, 3, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*5*");
		timeSlot.MaxParticipants.Should().Be(10);
	}

	[Test]
	public async Task Handle_ShouldNotifyOnlyVolunteersOnTheEditedSlot(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateWaitlistOpportunity();
		var editedSlot = opportunity.AddTimeSlot(BaseStart, BaseEnd, 10, DateTimeOffset.UtcNow).Value;
		var otherSlot = opportunity.AddTimeSlot(BaseStart.AddDays(2), BaseEnd.AddDays(2), 10, DateTimeOffset.UtcNow).Value;
		var opportunityId = opportunity.Id.Value;
		var editedSlotVolunteer = Guid.NewGuid();
		var otherSlotVolunteer = Guid.NewGuid();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), editedSlot.Id, cancellationToken)
			.Returns([editedSlotVolunteer]);

		var command = new UpdateTimeSlotCommand(
			opportunityId, editedSlot.Id.Value, BaseStart.AddHours(1), BaseEnd.AddHours(1), 10, DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.Kind == NotificationKind.OpportunityUpdated && n.RecipientId.Value == editedSlotVolunteer),
			cancellationToken);
		await _notifRepo.DidNotReceive().AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId.Value == otherSlotVolunteer),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: caller belongs to a different organization than the opportunity's.
		var opportunity = CreateWaitlistOpportunity();
		var timeSlot = opportunity.AddTimeSlot(BaseStart, BaseEnd, 10, DateTimeOffset.UtcNow).Value;
		var opportunityId = opportunity.Id.Value;

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new UpdateTimeSlotCommand(
			opportunityId, timeSlot.Id.Value, BaseStart.AddDays(1), BaseEnd.AddDays(1), 20, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		timeSlot.StartDateTime.Should().Be(BaseStart);
		timeSlot.EndDateTime.Should().Be(BaseEnd);
		timeSlot.MaxParticipants.Should().Be(10);
	}
}
