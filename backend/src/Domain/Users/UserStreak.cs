using Domain.Primitives;

namespace Domain.Users;

public sealed class UserStreak
	: AggregateRoot<UserStreakId>,
		IAuditableEntity
{
	public UserId UserId { get; private set; }

	public int LoginStreak { get; private set; }

	public DateOnly? LastLoginDate { get; private set; }

	public int ActivityStreak { get; private set; }

	public int? LastActiveIsoWeek { get; private set; }

	public int? LastActiveIsoYear { get; private set; }

	public int TotalConfirmedEngagements { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

#pragma warning disable CS8618
	private UserStreak() : base(default) { }
#pragma warning restore CS8618

	private UserStreak(UserStreakId id, UserId userId) : base(id)
	{
		UserId = userId;
		LoginStreak = 0;
		ActivityStreak = 0;
	}

	public static UserStreak Create(UserId userId) =>
		new(new UserStreakId(Guid.CreateVersion7()), userId);

	public void RecordLogin(DateOnly today)
	{
		if (LastLoginDate == today)
			return;

		if (LastLoginDate == today.AddDays(-1))
			LoginStreak++;
		else
			LoginStreak = 1;

		LastLoginDate = today;
	}

	public void RecordActivity(int isoYear, int isoWeek)
	{
		if (LastActiveIsoYear == isoYear && LastActiveIsoWeek == isoWeek)
			return;

		var isConsecutive =
			LastActiveIsoYear is not null &&
			LastActiveIsoWeek is not null &&
			IsPreviousWeek(LastActiveIsoYear.Value, LastActiveIsoWeek.Value, isoYear, isoWeek);

		ActivityStreak = isConsecutive ? ActivityStreak + 1 : 1;
		LastActiveIsoYear = isoYear;
		LastActiveIsoWeek = isoWeek;
	}

	public void RecordConfirmedEngagement()
	{
		TotalConfirmedEngagements++;
	}

	private static bool IsPreviousWeek(
		int prevYear, int prevWeek,
		int currentYear, int currentWeek)
	{
		if (currentYear == prevYear)
			return currentWeek == prevWeek + 1;

		if (currentYear == prevYear + 1)
			return prevWeek == IsoWeeksInYear(prevYear) && currentWeek == 1;

		return false;
	}

	private static int IsoWeeksInYear(int year)
	{
		var dec28 = new DateOnly(year, 12, 28);
		return System.Globalization.ISOWeek.GetWeekOfYear(dec28.ToDateTime(TimeOnly.MinValue));
	}
}
