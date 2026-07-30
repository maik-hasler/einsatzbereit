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
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly CreateEngagementCommandHandler _sut;

	private static readonly Address TestAddress = Address.Create("Main St", "1", "12345", "Berlin").Value;

	private VolunteerOpportunity CreateTestOpportunity(VolunteerOpportunityId id) =>
		VolunteerOpportunity.Create(
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

	public CreateEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_keycloakService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "volunteer", "Test", "User", "volunteer@example.com"));
		_dbContext.CountActiveEngagementsForTimeSlotAsync(Arg.Any<TimeSlotId>(), Arg.Any<CancellationToken>())
			.Returns(0);
		_dbContext.GetTerminalEngagementAsync(Arg.Any<UserId>(), Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns((Engagement?)null);
		_sut = new CreateEngagementCommandHandler(_dbContext, _keycloakService, _keycloakUserService, _emailService);
	}

	private void SetupOpportunityExists(VolunteerOpportunityId opportunityId)
	{
		var opportunity = CreateTestOpportunity(opportunityId);
		_opportunityRepo.FindAsync(opportunityId, Arg.Any<CancellationToken>())
			.Returns(opportunity);
	}

	private TimeSlotId SetupOpportunityExistsWithTimeSlot(VolunteerOpportunityId opportunityId, int? maxParticipants = 10)
	{
		var opportunity = CreateTestOpportunity(opportunityId);
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
}
