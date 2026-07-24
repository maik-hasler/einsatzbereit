using Application.Achievements.AwardAchievement.v1;
using Application.Achievements.BadgeCatalog;
using Application.Common.Persistence;
using AwesomeAssertions;
using Domain.Achievements;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Achievements.AwardAchievement;

public class AwardAchievementCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IBadgeCatalogService _catalogService = Substitute.For<IBadgeCatalogService>();
	private readonly IAggregateRepository<Achievement, AchievementId> _achievementRepo =
		Substitute.For<IAggregateRepository<Achievement, AchievementId>>();
	private readonly AwardAchievementCommandHandler _sut;

	private static readonly BadgeCatalogEntry TestBadge = new(
		"first-engagement",
		AchievementType.Milestone,
		"First Engagement",
		"Completed your first engagement.",
		false);

	public AwardAchievementCommandHandlerTests()
	{
		_dbContext.Achievements.Returns(_achievementRepo);
		_sut = new AwardAchievementCommandHandler(_dbContext, _catalogService);
	}

	[Test]
	public async Task Handle_ShouldReturnNull_WhenBadgeKeyIsUnknown(
		CancellationToken cancellationToken)
	{
		// Arrange
		_catalogService.FindByKey("unknown-badge").Returns((BadgeCatalogEntry?)null);
		var command = new AwardAchievementCommand(UserId.New(), "unknown-badge");

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeNull();
		await _achievementRepo.DidNotReceive().AddAsync(Arg.Any<Achievement>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldAwardAchievement_WhenNotAlreadyAwarded(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		_catalogService.FindByKey(TestBadge.Key).Returns(TestBadge);
		_dbContext.HasAchievementAsync(userId, TestBadge.Name, Arg.Any<CancellationToken>())
			.Returns(false);
		var command = new AwardAchievementCommand(userId, TestBadge.Key);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().NotBeNull().And.NotBe(Guid.Empty);
		await _achievementRepo.Received(1).AddAsync(Arg.Any<Achievement>(), cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldBeNoOp_WhenBadgeAlreadyAwarded(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		_catalogService.FindByKey(TestBadge.Key).Returns(TestBadge);
		_dbContext.HasAchievementAsync(userId, TestBadge.Name, Arg.Any<CancellationToken>())
			.Returns(true);
		var command = new AwardAchievementCommand(userId, TestBadge.Key);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().Be(Guid.Empty);
		await _achievementRepo.DidNotReceive().AddAsync(Arg.Any<Achievement>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldRemainIdempotent_WhenAwardedTwiceInSequence(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		_catalogService.FindByKey(TestBadge.Key).Returns(TestBadge);
		_dbContext.HasAchievementAsync(userId, TestBadge.Name, Arg.Any<CancellationToken>())
			.Returns(false);
		var firstCommand = new AwardAchievementCommand(userId, TestBadge.Key);

		// Act
		var firstResult = await _sut.Handle(firstCommand, cancellationToken);

		// Simulate the achievement now existing after the first award persisted.
		_dbContext.HasAchievementAsync(userId, TestBadge.Name, Arg.Any<CancellationToken>())
			.Returns(true);
		var secondCommand = new AwardAchievementCommand(userId, TestBadge.Key);
		var secondResult = await _sut.Handle(secondCommand, cancellationToken);

		// Assert
		firstResult.Should().NotBeNull().And.NotBe(Guid.Empty);
		secondResult.Should().Be(Guid.Empty);
		await _achievementRepo.Received(1).AddAsync(Arg.Any<Achievement>(), cancellationToken);
	}
}
