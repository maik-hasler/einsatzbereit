using Application.Common.Persistence;
using Application.Engagements.CancelEngagement.v1;
using AwesomeAssertions;
using Domain.Engagements;
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
    private readonly CancelEngagementCommandHandler _sut;

    public CancelEngagementCommandHandlerTests()
    {
        _dbContext.Engagements.Returns(_engagementRepo);
        _sut = new CancelEngagementCommandHandler(_dbContext);
    }

    private static Engagement CreatePendingWaitlistEngagement() =>
        Engagement.CreateWaitlistSignUp(
            new VolunteerOpportunityId(Guid.CreateVersion7()),
            new UserId(Guid.CreateVersion7()),
            new TimeSlotId(Guid.CreateVersion7()));

    [Test]
    public async Task Handle_ShouldCancelEngagement_WhenEngagementIsPending(
        CancellationToken cancellationToken)
    {
        // Arrange
        var engagementId = new EngagementId(Guid.CreateVersion7());
        var engagement = CreatePendingWaitlistEngagement();
        _engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

        // Act
        var result = await _sut.Handle(new CancelEngagementCommand(engagementId), cancellationToken);

        // Assert
        result.Status.Should().Be(EngagementStatus.Cancelled);
    }

    [Test]
    public async Task Handle_ShouldCancelEngagement_WhenEngagementIsConfirmed(
        CancellationToken cancellationToken)
    {
        // Arrange
        var engagementId = new EngagementId(Guid.CreateVersion7());
        var engagement = CreatePendingWaitlistEngagement();
        engagement.Confirm();
        _engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

        // Act
        var result = await _sut.Handle(new CancelEngagementCommand(engagementId), cancellationToken);

        // Assert
        result.Status.Should().Be(EngagementStatus.Cancelled);
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenEngagementNotFound(
        CancellationToken cancellationToken)
    {
        // Arrange
        var engagementId = new EngagementId(Guid.CreateVersion7());
        _engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

        // Act
        Func<Task> act = async () => await _sut.Handle(new CancelEngagementCommand(engagementId), cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage($"*{engagementId.Value}*");
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenEngagementIsAlreadyCancelled(
        CancellationToken cancellationToken)
    {
        // Arrange
        var engagementId = new EngagementId(Guid.CreateVersion7());
        var engagement = CreatePendingWaitlistEngagement();
        engagement.Cancel();
        _engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

        // Act
        Func<Task> act = async () => await _sut.Handle(new CancelEngagementCommand(engagementId), cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*already terminated*");
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenEngagementIsWithdrawn(
        CancellationToken cancellationToken)
    {
        // Arrange
        var engagementId = new EngagementId(Guid.CreateVersion7());
        var engagement = CreatePendingWaitlistEngagement();
        engagement.Withdraw();
        _engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

        // Act
        Func<Task> act = async () => await _sut.Handle(new CancelEngagementCommand(engagementId), cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*already terminated*");
    }

    [Test]
    public async Task Handle_ShouldReturnSameEngagement_Instance(
        CancellationToken cancellationToken)
    {
        // Arrange
        var engagementId = new EngagementId(Guid.CreateVersion7());
        var engagement = CreatePendingWaitlistEngagement();
        _engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

        // Act
        var result = await _sut.Handle(new CancelEngagementCommand(engagementId), cancellationToken);

        // Assert
        result.Should().BeSameAs(engagement);
    }
}
