using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements.UndoCheckInEngagement.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.UndoCheckInEngagement;

public class UndoCheckInEngagementCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly UndoCheckInEngagementCommandHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public UndoCheckInEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new UndoCheckInEngagementCommandHandler(_dbContext);
	}

	private VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", null, "Beschreibung", null, true, null, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;

	private static Engagement CreateCheckedInEngagement(VolunteerOpportunityId opportunityId)
	{
		var engagement = Engagement.CreateSlotSignUp(opportunityId, UserId.New(), TimeSlotId.New());
		engagement.Confirm();
		engagement.CheckIn();
		return engagement;
	}

	[Test]
	public async Task Handle_ShouldUndoCheckIn_WhenEngagementIsCheckedIn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var engagement = CreateCheckedInEngagement(opportunity.Id);
		var engagementId = engagement.Id;

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var command = new UndoCheckInEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.IsCheckedIn.Should().BeFalse();
		result.Status.Should().Be(EngagementStatus.Confirmed);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsNotCheckedIn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), TimeSlotId.New());
		engagement.Confirm();
		var engagementId = engagement.Id;

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var command = new UndoCheckInEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Code.Should().Be("Engagement.CheckInNotActive");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsTerminated(
		CancellationToken cancellationToken)
	{
		// Arrange

		var opportunity = CreateOpportunity();
		var engagement = CreateCheckedInEngagement(opportunity.Id);
		engagement.Cancel();
		var engagementId = engagement.Id;

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var command = new UndoCheckInEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Code.Should().Be("Engagement.AlreadyTerminated");
		engagement.IsCheckedIn.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var engagement = CreateCheckedInEngagement(opportunity.Id);
		var engagementId = engagement.Id;

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new UndoCheckInEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		engagement.IsCheckedIn.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldThrowNotFound_WhenEngagementIsGone(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var command = new UndoCheckInEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
	}

	[Test]
	public async Task Handle_ShouldThrowNotFound_WhenOpportunityIsGone(
		CancellationToken cancellationToken)
	{
		// Arrange

		var opportunityId = VolunteerOpportunityId.New();
		var engagement = CreateCheckedInEngagement(opportunityId);
		var engagementId = engagement.Id;

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunityId, cancellationToken).Returns((VolunteerOpportunity?)null);

		var command = new UndoCheckInEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
		engagement.IsCheckedIn.Should().BeTrue();
		await _dbContext
			.DidNotReceive()
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>());
	}
}
