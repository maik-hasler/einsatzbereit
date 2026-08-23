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
	private readonly ISender _sender = Substitute.For<ISender>();
	private readonly RecordLoginCommandHandler _sut;

	public RecordLoginCommandHandlerTests()
	{
		_sut = new RecordLoginCommandHandler(_dbContext, _sender);
	}

	private void ArrangeNoExistingStreak(UserId userId, CancellationToken cancellationToken)
	{
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns((UserStreak?)null);
		_dbContext.GetOrCreateUserStreakAsync(userId, cancellationToken).Returns(UserStreak.Create(userId));
	}

	[Test]
	public async Task Handle_ShouldReturnTrue(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		ArrangeNoExistingStreak(userId, cancellationToken);

		var result = await _sut.Handle(new RecordLoginCommand(userId, DateOnly.FromDateTime(DateTime.UtcNow)), cancellationToken);

		result.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldCreateNewStreak_WhenNoStreakExists(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		ArrangeNoExistingStreak(userId, cancellationToken);

		await _sut.Handle(new RecordLoginCommand(userId, DateOnly.FromDateTime(DateTime.UtcNow)), cancellationToken);

		await _dbContext.Received(1).GetOrCreateUserStreakAsync(userId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldSendEarlyAdopterAward_WhenUserIsAmongFirst100(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		ArrangeNoExistingStreak(userId, cancellationToken);
		_dbContext.CountUserStreaksAsync(cancellationToken).Returns(99);

		await _sut.Handle(new RecordLoginCommand(userId, DateOnly.FromDateTime(DateTime.UtcNow)), cancellationToken);

		await _sender.Received(1).Send(
			Arg.Is<AwardAchievementCommand>(c => c!.BadgeKey == "early-adopter" && c.UserId == userId),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotSendEarlyAdopterAward_WhenUserIsThe101stToLogIn(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		ArrangeNoExistingStreak(userId, cancellationToken);
		_dbContext.CountUserStreaksAsync(cancellationToken).Returns(100);

		await _sut.Handle(new RecordLoginCommand(userId, DateOnly.FromDateTime(DateTime.UtcNow)), cancellationToken);

		await _sender.DidNotReceive().Send(
			Arg.Is<AwardAchievementCommand>(c => c!.BadgeKey == "early-adopter"),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotSendEarlyAdopterAward_WhenUserAlreadyHasAStreak(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		var streak = BuildStreakWithLoginCount(userId, 3);
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns(streak);

		var nextDay = streak.LastLoginDate!.Value.AddDays(1);
		await _sut.Handle(new RecordLoginCommand(userId, nextDay), cancellationToken);

		await _dbContext.DidNotReceive().CountUserStreaksAsync(Arg.Any<CancellationToken>());
		await _dbContext.DidNotReceive().GetOrCreateUserStreakAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
		await _sender.DidNotReceive().Send(
			Arg.Is<AwardAchievementCommand>(c => c!.BadgeKey == "early-adopter"),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotSendAwardCommand_WhenStreakIsBelow7(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		var streak = BuildStreakWithLoginCount(userId, 5);
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns(streak);

		var nextDay = streak.LastLoginDate!.Value.AddDays(1);
		await _sut.Handle(new RecordLoginCommand(userId, nextDay), cancellationToken);

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
			Arg.Is<AwardAchievementCommand>(c => c!.BadgeKey == "on-a-roll-7" && c.UserId == userId),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotSendAwardCommand_WhenStreakExceeds7(CancellationToken cancellationToken)
	{
		var userId = UserId.New();
		var streak = BuildStreakWithLoginCount(userId, 8);
		_dbContext.GetUserStreakAsync(userId, cancellationToken).Returns(streak);

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

		await _sut.Handle(new RecordLoginCommand(userId, streak.LastLoginDate!.Value), cancellationToken);

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
