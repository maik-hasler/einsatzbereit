using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Common.Storage;
using Application.Users.DeleteMyAccount.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;


namespace Application.UnitTests.Users.DeleteMyAccount;

public class DeleteMyAccountCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<User, UserId> _usersRepo = Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
	private readonly DeleteMyAccountCommandHandler _sut;

	private static readonly UserId DefaultUserId = UserId.New();

	public DeleteMyAccountCommandHandlerTests()
	{
		_dbContext.Users.Returns(_usersRepo);
		_dbContext
			.GetEngagementsForVolunteerTrackingAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(new List<Engagement>());
		_sut = new DeleteMyAccountCommandHandler(_dbContext, _keycloakUserService, _fileStorage);
	}

	private static Engagement CreateEngagementFor(UserId volunteerId) =>
		Engagement.CreateWaitlistSignUp(VolunteerOpportunityId.New(), volunteerId, TimeSlotId.New());

	[Test]
	public async Task Handle_ShouldAnonymizeAllEngagements_ReturnedForVolunteerTracking(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementOne = CreateEngagementFor(DefaultUserId);
		var engagementTwo = CreateEngagementFor(DefaultUserId);
		_dbContext
			.GetEngagementsForVolunteerTrackingAsync(DefaultUserId, cancellationToken)
			.Returns([engagementOne, engagementTwo]);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		engagementOne.IsAnonymized.Should().BeTrue();
		engagementTwo.IsAnonymized.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldDeleteNotificationsForRecipient(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _dbContext.Received(1).DeleteNotificationsForRecipientAsync(DefaultUserId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldDeleteAvatarForEveryKnownExtension_AndSwallowFailures(
		CancellationToken cancellationToken)
	{
		// Arrange
		var user = User.Create(DefaultUserId);
		_usersRepo.FindAsync(DefaultUserId, cancellationToken).Returns(user);
		_fileStorage
			.DeleteAsync($"user-avatars/{DefaultUserId.Value}.jpg", Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("MinIO unavailable"));
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		// Issue #829: a failure deleting one avatar extension is swallowed rather than rolled back -
		// the remaining extensions are still attempted even though the .jpg deletion threw.
		await _fileStorage.Received(1).DeleteAsync($"user-avatars/{DefaultUserId.Value}.jpg", cancellationToken);
		await _fileStorage.Received(1).DeleteAsync($"user-avatars/{DefaultUserId.Value}.png", cancellationToken);
		await _fileStorage.Received(1).DeleteAsync($"user-avatars/{DefaultUserId.Value}.webp", cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldDeleteTheUserRow_WhenUserExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var user = User.Create(DefaultUserId);
		_usersRepo.FindAsync(DefaultUserId, cancellationToken).Returns(user);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		_usersRepo.Received(1).Delete(user);
	}

	[Test]
	public async Task Handle_ShouldSkipAvatarDeletionAndUserRowDelete_ButStillDeleteKeycloakAccount_WhenLocalUserRowIsMissing(
		CancellationToken cancellationToken)
	{
		// Arrange - FindAsync is unconfigured and defaults to null, simulating a missing local user row.
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
		_usersRepo.DidNotReceive().Delete(Arg.Any<User>());
		// The Keycloak deletion runs unconditionally, unlike the avatar cleanup and user-row delete above.
		await _keycloakUserService.Received(1).DeleteUserAsync(DefaultUserId.Value, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldDeleteTheKeycloakAccount_AsTheFinalStep(
		CancellationToken cancellationToken)
	{
		// Arrange
		var user = User.Create(DefaultUserId);
		_usersRepo.FindAsync(DefaultUserId, cancellationToken).Returns(user);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _keycloakUserService.Received(1).DeleteUserAsync(DefaultUserId.Value, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldReturnTrue(
		CancellationToken cancellationToken)
	{
		// Arrange
		var user = User.Create(DefaultUserId);
		_usersRepo.FindAsync(DefaultUserId, cancellationToken).Returns(user);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
	}
}
