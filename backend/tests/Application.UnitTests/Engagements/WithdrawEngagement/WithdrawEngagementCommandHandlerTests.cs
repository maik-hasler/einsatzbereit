using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.WithdrawEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.WithdrawEngagement;

public class WithdrawEngagementCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IKeycloakOrganizationService _keycloakService =
		Substitute.For<IKeycloakOrganizationService>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly WithdrawEngagementCommandHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;

	public WithdrawEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_keycloakService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_sut = new WithdrawEngagementCommandHandler(_dbContext, _keycloakService);
	}

	private VolunteerOpportunity CreateOpportunityForOrganizerNotification(VolunteerOpportunityId opportunityId, out Guid organizerUserId)
	{
		var opportunity = VolunteerOpportunity.Create(
			OrganizationId.New(), "Test", "Test", false, DefaultAddress,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None,
			_pinGenerator, status: OpportunityStatus.Draft).Value;
		_opportunityRepo.FindAsync(opportunityId, Arg.Any<CancellationToken>()).Returns(opportunity);

		organizerUserId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(opportunity.OrganizationId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerUserId, "organizer", "Org", "Anizer", "organizer@example.com", true)]);
		return opportunity;
	}

	private static (Engagement engagement, UserId volunteerId) CreatePendingEngagementWithVolunteer()
	{
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());
		return (engagement, volunteerId);
	}

	[Test]
	public async Task Handle_ShouldWithdrawEngagement_WhenCalledByOwner(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Status.Should().Be(EngagementStatus.Withdrawn);
	}

	[Test]
	public async Task Handle_ShouldWithdrawConfirmedEngagement_WhenCalledByOwner(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		engagement.Confirm();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Status.Should().Be(EngagementStatus.Withdrawn);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var command = new WithdrawEngagementCommand(engagementId, Guid.NewGuid());

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{engagementId.Value}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenCallerIsNotOwner(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, _) = CreatePendingEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		var differentUserId = Guid.NewGuid();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, differentUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Only the volunteer*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsAlreadyWithdrawn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		engagement.Withdraw();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*already terminated*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		engagement.Cancel();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*already terminated*");
	}

	// Regression for #1217: the ownership check below runs before Withdraw()'s
	// own IsAnonymized guard (#1140), so it used to dereference the null
	// VolunteerId directly and crash with a 500 instead of returning a 409.
	[Test]
	public async Task Handle_ShouldThrowConflict_WhenEngagementIsAnonymized(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, _) = CreatePendingEngagementWithVolunteer();
		engagement.Anonymize();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, Guid.NewGuid());

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsCheckedIn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		engagement.Confirm();
		engagement.CheckIn();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*checked-in*");
	}

	// --- Organizer notifications moved off the request path (#1174) ---
	//
	// The organizer withdrawal email (subscription-gated per #1055) is no
	// longer sent by this handler - IEmailService isn't even a dependency of
	// it any more, so a rapid create/withdraw loop can no longer hold this
	// request's DB transaction open across one synchronous SMTP send per
	// organizer. It moves onto the outbox, delivered by
	// EngagementWithdrawnDomainEventHandler; see that handler's tests for the
	// subscription-preference/localization coverage that used to live here.
	// The in-app bell-icon Notification row (unconditional, not
	// subscription-gated) stays synchronous.

	[Test]
	public async Task Handle_ShouldCreateInAppNotification_ForEachOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		CreateOpportunityForOrganizerNotification(engagement.OpportunityId, out var organizerUserId);
		var organizerId = UserId.Create(organizerUserId).GetValueOrThrow();

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId == organizerId && n.Kind == NotificationKind.EngagementWithdrawn),
			Arg.Any<CancellationToken>());
	}
}
