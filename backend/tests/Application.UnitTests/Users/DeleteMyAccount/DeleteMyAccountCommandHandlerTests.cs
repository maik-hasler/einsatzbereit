using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Common.Storage;
using Application.Users.DeleteMyAccount.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Organizations;
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
		_dbContext
			.GetOrganizerOrganizationsAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(new List<Organization>());
		_sut = new DeleteMyAccountCommandHandler(_dbContext, _keycloakUserService, _fileStorage);
	}

	private static Engagement CreateEngagementFor(UserId volunteerId) =>
		Engagement.CreateSlotSignUp(VolunteerOpportunityId.New(), volunteerId, TimeSlotId.New());

	private static Organization CreateOrganization(string name) =>
		Organization.Create(OrganizationId.New(), name).Value;

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
	public async Task Handle_ShouldDeleteAvatarByItsExactObjectKey_AndSwallowFailures(
		CancellationToken cancellationToken)
	{
		// Arrange
		var user = User.Create(DefaultUserId);
		user.SetAvatarUrl($"https://example.com/user-avatars/{DefaultUserId.Value}/abc123.png");
		_usersRepo.FindAsync(DefaultUserId, cancellationToken).Returns(user);
		_fileStorage
			.GetObjectKeyFromPublicUrl($"https://example.com/user-avatars/{DefaultUserId.Value}/abc123.png")
			.Returns($"user-avatars/{DefaultUserId.Value}/abc123.png");
		_fileStorage
			.DeleteAsync($"user-avatars/{DefaultUserId.Value}/abc123.png", Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("MinIO unavailable"));
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		// Issue #829: a failure deleting the avatar is swallowed rather than rolled back.
		await _fileStorage.Received(1).DeleteAsync($"user-avatars/{DefaultUserId.Value}/abc123.png", cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotAttemptAvatarDeletion_WhenUserHasNoAvatar(
		CancellationToken cancellationToken)
	{
		// Arrange
		var user = User.Create(DefaultUserId);
		_usersRepo.FindAsync(DefaultUserId, cancellationToken).Returns(user);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotAttemptAvatarDeletion_WhenStoredAvatarUrlDoesNotMatchAnyKnownObjectKey(
		CancellationToken cancellationToken)
	{
		// Arrange: a malformed/legacy AvatarUrl that GetObjectKeyFromPublicUrl can't parse back
		// into an object key - defaults to null via the unconfigured NSubstitute mock.
		var user = User.Create(DefaultUserId);
		user.SetAvatarUrl("not-a-valid-storage-url");
		_usersRepo.FindAsync(DefaultUserId, cancellationToken).Returns(user);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
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

	[Test]
	public async Task Handle_ShouldDeleteTheUserStreak(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _dbContext.Received(1).DeleteUserStreakAsync(DefaultUserId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldDeleteAchievementsForTheUser(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _dbContext.Received(1).DeleteAchievementsForUserAsync(DefaultUserId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldRemoveOrganizationMembershipsAndDashboardLayoutsForTheUser(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _dbContext.Received(1).RemoveMembershipsForUserAsync(DefaultUserId, cancellationToken);
		await _dbContext.Received(1).RemoveDashboardLayoutsForUserAsync(DefaultUserId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldDeleteOrganizationInvitationsForTheUser(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _dbContext.Received(1).DeleteInvitationsForUserAsync(DefaultUserId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_AndPerformNoOtherAction_WhenSoleOrganizerOfAnOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organization = CreateOrganization("Solo Org");
		_dbContext
			.GetOrganizerOrganizationsAsync(DefaultUserId, cancellationToken)
			.Returns([organization]);
		_dbContext
			.CountOrganizersAsync(organization.Id, cancellationToken)
			.Returns(1);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		var thrown = await act.Should().ThrowAsync<ResultFailureException>();
		thrown.Which.Message.Should().Contain("Solo Org");
		await _dbContext.DidNotReceive().DeleteNotificationsForRecipientAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
		await _dbContext.DidNotReceive().RemoveMembershipsForUserAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
		await _dbContext.DidNotReceive().DeleteUserStreakAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
		_usersRepo.DidNotReceive().Delete(Arg.Any<User>());
		await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
		await _keycloakUserService.DidNotReceive().DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldListEveryBlockingOrganizationByName_WhenSoleOrganizerOfMultipleOrganizations(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgAlpha = CreateOrganization("Org Alpha");
		var orgBeta = CreateOrganization("Org Beta");
		_dbContext
			.GetOrganizerOrganizationsAsync(DefaultUserId, cancellationToken)
			.Returns([orgAlpha, orgBeta]);
		_dbContext.CountOrganizersAsync(orgAlpha.Id, cancellationToken).Returns(1);
		_dbContext.CountOrganizersAsync(orgBeta.Id, cancellationToken).Returns(1);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		var thrown = await act.Should().ThrowAsync<ResultFailureException>();
		thrown.Which.Message.Should().Contain("Org Alpha");
		thrown.Which.Message.Should().Contain("Org Beta");
	}

	[Test]
	public async Task Handle_ShouldProceed_WhenOrganizerButOtherOrganizersRemainForThatOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organization = CreateOrganization("Shared Org");
		_dbContext
			.GetOrganizerOrganizationsAsync(DefaultUserId, cancellationToken)
			.Returns([organization]);
		_dbContext
			.CountOrganizersAsync(organization.Id, cancellationToken)
			.Returns(2);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _dbContext.Received(1).RemoveMembershipsForUserAsync(DefaultUserId, cancellationToken);
		await _keycloakUserService.Received(1).DeleteUserAsync(DefaultUserId.Value, cancellationToken);
	}
}
