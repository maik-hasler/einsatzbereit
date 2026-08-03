using Application.Achievements;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Users.GetPublicUserProfile.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.GetPublicUserProfile;

public class GetPublicUserProfileQueryHandlerTests
{
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAchievementReadRepository _achievementReadRepository = Substitute.For<IAchievementReadRepository>();
	private readonly IAggregateRepository<User, UserId> _userRepo = Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly GetPublicUserProfileQueryHandler _sut;

	public GetPublicUserProfileQueryHandlerTests()
	{
		_dbContext.Users.Returns(_userRepo);
		_achievementReadRepository
			.GetByUserAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_sut = new GetPublicUserProfileQueryHandler(_keycloakUserService, _dbContext, _achievementReadRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnBioSkillsAndLanguages_WhenUserRowExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		_keycloakUserService
			.GetUserAsync(userId.Value, cancellationToken)
			.Returns(new KeycloakUserProfile(userId.Value, "vera", "Vera", "Volunteer", "vera@test.de"));

		var user = User.Create(userId);
		user.ChangeBio("Loves helping out");
		user.UpdateSkills(["First aid"]);
		user.UpdateLanguages(["German", "English"]);
		_userRepo.FindAsync(userId, cancellationToken).Returns(user);

		// Act
		var result = await _sut.Handle(new GetPublicUserProfileQuery(userId), cancellationToken);

		// Assert
		result.Should().NotBeNull();
		result!.Bio.Should().Be("Loves helping out");
		result.Skills.Should().ContainSingle().Which.Should().Be("First aid");
		result.Languages.Should().BeEquivalentTo(["German", "English"]);
	}

	[Test]
	public async Task Handle_ShouldNotExposePreferredContactOrPhone_EvenWhenSet(
		CancellationToken cancellationToken)
	{
		// #1028: this endpoint is AllowAnonymous() - PreferredContact/Phone must
		// never leak to an anonymous visitor, regardless of what the volunteer
		// has set on their own profile. Contact info only ever reaches an
		// organizer through an actual engagement (EngagementSummary).
		var userId = UserId.New();
		_keycloakUserService
			.GetUserAsync(userId.Value, cancellationToken)
			.Returns(new KeycloakUserProfile(userId.Value, "vera", "Vera", "Volunteer", "vera@test.de"));

		var user = User.Create(userId);
		user.SetPreferredContact(PreferredContact.Phone);
		user.SetPhone("+49 555 1234567");
		_userRepo.FindAsync(userId, cancellationToken).Returns(user);

		// Act
		var result = await _sut.Handle(new GetPublicUserProfileQuery(userId), cancellationToken);

		// Assert
		result.Should().NotBeNull();
		result!.GetType().GetProperty("PreferredContact").Should().BeNull();
		result!.GetType().GetProperty("Phone").Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyProfileFields_WhenNoUserRowExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		_keycloakUserService
			.GetUserAsync(userId.Value, cancellationToken)
			.Returns(new KeycloakUserProfile(userId.Value, "vera", "Vera", "Volunteer", "vera@test.de"));

		_userRepo.FindAsync(userId, cancellationToken).Returns((User?)null);

		// Act
		var result = await _sut.Handle(new GetPublicUserProfileQuery(userId), cancellationToken);

		// Assert
		result.Should().NotBeNull();
		result!.Bio.Should().BeNull();
		result.Skills.Should().BeEmpty();
		result.Languages.Should().BeEmpty();
	}
}
