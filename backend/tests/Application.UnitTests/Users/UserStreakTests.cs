using AwesomeAssertions;
using Domain.Users;

namespace Application.UnitTests.Users;

public class UserStreakTests
{
	private static readonly DateOnly Today = new(2025, 6, 15);

	[Test]
	public void RecordLogin_ShouldInitializeStreakToOne_OnFirstLogin()
	{
		var streak = UserStreak.Create(UserId.New());

		streak.RecordLogin(Today);

		streak.LoginStreak.Should().Be(1);
		streak.LastLoginDate.Should().Be(Today);
	}

	[Test]
	public void RecordLogin_ShouldIncrementStreak_OnConsecutiveDays()
	{
		var streak = UserStreak.Create(UserId.New());
		streak.RecordLogin(Today);

		streak.RecordLogin(Today.AddDays(1));

		streak.LoginStreak.Should().Be(2);
	}

	[Test]
	public void RecordLogin_ShouldResetStreakToOne_WhenDayIsSkipped()
	{
		var streak = UserStreak.Create(UserId.New());
		streak.RecordLogin(Today);

		streak.RecordLogin(Today.AddDays(2));

		streak.LoginStreak.Should().Be(1);
	}

	[Test]
	public void RecordLogin_ShouldBeIdempotent_ForSameDay()
	{
		var streak = UserStreak.Create(UserId.New());
		streak.RecordLogin(Today);

		streak.RecordLogin(Today);

		streak.LoginStreak.Should().Be(1);
		streak.LastLoginDate.Should().Be(Today);
	}

	[Test]
	public void RecordLogin_ShouldReach7_AfterSevenConsecutiveDays()
	{
		var streak = UserStreak.Create(UserId.New());

		for (var i = 0; i < 7; i++)
		{
			streak.RecordLogin(Today.AddDays(i));
		}

		streak.LoginStreak.Should().Be(7);
	}

	[Test]
	public void RecordLogin_ShouldContinuePastSeven_OnDay8()
	{
		var streak = UserStreak.Create(UserId.New());

		for (var i = 0; i < 8; i++)
		{
			streak.RecordLogin(Today.AddDays(i));
		}

		streak.LoginStreak.Should().Be(8);
	}

	[Test]
	public void RecordLogin_ShouldResetAfterBreak_EvenAfterLongStreak()
	{
		var streak = UserStreak.Create(UserId.New());

		for (var i = 0; i < 7; i++)
		{
			streak.RecordLogin(Today.AddDays(i));
		}

		// Skip a day
		streak.RecordLogin(Today.AddDays(8));

		streak.LoginStreak.Should().Be(1);
	}

	[Test]
	public void RecordConfirmedEngagement_ShouldStartAtZero_BeforeAnyConfirmation()
	{
		var streak = UserStreak.Create(UserId.New());

		streak.TotalConfirmedEngagements.Should().Be(0);
	}

	[Test]
	public void RecordConfirmedEngagement_ShouldIncrementByOne_PerCall()
	{
		var streak = UserStreak.Create(UserId.New());

		streak.RecordConfirmedEngagement();
		streak.RecordConfirmedEngagement();
		streak.RecordConfirmedEngagement();

		streak.TotalConfirmedEngagements.Should().Be(3);
	}

	[Test]
	public void RecordConfirmedEngagement_ShouldNeverDecrease_RegardlessOfOtherState()
	{
		// The counter has no "undo" - it must stay monotonic even if unrelated
		// engagements are later cancelled or their opportunities deleted, since
		// those transitions never call this method.
		var streak = UserStreak.Create(UserId.New());

		for (var i = 0; i < 5; i++)
			streak.RecordConfirmedEngagement();

		streak.TotalConfirmedEngagements.Should().Be(5);
	}
}
