using Application.Common.Exceptions;
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
		_sut = new DeleteMyAccountCommandHandler(_dbContext, _fileStorage);
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
	public async Task Handle_ShouldWithdrawPendingEngagement_BeforeAnonymizing_SoItStopsOccupyingCapacity(
		CancellationToken cancellationToken)
	{
		// Arrange - issue #1140: a stuck Pending/Confirmed row would otherwise occupy
		// time-slot capacity forever once anonymized, since nothing else ever terminates it.
		var engagement = CreateEngagementFor(DefaultUserId);
		_dbContext
			.GetEngagementsForVolunteerTrackingAsync(DefaultUserId, cancellationToken)
			.Returns([engagement]);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		engagement.Status.Should().Be(EngagementStatus.Withdrawn);
		engagement.IsAnonymized.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldWithdrawConfirmedNotCheckedInEngagement_BeforeAnonymizing(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagement = CreateEngagementFor(DefaultUserId);
		engagement.Confirm().ThrowIfFailure();
		_dbContext
			.GetEngagementsForVolunteerTrackingAsync(DefaultUserId, cancellationToken)
			.Returns([engagement]);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		engagement.Status.Should().Be(EngagementStatus.Withdrawn);
		engagement.IsAnonymized.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldLeaveCheckedInEngagementConfirmed_ButStillAnonymizeIt(
		CancellationToken cancellationToken)
	{
		// Arrange - a checked-in engagement is historical record of a completed shift;
		// Withdraw() refuses a checked-in engagement, so it stays Confirmed but anonymized.
		var engagement = CreateEngagementFor(DefaultUserId);
		engagement.Confirm().ThrowIfFailure();
		engagement.CheckIn().ThrowIfFailure();
		_dbContext
			.GetEngagementsForVolunteerTrackingAsync(DefaultUserId, cancellationToken)
			.Returns([engagement]);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		engagement.Status.Should().Be(EngagementStatus.Confirmed);
		engagement.IsCheckedIn.Should().BeTrue();
		engagement.IsAnonymized.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldLeaveAlreadyTerminatedEngagementAsIs_ButStillAnonymizeIt(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagement = CreateEngagementFor(DefaultUserId);
		engagement.Cancel().ThrowIfFailure();
		_dbContext
			.GetEngagementsForVolunteerTrackingAsync(DefaultUserId, cancellationToken)
			.Returns([engagement]);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		engagement.Status.Should().Be(EngagementStatus.Cancelled);
		engagement.IsAnonymized.Should().BeTrue();
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
	public async Task Handle_ShouldSkipAvatarDeletionAndUserRowDelete_WhenLocalUserRowIsMissing(
		CancellationToken cancellationToken)
	{
		// Arrange - FindAsync is unconfigured and defaults to null, simulating a missing local user row.
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
		_usersRepo.DidNotReceive().Delete(Arg.Any<User>());
	}

	[Test]
	public async Task Handle_ShouldRaiseUserAccountDeletedEvent_OnlyAfterUserRowIsFoundAndMarkedForDeletion(
		CancellationToken cancellationToken)
	{
		// Arrange - issue #1141: the Keycloak identity is irreversible, so its deletion must be
		// deferred to a post-commit domain-event handler rather than called inline here (see
		// UserAccountDeletedDomainEventHandler). The handler's only job is to raise that event
		// on the aggregate once the local row is actually about to be deleted.
		var user = User.Create(DefaultUserId);
		_usersRepo.FindAsync(DefaultUserId, cancellationToken).Returns(user);
		var command = new DeleteMyAccountCommand(DefaultUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		user.Events.Should().ContainSingle()
			.Which.Should().BeOfType<UserAccountDeletedDomainEvent>()
			.Which.UserId.Should().Be(DefaultUserId);
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
	}
}
