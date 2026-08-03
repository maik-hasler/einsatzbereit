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
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly GetUserProfileQueryHandler _sut;

	private static readonly UserId DefaultUserId = UserId.New();

	public GetUserProfileQueryHandlerTests()
	{
		_keycloakUserService
			.GetUserAsync(DefaultUserId.Value, Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(DefaultUserId.Value, "vera", "Vera", "Volunteer", "vera@example.com"));
		_sut = new GetUserProfileQueryHandler(_keycloakUserService, _dbContext);
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
		_dbContext.GetOrCreateUserAsync(userId, Arg.Any<string?>(), cancellationToken).Returns(user);

		var result = await _sut.Handle(new GetUserProfileQuery(userId, null), cancellationToken);

		result.Phone.Should().Be("+49 30 1234567");
	}

	[Test]
	public async Task Handle_ShouldDelegateLazyRowCreation_ToTheIdempotentGetOrCreate(
		CancellationToken cancellationToken)
	{
		// #1148: this handler must not lazily Add+SaveChanges itself (no ambient
		// transaction to protect it) - it delegates to the atomic, idempotent
		// GetOrCreateUserAsync instead, passing along the resolved request language
		// so a genuinely new row is seeded with it.
		var user = User.Create(DefaultUserId);
		_dbContext.GetOrCreateUserAsync(DefaultUserId, "en", cancellationToken).Returns(user);
		var query = new GetUserProfileQuery(DefaultUserId, "en");

		await _sut.Handle(query, cancellationToken);

		await _dbContext.Received(1).GetOrCreateUserAsync(DefaultUserId, "en", cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldDefaultPreferredLanguageToGerman_WhenRequestLanguageIsUnsupported(
		CancellationToken cancellationToken)
	{
		// Arrange
		var user = User.Create(DefaultUserId);
		_dbContext.GetOrCreateUserAsync(DefaultUserId, "de", cancellationToken).Returns(user);
		var query = new GetUserProfileQuery(DefaultUserId, "fr");

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.PreferredLanguage.Should().Be("de");
	}

	[Test]
	public async Task Handle_ShouldReturnExistingPreferredLanguage_WhenUserRowAlreadyExists(
		CancellationToken cancellationToken)
	{
		// Arrange - a returning user whose stored preference must survive even
		// though this request's language header says something different (e.g.
		// a different browser/session); GetOrCreateUserAsync itself is what
		// guarantees an existing row's PreferredLanguage is never overwritten,
		// this just asserts the handler returns whatever it gets back verbatim.
		var existingUser = User.Create(DefaultUserId);
		existingUser.SetPreferredLanguage("de");
		_dbContext.GetOrCreateUserAsync(DefaultUserId, "en", cancellationToken).Returns(existingUser);
		var query = new GetUserProfileQuery(DefaultUserId, "en");

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.PreferredLanguage.Should().Be("de");
	}
}
