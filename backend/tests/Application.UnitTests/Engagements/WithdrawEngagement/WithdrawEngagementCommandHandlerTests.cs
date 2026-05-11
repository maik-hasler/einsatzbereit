using Application.Common.Persistence;
using Application.Engagements.WithdrawEngagement.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.WithdrawEngagement;

public class WithdrawEngagementCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly WithdrawEngagementCommandHandler _sut;

	public WithdrawEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_sut = new WithdrawEngagementCommandHandler(_dbContext);
	}

	private static (Engagement engagement, UserId volunteerId) CreatePendingEngagementWithVolunteer()
	{
		var volunteerId = new UserId(Guid.CreateVersion7());
		var engagement = Engagement.CreateWaitlistSignUp(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			volunteerId,
			new TimeSlotId(Guid.CreateVersion7()));
		return (engagement, volunteerId);
	}

	[Test]
	public async Task Handle_ShouldWithdrawEngagement_WhenCalledByOwner(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		var engagementId = new EngagementId(Guid.CreateVersion7());
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
		var engagementId = new EngagementId(Guid.CreateVersion7());
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
		var engagementId = new EngagementId(Guid.CreateVersion7());
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var command = new WithdrawEngagementCommand(engagementId, Guid.NewGuid());

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage($"*{engagementId.Value}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenCallerIsNotOwner(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, _) = CreatePendingEngagementWithVolunteer();
		var engagementId = new EngagementId(Guid.CreateVersion7());
		var differentUserId = Guid.NewGuid();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, differentUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*Only the volunteer*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsAlreadyWithdrawn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		engagement.Withdraw();
		var engagementId = new EngagementId(Guid.CreateVersion7());
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>().WithMessage("*already terminated*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		engagement.Cancel();
		var engagementId = new EngagementId(Guid.CreateVersion7());
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>().WithMessage("*already terminated*");
	}
}
