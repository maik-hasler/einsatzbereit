using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Users.UpdateUserProfile.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.UpdateUserProfile;

public class UpdateUserProfileCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<User, UserId> _usersRepo = Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly UpdateUserProfileCommandHandler _sut;

	private static readonly UserId DefaultUserId = UserId.New();

	public UpdateUserProfileCommandHandlerTests()
	{
		_dbContext.Users.Returns(_usersRepo);
		_sut = new UpdateUserProfileCommandHandler(_keycloakUserService, _dbContext, _unitOfWork);
	}

	private static UpdateUserProfileCommand CreateCommand(string preferredLanguage) =>
		new(DefaultUserId, "Vera", "Volunteer", "Bio", null, [], [], null, preferredLanguage);

	[Test]
	public async Task Handle_ShouldSetPhone_OnExistingUserRow(
		CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		var user = User.Create(userId);
		_usersRepo.FindAsync(userId, cancellationToken).Returns(user);

		var command = new UpdateUserProfileCommand(
			userId, "Vera", "Volunteer", "Bio", "+49 30 1234567", [], [], null, "de");

		await _sut.Handle(command, cancellationToken);

		user.Phone.Should().Be("+49 30 1234567");
	}

	[Test]
	public async Task Handle_ShouldClearPhone_WhenNullGiven(
		CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		var user = User.Create(userId);
		user.SetPhone("+49 30 1234567");
		_usersRepo.FindAsync(userId, cancellationToken).Returns(user);

		var command = new UpdateUserProfileCommand(
			userId, "Vera", "Volunteer", "Bio", null, [], [], null, "de");

		await _sut.Handle(command, cancellationToken);

		user.Phone.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldSetPhone_OnNewlyCreatedUserRow(
		CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		_usersRepo.FindAsync(userId, cancellationToken).Returns((User?)null);

		User? added = null;
		await _usersRepo.AddAsync(Arg.Do<User>(u => added = u), cancellationToken);

		var command = new UpdateUserProfileCommand(
			userId, "Vera", "Volunteer", "Bio", "+49 30 1234567", [], [], null, "de");

		await _sut.Handle(command, cancellationToken);

		added.Should().NotBeNull();
		added!.Phone.Should().Be("+49 30 1234567");
	}

	[Test]
	public async Task Handle_ShouldSetPreferredLanguage_OnANewUserRow(
		CancellationToken cancellationToken)
	{
		// Arrange - FindAsync unconfigured, defaults to null: no local row exists yet.
		var command = CreateCommand("en");

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _usersRepo.Received(1).AddAsync(
			Arg.Is<User>(u => u!.PreferredLanguage == "en"), cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldOverwritePreferredLanguage_OnAnExistingUserRow(
		CancellationToken cancellationToken)
	{
		// Arrange - explicit profile save always wins, unlike the passive
		// creation-time seed in GetUserProfileQueryHandler.
		var existingUser = User.Create(DefaultUserId);
		existingUser.SetPreferredLanguage("de");
		_usersRepo.FindAsync(DefaultUserId, Arg.Any<CancellationToken>()).Returns(existingUser);
		var command = CreateCommand("en");

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		existingUser.PreferredLanguage.Should().Be("en");
	}
}
