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
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly UpdateUserProfileCommandHandler _sut;

	private static readonly UserId DefaultUserId = UserId.New();

	public UpdateUserProfileCommandHandlerTests()
	{
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
		_dbContext.GetOrCreateUserAsync(userId, Arg.Any<string?>(), cancellationToken).Returns(user);

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
		_dbContext.GetOrCreateUserAsync(userId, Arg.Any<string?>(), cancellationToken).Returns(user);

		var command = new UpdateUserProfileCommand(
			userId, "Vera", "Volunteer", "Bio", null, [], [], null, "de");

		await _sut.Handle(command, cancellationToken);

		user.Phone.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldSetPhone_OnALazilyCreatedUserRow(
		CancellationToken cancellationToken)
	{
		// #1148: the row is fetched-or-created via the idempotent GetOrCreateUserAsync
		// rather than a check-then-Add the handler used to do itself - the handler
		// doesn't know or care whether the returned row was just created.
		var userId = UserId.New();
		var user = User.Create(userId);
		_dbContext.GetOrCreateUserAsync(userId, Arg.Any<string?>(), cancellationToken).Returns(user);

		var command = new UpdateUserProfileCommand(
			userId, "Vera", "Volunteer", "Bio", "+49 30 1234567", [], [], null, "de");

		await _sut.Handle(command, cancellationToken);

		user.Phone.Should().Be("+49 30 1234567");
	}

	[Test]
	public async Task Handle_ShouldSetPreferredLanguage_OnALazilyCreatedUserRow(
		CancellationToken cancellationToken)
	{
		// Arrange
		var user = User.Create(DefaultUserId);
		_dbContext.GetOrCreateUserAsync(DefaultUserId, Arg.Any<string?>(), cancellationToken).Returns(user);
		var command = CreateCommand("en");

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		user.PreferredLanguage.Should().Be("en");
	}

	[Test]
	public async Task Handle_ShouldOverwritePreferredLanguage_OnAnExistingUserRow(
		CancellationToken cancellationToken)
	{
		// Arrange - explicit profile save always wins, unlike the passive
		// creation-time seed in GetUserProfileQueryHandler.
		var existingUser = User.Create(DefaultUserId);
		existingUser.SetPreferredLanguage("de");
		_dbContext.GetOrCreateUserAsync(DefaultUserId, Arg.Any<string?>(), cancellationToken).Returns(existingUser);
		var command = CreateCommand("en");

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		existingUser.PreferredLanguage.Should().Be("en");
	}
}
