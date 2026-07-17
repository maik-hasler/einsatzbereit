using Application.Achievements.AwardAchievement.v1;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Users.RecordLogin.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.RecordLogin;

public class RecordLoginCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<UserStreak, UserStreakId> _streakRepo =
		Substitute.For<IAggregateRepository<UserStreak, UserStreakId>>();
	private readonly ISender _sender = Substitute.For<ISender>();
	private readonly RecordLoginCommandHandler _sut;

	public RecordLoginCommandHandlerTests()
	{
		_dbContext.UserStreaks.Returns(_streakRepo);
		_sut = new RecordLoginCommandHandler(_dbContext, _sender);
	}

	[Test]
	public async Task Handle_ShouldReturnTrue(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns((UserStreak?)null);

		var result = await _sut.Handle(new RecordLoginCommand(userId, DateOnly.FromDateTime(DateTime.UtcNow)), cancellationToken);

		result.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldCreateNewStreak_WhenNoStreakExists(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns((UserStreak?)null);

		await _sut.Handle(new RecordLoginCommand(userId, DateOnly.FromDateTime(DateTime.UtcNow)), cancellationToken);

		await _streakRepo.Received(1).AddAsync(Arg.Any<UserStreak>(), cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotSendAwardCommand_WhenStreakIsBelow7(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		// Streak of 6: login 6 consecutive days then record the 7th day NOT via the handler
		var streak = BuildStreakWithLoginCount(userId, 5);
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns(streak);

		// Calling on day 6 (one after the streak's last day)
		var nextDay = streak.LastLoginDate!.Value.AddDays(1);
		await _sut.Handle(new RecordLoginCommand(userId, nextDay), cancellationToken);

		// LoginStreak is now 6 - no badge yet
		await _sender.DidNotReceive().Send(
			Arg.Any<AwardAchievementCommand>(),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSendAwardCommand_WhenStreakReaches7(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		var streak = BuildStreakWithLoginCount(userId, 6);
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns(streak);

		var nextDay = streak.LastLoginDate!.Value.AddDays(1);
		await _sut.Handle(new RecordLoginCommand(userId, nextDay), cancellationToken);

		await _sender.Received(1).Send(
			Arg.Is<AwardAchievementCommand>(c => c.BadgeKey == "on-a-roll-7" && c.UserId == userId),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotSendAwardCommand_WhenStreakExceeds7(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		var streak = BuildStreakWithLoginCount(userId, 8);
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns(streak);

		// Day 9 - streak becomes 9, not 7
		var nextDay = streak.LastLoginDate!.Value.AddDays(1);
		await _sut.Handle(new RecordLoginCommand(userId, nextDay), cancellationToken);

		await _sender.DidNotReceive().Send(
			Arg.Any<AwardAchievementCommand>(),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotSendAwardCommand_WhenSameDayLogin(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		var streak = BuildStreakWithLoginCount(userId, 7);
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns(streak);

		// Same day as last login - RecordLogin is a no-op, streak stays 7
		await _sut.Handle(new RecordLoginCommand(userId, streak.LastLoginDate!.Value), cancellationToken);

		// No award sent for repeated same-day call
		await _sender.DidNotReceive().Send(
			Arg.Any<AwardAchievementCommand>(),
			Arg.Any<CancellationToken>());
	}

	private static UserStreak BuildStreakWithLoginCount(UserId userId, int count)
	{
		var streak = UserStreak.Create(userId);
		var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-count);
		for (var i = 0; i < count; i++)
		{
			streak.RecordLogin(day.AddDays(i));
		}
		return streak;
	}
}
