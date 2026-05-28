using AwesomeAssertions;
using Domain.Users;

namespace Application.UnitTests.Users;

public class UserStreakTests
{
	private static readonly DateOnly Today = new(2025, 6, 15);

	[Test]
	public void RecordLogin_ShouldInitializeStreakToOne_OnFirstLogin()
	{
		var streak = UserStreak.Create(new UserId(Guid.CreateVersion7()));

		streak.RecordLogin(Today);

		streak.LoginStreak.Should().Be(1);
		streak.LastLoginDate.Should().Be(Today);
	}

	[Test]
	public void RecordLogin_ShouldIncrementStreak_OnConsecutiveDays()
	{
		var streak = UserStreak.Create(new UserId(Guid.CreateVersion7()));
		streak.RecordLogin(Today);

		streak.RecordLogin(Today.AddDays(1));

		streak.LoginStreak.Should().Be(2);
	}

	[Test]
	public void RecordLogin_ShouldResetStreakToOne_WhenDayIsSkipped()
	{
		var streak = UserStreak.Create(new UserId(Guid.CreateVersion7()));
		streak.RecordLogin(Today);

		streak.RecordLogin(Today.AddDays(2));

		streak.LoginStreak.Should().Be(1);
	}

	[Test]
	public void RecordLogin_ShouldBeIdempotent_ForSameDay()
	{
		var streak = UserStreak.Create(new UserId(Guid.CreateVersion7()));
		streak.RecordLogin(Today);

		streak.RecordLogin(Today);

		streak.LoginStreak.Should().Be(1);
		streak.LastLoginDate.Should().Be(Today);
	}

	[Test]
	public void RecordLogin_ShouldReach7_AfterSevenConsecutiveDays()
	{
		var streak = UserStreak.Create(new UserId(Guid.CreateVersion7()));

		for (var i = 0; i < 7; i++)
		{
			streak.RecordLogin(Today.AddDays(i));
		}

		streak.LoginStreak.Should().Be(7);
	}

	[Test]
	public void RecordLogin_ShouldContinuePastSeven_OnDay8()
	{
		var streak = UserStreak.Create(new UserId(Guid.CreateVersion7()));

		for (var i = 0; i < 8; i++)
		{
			streak.RecordLogin(Today.AddDays(i));
		}

		streak.LoginStreak.Should().Be(8);
	}

	[Test]
	public void RecordLogin_ShouldResetAfterBreak_EvenAfterLongStreak()
	{
		var streak = UserStreak.Create(new UserId(Guid.CreateVersion7()));

		for (var i = 0; i < 7; i++)
		{
			streak.RecordLogin(Today.AddDays(i));
		}

		// Skip a day
		streak.RecordLogin(Today.AddDays(8));

		streak.LoginStreak.Should().Be(1);
	}
}
