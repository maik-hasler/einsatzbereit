using Application.Common.Persistence;
using Application.Users.RecordActivity.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.RecordActivity;

public class RecordActivityCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<UserStreak, UserStreakId> _userStreaksRepo =
		Substitute.For<IAggregateRepository<UserStreak, UserStreakId>>();
	private readonly RecordActivityCommandHandler _sut;

	public RecordActivityCommandHandlerTests()
	{
		_dbContext.UserStreaks.Returns(_userStreaksRepo);
		_sut = new RecordActivityCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldCreateStreak_AndStartItAtOne_WhenUserHasNoStreakYet(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns((UserStreak?)null);

		var command = new RecordActivityCommand(userId, IsoYear: 2026, IsoWeek: 10);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _userStreaksRepo.Received(1).AddAsync(
			Arg.Is<UserStreak>(s => s!.UserId == userId && s.ActivityStreak == 1),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldIncrementStreak_WhenActivityFallsOnTheConsecutiveIsoWeek(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		var streak = UserStreak.Create(userId);
		streak.RecordActivity(2026, 9);
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns(streak);

		var command = new RecordActivityCommand(userId, IsoYear: 2026, IsoWeek: 10);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		streak.ActivityStreak.Should().Be(2);
		await _userStreaksRepo.DidNotReceive().AddAsync(Arg.Any<UserStreak>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldResetStreakToOne_WhenActivityGapSkipsAWeek(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		var streak = UserStreak.Create(userId);
		streak.RecordActivity(2026, 5);
		streak.RecordActivity(2026, 6);
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns(streak);

		// Week 8 is not consecutive to week 6 - week 7 was skipped entirely.
		var command = new RecordActivityCommand(userId, IsoYear: 2026, IsoWeek: 8);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		streak.ActivityStreak.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldIncrementStreakAcrossAnIsoYearBoundary(
		CancellationToken cancellationToken)
	{
		// Arrange: ISO 2026 has 53 weeks, so week 53 -> week 1 of 2027 is the
		// consecutive-week case that a naive "isoWeek + 1" comparison would miss.
		var userId = UserId.New();
		var streak = UserStreak.Create(userId);
		streak.RecordActivity(2026, 53);
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns(streak);

		var command = new RecordActivityCommand(userId, IsoYear: 2027, IsoWeek: 1);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		streak.ActivityStreak.Should().Be(2);
	}

	[Test]
	public async Task Handle_ShouldNotDoubleCount_WhenCalledAgainForTheSameIsoWeek(
		CancellationToken cancellationToken)
	{
		// Arrange: RecordActivity fires on every activity-triggering request within
		// a week, not once - a second call for the same week must be a no-op.
		var userId = UserId.New();
		var streak = UserStreak.Create(userId);
		streak.RecordActivity(2026, 10);
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns(streak);

		var command = new RecordActivityCommand(userId, IsoYear: 2026, IsoWeek: 10);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		streak.ActivityStreak.Should().Be(1);
	}
}
