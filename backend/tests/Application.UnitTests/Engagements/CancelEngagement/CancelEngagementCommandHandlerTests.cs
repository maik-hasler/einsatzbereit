using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements.CancelEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.CancelEngagement;

public class CancelEngagementCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly CancelEngagementCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = UserId.New();
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;

	public CancelEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(CreateDefaultOpportunity());
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new CancelEngagementCommandHandler(_dbContext);
	}

	private VolunteerOpportunity CreateDefaultOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Test", "Test", false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	private static Engagement CreatePendingScheduledSlotsEngagement() =>
		Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			UserId.New(),
			TimeSlotId.New());

	[Test]
	public async Task Handle_ShouldCancelEngagement_WhenEngagementIsPending(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		var engagement = CreatePendingScheduledSlotsEngagement();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		// Act
		var result = await _sut.Handle(new CancelEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		// Assert
		result.Status.Should().Be(EngagementStatus.Cancelled);
	}

	[Test]
	public async Task Handle_ShouldCancelEngagement_WhenEngagementIsConfirmed(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		var engagement = CreatePendingScheduledSlotsEngagement();
		engagement.Confirm();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		// Act
		var result = await _sut.Handle(new CancelEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		// Assert
		result.Status.Should().Be(EngagementStatus.Cancelled);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(new CancelEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{engagementId.Value}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsAlreadyCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		var engagement = CreatePendingScheduledSlotsEngagement();
		engagement.Cancel();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		// Act
		Func<Task> act = async () => await _sut.Handle(new CancelEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*already terminated*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsWithdrawn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		var engagement = CreatePendingScheduledSlotsEngagement();
		engagement.Withdraw();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		// Act
		Func<Task> act = async () => await _sut.Handle(new CancelEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*already terminated*");
	}

	[Test]
	public async Task Handle_ShouldReturnSameEngagement_Instance(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		var engagement = CreatePendingScheduledSlotsEngagement();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		// Act
		var result = await _sut.Handle(new CancelEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		// Assert
		result.Should().BeSameAs(engagement);
	}

	[Test]
	public async Task Handle_ShouldNotifyVolunteer_WhenEngagementCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		var engagement = CreatePendingScheduledSlotsEngagement();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		// Act
		await _sut.Handle(new CancelEngagementCommand(engagementId, DefaultRequestingUserId, "No longer needed."), cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId == engagement.VolunteerId!.Value
				&& n.Kind == NotificationKind.EngagementCancelled
				&& n.RelatedEntityId == engagement.Id.Value),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldRaiseCancelledEventCarryingTheOpportunityTitle_ForThePostCommitNotificationHandler(
		CancellationToken cancellationToken)
	{
		// #1150: the email itself moved to EngagementCancelledNotificationHandler,
		// dispatched post-commit - the title is denormalized onto the event here
		// since some other callers cancel as part of deleting the opportunity in
		// the same transaction (nothing left to look up by the time it dispatches).
		var engagementId = EngagementId.New();
		var engagement = CreatePendingScheduledSlotsEngagement();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		await _sut.Handle(new CancelEngagementCommand(engagementId, DefaultRequestingUserId, "No longer needed."), cancellationToken);

		engagement.Events.Should().ContainSingle(e => e is EngagementCancelledDomainEvent)
			.Which.Should().BeOfType<EngagementCancelledDomainEvent>()
			.Which.OpportunityTitle.Should().Be("Test");
	}
}
