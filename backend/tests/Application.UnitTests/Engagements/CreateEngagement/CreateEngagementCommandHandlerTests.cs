using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.CreateEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.CreateEngagement;

public class CreateEngagementCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IKeycloakOrganizationService _keycloakService =
		Substitute.For<IKeycloakOrganizationService>();
	private readonly IKeycloakUserService _keycloakUserService =
		Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IAggregateRepository<User, UserId> _userRepo =
		Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly CreateEngagementCommandHandler _sut;

	private static readonly Address TestAddress = Address.Create("Main St", "1", "12345", "Berlin").Value;

	private VolunteerOpportunity CreateTestOpportunity(
		VolunteerOpportunityId id, OpportunityStatus status = OpportunityStatus.Published)
	{
		var opportunity = VolunteerOpportunity.Create(
			OrganizationId.New(),
			"Test Opportunity",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			CheckInMethod.None,
			_pinGenerator,
			status: OpportunityStatus.Draft).Value;

		if (status == OpportunityStatus.Draft)
			return opportunity;

		// Published is unreachable at construction time for ScheduledSlots (see
		// Create's ScheduledSlotsMustStartAsDraft guard) - add a throwaway slot
		// so Publish()'s own ScheduledSlotsRequiresTimeSlot check is satisfied,
		// then walk to the requested terminal status the same way real code would.
		_ = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
			maxParticipants: 10,
			DateTimeOffset.UtcNow).Value;
		opportunity.Publish().ThrowIfFailure();

		if (status == OpportunityStatus.Unpublished)
			opportunity.Unpublish().ThrowIfFailure();
		else if (status == OpportunityStatus.Cancelled)
			opportunity.Cancel().ThrowIfFailure();

		return opportunity;
	}

	public CreateEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_dbContext.Users.Returns(_userRepo);
		_keycloakService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "volunteer", "Test", "User", "volunteer@example.com"));
		_dbContext.CountActiveEngagementsForTimeSlotAsync(Arg.Any<TimeSlotId>(), Arg.Any<CancellationToken>())
			.Returns(0);
		_dbContext.GetTerminalEngagementAsync(Arg.Any<UserId>(), Arg.Any<VolunteerOpportunityId>(), Arg.Any<TimeSlotId?>(), Arg.Any<CancellationToken>())
			.Returns((Engagement?)null);
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call => ((IReadOnlyCollection<UserId>)call[0]!).Select(User.Create).ToList());
		_sut = new CreateEngagementCommandHandler(_dbContext, _keycloakService, _keycloakUserService, _emailService, _emailTemplateRenderer);
	}

	private void SetupOpportunityExists(
		VolunteerOpportunityId opportunityId, OpportunityStatus status = OpportunityStatus.Published)
	{
		var opportunity = CreateTestOpportunity(opportunityId, status);
		_opportunityRepo.FindAsync(opportunityId, Arg.Any<CancellationToken>())
			.Returns(opportunity);
	}

	private TimeSlotId SetupOpportunityExistsWithTimeSlot(
		VolunteerOpportunityId opportunityId, int? maxParticipants = 10, OpportunityStatus status = OpportunityStatus.Published)
	{
		var opportunity = CreateTestOpportunity(opportunityId, status);
		var timeSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
			maxParticipants,
			DateTimeOffset.UtcNow).Value;
		_opportunityRepo.FindAsync(opportunityId, Arg.Any<CancellationToken>())
			.Returns(opportunity);
		return timeSlot.Id;
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityDoesNotExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		_opportunityRepo.FindAsync(opportunityId, Arg.Any<CancellationToken>())
			.Returns((VolunteerOpportunity?)null);
		var command = new CreateEngagementCommand(opportunityId, UserId.New(),
			TimeSlotId.New(), Message: null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*not found*");
	}

	// --- Publish status gate (#1182) ---

	[Test]
	[Arguments(OpportunityStatus.Draft)]
	[Arguments(OpportunityStatus.Unpublished)]
	[Arguments(OpportunityStatus.Cancelled)]
	public async Task Handle_ShouldThrow_WhenOpportunityIsNotPublished(
		OpportunityStatus status, CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		SetupOpportunityExists(opportunityId, status);
		var command = new CreateEngagementCommand(opportunityId, UserId.New(), TimeSlotId: null, "Ich helfe gerne!");

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
		await _engagementRepo.DidNotReceive().AddAsync(Arg.Any<Engagement>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldCreateScheduledSlotsEngagement_WhenTimeSlotIdIsProvided(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		var volunteerId = UserId.New();
		var timeSlotId = SetupOpportunityExistsWithTimeSlot(opportunityId);
		var command = new CreateEngagementCommand(opportunityId, volunteerId, timeSlotId, Message: null);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.TimeSlotId.Should().Be(timeSlotId);
		result.Message.Should().BeNull();
		result.Status.Should().Be(EngagementStatus.Pending);
	}

	[Test]
	public async Task Handle_ShouldCreateIndividualContactEngagement_WhenTimeSlotIdIsNull(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		var volunteerId = UserId.New();
		SetupOpportunityExists(opportunityId);
		var command = new CreateEngagementCommand(opportunityId, volunteerId, TimeSlotId: null, "Ich helfe gerne!");

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.TimeSlotId.Should().BeNull();
		result.Message.Should().Be("Ich helfe gerne!");
		result.Status.Should().Be(EngagementStatus.Pending);
	}

	[Test]
	public async Task Handle_ShouldPersistEngagement_ToRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		var timeSlotId = SetupOpportunityExistsWithTimeSlot(opportunityId);
		var command = new CreateEngagementCommand(
			opportunityId,
			UserId.New(),
			timeSlotId,
			Message: null);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _engagementRepo.Received(1).AddAsync(Arg.Any<Engagement>(), cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenTimeSlotIdIsNullAndMessageIsNull(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		SetupOpportunityExists(opportunityId);
		var command = new CreateEngagementCommand(
			opportunityId,
			UserId.New(),
			TimeSlotId: null,
			Message: null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>();
	}

	[Test]
	public async Task Handle_ShouldSetCorrectOpportunityId_OnCreatedEngagement(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		var timeSlotId = SetupOpportunityExistsWithTimeSlot(opportunityId);
		var command = new CreateEngagementCommand(
			opportunityId,
			UserId.New(),
			timeSlotId,
			Message: null);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.OpportunityId.Should().Be(opportunityId);
	}

	[Test]
	public async Task Handle_ShouldSetCorrectVolunteerId_OnCreatedEngagement(
		CancellationToken cancellationToken)
	{
		// Arrange
		var volunteerId = UserId.New();
		var opportunityId = VolunteerOpportunityId.New();
		var timeSlotId = SetupOpportunityExistsWithTimeSlot(opportunityId);
		var command = new CreateEngagementCommand(
			opportunityId,
			volunteerId,
			timeSlotId,
			Message: null);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.VolunteerId.Should().Be(volunteerId);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenTimeSlotIsFull(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		var timeSlotId = SetupOpportunityExistsWithTimeSlot(opportunityId, maxParticipants: 3);
		_dbContext.CountActiveEngagementsForTimeSlotAsync(timeSlotId, Arg.Any<CancellationToken>())
			.Returns(3);
		var command = new CreateEngagementCommand(opportunityId, UserId.New(), timeSlotId, Message: null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
		await _engagementRepo.DidNotReceive().AddAsync(Arg.Any<Engagement>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSucceed_WhenTimeSlotHasUnlimitedCapacity_RegardlessOfActiveEngagementCount(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		var volunteerId = UserId.New();
		var timeSlotId = SetupOpportunityExistsWithTimeSlot(opportunityId, maxParticipants: null);
		_dbContext.CountActiveEngagementsForTimeSlotAsync(timeSlotId, Arg.Any<CancellationToken>())
			.Returns(1000);
		var command = new CreateEngagementCommand(opportunityId, volunteerId, timeSlotId, Message: null);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.TimeSlotId.Should().Be(timeSlotId);
		result.Status.Should().Be(EngagementStatus.Pending);
	}

	// --- Time-slot-scoped uniqueness (#1067) ---

	[Test]
	public async Task Handle_ShouldCheckForDuplicateSignUp_ScopedToTheRequestedTimeSlot(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		var volunteerId = UserId.New();
		var timeSlotId = SetupOpportunityExistsWithTimeSlot(opportunityId);
		var command = new CreateEngagementCommand(opportunityId, volunteerId, timeSlotId, Message: null);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert - the duplicate check is scoped to this time slot, not the whole
		// opportunity, so a second sign-up for a different slot isn't blocked.
		await _dbContext.Received(1).HasEngagementAsync(volunteerId, opportunityId, timeSlotId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldLookUpTerminalEngagement_ScopedToTheRequestedTimeSlot(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		var volunteerId = UserId.New();
		var timeSlotId = SetupOpportunityExistsWithTimeSlot(opportunityId);
		var command = new CreateEngagementCommand(opportunityId, volunteerId, timeSlotId, Message: null);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert - a terminated engagement for a *different* slot must never
		// surface here, or reactivating it would wipe that other slot's
		// attendance/feedback data (the #1067 compounding bug).
		await _dbContext.Received(1).GetTerminalEngagementAsync(volunteerId, opportunityId, timeSlotId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldReuseTerminalEngagement_WhenOneExistsForTheRequestedTimeSlot(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		var volunteerId = UserId.New();
		var timeSlotId = SetupOpportunityExistsWithTimeSlot(opportunityId);
		var terminalEngagement = Engagement.CreateSlotSignUp(opportunityId, volunteerId, timeSlotId);
		terminalEngagement.Withdraw();
		_dbContext.GetTerminalEngagementAsync(volunteerId, opportunityId, timeSlotId, Arg.Any<CancellationToken>())
			.Returns(terminalEngagement);
		var command = new CreateEngagementCommand(opportunityId, volunteerId, timeSlotId, Message: null);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeSameAs(terminalEngagement);
		result.Status.Should().Be(EngagementStatus.Pending);
		await _engagementRepo.DidNotReceive().AddAsync(Arg.Any<Engagement>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldCreateNewEngagement_WhenATerminalEngagementOnlyExistsForAnotherTimeSlot(
		CancellationToken cancellationToken)
	{
		// Arrange - simulates the volunteer having a cancelled/attended
		// engagement on a different slot of the same opportunity: since
		// GetTerminalEngagementAsync is stubbed per-timeSlotId, it returns null
		// for *this* slot, exactly like the real time-slot-scoped query would.
		var opportunityId = VolunteerOpportunityId.New();
		var volunteerId = UserId.New();
		var timeSlotId = SetupOpportunityExistsWithTimeSlot(opportunityId);
		var command = new CreateEngagementCommand(opportunityId, volunteerId, timeSlotId, Message: null);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert - a fresh engagement is created for this slot; nothing about the
		// other slot's terminal engagement is touched.
		result.TimeSlotId.Should().Be(timeSlotId);
		result.Status.Should().Be(EngagementStatus.Pending);
		await _engagementRepo.Received(1).AddAsync(Arg.Any<Engagement>(), cancellationToken);
	}

	// --- Localized emails (#1052) ---

	[Test]
	public async Task Handle_ShouldRenderVolunteerEmail_InVolunteersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		var volunteerId = UserId.New();
		SetupOpportunityExists(opportunityId);
		var volunteer = User.Create(volunteerId);
		volunteer.SetPreferredLanguage("en");
		_userRepo.FindAsync(volunteerId, Arg.Any<CancellationToken>()).Returns(volunteer);
		var command = new CreateEngagementCommand(opportunityId, volunteerId, TimeSlotId: null, "Hi!");

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementRequestReceived,
			"en",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldDefaultVolunteerEmailToGerman_WhenNoProfileExistsYet(
		CancellationToken cancellationToken)
	{
		// Arrange - a volunteer who signs up without ever having loaded their
		// profile page has no User row yet, so PreferredLanguage can't have
		// been seeded; the recipient's language must still resolve, never NRE.
		var opportunityId = VolunteerOpportunityId.New();
		var volunteerId = UserId.New();
		SetupOpportunityExists(opportunityId);
		_userRepo.FindAsync(volunteerId, Arg.Any<CancellationToken>()).Returns((User?)null);
		var command = new CreateEngagementCommand(opportunityId, volunteerId, TimeSlotId: null, "Hi!");

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementRequestReceived,
			"de",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	// --- Organizer notifications moved off the request path (#1174) ---
	//
	// The organizer "New sign-up" email (subscription-gated per #1055) is no
	// longer sent by this handler - it moves onto the outbox, delivered by
	// EngagementCreatedDomainEventHandler/EngagementReactivatedDomainEventHandler.
	// See those handlers' tests for the subscription-preference coverage that
	// used to live here.

	[Test]
	public async Task Handle_ShouldNotEmailOrganizersSynchronously_RegardlessOfHowManyExist(
		CancellationToken cancellationToken)
	{
		// Arrange - a rapid create/withdraw loop must no longer hold this
		// request's DB transaction open across one synchronous SMTP send per
		// organizer.
		var opportunityId = VolunteerOpportunityId.New();
		var timeSlotId = SetupOpportunityExistsWithTimeSlot(opportunityId);
		_keycloakService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([
				new KeycloakOrganizationMember(Guid.NewGuid(), "olaf", "Olaf", "Organizer", "olaf@example.com", true),
				new KeycloakOrganizationMember(Guid.NewGuid(), "petra", "Petra", "Organizer", "petra@example.com", true),
			]);
		var command = new CreateEngagementCommand(opportunityId, UserId.New(), timeSlotId, Message: null);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert - exactly one email goes out synchronously: the volunteer's own
		// receipt (#1055).
		await _emailService.Received(1).SendAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
