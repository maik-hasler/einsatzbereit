using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Users.GetUserProfile.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.GetUserProfile;

public class GetUserProfileQueryHandlerTests
{
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IAggregateRepository<User, UserId> _userRepo = Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly GetUserProfileQueryHandler _sut;

	public GetUserProfileQueryHandlerTests()
	{
		_dbContext.Users.Returns(_userRepo);
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
		_userRepo.FindAsync(userId, cancellationToken).Returns(user);

		var result = await _sut.Handle(new GetUserProfileQuery(userId), cancellationToken);

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

		_userRepo.FindAsync(userId, cancellationToken).Returns((User?)null);

		var result = await _sut.Handle(new GetUserProfileQuery(userId), cancellationToken);

		result.Phone.Should().BeNull();
	}
}
