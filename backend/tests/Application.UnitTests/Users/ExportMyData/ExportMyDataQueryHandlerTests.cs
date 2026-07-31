using Application.Achievements;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Organizations;
using Application.Users.ExportMyData.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.ExportMyData;

public class ExportMyDataQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<User, UserId> _usersRepo = Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEngagementReadRepository _engagementReadRepository = Substitute.For<IEngagementReadRepository>();
	private readonly IAchievementReadRepository _achievementReadRepository = Substitute.For<IAchievementReadRepository>();
	private readonly ExportMyDataQueryHandler _sut;

	private static readonly UserId DefaultUserId = UserId.New();

	public ExportMyDataQueryHandlerTests()
	{
		_dbContext.Users.Returns(_usersRepo);
		_keycloakUserService
			.GetUserAsync(DefaultUserId.Value, Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(DefaultUserId.Value, "vera", "Vera", "Volunteer", "vera@example.com"));
		_engagementReadRepository
			.GetAllByVolunteerAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(new List<EngagementSummary>());
		_achievementReadRepository
			.GetByUserAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(new List<AchievementSummary>());
		_dbContext
			.GetMembershipsForUserAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(new List<OrganizationMembershipSummary>());

		_sut = new ExportMyDataQueryHandler(
			_keycloakUserService,
			_dbContext,
			_engagementReadRepository,
			_achievementReadRepository);
	}

	[Test]
	public async Task Handle_ShouldMergeKeycloakAndLocalProfileFields_WhenLocalUserRowExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var user = User.Create(DefaultUserId);
		user.ChangeBio("Loves volunteering");
		user.SetPhone("+49 30 1234567");
		user.UpdateSkills(["First Aid", "Driving"]);
		_usersRepo.FindAsync(DefaultUserId, cancellationToken).Returns(user);
		var query = new ExportMyDataQuery(DefaultUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Profile.Id.Should().Be(DefaultUserId.Value);
		result.Profile.Username.Should().Be("vera");
		result.Profile.Email.Should().Be("vera@example.com");
		result.Profile.Bio.Should().Be("Loves volunteering");
		result.Profile.Phone.Should().Be("+49 30 1234567");
		result.Profile.Skills.Should().BeEquivalentTo(["First Aid", "Driving"]);
	}

	[Test]
	public async Task Handle_ShouldFallBackToEmptyLocalProfileFields_WhenLocalUserRowIsMissing(
		CancellationToken cancellationToken)
	{
		// Arrange - FindAsync is unconfigured and defaults to null, simulating a user who never
		// opened their profile page and so has no local User row.
		var query = new ExportMyDataQuery(DefaultUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Profile.Username.Should().Be("vera");
		result.Profile.Bio.Should().BeNull();
		result.Profile.Phone.Should().BeNull();
		result.Profile.Skills.Should().BeEmpty();
		result.Profile.Languages.Should().BeEmpty();
		result.Profile.PreferredContact.Should().BeNull();
		result.Profile.PreferredLanguage.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldReturnEngagements_FromTheReadRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			Guid.NewGuid(),
			"Beach Cleanup",
			Guid.NewGuid(),
			"Ocean Org",
			DefaultUserId.Value,
			null,
			null,
			"Confirmed",
			false,
			false,
			DateTimeOffset.UtcNow);
		_engagementReadRepository
			.GetAllByVolunteerAsync(DefaultUserId, cancellationToken)
			.Returns([engagement]);
		var query = new ExportMyDataQuery(DefaultUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Engagements.Should().ContainSingle().Which.Should().BeEquivalentTo(engagement);
	}

	[Test]
	public async Task Handle_ShouldReturnAchievements_FromTheReadRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var achievement = new AchievementSummary(
			Guid.NewGuid(),
			"Milestone",
			"first-engagement",
			"First Steps",
			"Completed your first engagement",
			DateTimeOffset.UtcNow);
		_achievementReadRepository
			.GetByUserAsync(DefaultUserId, cancellationToken)
			.Returns([achievement]);
		var query = new ExportMyDataQuery(DefaultUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Achievements.Should().ContainSingle().Which.Should().BeEquivalentTo(achievement);
	}

	[Test]
	public async Task Handle_ShouldReturnZeroStreak_WhenNoStreakRowExists(
		CancellationToken cancellationToken)
	{
		// Arrange - GetUserStreakAsync is unconfigured and defaults to null.
		var query = new ExportMyDataQuery(DefaultUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Streak.LoginStreak.Should().Be(0);
		result.Streak.ActivityStreak.Should().Be(0);
	}

	[Test]
	public async Task Handle_ShouldReturnStreakCounts_WhenStreakRowExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var streak = UserStreak.Create(DefaultUserId);
		streak.RecordLogin(DateOnly.FromDateTime(DateTime.UtcNow));
		_dbContext.GetUserStreakAsync(DefaultUserId, cancellationToken).Returns(streak);
		var query = new ExportMyDataQuery(DefaultUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Streak.LoginStreak.Should().Be(streak.LoginStreak);
		result.Streak.ActivityStreak.Should().Be(streak.ActivityStreak);
	}

	[Test]
	public async Task Handle_ShouldReturnOrganizationMemberships_FromTheDbContext(
		CancellationToken cancellationToken)
	{
		// Arrange
		var membership = new OrganizationMembershipSummary(Guid.NewGuid(), "Ocean Org", "Member");
		_dbContext
			.GetMembershipsForUserAsync(DefaultUserId, cancellationToken)
			.Returns([membership]);
		var query = new ExportMyDataQuery(DefaultUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.OrganizationMemberships.Should().ContainSingle().Which.Should().BeEquivalentTo(membership);
	}
}
