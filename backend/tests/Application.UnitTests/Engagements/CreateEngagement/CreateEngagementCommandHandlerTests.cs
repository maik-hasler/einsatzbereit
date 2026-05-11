using Application.Common.Persistence;
using Application.Engagements.CreateEngagement.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.CreateEngagement;

public class CreateEngagementCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly CreateEngagementCommandHandler _sut;

	public CreateEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_sut = new CreateEngagementCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldCreateWaitlistEngagement_WhenTimeSlotIdIsProvided(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = new VolunteerOpportunityId(Guid.CreateVersion7());
		var volunteerId = new UserId(Guid.CreateVersion7());
		var timeSlotId = new TimeSlotId(Guid.CreateVersion7());
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
		var opportunityId = new VolunteerOpportunityId(Guid.CreateVersion7());
		var volunteerId = new UserId(Guid.CreateVersion7());
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
		var command = new CreateEngagementCommand(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			new UserId(Guid.CreateVersion7()),
			new TimeSlotId(Guid.CreateVersion7()),
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
		var command = new CreateEngagementCommand(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			new UserId(Guid.CreateVersion7()),
			TimeSlotId: null,
			Message: null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>();
	}

	[Test]
	public async Task Handle_ShouldSetCorrectOpportunityId_OnCreatedEngagement(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = new VolunteerOpportunityId(Guid.CreateVersion7());
		var command = new CreateEngagementCommand(
			opportunityId,
			new UserId(Guid.CreateVersion7()),
			new TimeSlotId(Guid.CreateVersion7()),
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
		var volunteerId = new UserId(Guid.CreateVersion7());
		var command = new CreateEngagementCommand(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			volunteerId,
			new TimeSlotId(Guid.CreateVersion7()),
			Message: null);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.VolunteerId.Should().Be(volunteerId);
	}
}
