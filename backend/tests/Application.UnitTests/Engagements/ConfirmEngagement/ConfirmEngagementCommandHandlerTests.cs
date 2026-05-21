using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.ConfirmEngagement.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.ConfirmEngagement;

public class ConfirmEngagementCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly ConfirmEngagementCommandHandler _sut;

	public ConfirmEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "user", null, null, "user@example.com"));
		_sut = new ConfirmEngagementCommandHandler(_dbContext, _keycloakUserService, _emailService);
	}

	[Test]
	public async Task Handle_ShouldConfirmEngagement_WhenEngagementIsPending(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = new EngagementId(Guid.CreateVersion7());
		var engagement = Engagement.CreateWaitlistSignUp(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			new UserId(Guid.CreateVersion7()),
			new TimeSlotId(Guid.CreateVersion7()));

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new ConfirmEngagementCommand(engagementId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Status.Should().Be(EngagementStatus.Confirmed);
	}

	[Test]
	public async Task Handle_ShouldReturnEngagement_WithCorrectId(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = new EngagementId(Guid.CreateVersion7());
		var engagement = Engagement.CreateWaitlistSignUp(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			new UserId(Guid.CreateVersion7()),
			new TimeSlotId(Guid.CreateVersion7()));

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new ConfirmEngagementCommand(engagementId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeSameAs(engagement);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = new EngagementId(Guid.CreateVersion7());
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var command = new ConfirmEngagementCommand(engagementId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage($"*{engagementId.Value}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsAlreadyConfirmed(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = new EngagementId(Guid.CreateVersion7());
		var engagement = Engagement.CreateWaitlistSignUp(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			new UserId(Guid.CreateVersion7()),
			new TimeSlotId(Guid.CreateVersion7()));
		engagement.Confirm();

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new ConfirmEngagementCommand(engagementId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>().WithMessage("*Only pending*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = new EngagementId(Guid.CreateVersion7());
		var engagement = Engagement.CreateWaitlistSignUp(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			new UserId(Guid.CreateVersion7()),
			new TimeSlotId(Guid.CreateVersion7()));
		engagement.Cancel();

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new ConfirmEngagementCommand(engagementId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>().WithMessage("*Only pending*");
	}
}
