using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Users.GetUserProfile.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.GetUserProfile;

public class GetUserProfileQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<User, UserId> _usersRepo = Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly GetUserProfileQueryHandler _sut;

	private static readonly UserId DefaultUserId = UserId.New();

	public GetUserProfileQueryHandlerTests()
	{
		_dbContext.Users.Returns(_usersRepo);
		_keycloakUserService
			.GetUserAsync(DefaultUserId.Value, Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(DefaultUserId.Value, "vera", "Vera", "Volunteer", "vera@example.com"));
		_sut = new GetUserProfileQueryHandler(_keycloakUserService, _dbContext, _unitOfWork);
	}

	[Test]
	public async Task Handle_ShouldReturnPhone_WhenUserRowHasOne(
		CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		_keycloakUserService
			.GetUserAsync(userId.Value, cancellationToken)
			.Returns(new KeycloakUserProfile(userId.Value, "vera", "Vera", "Volunteer", "vera@test.de"));

		var user = User.Create(userId);
		user.SetPhone("+49 30 1234567");
		_usersRepo.FindAsync(userId, cancellationToken).Returns(user);

		var result = await _sut.Handle(new GetUserProfileQuery(userId, null), cancellationToken);

		result.Phone.Should().Be("+49 30 1234567");
	}

	[Test]
	public async Task Handle_ShouldReturnNullPhone_WhenNoUserRowExistsYet(
		CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		_keycloakUserService
			.GetUserAsync(userId.Value, cancellationToken)
			.Returns(new KeycloakUserProfile(userId.Value, "vera", "Vera", "Volunteer", "vera@test.de"));

		_usersRepo.FindAsync(userId, cancellationToken).Returns((User?)null);

		var result = await _sut.Handle(new GetUserProfileQuery(userId, null), cancellationToken);

		result.Phone.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldSeedPreferredLanguage_FromRequestLanguage_WhenCreatingANewUserRow(
		CancellationToken cancellationToken)
	{
		// Arrange - FindAsync unconfigured, defaults to null: no local row exists yet.
		var query = new GetUserProfileQuery(DefaultUserId, "en");

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.PreferredLanguage.Should().Be("en");
		await _usersRepo.Received(1).AddAsync(
			Arg.Is<User>(u => u!.PreferredLanguage == "en"), cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldDefaultPreferredLanguageToGerman_WhenRequestLanguageIsUnsupported(
		CancellationToken cancellationToken)
	{
		// Arrange
		var query = new GetUserProfileQuery(DefaultUserId, "fr");

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.PreferredLanguage.Should().Be("de");
	}

	[Test]
	public async Task Handle_ShouldNotOverwritePreferredLanguage_WhenUserRowAlreadyExists(
		CancellationToken cancellationToken)
	{
		// Arrange - a returning user whose stored preference must survive even
		// though this request's language header says something different (e.g.
		// a different browser/session), so a session elsewhere can never
		// silently flip their saved choice.
		var existingUser = User.Create(DefaultUserId);
		existingUser.SetPreferredLanguage("de");
		_usersRepo.FindAsync(DefaultUserId, Arg.Any<CancellationToken>()).Returns(existingUser);
		var query = new GetUserProfileQuery(DefaultUserId, "en");

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.PreferredLanguage.Should().Be("de");
		await _usersRepo.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
	}
}
